module CardExport

open AnkiApkgExport
open LLDatabase
open LudumLinguarumPlugins
open MediaDatabase
open TtsProviders
open System
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions

type CommonExportTarget = 
    | Anki
    | AnkiApkg
    | SuperMemo
    | Mnemosyne
    | AnyMemo
    | Brainscape

type TtsLanguageSelection =
    | NoTts
    | ProductionOnly
    | RecognitionOnly
    | BothLanguages

type MediaExportConfiguration =
    {
        DeckName: string
        MediaStore: IMediaStore option
        TtsProvider: ITtsProvider option
        TtsLanguages: TtsLanguageSelection
        RegenerateTts: bool
        IncludeListeningCards: bool
        OnlyWithAudio: bool
        OnlyWithExtractedAudio: bool
    }
    static member Default =
        {
            DeckName = "LudumLinguarum"
            MediaStore = None
            TtsProvider = None
            TtsLanguages = NoTts
            RegenerateTts = false
            IncludeListeningCards = false
            OnlyWithAudio = false
            OnlyWithExtractedAudio = false
        }

type CommonExporterConfiguration =
    {
        Target: CommonExportTarget
        LessonToExport: string option
        LessonRegexToExport: string option
        ExportPath: string
        RecognitionLanguage: string
        ProductionLanguage: string
        RecognitionLengthLimit: int option
        ProductionLengthLimit: int option
        RecognitionWordLimit: int option
        ProductionWordLimit: int option
    }

let internal mkLengthFilter(lo: int option) = 
    match lo with
    | Some(l) -> (fun (c: CardRecord) -> c.Text.Length <= l)
    | _ -> (fun (_: CardRecord) -> true)

let internal mkWordCountFilter(lo: int option) = 
    match lo with
    | Some(l) -> (fun (c: CardRecord) -> (c.Text.ToCharArray() |> Array.sumBy(fun c -> if Char.IsWhiteSpace(c) then 1 else 0)) <= (l - 1))
    | _ -> (fun (_: CardRecord) -> true)

type ICardWriter = 
    interface
        abstract member WriteCard: string -> unit
    end

type private NonPaginatingWriter(otw: TextWriter, exportBasePath: string) = 
    let streamWriter = new StreamWriter(exportBasePath)

    do otw.WriteLine("Writing to [" + exportBasePath + "]")
    interface IDisposable with
        member this.Dispose() = streamWriter.Dispose()

    interface ICardWriter with
        member this.WriteCard(line: String) = streamWriter.WriteLine(line)

type private SuperMemoPaginatingWriter(otw: TextWriter, exportBasePath: string) = 
    let mutable lineCounter = 0
    let mutable fileCounter = 1
    let mutable streamWriter = new StreamWriter(exportBasePath)

    do otw.WriteLine("Writing to [" + exportBasePath + "]")
    interface IDisposable with
        member this.Dispose() = streamWriter.Dispose()

    interface ICardWriter with
        member this.WriteCard(line: String) = 
            if (lineCounter >= 99) then
                streamWriter.Dispose()

                let newFileName = 
                    Path.GetDirectoryName(exportBasePath) + 
                    new string([|Path.DirectorySeparatorChar|]) + 
                    Path.GetFileNameWithoutExtension(exportBasePath) + 
                    "_" + 
                    fileCounter.ToString("000") + 
                    Path.GetExtension(exportBasePath)

                streamWriter <- new StreamWriter(newFileName)
                otw.WriteLine("Writing to [" + newFileName + "]")

                fileCounter <- fileCounter + 1
                lineCounter <- 0

            streamWriter.WriteLine(line)
            lineCounter <- lineCounter + 1

// Writes cards as UTF-8 CSV files with two columns, which Brainscape's
// "Import Flashcards" feature reads as question and answer. If more than
// pageSize records are written, the output is split into numerically
// suffixed files (e.g. deck_001.csv) holding at most pageSize records each.
type internal BrainscapeCsvWriter(otw: TextWriter, exportBasePath: string, pageSize: int) = 
    let mkCsvConfiguration() = CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
    let mutable recordCounter = 0
    let mutable fileCounter = 1
    let mutable streamWriter = new StreamWriter(exportBasePath, false, Encoding.UTF8)
    let mutable csvWriter = new CsvHelper.CsvWriter(streamWriter, mkCsvConfiguration())

    do otw.WriteLine("Writing to [" + exportBasePath + "]")
    interface IDisposable with
        member this.Dispose() = 
            csvWriter.Dispose()
            streamWriter.Dispose()

    interface ICardWriter with
        member this.WriteCard(line: String) = 
            if (recordCounter >= pageSize) then
                csvWriter.Dispose()

                let newFileName = 
                    Path.GetDirectoryName(exportBasePath) + 
                    new string([|Path.DirectorySeparatorChar|]) + 
                    Path.GetFileNameWithoutExtension(exportBasePath) + 
                    "_" + 
                    fileCounter.ToString("000") + 
                    Path.GetExtension(exportBasePath)

                streamWriter <- new StreamWriter(newFileName, false, Encoding.UTF8)
                csvWriter <- new CsvHelper.CsvWriter(streamWriter, mkCsvConfiguration())
                otw.WriteLine("Writing to [" + newFileName + "]")

                fileCounter <- fileCounter + 1
                recordCounter <- 0

            // Card text has already had tabs, CRs, and LFs stripped, so the
            // line is exactly two tab-separated fields: question and answer.
            let fields = line.Split('\t')
            csvWriter.WriteField(fields.[0])
            csvWriter.WriteField(fields.[1])
            csvWriter.NextRecord()
            recordCounter <- recordCounter + 1

