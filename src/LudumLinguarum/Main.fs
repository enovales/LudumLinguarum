module LudumLinguarumConsole

open Argu
open AudioPipeline
open LLDatabase
open LudumLinguarumPlugins
open MediaDatabase
open TtsProviders
open System
open System.IO
open System.Reflection
open System.Text
open System.Text.RegularExpressions

type ImportArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<Mandatory; EqualsAssignment>] Game_Dir of string
    | Extract_Audio
    | Extract_Images
    | [<EqualsAssignment>] Ffmpeg_Path of string
    | [<EqualsAssignment>] Vgmstream_Path of string
    | No_Normalize_Loudness
    | Trim_Silence
    | [<GatherUnrecognized; Hidden>] Plugin_Arguments of values:string
    with 
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "game to import"
                | Game_Dir _ -> "directory where the game is located"
                | Extract_Audio -> "extract localized audio assets into the companion .media.db3 database (if supported by the game)"
                | Extract_Images -> "extract localized visual assets into the companion .media.db3 database (if supported by the game)"
                | Ffmpeg_Path _ -> "optional path to the ffmpeg executable (or directory containing it); defaults to PATH discovery"
                | Vgmstream_Path _ -> "optional path to the vgmstream-cli executable (or directory containing it); defaults to PATH discovery"
                | No_Normalize_Loudness -> "disable EBU R128 loudness normalization when transcoding extracted audio to MP3"
                | Trim_Silence -> "trim leading and trailing silence when transcoding extracted audio to MP3"
                | Plugin_Arguments _ -> "additional arguments for import"
and ListGamesArgs = 
    | [<EqualsAssignment>] Filter_Regex of string
    | Languages of string list
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Filter_Regex _ -> "An optional regular expression to filter the list of games"
                | Languages _ -> "Specifies a language that the game must support. Apply this multiple times if desired."
and ListSupportedGamesArgs = 
    | Languages of string list
    | Audio_Only
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Languages _ -> "Specifies a language that the game must support. Apply this multiple times if desired."
                | Audio_Only -> "Only list games that declare native audio extraction support."
and ListSupportedLanguagesArgs = 
    | [<Hidden>] Nothing of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | _ -> ""
and ListLessonsArgs = 
    | [<EqualsAssignment>] Game_Regex of string
    | [<EqualsAssignment>] Filter_Regex of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game_Regex _ -> "An optional regular expression to filter the list of games searched"
                | Filter_Regex _ -> "An optional regular expression filter for the name of lessons returned"
and DeleteGameArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "The name of the game to delete"
and DeleteLessonsArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<EqualsAssignment>] Filter_Regex of string
    | [<EqualsAssignment>] Lesson_Name of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "The name of the game for which lessons are to be deleted."
                | Filter_Regex _ -> "An optional regular expression filter for the name of lessons to delete. Either this or lesson-name must be specified."
                | Lesson_Name _ -> "The name of the lesson to delete. Either this or filter-regex must be specified."
and ClearTtsCacheArgs =
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<EqualsAssignment>] Language of string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Game _ -> "The name of the game whose cached TTS audio should be cleared."
                | Language _ -> "Optional language tag to restrict TTS cache clearing to a single language."
and DumpTextArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<EqualsAssignment>] Lesson_Filter_Regex of string
    | [<EqualsAssignment>] Content_Filter_Regex of string
    | [<Mandatory>] Languages of string list
    | Include_Key of bool
    | Include_Language of bool
    | Include_Lesson of bool
    | [<EqualsAssignment>] Sample_Size of int
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "The name of the game for which text should be dumped."
                | Lesson_Filter_Regex _ -> "An optional regular expression filter for the name of lessons to dump."
                | Content_Filter_Regex _ -> "An optional regular expression filter for the contents of the strings being dumped."
                | Languages _ -> "The set of languages which should be dumped."
                | Include_Key _ -> "Optionally includes the internal key used to distinguish the string in a tabbed column."
                | Include_Language _ -> "Optionally includes the language tag for each string in a tabbed column."
                | Include_Lesson _ -> "Optionally includes the lesson name for each string in a tabbed column."
                | Sample_Size _ -> "If set, only includes a random sample of the number of strings specified."
and ExportCommonArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<EqualsAssignment>] Lesson of string
    | [<EqualsAssignment>] Lesson_Regex of string
    | [<Mandatory; EqualsAssignment>] Export_Path of string
    | [<Mandatory; EqualsAssignment>] Recognition_Language of string
    | [<Mandatory; EqualsAssignment>] Production_Language of string
    | [<EqualsAssignment>] Recognition_Length_Limit of int
    | [<EqualsAssignment>] Production_Length_Limit of int
    | [<EqualsAssignment>] Recognition_Word_Limit of int
    | [<EqualsAssignment>] Production_Word_Limit of int
    | [<EqualsAssignment>] Export_Format of string
    | [<EqualsAssignment>] Deck_Name of string
    | Include_Listening_Cards
    | Only_With_Audio
    | Only_With_Extracted_Audio
    | [<EqualsAssignment>] Tts_Provider of string
    | [<EqualsAssignment>] Tts_Languages of string
    | [<EqualsAssignment>] Tts_Voice of string
    | [<EqualsAssignment>] Azure_Speech_Key of string
    | [<EqualsAssignment>] Azure_Speech_Region of string
    | [<EqualsAssignment>] Piper_Command of string
    | [<EqualsAssignment>] Piper_Model of string
    | [<EqualsAssignment>] Piper_Models_Dir of string
    | Piper_Cuda
    | [<EqualsAssignment>] Ffmpeg_Path of string
    | Regenerate_Tts
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "The name of the game whose content should be exported"
                | Lesson _ -> "The name of a single lesson to export"
                | Lesson_Regex _ -> "A regular expression defining which lesson should be exported"
                | Export_Path _ -> "The path to which the exported file (or .apkg package) should be written"
                | Recognition_Language _ -> "The 'source' language for the flash card"
                | Production_Language _ -> "The 'target' language for the flash card -- what you want to practice recalling"
                | Recognition_Length_Limit _ -> "The character limit of cards to include, based on the string in the recognition language"
                | Production_Length_Limit _ -> "The character limit for cards to include, based on the string in the production language"
                | Recognition_Word_Limit _ -> "The limit of whitespace-delimited words for cards to include, based on the string in the recognition language"
                | Production_Word_Limit _ -> "The limit of whitespace-delimited words for cards to include, based on the string in the production language"
                | Export_Format _ -> "Export format for Anki ('tsv' or 'apkg'). Defaults to 'apkg' if --export-path ends with .apkg, otherwise 'tsv'."
                | Deck_Name _ -> "Deck name to embed in an Anki .apkg export."
                | Include_Listening_Cards -> "For Anki .apkg export, also generate blind listening comprehension cards when production audio is present."
                | Only_With_Audio -> "For Anki .apkg export, only include card pairs that have audio on at least one side (after TTS resolution)."
                | Only_With_Extracted_Audio -> "For Anki .apkg export, filter to card pairs that have native extracted audio on at least one side BEFORE running TTS fallback."
                | Tts_Provider _ -> "TTS provider for missing audio on export: 'none', 'azure', or 'piper'."
                | Tts_Languages _ -> "Which card side(s) receive TTS fallback: 'production' (default when TTS is enabled), 'recognition', 'both', or 'none'."
                | Tts_Voice _ -> "Azure TTS voice override (single voice name or 'lang=voice;lang2=voice2')."
                | Azure_Speech_Key _ -> "Azure AI Speech subscription key (falls back to AZURE_SPEECH_KEY environment variable)."
                | Azure_Speech_Region _ -> "Azure AI Speech region, e.g. 'eastus' or 'westeurope' (falls back to AZURE_SPEECH_REGION environment variable)."
                | Piper_Command _ -> "Command to invoke Piper TTS (e.g. 'piper' or 'py -3.14 -m piper'). Auto-discovered if omitted."
                | Piper_Model _ -> "Path to a Piper .onnx voice model (or 'lang=path;lang2=path2')."
                | Piper_Models_Dir _ -> "Directory containing Piper .onnx voice models for automatic language matching."
                | Piper_Cuda -> "Enable CUDA GPU acceleration for Piper TTS (requires onnxruntime-gpu)."
                | Ffmpeg_Path _ -> "Optional path to ffmpeg (used to encode Piper WAV output to MP3)."
                | Regenerate_Tts -> "Ignore cached TTS entries and regenerate them with the current TTS provider/voice."
and ScanForTextArgs = 
    | [<Mandatory; EqualsAssignment>] Path of string
    | [<EqualsAssignment>] Minimum_Length of int
    | [<EqualsAssignment>] Maximum_Length of int
    | [<EqualsAssignment>] Dictionary_File of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Path _ -> "The path to recursively search for text"
                | Minimum_Length _ -> "The minimum length of string to match"
                | Maximum_Length _ -> "The maximum length of string to match"
                | Dictionary_File _ -> "Dictionary used to match words in files"
and [<RequireSubcommandAttribute>] BaseArgs =
    | [<Inherit; EqualsAssignment>] Database_Path of string
    | [<Inherit; EqualsAssignment>] Command_File of string
    | [<Inherit; EqualsAssignment>] Log_File of string
    | [<CliPrefix(CliPrefix.None)>] Import of ParseResults<ImportArgs>
    | [<CliPrefix(CliPrefix.None)>] List_Supported_Games of ParseResults<ListSupportedGamesArgs>
    | [<CliPrefix(CliPrefix.None)>] List_Supported_Languages of ParseResults<ListSupportedLanguagesArgs>
    | [<CliPrefix(CliPrefix.None)>] List_Games of ParseResults<ListGamesArgs>
    | [<CliPrefix(CliPrefix.None)>] List_Lessons of ParseResults<ListLessonsArgs>
    | [<CliPrefix(CliPrefix.None)>] Delete_Game of ParseResults<DeleteGameArgs>
    | [<CliPrefix(CliPrefix.None)>] Delete_Lessons of ParseResults<DeleteLessonsArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear_Tts_Cache of ParseResults<ClearTtsCacheArgs>
    | [<CliPrefix(CliPrefix.None)>] Dump_Text of ParseResults<DumpTextArgs>
    | [<CliPrefix(CliPrefix.None)>] Export_Anki of ParseResults<ExportCommonArgs>
    | [<CliPrefix(CliPrefix.None)>] Export_SuperMemo of ParseResults<ExportCommonArgs>
    | [<CliPrefix(CliPrefix.None)>] Export_Mnemosyne of ParseResults<ExportCommonArgs>
    | [<CliPrefix(CliPrefix.None)>] Export_AnyMemo of ParseResults<ExportCommonArgs>
    | [<CliPrefix(CliPrefix.None)>] Export_Brainscape of ParseResults<ExportCommonArgs>
    | [<CliPrefix(CliPrefix.None)>] Scan_For_Text of ParseResults<ScanForTextArgs>
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Database_Path _ -> "Directory for SQLite databases"
                | Command_File _ -> "File from which arguments should be read"
                | Log_File _ -> "Optional log file to which output should be redirected"
                | Import _ -> "Import localized content from a game"
                | List_Supported_Games _ -> "List all games supported for extraction"
                | List_Supported_Languages _ -> "List all languages that are supported by at least one game"
                | List_Games _ -> "List all imported games"
                | List_Lessons _ -> "List lessons, filtering by game and lesson names"
                | Delete_Game _ -> "Delete a single game"
                | Delete_Lessons _ -> "Delete lessons for a game, filtered by name"
                | Clear_Tts_Cache _ -> "Clear cached TTS audio entries for an imported game"
                | Dump_Text _ -> "Dumps extracted strings for inspection."
                | Export_Anki _ -> "Exports extracted text (and optional media/TTS into .apkg) for Anki"
                | Export_SuperMemo _ -> "Exports extracted text for use with the SuperMemo spaced repetition software"
                | Export_Mnemosyne _ -> "Exports extracted text for use with the Mnemosyne spaced repetition software"
                | Export_AnyMemo _ -> "Exports extracted text for use with the AnyMemo spaced repetition software"
                | Export_Brainscape _ -> "Exports extracted text as a UTF-8 CSV for bulk import into the Brainscape spaced repetition app via its 'Import Flashcards' feature"
                | Scan_For_Text _ -> "Used to scan arbitrary binary data for strings, to locate localized content"


