module MediaDatabaseTests

open Expecto
open FsCheck
open LLDatabase
open MediaDatabase
open System
open System.IO

let private mkExtractedAudio (lang: string) (data: byte array) : MediaRecord =
    {
        Hash = ""
        Kind = MediaKind.Audio
        MimeType = "audio/mpeg"
        FileExtension = ".mp3"
        LanguageTag = lang
        Source = MediaSource.Extracted
        SourceDescription = "synthetic/voice.wav"
        TtsProvider = ""
        TtsVoice = ""
        TtsModel = ""
        GeneratedAtUtc = "2026-11-21T00:00:00Z"
        DurationMs = 1500
        Width = 0
        Height = 0
        ByteLength = data.Length
        Data = data
    }

let private mkTtsAudio (lang: string) (provider: string) (voice: string) (model: string) (data: byte array) : MediaRecord =
    {
        Hash = ""
        Kind = MediaKind.Audio
        MimeType = "audio/mpeg"
        FileExtension = ".mp3"
        LanguageTag = lang
        Source = MediaSource.Tts
        SourceDescription = sprintf "%s:%s" provider voice
        TtsProvider = provider
        TtsVoice = voice
        TtsModel = model
        GeneratedAtUtc = "2026-11-21T00:00:00Z"
        DurationMs = 1200
        Width = 0
        Height = 0
        ByteLength = data.Length
        Data = data
    }