type CommonExporter(iPluginManager: IPluginManager, outputTextWriter: TextWriter, 
                    llDatabase: LLDatabase, config: CommonExporterConfiguration,
                    ?mediaConfig: MediaExportConfiguration) = 

    let mediaCfg = defaultArg mediaConfig MediaExportConfiguration.Default
    let recognitionLengthFilter = mkLengthFilter(config.RecognitionLengthLimit)
    let productionLengthFilter = mkLengthFilter(config.ProductionLengthLimit)
    let recognitionWordCountFilter = mkWordCountFilter(config.RecognitionWordLimit)
    let productionWordCountFilter = mkWordCountFilter(config.ProductionWordLimit)

    member private this.GenerateRecognitionAndProductionCardSets
        (lesson: LessonRecord, recognitionLanguage: string, productionLanguage: string) = 
        // get the recognition and production cards for this lesson
        let recognitionCards = llDatabase.CardsFromLessonAndLanguageTag(lesson, recognitionLanguage)
        let productionCards = llDatabase.CardsFromLessonAndLanguageTag(lesson, productionLanguage)

        // Check if one of the card sets has populated genders, and the other has none. If this
        // is the case, then replicate the non-gendered cards using the populated gender set. This
        // will make it easier to match up the recognition and production cards later.
        let recognitionGenders = recognitionCards |> Array.distinctBy(fun t -> t.Gender) |> Array.map(fun t -> t.Gender)
        let productionGenders = productionCards |> Array.distinctBy(fun t -> t.Gender) |> Array.map(fun t -> t.Gender)

        let duplicateIntoGenders(c: CardRecord array, g: string array) = 
            c |> Array.collect(fun t -> g |> Array.map(fun u -> { t with Gender = u }))

        // Only support unpacking many-to-1 or 1-to-many. Anything else
        // can't be disambiguated without actual domain knowledge about
        // these languages.
        match (recognitionGenders.Length, productionGenders.Length) with
        | (a, b) when a > b -> 
            (recognitionCards, duplicateIntoGenders(productionCards, recognitionGenders))
        | (a, b) when a < b -> 
            (duplicateIntoGenders(recognitionCards, productionGenders), productionCards)
        | (a, b) when a = b -> (recognitionCards, productionCards)
        | _ -> failwith "can't disambiguate m-to-n language pairs"

    member private this.MatchPairsForLesson(lesson: LessonRecord) : (CardRecord * CardRecord) array =
        let (recognitionCards, productionCards) = 
            this.GenerateRecognitionAndProductionCardSets(lesson, config.RecognitionLanguage, config.ProductionLanguage)

        // filter and then group cards by their key and gender, and then match them up with each other.
        let recognitionByKeys = 
            recognitionCards 
            |> Array.filter(recognitionLengthFilter)
            |> Array.filter(recognitionWordCountFilter)
            |> Array.groupBy(fun t -> (t.GenderlessKey, t.Gender))
        let productionByKeys = 
            productionCards 
            |> Array.filter(productionLengthFilter)
            |> Array.filter(productionWordCountFilter)
            |> Array.groupBy(fun t -> (t.GenderlessKey, t.Gender)) |> Map.ofArray

        // Match up recognition cards with the same key and gender string with
        // production cards with the same key and gender string.
        recognitionByKeys
        |> Array.choose (fun (k, rs) ->
            productionByKeys
            |> Map.tryFind k
            |> Option.map (fun ps -> Array.zip rs ps)
        )
        |> Array.collect id

    member private this.RunLessonExport(cardWriter: ICardWriter, applicationTarget: CommonExportTarget)(lesson: LessonRecord) = 
        let writeCard(r: CardRecord, p: CardRecord) = 
            let recogText = r.Text.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")
            let prodText = p.Text.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")
            let reverseText = 
                match (applicationTarget, r.Reversible, p.Reversible) with
                | (CommonExportTarget.Anki, true, true) -> "\ty"
                | (CommonExportTarget.Anki, _, _) -> "\t"
                | (CommonExportTarget.AnkiApkg, _, _) -> ""
                | (CommonExportTarget.SuperMemo, _, _) -> ""
                | (CommonExportTarget.Mnemosyne, _, _) -> "\t"
                | (CommonExportTarget.AnyMemo, _, _) -> "\t"
                | (CommonExportTarget.Brainscape, _, _) -> ""

            cardWriter.WriteCard(recogText + "\t" + prodText + reverseText)

            // Brainscape's CSV import has no reverse-card column, so reversible
            // pairs are additionally exported in the reverse direction.
            if (applicationTarget = CommonExportTarget.Brainscape && r.Reversible && p.Reversible) then
                cardWriter.WriteCard(prodText + "\t" + recogText)

        this.MatchPairsForLesson(lesson)
        |> Array.iter writeCard

    member private this.RunApkgExport(lessonsToExport: LessonRecord array) =
        let allowRecogTts =
            match mediaCfg.TtsLanguages with
            | RecognitionOnly | BothLanguages -> true
            | _ -> false

        let allowProdTts =
            match mediaCfg.TtsLanguages with
            | ProductionOnly | BothLanguages -> true
            | _ -> false

        let toAttachment (mediaOpt: MediaRecord option) : ApkgMediaAttachment option =
            mediaOpt
            |> Option.map (fun m ->
                let ext =
                    if String.IsNullOrWhiteSpace(m.FileExtension) then ".mp3"
                    elif m.FileExtension.StartsWith(".") then m.FileExtension
                    else "." + m.FileExtension
                {
                    FileName = m.Hash + ext
                    Data = m.Data
                }
            )

        let resolveImage (r: CardRecord) (p: CardRecord) : ApkgMediaAttachment option =
            let hash =
                if not (String.IsNullOrWhiteSpace(p.ImageResource)) then p.ImageResource
                elif not (String.IsNullOrWhiteSpace(r.ImageResource)) then r.ImageResource
                else ""
            if String.IsNullOrWhiteSpace(hash) then None
            else
                mediaCfg.MediaStore
                |> Option.bind (fun store -> store.TryGetMedia(hash))
                |> toAttachment

        let hasExtractedAudio (card: CardRecord) : bool =
            if String.IsNullOrWhiteSpace(card.SoundResource) then
                false
            else
                match mediaCfg.MediaStore with
                | Some store ->
                    match store.TryGetMetadata(card.SoundResource) with
                    | Some meta -> meta.Source = MediaSource.Extracted
                    | None -> false
                | None -> false

        let lessonPairs =
            lessonsToExport
            |> Array.map (fun lesson ->
                let matched = this.MatchPairsForLesson(lesson)
                let filtered =
                    if mediaCfg.OnlyWithExtractedAudio then
                        matched |> Array.filter (fun (r, p) -> hasExtractedAudio r || hasExtractedAudio p)
                    else
                        matched
                (lesson, filtered)
            )
            |> Array.filter (fun (_, pairs) -> pairs.Length > 0)

        let totalPairs = lessonPairs |> Array.sumBy (fun (_, pairs) -> pairs.Length)
        outputTextWriter.WriteLine(sprintf "Preparing to export %d card pair(s) across %d lesson(s)..." totalPairs lessonPairs.Length)

        let notes = ResizeArray<ApkgNoteInput>()
        let mutable processedPairs = 0
        let mutable nativeAudioCount = 0
        let mutable ttsCachedCount = 0
        let mutable ttsSynthesizedCount = 0
        let mutable ttsErrors = 0

        for (lesson, pairs) in lessonPairs do
            outputTextWriter.WriteLine(sprintf "Exporting lesson [%s] (%d pairs)" lesson.Name pairs.Length)
            for (r, p) in pairs do
                let resolveSide (card: CardRecord) (allowTts: bool) =
                    match resolveCardAudioDetailed card.SoundResource card.LanguageTag card.Text mediaCfg.MediaStore mediaCfg.TtsProvider allowTts mediaCfg.RegenerateTts with
                    | Ok NoAudio -> None
                    | Ok (NativeExtracted media) ->
                        nativeAudioCount <- nativeAudioCount + 1
                        toAttachment (Some media)
                    | Ok (CachedTts media) ->
                        ttsCachedCount <- ttsCachedCount + 1
                        toAttachment (Some media)
                    | Ok (NewlySynthesizedTts media) ->
                        ttsSynthesizedCount <- ttsSynthesizedCount + 1
                        toAttachment (Some media)
                    | Error err ->
                        ttsErrors <- ttsErrors + 1
                        if ttsErrors <= 10 then
                            outputTextWriter.WriteLine("Warning (TTS): " + err)
                        None

                let recogAudio = resolveSide r allowRecogTts
                let prodAudio = resolveSide p allowProdTts
                let image = resolveImage r p

                let includePair =
                    if mediaCfg.OnlyWithAudio then
                        recogAudio.IsSome || prodAudio.IsSome
                    else
                        true

                if includePair then
                    let speaker =
                        if not (String.IsNullOrWhiteSpace(p.Speaker)) then p.Speaker
                        else r.Speaker

                    let stableKey =
                        sprintf "%s|%s|%s|%s|%s" lesson.Name r.GenderlessKey r.Gender r.LanguageTag p.LanguageTag

                    notes.Add
                        {
                            StableKey = stableKey
                            RecognitionText = r.Text
                            ProductionText = p.Text
                            RecognitionAudio = recogAudio
                            ProductionAudio = prodAudio
                            Image = image
                            Lesson = lesson.Name
                            Speaker = speaker
                            Reversible = r.Reversible && p.Reversible
                            IncludeListeningCard = mediaCfg.IncludeListeningCards
                            Tags = [ lesson.Name ]
                        }

                processedPairs <- processedPairs + 1
                if totalPairs >= 25 && (processedPairs % 25 = 0 || processedPairs = totalPairs) then
                    outputTextWriter.WriteLine(
                        sprintf "Progress: %d/%d pairs (native audio: %d, TTS synthesized: %d, TTS cache hits: %d)"
                            processedPairs totalPairs nativeAudioCount ttsSynthesizedCount ttsCachedCount
                    )

        if ttsErrors > 10 then
            outputTextWriter.WriteLine(sprintf "Warning: %d total TTS errors occurred during export." ttsErrors)

        outputTextWriter.WriteLine("Writing Anki package to [" + config.ExportPath + "]")
        let (noteCount, cardCount) = writeApkg config.ExportPath mediaCfg.DeckName notes
        outputTextWriter.WriteLine(
            sprintf "Exported %d notes (%d cards) to [%s] (native audio: %d, TTS synthesized: %d, TTS cache hits: %d)."
                noteCount cardCount config.ExportPath nativeAudioCount ttsSynthesizedCount ttsCachedCount
        )

    member private this.RunGameExport(lesson: string option, lessonRegex: string option) = 
        let filterLessonsNoWildcard(name: string)(l: LessonRecord) = 
            (l.Name = name)
        let filterLessonsWildcard(name: string)(l: LessonRecord) = 
            let rootName = name.Substring(0, name.IndexOf('*'))
            (l.Name.StartsWith(rootName, StringComparison.CurrentCultureIgnoreCase))
        let filterLessonsRegex(regex: Regex)(l: LessonRecord) = 
            (regex.IsMatch(l.Name))

        // Prefer the regex over using the name. If no name is specified, then 
        // export all lessons for the game.
        let lessonFilter = 
            match (lesson, lessonRegex) with
            | (_, Some(regex)) -> filterLessonsRegex(new Regex(regex))
            | (Some(l), _) when not(l.Contains("*")) -> filterLessonsNoWildcard(l)
            | (Some(l), _) -> filterLessonsWildcard(l)
            | _ -> (fun _ -> true)

        let lessonsToExport = llDatabase.Lessons |> Array.filter lessonFilter

        match config.Target with
        | CommonExportTarget.AnkiApkg ->
            this.RunApkgExport(lessonsToExport)
        | _ ->
            let reportLessonExport(l: LessonRecord) =
                outputTextWriter.WriteLine("Exporting lesson [" + l.Name + "]")
                l

            let cardWriter: ICardWriter = 
                match config.Target with
                | CommonExportTarget.SuperMemo -> new SuperMemoPaginatingWriter(outputTextWriter, config.ExportPath) :> ICardWriter
                | CommonExportTarget.Brainscape -> new BrainscapeCsvWriter(outputTextWriter, config.ExportPath, 500) :> ICardWriter
                | CommonExportTarget.Anki 
                | CommonExportTarget.Mnemosyne 
                | CommonExportTarget.AnyMemo -> new NonPaginatingWriter(outputTextWriter, config.ExportPath) :> ICardWriter
                | CommonExportTarget.AnkiApkg -> failwith "unreachable"
            let disposableCardWriter = cardWriter :?> IDisposable

            lessonsToExport |> Array.iter(reportLessonExport >> this.RunLessonExport(cardWriter, config.Target))
            disposableCardWriter.Dispose() |> ignore

    member this.RunExportAction() = 
        this.RunGameExport(config.LessonToExport, config.LessonRegexToExport)
        ()