let private multipleWhitespaceRegex = new Regex(@"\s\s+")

let private sanitizeGameNameForFile(n: string) = 
    Path.GetInvalidFileNameChars() |> Array.fold (fun (s: string)(c: char) -> s.Replace(c, '_')) n

let private makeDatabaseFilenameForGameName(n: string) = sanitizeGameNameForFile(n) + ".db3"
let private makeDatabaseFilenameForGameMetadata(gmd: GameMetadata) = makeDatabaseFilenameForGameName(gmd.name)

let private makeLessonRegexFilter(reOpt: string option) = 
    match reOpt with
    | Some(fr) ->
        let re = new Regex(fr)
        fun (t: LessonRecord) -> re.IsMatch(t.Name)
    | _ -> fun (_: LessonRecord) -> true

let private makeLessonNameFilter(nameOpt: string option) =
    match nameOpt with
    | Some(name) -> fun (t: LessonRecord) -> t.Name = name
    | _ -> fun (_: LessonRecord) -> true

let private normalizeIETFLanguageTagRegion(languageTag: string) = 
    let splits = languageTag.Split([| '-' |])
    if (splits.Length > 1) then
        splits.[splits.Length - 1] <- splits.[splits.Length - 1].ToUpper()
        String.Join("-", splits)
    else
        languageTag