[<Tests>]
let tests =
    testList "MediaDatabase tests" [
        test "makeMediaDatabasePathForTextDb derives .media.db3 from .db3 path" {
            Expect.equal
                (makeMediaDatabasePathForTextDb @"C:\dbs\SampleGame.db3")
                @"C:\dbs\SampleGame.media.db3"
                "should replace .db3 with .media.db3"
            Expect.isTrue (isMediaDatabaseFilename "SampleGame.media.db3") "should recognize .media.db3"
            Expect.isFalse (isMediaDatabaseFilename "SampleGame.db3") "should not flag text .db3"
            Expect.equal (makeMediaDatabasePathForTextDb ":memory:") ":memory:" "in-memory path should stay :memory:"
        }

        test "PutMedia and TryGetMedia round-trip audio and visual assets with SHA-256 deduplication" {
            use db = new MediaDatabase(":memory:")
            let store = db :> IMediaStore

            let audioBytes = [| 0x49uy; 0x44uy; 0x33uy; 0x01uy; 0x02uy; 0x03uy |]
            let hash1 = store.PutMedia(mkExtractedAudio "de" audioBytes)
            let hash2 = store.PutMedia(mkExtractedAudio "de" audioBytes)

            Expect.equal hash1 hash2 "identical payloads should produce identical content hashes"
            Expect.equal store.Count 1 "duplicate payload should be deduplicated"

            let loaded = store.TryGetMedia(hash1)
            Expect.isSome loaded "stored media should be retrievable"
            Expect.equal loaded.Value.Data audioBytes "binary payload should match"
            Expect.equal loaded.Value.Kind MediaKind.Audio "kind should be Audio"
            Expect.equal loaded.Value.Source MediaSource.Extracted "source should be Extracted"

            let imageBytes = [| 0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0x0Duy; 0x0Auy; 0x1Auy; 0x0Auy |]
            let imgRecord =
                {
                    mkExtractedAudio "de" imageBytes with
                        Kind = MediaKind.Image
                        MimeType = "image/png"
                        FileExtension = ".png"
                        DurationMs = 0
                        Width = 320
                        Height = 240
                }
            let imgHash = store.PutMedia(imgRecord)
            Expect.equal store.Count 2 "image should be stored as a second distinct asset"

            let metaOpt = store.TryGetMetadata(imgHash)
            Expect.isSome metaOpt "metadata query should succeed"
            Expect.equal metaOpt.Value.Kind MediaKind.Image "metadata kind should be Image"
            Expect.equal metaOpt.Value.Width 320 "width should round-trip"
            Expect.equal metaOpt.Value.Height 240 "height should round-trip"
        }

        test "TTS entry never downgrades an existing Extracted asset with identical bytes" {
            use db = new MediaDatabase(":memory:")
            let store = db :> IMediaStore

            let payload = [| 1uy; 2uy; 3uy; 4uy; 5uy |]
            let h1 = store.PutMedia(mkExtractedAudio "de" payload)
            let h2 = store.PutMedia(mkTtsAudio "de" "piper" "de_DE-synthetic" "model.onnx" payload)

            Expect.equal h1 h2 "hash should match"
            let loaded = store.TryGetMedia(h1).Value
            Expect.equal loaded.Source MediaSource.Extracted "native extracted provenance must be preserved"
        }

        test "TTS cache stores provenance and replaces orphaned blobs on regeneration" {
            use db = new MediaDatabase(":memory:")
            let store = db :> IMediaStore

            let firstAudio = [| 10uy; 20uy; 30uy |]
            let firstHash = store.PutTts("de", "Guten Morgen", mkTtsAudio "de" "piper" "de_DE-voice-a" "voice-a.onnx" firstAudio)

            let cached1 = store.TryGetTts("de", "Guten Morgen")
            Expect.isSome cached1 "first TTS clip should be cached"
            let (media1, prov1) = cached1.Value
            Expect.equal media1.Hash firstHash "hash should match"
            Expect.equal prov1.Provider "piper" "provider provenance should match"
            Expect.equal prov1.Voice "de_DE-voice-a" "voice provenance should match"
            Expect.equal prov1.Model "voice-a.onnx" "model provenance should match"

            // Regenerate with a different provider/voice
            let secondAudio = [| 40uy; 50uy; 60uy; 70uy |]
            let secondHash = store.PutTts("de", "Guten Morgen", mkTtsAudio "de" "azure" "de-DE-KatjaNeural" "audio-24khz-48kbitrate-mono-mp3" secondAudio)

            Expect.notEqual firstHash secondHash "regenerated clip should have a new hash"
            Expect.isFalse (store.HasMedia(firstHash)) "unreferenced old TTS blob should be cleaned up"
            Expect.isTrue (store.HasMedia(secondHash)) "new TTS blob should exist"

            let (_, prov2) = store.TryGetTts("de", "Guten Morgen").Value
            Expect.equal prov2.Provider "azure" "updated provider provenance should be stored"
            Expect.equal prov2.Voice "de-DE-KatjaNeural" "updated voice provenance should be stored"
        }

        test "PruneUnreferencedExtracted removes only orphaned extracted assets and keeps TTS cache" {
            use db = new MediaDatabase(":memory:")
            let store = db :> IMediaStore

            let keepHash = store.PutMedia(mkExtractedAudio "de" [| 1uy; 1uy; 1uy |])
            let orphanHash = store.PutMedia(mkExtractedAudio "de" [| 2uy; 2uy; 2uy |])
            let ttsHash = store.PutTts("de", "Hallo", mkTtsAudio "de" "piper" "v1" "m1.onnx" [| 3uy; 3uy; 3uy |])

            let pruned = store.PruneUnreferencedExtracted(set [ keepHash ])
            Expect.equal pruned 1 "only the unreferenced extracted asset should be pruned"
            Expect.isTrue (store.HasMedia(keepHash)) "referenced extracted asset must remain"
            Expect.isFalse (store.HasMedia(orphanHash)) "unreferenced extracted asset must be removed"
            Expect.isTrue (store.HasMedia(ttsHash)) "TTS cache asset must not be pruned by extracted cleanup"
        }

        test "ClearTtsCache can clear a single language or all languages" {
            use db = new MediaDatabase(":memory:")
            let store = db :> IMediaStore

            let deHash = store.PutTts("de", "Eins", mkTtsAudio "de" "piper" "v-de" "de.onnx" [| 11uy; 12uy |])
            let enHash = store.PutTts("en", "One", mkTtsAudio "en" "piper" "v-en" "en.onnx" [| 21uy; 22uy |])

            let clearedDe = store.ClearTtsCache(Some "de")
            Expect.equal clearedDe 1 "should clear only German TTS entry"
            Expect.isFalse (store.HasMedia(deHash)) "German TTS blob should be removed"
            Expect.isTrue (store.HasMedia(enHash)) "English TTS blob should remain"

            let clearedAll = store.ClearTtsCache(None)
            Expect.equal clearedAll 1 "should clear remaining English TTS entry"
            Expect.isFalse (store.HasMedia(enHash)) "English TTS blob should be removed"
        }

        test "Re-importing cards without media preserves existing SoundResource, ImageResource, and Speaker" {
            let dbPath = Path.Combine(Path.GetTempPath(), "ll-preserve-media-" + Guid.NewGuid().ToString("N") + ".db3")
            try
                use db = new LLDatabase(dbPath)
                let lid = db.CreateOrUpdateLesson({ LessonRecord.ID = 0; Name = "Dialogue" })
                let cardWithMedia =
                    {
                        CardRecord.ID = 0
                        LessonID = lid
                        Text = "Synthetic line"
                        Gender = "masculine"
                        Key = "k1"
                        GenderlessKey = "k1"
                        KeyHash = 0
                        GenderlessKeyHash = 0
                        SoundResource = "abc123hash"
                        ImageResource = "img999hash"
                        Speaker = "Narrator"
                        LanguageTag = "de"
                        Reversible = true
                    }
                db.CreateOrUpdateCards([| cardWithMedia |])

                let textOnlyUpdate =
                    {
                        cardWithMedia with
                            Text = "Synthetic line updated"
                            SoundResource = ""
                            ImageResource = ""
                            Speaker = ""
                    }
                db.CreateOrUpdateCards([| textOnlyUpdate |])

                Expect.equal db.Cards.Length 1 "should update in place"
                Expect.equal db.Cards.[0].Text "Synthetic line updated" "text should update"
                Expect.equal db.Cards.[0].SoundResource "abc123hash" "existing SoundResource should be preserved"
                Expect.equal db.Cards.[0].ImageResource "img999hash" "existing ImageResource should be preserved"
                Expect.equal db.Cards.[0].Speaker "Narrator" "existing Speaker should be preserved"
            finally
                if File.Exists(dbPath) then File.Delete(dbPath)
        }

        testProperty "computeSha256Hex is deterministic 64-char lowercase hex and MediaDatabase round-trips arbitrary bytes" <|
            fun (payload: byte array) ->
                let bytes = if isNull payload then Array.empty else payload
                let h1 = computeSha256Hex bytes
                let h2 = computeSha256Hex bytes
                use db = new MediaDatabase(":memory:")
                let store = db :> IMediaStore
                let storedHash = store.PutMedia(mkExtractedAudio "en" bytes)
                let roundTripped = store.TryGetMedia(storedHash)
                h1 = h2
                && h1.Length = 64
                && storedHash = h1
                && roundTripped.IsSome
                && roundTripped.Value.Data = bytes
    ]
