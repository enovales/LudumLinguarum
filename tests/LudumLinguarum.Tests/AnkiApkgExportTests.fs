module AnkiApkgExportTests

open AnkiApkgExport
open CardExport
open Expecto
open FsCheck
open LLDatabase
open LudumLinguarumPlugins
open MediaDatabase
open SQLite
open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Text.Json
open TtsProviders

type NoteRow() =
    member val id = 0L with get, set
    member val guid = "" with get, set
    member val flds = "" with get, set
    member val sfld = "" with get, set
    member val csum = 0L with get, set
    member val tags = "" with get, set

type CardRow() =
    member val id = 0L with get, set
    member val nid = 0L with get, set
    member val ord = 0 with get, set

type ColRow() =
    member val ver = 0 with get, set
    member val models = "" with get, set
    member val decks = "" with get, set

let private readApkgContents (apkgPath: string) =
    let tempDir = Path.Combine(Path.GetTempPath(), "ll-apkg-read-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(tempDir) |> ignore
    try
        use zip = ZipFile.OpenRead(apkgPath)
        let dbEntry = zip.GetEntry("collection.anki2")
        let mediaEntry = zip.GetEntry("media")
        Expect.isNotNull dbEntry "collection.anki2 must exist in .apkg"
        Expect.isNotNull mediaEntry "media JSON must exist in .apkg"

        let dbPath = Path.Combine(tempDir, "collection.anki2")
        dbEntry.ExtractToFile(dbPath)

        let mediaJson =
            use sr = new StreamReader(mediaEntry.Open())
            sr.ReadToEnd()
        let mediaMap = JsonSerializer.Deserialize<Dictionary<string, string>>(mediaJson)

        let mediaPayloads =
            mediaMap
            |> Seq.map (fun kv ->
                let entry = zip.GetEntry(kv.Key)
                Expect.isNotNull entry (sprintf "media entry '%s' must exist" kv.Key)
                use ms = new MemoryStream()
                use s = entry.Open()
                s.CopyTo(ms)
                (kv.Value, ms.ToArray())
            )
            |> Map.ofSeq

        use conn = new SQLiteConnection(dbPath)
        let cols = conn.Query<ColRow>("select ver, models, decks from col") |> Array.ofSeq
        let notes = conn.Query<NoteRow>("select id, guid, flds, sfld, csum, tags from notes order by id") |> Array.ofSeq
        let cards = conn.Query<CardRow>("select id, nid, ord from cards order by id") |> Array.ofSeq

        (cols, notes, cards, mediaPayloads)
    finally
        try Directory.Delete(tempDir, true) with _ -> ()

type private StubTts(voice: string, audio: byte array) =
    interface ITtsProvider with
        member _.Name = "stub"
        member _.ResolveVoice(_) = Some voice
        member _.Synthesize(lang, _text) =
            Ok
                {
                    AudioBytes = audio
                    MimeType = "audio/mpeg"
                    FileExtension = ".mp3"
                    DurationMs = 900
                    Provenance =
                        {
                            Provider = "stub"
                            Voice = voice
                            Model = "stub-model"
                            LanguageTag = lang
                            GeneratedAtUtc = "2026-11-21T00:00:00Z"
                        }
                }

[<Tests>]
let tests =
    testList "AnkiApkgExport tests" [
        test "writeApkg creates a valid collection.anki2 database with deduplicated media and listening cards" {
            let apkgPath = Path.Combine(Path.GetTempPath(), "ll-test-" + Guid.NewGuid().ToString("N") + ".apkg")
            try
                let sharedAudio = [| 0x49uy; 0x44uy; 0x33uy; 0x10uy; 0x20uy |]
                let sampleImage = [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0x01uy |]

                let notesInput =
                    [|
                        {
                            StableKey = "lesson1|k1|m|en|de"
                            RecognitionText = "Good morning"
                            ProductionText = "Guten Morgen"
                            RecognitionAudio = None
                            ProductionAudio = Some { FileName = "shared.mp3"; Data = sharedAudio }
                            Image = Some { FileName = "icon.png"; Data = sampleImage }
                            Lesson = "Greetings"
                            Speaker = "Guide"
                            Reversible = true
                            IncludeListeningCard = true
                            Tags = [ "Greetings" ]
                        }
                        {
                            StableKey = "lesson1|k2|m|en|de"
                            RecognitionText = "Good day"
                            ProductionText = "Guten Tag"
                            RecognitionAudio = None
                            ProductionAudio = Some { FileName = "shared.mp3"; Data = sharedAudio }
                            Image = None
                            Lesson = "Greetings"
                            Speaker = ""
                            Reversible = false
                            IncludeListeningCard = true
                            Tags = [ "Greetings" ]
                        }
                    |]

                let (noteCount, cardCount) = writeApkg apkgPath "Synthetic Deck" notesInput
                Expect.equal noteCount 2 "should write 2 notes"
                // Note 1: Recognition + Production + Listening = 3 cards
                // Note 2: Recognition + Listening (not reversible) = 2 cards
                Expect.equal cardCount 5 "should write 5 cards total"

                let (cols, notes, cards, mediaPayloads) = readApkgContents apkgPath
                Expect.equal cols.Length 1 "col table should have 1 row"
                Expect.equal cols.[0].ver 11 "schema version should be 11"
                Expect.isTrue (cols.[0].decks.Contains("Synthetic Deck")) "deck name should be present in decks JSON"
                Expect.equal notes.Length 2 "notes table should contain 2 notes"
                Expect.equal cards.Length 5 "cards table should contain 5 cards"
                Expect.equal mediaPayloads.Count 2 "shared audio should be deduplicated (1 mp3 + 1 png)"
                Expect.equal mediaPayloads.["shared.mp3"] sharedAudio "audio bytes should match"
                Expect.equal mediaPayloads.["icon.png"] sampleImage "image bytes should match"

                let note1Fields = notes.[0].flds.Split('\u001f')
                Expect.equal note1Fields.Length 9 "note should have 9 fields"
                Expect.equal note1Fields.[0] "Good morning" "recognition text"
                Expect.equal note1Fields.[1] "Guten Morgen" "production text"
                Expect.equal note1Fields.[3] "[sound:shared.mp3]" "production audio tag"
                Expect.equal note1Fields.[4] "<img src=\"icon.png\">" "image tag"
                Expect.equal note1Fields.[6] "Guide" "speaker field"
                Expect.equal note1Fields.[7] "y" "AddReverse should be y"
                Expect.equal note1Fields.[8] "y" "AddListening should be y"
            finally
                if File.Exists(apkgPath) then File.Delete(apkgPath)
        }

        test "CommonExporter AnkiApkg combines native extracted audio with TTS fallback and caches TTS" {
            let apkgPath = Path.Combine(Path.GetTempPath(), "ll-hybrid-" + Guid.NewGuid().ToString("N") + ".apkg")
            try
                use db = new LLDatabase(":memory:")
                use mediaDb = new MediaDatabase(":memory:")
                let store = mediaDb :> IMediaStore

                let nativeEnAudio = [| 0x49uy; 0x44uy; 0x33uy; 0x01uy; 0x01uy |]
                let nativeHash =
                    store.PutMedia
                        {
                            Hash = ""
                            Kind = MediaKind.Audio
                            MimeType = "audio/mpeg"
                            FileExtension = ".mp3"
                            LanguageTag = "en"
                            Source = MediaSource.Extracted
                            SourceDescription = "game/en_line.mp3"
                            TtsProvider = ""
                            TtsVoice = ""
                            TtsModel = ""
                            GeneratedAtUtc = "2026-11-21T00:00:00Z"
                            DurationMs = 800
                            Width = 0
                            Height = 0
                            ByteLength = nativeEnAudio.Length
                            Data = nativeEnAudio
                        }

                let lid = db.AddLesson({ LessonRecord.ID = 0; Name = "Dialogue 1" })
                let mkCard key lang text soundHash =
                    {
                        CardRecord.ID = 0
                        LessonID = lid
                        Text = text
                        Gender = "masculine"
                        Key = key
                        GenderlessKey = key
                        KeyHash = 0
                        GenderlessKeyHash = 0
                        SoundResource = soundHash
                        ImageResource = ""
                        Speaker = "Mentor"
                        LanguageTag = lang
                        Reversible = true
                    }

                db.AddCard(mkCard "k1" "en" "Welcome traveler" nativeHash) |> ignore
                db.AddCard(mkCard "k1" "de" "Willkommen Reisender" "") |> ignore

                let ttsAudio = [| 0x49uy; 0x44uy; 0x33uy; 0x02uy; 0x02uy |]
                let stubTts = StubTts("de-DE-KatjaNeural", ttsAudio)

                let exportCfg: CommonExporterConfiguration =
                    {
                        Target = CommonExportTarget.AnkiApkg
                        LessonToExport = None
                        LessonRegexToExport = None
                        ExportPath = apkgPath
                        RecognitionLanguage = "en"
                        ProductionLanguage = "de"
                        RecognitionLengthLimit = None
                        ProductionLengthLimit = None
                        RecognitionWordLimit = None
                        ProductionWordLimit = None
                    }
                let mediaCfg: MediaExportConfiguration =
                    {
                        DeckName = "Hybrid Test Deck"
                        MediaStore = Some store
                        TtsProvider = Some (stubTts :> ITtsProvider)
                        TtsLanguages = ProductionOnly
                        RegenerateTts = false
                        IncludeListeningCards = true
                        OnlyWithAudio = false
                        OnlyWithExtractedAudio = false
                    }

                let exporter = new CommonExporter(Unchecked.defaultof<IPluginManager>, TextWriter.Null, db, exportCfg, mediaCfg)
                exporter.RunExportAction()

                let (_, notes, cards, mediaPayloads) = readApkgContents apkgPath
                Expect.equal notes.Length 1 "should export 1 paired note"
                Expect.equal cards.Length 3 "should create Recognition + Production + Listening cards"
                Expect.equal mediaPayloads.Count 2 "should embed both native EN audio and generated DE TTS audio"

                // Verify TTS clip was cached with provenance in companion media DB
                let cached = store.TryGetTts("de", "Willkommen Reisender")
                Expect.isSome cached "German TTS should be cached in MediaDatabase"
                let (_, prov) = cached.Value
                Expect.equal prov.Provider "stub" "TTS provider provenance should be stored"
                Expect.equal prov.Voice "de-DE-KatjaNeural" "TTS voice provenance should be stored"
            finally
                if File.Exists(apkgPath) then File.Delete(apkgPath)
        }

        test "OnlyWithExtractedAudio filters cards before running TTS fallback" {
            let apkgPath = Path.Combine(Path.GetTempPath(), "ll-extracted-filter-" + Guid.NewGuid().ToString("N") + ".apkg")
            try
                use db = new LLDatabase(":memory:")
                use mediaDb = new MediaDatabase(":memory:")
                let store = mediaDb :> IMediaStore

                let nativeAudio = [| 0x49uy; 0x44uy; 0x33uy; 0x05uy; 0x06uy |]
                let nativeHash =
                    store.PutMedia
                        {
                            Hash = ""
                            Kind = MediaKind.Audio
                            MimeType = "audio/mpeg"
                            FileExtension = ".mp3"
                            LanguageTag = "en"
                            Source = MediaSource.Extracted
                            SourceDescription = "game/quote1.mp3"
                            TtsProvider = ""
                            TtsVoice = ""
                            TtsModel = ""
                            GeneratedAtUtc = "2026-11-21T00:00:00Z"
                            DurationMs = 700
                            Width = 0
                            Height = 0
                            ByteLength = nativeAudio.Length
                            Data = nativeAudio
                        }

                let lid = db.AddLesson({ LessonRecord.ID = 0; Name = "Mixed" })
                let mkCard key lang text soundHash =
                    {
                        CardRecord.ID = 0
                        LessonID = lid
                        Text = text
                        Gender = "masculine"
                        Key = key
                        GenderlessKey = key
                        KeyHash = 0
                        GenderlessKeyHash = 0
                        SoundResource = soundHash
                        ImageResource = ""
                        Speaker = ""
                        LanguageTag = lang
                        Reversible = true
                    }

                // Pair 1 has native English audio
                db.AddCard(mkCard "k1" "en" "Spoken quote" nativeHash) |> ignore
                db.AddCard(mkCard "k1" "de" "Gesprochenes Zitat" "") |> ignore
                // Pair 2 has NO native audio on either side
                db.AddCard(mkCard "k2" "en" "Silent menu label" "") |> ignore
                db.AddCard(mkCard "k2" "de" "Stummes Menü" "") |> ignore

                let mutable ttsCalls = 0
                let countingTts =
                    { new ITtsProvider with
                        member _.Name = "counting"
                        member _.ResolveVoice(_) = Some "v1"
                        member _.Synthesize(lang, _text) =
                            ttsCalls <- ttsCalls + 1
                            Ok
                                {
                                    AudioBytes = [| 0x49uy; 0x44uy; 0x33uy; byte ttsCalls |]
                                    MimeType = "audio/mpeg"
                                    FileExtension = ".mp3"
                                    DurationMs = 500
                                    Provenance =
                                        {
                                            Provider = "counting"
                                            Voice = "v1"
                                            Model = "m1"
                                            LanguageTag = lang
                                            GeneratedAtUtc = "2026-11-21T00:00:00Z"
                                        }
                                }
                    }

                let exportCfg: CommonExporterConfiguration =
                    {
                        Target = CommonExportTarget.AnkiApkg
                        LessonToExport = None
                        LessonRegexToExport = None
                        ExportPath = apkgPath
                        RecognitionLanguage = "en"
                        ProductionLanguage = "de"
                        RecognitionLengthLimit = None
                        ProductionLengthLimit = None
                        RecognitionWordLimit = None
                        ProductionWordLimit = None
                    }
                let mediaCfg: MediaExportConfiguration =
                    {
                        DeckName = "Extracted Only Test"
                        MediaStore = Some store
                        TtsProvider = Some countingTts
                        TtsLanguages = ProductionOnly
                        RegenerateTts = false
                        IncludeListeningCards = true
                        OnlyWithAudio = false
                        OnlyWithExtractedAudio = true
                    }

                let exporter = new CommonExporter(Unchecked.defaultof<IPluginManager>, TextWriter.Null, db, exportCfg, mediaCfg)
                exporter.RunExportAction()

                Expect.equal ttsCalls 1 "TTS should only be invoked for the 1 pair that has native extracted audio"
                let (_, notes, cards, _) = readApkgContents apkgPath
                Expect.equal notes.Length 1 "only the pair with native extracted audio should be exported"
                Expect.equal cards.Length 3 "should include Recognition, Production, and Listening cards"
            finally
                if File.Exists(apkgPath) then File.Delete(apkgPath)
        }

        testProperty "computeStableGuid is deterministic 10-char ASCII and computeAnkiFieldChecksum is non-negative" <|
            fun (NonEmptyString seed, NonEmptyString fieldText) ->
                let g1 = computeStableGuid seed
                let g2 = computeStableGuid seed
                let csum = computeAnkiFieldChecksum fieldText
                g1 = g2 && g1.Length = 10 && csum >= 0L
    ]