let runImportAction(iPluginManager: IPluginManager, 
                    otw: TextWriter, dbRoot: string)(vc: ParseResults<ImportArgs>) = 
    let gameName = vc.GetResult(ImportArgs.Game)
    let pluginOpt = iPluginManager.GetPluginForGame(gameName)
    match pluginOpt with
    | Some(plugin) -> 
        // run the import action with the plugin
        let argv = 
            vc.GetResults(ImportArgs.Plugin_Arguments)
            |> Seq.collect (fun args -> args.Split([| '\r'; '\n'; ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries))
            |> Array.ofSeq

        let textDbPath = Path.Combine(dbRoot, makeDatabaseFilenameForGameName(gameName))
        let extractAudio = vc.Contains(ImportArgs.Extract_Audio)
        let extractImages = vc.Contains(ImportArgs.Extract_Images)

        use llDatabase = new LLDatabase(textDbPath)

        let extractedContent =
            if extractAudio || extractImages then
                match plugin with
                | :? IMediaExtractorPlugin as mediaPlugin ->
                    let mediaDbPath = makeMediaDatabasePathForTextDb textDbPath
                    use mediaDb = new MediaDatabase(mediaDbPath)
                    let mediaCtx =
                        {
                            MediaExtractionContext.ExtractAudio = extractAudio
                            ExtractImages = extractImages
                            MediaStore = Some (mediaDb :> IMediaStore)
                            AudioTools =
                                {
                                    FfmpegPath = vc.TryGetResult(ImportArgs.Ffmpeg_Path)
                                    VgmstreamPath = vc.TryGetResult(ImportArgs.Vgmstream_Path)
                                }
                            AudioOptions =
                                {
                                    AudioIngestOptions.Default with
                                        NormalizeLoudness = not (vc.Contains(ImportArgs.No_Normalize_Loudness))
                                        TrimSilence = vc.Contains(ImportArgs.Trim_Silence)
                                }
                            Log = fun msg -> otw.WriteLine(msg)
                        }
                    mediaPlugin.ExtractAllWithMedia(gameName, vc.GetResult(ImportArgs.Game_Dir), argv, mediaCtx)
                | _ ->
                    otw.WriteLine(sprintf "Note: plugin '%s' does not implement media extraction; extracting text only." plugin.Name)
                    plugin.ExtractAll(gameName, vc.GetResult(ImportArgs.Game_Dir), argv)
            else
                plugin.ExtractAll(gameName, vc.GetResult(ImportArgs.Game_Dir), argv)

        // Now, add lessons to the database, and remap lesson IDs in the cards before adding them.
        let actualLessonIds = 
            extractedContent.lessons 
            |> Array.map(fun l -> llDatabase.CreateOrUpdateLesson(l))
        let lessonIdMapping = 
            actualLessonIds
            |> Array.zip(extractedContent.lessons |> Array.map(fun l -> l.ID))
            |> Map.ofArray

        let remappedCards =
            extractedContent.cards
            |> Array.map(fun c -> { c with LessonID = lessonIdMapping |> Map.find(c.LessonID) })
            |> Array.filter(fun c -> not(String.IsNullOrWhiteSpace(c.Text)))
            |> Array.map(fun c -> { c with Text = multipleWhitespaceRegex.Replace(c.Text, " ").Trim() })
            |> Array.map(fun c -> { c with LanguageTag = normalizeIETFLanguageTagRegion(c.LanguageTag) })

        llDatabase.CreateOrUpdateCards(remappedCards)

    | _ ->
        failwith("Could not find installed plugin for '" + vc.GetResult(ImportArgs.Game) + "'")

let private resolveEffectiveExportTarget (baseTarget: CardExport.CommonExportTarget) (args: ParseResults<ExportCommonArgs>) =
    match baseTarget with
    | CardExport.CommonExportTarget.Anki ->
        match args.TryGetResult(ExportCommonArgs.Export_Format) with
        | Some fmt ->
            match fmt.Trim().ToLowerInvariant() with
            | "apkg" -> CardExport.CommonExportTarget.AnkiApkg
            | "tsv" -> CardExport.CommonExportTarget.Anki
            | other -> failwith ("Unrecognized --export-format '" + other + "'. Expected 'tsv' or 'apkg'.")
        | None ->
            let exportPath = args.GetResult(ExportCommonArgs.Export_Path)
            if exportPath.EndsWith(".apkg", StringComparison.OrdinalIgnoreCase) then
                CardExport.CommonExportTarget.AnkiApkg
            else
                CardExport.CommonExportTarget.Anki
    | other -> other

let private parseTtsLanguagesSelection (hasProvider: bool) (args: ParseResults<ExportCommonArgs>) : CardExport.TtsLanguageSelection =
    match args.TryGetResult(ExportCommonArgs.Tts_Languages) with
    | Some raw ->
        match raw.Trim().ToLowerInvariant() with
        | "none" -> CardExport.NoTts
        | "production" -> CardExport.ProductionOnly
        | "recognition" -> CardExport.RecognitionOnly
        | "both" -> CardExport.BothLanguages
        | other -> failwith ("Unrecognized --tts-languages '" + other + "'. Expected 'production', 'recognition', 'both', or 'none'.")
    | None ->
        if hasProvider then CardExport.ProductionOnly else CardExport.NoTts

let private createTtsProvider (args: ParseResults<ExportCommonArgs>) : (ITtsProvider * IDisposable option) option =
    match args.TryGetResult(ExportCommonArgs.Tts_Provider) with
    | None -> None
    | Some raw ->
        match raw.Trim().ToLowerInvariant() with
        | "none" | "" -> None
        | "azure" ->
            let key =
                args.TryGetResult(ExportCommonArgs.Azure_Speech_Key)
                |> Option.orElseWith (fun () ->
                    let envVal = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY")
                    if String.IsNullOrWhiteSpace(envVal) then None else Some envVal)
                |> Option.defaultValue ""
            let region =
                args.TryGetResult(ExportCommonArgs.Azure_Speech_Region)
                |> Option.orElseWith (fun () ->
                    let envVal = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION")
                    if String.IsNullOrWhiteSpace(envVal) then None else Some envVal)
                |> Option.defaultValue ""
            let provider =
                new AzureTtsProvider(
                    {
                        SubscriptionKey = key
                        Region = region
                        VoiceSpec = args.TryGetResult(ExportCommonArgs.Tts_Voice)
                        OutputFormat = AzureTtsConfig.DefaultOutputFormat
                    }
                )
            Some (provider :> ITtsProvider, Some (provider :> IDisposable))
        | "piper" ->
            let provider =
                new PiperTtsProvider(
                    {
                        PiperCommand = args.TryGetResult(ExportCommonArgs.Piper_Command)
                        ModelSpec = args.TryGetResult(ExportCommonArgs.Piper_Model)
                        ModelsDirectory = args.TryGetResult(ExportCommonArgs.Piper_Models_Dir)
                        UseCuda = args.Contains(ExportCommonArgs.Piper_Cuda)
                        AudioTools =
                            {
                                FfmpegPath = args.TryGetResult(ExportCommonArgs.Ffmpeg_Path)
                                VgmstreamPath = None
                            }
                        AudioOptions = AudioIngestOptions.Default
                    }
                )
            Some (provider :> ITtsProvider, Some (provider :> IDisposable))
        | other ->
            failwith ("Unrecognized --tts-provider '" + other + "'. Expected 'none', 'azure', or 'piper'.")

let private generateConfigForCommonExport(target: CardExport.CommonExportTarget, args: ParseResults<ExportCommonArgs>) = 
    {
        CardExport.CommonExporterConfiguration.Target = target
        CardExport.CommonExporterConfiguration.ExportPath = args.GetResult(ExportCommonArgs.Export_Path)
        CardExport.CommonExporterConfiguration.LessonToExport = args.TryGetResult(ExportCommonArgs.Lesson)
        CardExport.CommonExporterConfiguration.LessonRegexToExport = args.TryGetResult(ExportCommonArgs.Lesson_Regex)
        CardExport.CommonExporterConfiguration.RecognitionLanguage = args.GetResult(ExportCommonArgs.Recognition_Language)
        CardExport.CommonExporterConfiguration.ProductionLanguage = args.GetResult(ExportCommonArgs.Production_Language)
        CardExport.CommonExporterConfiguration.RecognitionLengthLimit = args.TryGetResult(ExportCommonArgs.Recognition_Length_Limit)
        CardExport.CommonExporterConfiguration.ProductionLengthLimit = args.TryGetResult(ExportCommonArgs.Production_Length_Limit)
        CardExport.CommonExporterConfiguration.RecognitionWordLimit = args.TryGetResult(ExportCommonArgs.Recognition_Word_Limit)
        CardExport.CommonExporterConfiguration.ProductionWordLimit = args.TryGetResult(ExportCommonArgs.Production_Word_Limit)
    }
    
let runCommonExportAction(iPluginManager: IPluginManager, outputTextWriter: TextWriter, dbRoot: string, target: CardExport.CommonExportTarget)(args: ParseResults<ExportCommonArgs>) = 
    let effectiveTarget = resolveEffectiveExportTarget target args
    let vc = generateConfigForCommonExport(effectiveTarget, args)
    let gameName = args.GetResult(ExportCommonArgs.Game)
    let textDbPath = Path.Combine(dbRoot, makeDatabaseFilenameForGameName(gameName))
    use llDatabase = new LLDatabase(textDbPath)

    match effectiveTarget with
    | CardExport.CommonExportTarget.AnkiApkg ->
        let ttsProviderAndDisp = createTtsProvider args
        let ttsProviderOpt = ttsProviderAndDisp |> Option.map fst
        let ttsLanguages = parseTtsLanguagesSelection ttsProviderOpt.IsSome args
        let mediaDbPath = makeMediaDatabasePathForTextDb textDbPath
        let shouldOpenMediaDb = File.Exists(mediaDbPath) || ttsProviderOpt.IsSome
        let mediaDbOpt =
            if shouldOpenMediaDb then Some (new MediaDatabase(mediaDbPath)) else None

        try
            let defaultDeckName =
                sprintf "%s (%s -> %s)" gameName vc.RecognitionLanguage vc.ProductionLanguage
            let deckName = args.GetResult(ExportCommonArgs.Deck_Name, defaultValue = defaultDeckName)
            let mediaConfig: CardExport.MediaExportConfiguration =
                {
                    DeckName = deckName
                    MediaStore = mediaDbOpt |> Option.map (fun m -> m :> IMediaStore)
                    TtsProvider = ttsProviderOpt
                    TtsLanguages = ttsLanguages
                    RegenerateTts = args.Contains(ExportCommonArgs.Regenerate_Tts)
                    IncludeListeningCards = args.Contains(ExportCommonArgs.Include_Listening_Cards)
                    OnlyWithAudio = args.Contains(ExportCommonArgs.Only_With_Audio)
                    OnlyWithExtractedAudio = args.Contains(ExportCommonArgs.Only_With_Extracted_Audio)
                }
            let exporter = new CardExport.CommonExporter(iPluginManager, outputTextWriter, llDatabase, vc, mediaConfig)
            exporter.RunExportAction()
        finally
            mediaDbOpt |> Option.iter (fun m -> (m :> IDisposable).Dispose())
            ttsProviderAndDisp |> Option.bind snd |> Option.iter (fun d -> d.Dispose())
    | _ ->
        let exporter = new CardExport.CommonExporter(iPluginManager, outputTextWriter, llDatabase, vc)
        exporter.RunExportAction()

let runScanForTextAction(otw: TextWriter)(vc: ParseResults<ScanForTextArgs>) = 
    let config = 
        {
            DebugTools.TextScannerConfiguration.Path = vc.GetResult(ScanForTextArgs.Path)
            DebugTools.TextScannerConfiguration.MinimumLength = vc.GetResult(ScanForTextArgs.Minimum_Length, defaultValue = 1)
            DebugTools.TextScannerConfiguration.MaximumLength = vc.GetResult(ScanForTextArgs.Maximum_Length, defaultValue = 10)
            DebugTools.TextScannerConfiguration.DictionaryFile = vc.TryGetResult(ScanForTextArgs.Dictionary_File)
        }
    let scanner = new DebugTools.StringScanner(config)
    let results = scanner.Scan()
    let writeFoundStrings(fn: string, strs: DebugTools.FoundString array) =
        let writeOffsetAndValue(s: DebugTools.FoundString) = 
            otw.WriteLine(s.offset.ToString("X8") + ": " + s.value)

        otw.WriteLine("Found strings in " + fn + ":")
        strs |> Array.iter writeOffsetAndValue

    results |> Array.iter writeFoundStrings

let private outputForGameMetadata(gmd: GameMetadata) = 
    let baseText = gmd.name + " [" + (String.Join(", ", gmd.supportedLanguages)) + "]"
    let mediaParts =
        [
            if gmd.supportedAudioLanguages.Length > 0 then
                yield "audio: " + String.Join(", ", gmd.supportedAudioLanguages)
            if gmd.supportedImageLanguages.Length > 0 then
                yield "images: " + String.Join(", ", gmd.supportedImageLanguages)
        ]
    if mediaParts.IsEmpty then
        baseText
    else
        baseText + " (" + String.Join("; ", mediaParts) + ")"

let private getTextDatabasePaths (dbRoot: string) =
    Directory.GetFiles(dbRoot, "*.db3", SearchOption.AllDirectories)
    |> Array.filter (not << isMediaDatabaseFilename)

/// <summary>
/// Runs the 'list-games' action, using the optional filter regex.
/// </summary>
/// <param name="otw">output channel</param>
/// <param name="dbRoot">root directory for databases</param>
/// <param name="vc">configuration for this verb handler</param>
let runListGamesAction(iPluginManager: IPluginManager, otw: TextWriter, dbRoot: string)(vc: ParseResults<ListGamesArgs>) = 
    let dbPaths = getTextDatabasePaths dbRoot
    let gameFilterFunc = 
        match vc.TryGetResult(ListGamesArgs.Filter_Regex) with
        | Some(rs) ->
            let rx = new Regex(rs)
            (fun gmd -> rx.IsMatch(gmd.name))
        | _ -> (fun _ -> true)
    let filteredGames = 
        iPluginManager.SupportedGames
        |> Array.filter gameFilterFunc

    let databaseNamesToGameNames = 
        filteredGames
        |> Array.zip (filteredGames |> Array.map makeDatabaseFilenameForGameMetadata)
        |> Map.ofArray

    let languagesToSearch = vc.TryGetResult(ListGamesArgs.Languages) |> Option.map Set.ofList
    let listGamesForDb(dbPath: string) = 
        match (databaseNamesToGameNames |> Map.tryFind(Path.GetFileName(dbPath)), languagesToSearch) with
        | (Some(gn), Some(lts)) ->
            use db = new LLDatabase(dbPath)
            let lessonLanguageSet = 
                db.Lessons
                |> Seq.collect(db.LanguagesForLesson)
                |> Set.ofSeq

            if (lts |> Set.intersect(lessonLanguageSet) |> Set.isEmpty) then
                None
            else
                Some(gn)
        | (Some(gn), _) -> Some(gn)
        | _ -> None

    dbPaths
    |> Array.collect (listGamesForDb >> Option.toArray)
    |> Array.sortBy(fun gmd -> gmd.name)
    |> Array.iter (outputForGameMetadata >> otw.WriteLine)

/// <summary>
/// Runs the 'list-supported-games' action.
/// </summary>
/// <param name="iPluginManager">plugin manager</param>
/// <param name="otw">output writer</param>
/// <param name="vc">configuration for this verb handler</param>
let runListSupportedGamesAction(iPluginManager: IPluginManager, otw: TextWriter)(vc: ParseResults<ListSupportedGamesArgs>) = 
    let languagesToSearch = vc.TryGetResult(ListSupportedGamesArgs.Languages) |> Option.map Set.ofList
    let audioOnly = vc.Contains(ListSupportedGamesArgs.Audio_Only)
    let languageFilterFunc = 
        fun (gmd: GameMetadata) ->
            let langMatch =
                match languagesToSearch with
                | Some(languages) -> languages |> Set.exists(fun l -> gmd.supportedLanguages |> Array.contains(l))
                | _ -> true
            let audioMatch = (not audioOnly) || gmd.SupportsAudio
            langMatch && audioMatch

    let results = 
        iPluginManager.SupportedGames
        |> Array.filter languageFilterFunc
        |> Array.sortBy(fun gmd -> gmd.name)

    match languagesToSearch with
    | None -> otw.WriteLine("Supported games:")
    | Some(languages) -> otw.WriteLine("Games that support [" + String.Join(", ", languages) + "]:")

    results
    |> Array.iter (outputForGameMetadata >> otw.WriteLine)

    otw.WriteLine(results.Length.ToString() + " results shown.")

/// <summary>
/// Runs the 'list-supported-languages' action.
/// </summary>
/// <param name="iPluginManager">plugin manager</param>
/// <param name="otw">output writer</param>
let runListSupportedLanguagesAction(iPluginManager: IPluginManager, otw: TextWriter) = 
    otw.WriteLine("Supported languages:")
    let languages = 
        iPluginManager.SupportedGames
        |> Array.collect (fun gmd -> gmd.supportedLanguages)
        |> Array.distinct
        |> Array.sort

    otw.WriteLine(String.Join(", ", languages))

let runListLessonsAction(iPluginManager: IPluginManager, otw: TextWriter, dbRoot: string)(vc: ParseResults<ListLessonsArgs>) = 
    let dbPaths = getTextDatabasePaths dbRoot

    let gameFilterFunc = 
        match vc.TryGetResult(ListLessonsArgs.Game_Regex) with
        | Some(rs) ->
            let rx = new Regex(rs)
            (fun gmd -> rx.IsMatch(gmd.name))
        | _ -> (fun _ -> true)
    let filteredGames = 
        iPluginManager.SupportedGames
        |> Array.filter gameFilterFunc

    let databaseNamesToGameNames = 
        filteredGames
        |> Array.zip (filteredGames |> Array.map makeDatabaseFilenameForGameMetadata)
        |> Map.ofArray

    let lessonFilter = makeLessonRegexFilter(vc.TryGetResult(ListLessonsArgs.Filter_Regex))

    let listLessonsForDb(dbPath: string) =
        use db = new LLDatabase(dbPath)

        match databaseNamesToGameNames |> Map.tryFind(Path.GetFileName(dbPath)) with
        | Some(_) ->
            db.Lessons
            |> Array.filter lessonFilter
            |> Array.map (fun l -> l.Name)
        | _ -> Array.empty

    dbPaths
    |> Array.collect listLessonsForDb
    |> Array.sort
    |> Array.iter otw.WriteLine

let runDeleteGameAction(otw: TextWriter, dbRoot: string)(vc: ParseResults<DeleteGameArgs>) = 
    let gameName = vc.GetResult(DeleteGameArgs.Game)
    let dbPath = Path.Combine(dbRoot, makeDatabaseFilenameForGameName(gameName))
    let mediaDbPath = makeMediaDatabasePathForTextDb dbPath
    try
        File.Delete(dbPath)
        if File.Exists(mediaDbPath) then
            File.Delete(mediaDbPath)
        otw.WriteLine("Deleted [" + gameName + "].")
    with
        | _ -> failwith("Couldn't delete database file [" + dbPath + "] for game [" + gameName + "]")
            
let runDeleteLessonsAction(otw: TextWriter, dbRoot: string)(vc: ParseResults<DeleteLessonsArgs>) = 
    let gameName = vc.GetResult(DeleteLessonsArgs.Game)
    let dbPath = Path.Combine(dbRoot, makeDatabaseFilenameForGameName(gameName))
    let mediaDbPath = makeMediaDatabasePathForTextDb dbPath

    if File.Exists(dbPath) then
        use db = new LLDatabase(dbPath)

        let lessonFilter = 
            let f1 = makeLessonRegexFilter(vc.TryGetResult(DeleteLessonsArgs.Filter_Regex))
            let f2 = makeLessonNameFilter(vc.TryGetResult(DeleteLessonsArgs.Lesson_Name))
            fun (l: LessonRecord) -> f1(l) || f2(l)

        let deleteLesson(l: LessonRecord) = 
            db.DeleteLesson(l)
            otw.WriteLine("Deleted lesson [" + l.Name + "]")

        db.Lessons 
        |> Array.filter lessonFilter
        |> Array.iter deleteLesson

        if File.Exists(mediaDbPath) then
            use mediaDb = new MediaDatabase(mediaDbPath)
            let pruned = mediaDb.PruneUnreferencedExtracted(db.ReferencedMediaHashes())
            if pruned > 0 then
                otw.WriteLine(sprintf "Pruned %d unreferenced extracted media asset(s) from companion media database." pruned)
    else
        failwith("Game database [" + dbPath + "] does not exist")

let runClearTtsCacheAction(otw: TextWriter, dbRoot: string)(vc: ParseResults<ClearTtsCacheArgs>) =
    let gameName = vc.GetResult(ClearTtsCacheArgs.Game)
    let dbPath = Path.Combine(dbRoot, makeDatabaseFilenameForGameName(gameName))
    let mediaDbPath = makeMediaDatabasePathForTextDb dbPath
    if File.Exists(mediaDbPath) then
        use mediaDb = new MediaDatabase(mediaDbPath)
        let langOpt = vc.TryGetResult(ClearTtsCacheArgs.Language)
        let cleared = mediaDb.ClearTtsCache(langOpt)
        otw.WriteLine(sprintf "Cleared %d cached TTS entries for [%s]." cleared gameName)
    else
        otw.WriteLine(sprintf "No companion media database exists for [%s]." gameName)

/// <summary>
/// Runs the 'dump-text' action.
/// </summary>
/// <param name="otw">output destination</param>
/// <param name="dbRoot">root path for databases</param>
/// <param name="vc">configuration for the action</param>
let runDumpTextAction(otw: TextWriter, dbRoot: string)(vc: ParseResults<DumpTextArgs>) = 
    let gameName = vc.GetResult(DumpTextArgs.Game)
    use db = new LLDatabase(Path.Combine(dbRoot, makeDatabaseFilenameForGameName(gameName)))

    let lessonNameFilter = makeLessonRegexFilter(vc.TryGetResult(DumpTextArgs.Lesson_Filter_Regex))
    let contentFilter = 
        match vc.TryGetResult(DumpTextArgs.Content_Filter_Regex) with
        | Some(filterRegex) -> 
            let regex = new Regex(filterRegex)
            (fun (c: CardRecord) -> regex.IsMatch(c.Text))
        | _ -> (fun (_: CardRecord) -> true)

    let languagesFilter(c: CardRecord) = 
        match vc.TryGetResult(DumpTextArgs.Languages) with
        | Some(parameterLanguages) -> parameterLanguages |> Seq.contains(c.LanguageTag)
        | _ -> true

    let dumpCard(c: CardRecord) = 
        let includeKeyPrefix = 
            if vc.Contains(DumpTextArgs.Include_Key) then
                c.Key + "\t"
            else
                ""

        let includeLanguagePrefix = 
            if vc.Contains(DumpTextArgs.Include_Language) then
                c.LanguageTag + "\t"
            else
                ""

        let includeLessonPrefix = 
            if vc.Contains(DumpTextArgs.Include_Lesson) then
                (db.Lessons |> Array.find(fun l -> l.ID = c.LessonID)).Name + "\t"
            else
                ""

        otw.WriteLine(includeLessonPrefix + includeKeyPrefix + includeLanguagePrefix + c.Text.Replace(Environment.NewLine, " ").Replace("\n", " ").Replace("\r", " ").Replace("\t", "    "))

    let getCardsForLesson(l: LessonRecord) = 
        db.CardsFromLesson(l)
        |> Array.filter languagesFilter
        |> Array.filter contentFilter
        |> Array.groupBy(fun c -> c.Key)
        |> Array.collect(fun (_, cs) -> cs |> Array.sortBy(fun c -> c.LanguageTag))

    // If a sample size was set, then generate a set of sampled keys of the appropriate
    // size, and then filter on those. Unfortunately, we need to sample on the tuple of
    // lesson and key, which makes this a little bit unwieldy.
    let sampleCards(cards: CardRecord array) = 
        let sampledIndices(sampleSize: int, eligibleCardCount: int) = 
            let r = new Random()
            Seq.initInfinite(fun _ -> r.Next(eligibleCardCount)) |> Seq.distinct |> Seq.take(sampleSize) |> Array.ofSeq

        let cardsByLessonAndKey = 
            cards
            |> Array.groupBy(fun c -> (c.LessonID, c.Key))

        let sampleSize = vc.GetResult(DumpTextArgs.Sample_Size, defaultValue = 0)
        if sampleSize = 0 then
            cards
        else
            sampledIndices(Math.Min(sampleSize, cardsByLessonAndKey.Length), cardsByLessonAndKey.Length)
            |> Array.map(fun i -> cardsByLessonAndKey |> Array.item(i)) 
            |> Array.collect(fun (_, c) -> c)

    db.Lessons 
    |> Array.filter lessonNameFilter
    |> Array.collect getCardsForLesson
    |> sampleCards
    |> Array.iter dumpCard

let rec parseCommands(cs: string array) = 
    let parser = ArgumentParser.Create<BaseArgs>(errorHandler = new ProcessExiter())
    let results = parser.Parse(cs)

    if (results.IsUsageRequested) || (results.GetAllResults() |> List.isEmpty) then
        Console.WriteLine(parser.PrintUsage())
        results
    else
        match results.TryGetResult(Command_File) with
        | Some(commandFile) ->
            let commands = File.ReadAllText(commandFile)
            let newArgs = commands.Split([| '\n'; '\r'; ' ' |], StringSplitOptions.RemoveEmptyEntries)
            parseCommands(newArgs)
        | _ -> results
    
[<EntryPoint>]
let main argv = 
    let results = parseCommands(argv)

    let pluginManager = new PluginManager()
    let iPluginManager = pluginManager :> IPluginManager
    
    // Set output encoding, and import codepages that are not included by default with netcore.
    Console.OutputEncoding <- Encoding.UTF8

    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

    let otw = 
        match results.TryGetResult(Log_File) with
        | Some(lf) -> new StreamWriter(lf, false, Encoding.UTF8) :> TextWriter
        | _ -> System.Console.Out

    let fldbPath = 
        match results.TryGetResult(Database_Path) with
        | Some(dbPath) when Directory.Exists(dbPath) -> dbPath
        | Some(dbPath) -> failwith("Database path [" + dbPath + "] doesn't exist")
        | _ -> 
            let defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"LudumLinguarum")
            if not(Directory.Exists(defaultPath)) then
                Directory.CreateDirectory(defaultPath) |> ignore
            defaultPath

    iPluginManager.Instantiate(otw, typeof<OneOffGames.OneOffGamesPlugin>, [||])
    iPluginManager.Instantiate(otw, typeof<InfinityAuroraEngine.AuroraPlugin>, [||])
    iPluginManager.Instantiate(otw, typeof<RggEngine.RggPlugin>, [||])

    match results.TryGetSubCommand() with
    | Some(Import ia) -> runImportAction(iPluginManager, otw, fldbPath)(ia)
    | Some(List_Supported_Games lsga) -> runListSupportedGamesAction(iPluginManager, otw)(lsga)
    | Some(List_Supported_Languages _) -> runListSupportedLanguagesAction(iPluginManager, otw)
    | Some(List_Games lga) -> runListGamesAction(iPluginManager, otw, fldbPath)(lga)
    | Some(List_Lessons lla) -> runListLessonsAction(iPluginManager, otw, fldbPath)(lla)
    | Some(Delete_Game dga) -> runDeleteGameAction(otw, fldbPath)(dga)
    | Some(Delete_Lessons dla) -> runDeleteLessonsAction(otw, fldbPath)(dla)
    | Some(Clear_Tts_Cache ctca) -> runClearTtsCacheAction(otw, fldbPath)(ctca)
    | Some(Dump_Text dta) -> runDumpTextAction(otw, fldbPath)(dta)
    | Some(Export_Anki exportArgs) -> runCommonExportAction(iPluginManager, otw, fldbPath, CardExport.CommonExportTarget.Anki)(exportArgs)
    | Some(Export_SuperMemo exportArgs) -> runCommonExportAction(iPluginManager, otw, fldbPath, CardExport.CommonExportTarget.SuperMemo)(exportArgs)
    | Some(Export_Mnemosyne exportArgs) -> runCommonExportAction(iPluginManager, otw, fldbPath, CardExport.CommonExportTarget.Mnemosyne)(exportArgs)
    | Some(Export_AnyMemo exportArgs) -> runCommonExportAction(iPluginManager, otw, fldbPath, CardExport.CommonExportTarget.AnyMemo)(exportArgs)
    | Some(Export_Brainscape exportArgs) -> runCommonExportAction(iPluginManager, otw, fldbPath, CardExport.CommonExportTarget.Brainscape)(exportArgs)
    | Some(Scan_For_Text sfta) -> runScanForTextAction(otw)(sfta)
    | None -> ()
    | _ -> failwith "unrecognized subcommand"

    0
