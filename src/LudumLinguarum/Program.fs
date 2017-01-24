module LudumLinguarumConsole

open Argu
open CommandLine
open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Reflection
open System.Text
open System.Text.RegularExpressions

type ImportArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<Mandatory; EqualsAssignment>] Game_Dir of string
    with 
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "game to import"
                | Game_Dir _ -> "directory where the game is located"
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
                | Filter_Regex _ -> "An optional regular expression to filter the list of lessons returned"
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
                | Languages _ -> "The comma-separated list of languages which should be dumped."
                | Include_Key _ -> "Optionally includes the internal key used to distinguish the string in a tabbed column."
                | Include_Language _ -> "Optionally includes the language tag for each string in a tabbed column."
                | Include_Lesson _ -> "Optionally includes the lesson name for each string in a tabbed column."
                | Sample_Size _ -> "If set, only includes a random sample of the number of strings specified."
and ExportAnkiArgs = 
    | [<Mandatory; EqualsAssignment>] Game of string
    | [<EqualsAssignment>] Lesson of string
    | [<EqualsAssignment>] Lesson_Regex of string
    | [<Mandatory; EqualsAssignment>] Export_Path of string
    | [<Mandatory; EqualsAssignment>] Recognition_Language of string
    | [<Mandatory; EqualsAssignment>] Production_Language of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Game _ -> "The name of the game whose content should be exported"
                | Lesson _ -> "The name of a single lesson to export"
                | Lesson_Regex _ -> "A regular expression defining which lesson should be exported"
                | Export_Path _ -> "The path to which the text file containing importable Anki cards should be written"
                | Recognition_Language _ -> "The 'source' language for the flash card"
                | Production_Language _ -> "The 'target' language for the flash card -- what you want to practice recalling"
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
    | [<CliPrefix(CliPrefix.None)>] List_Games of ParseResults<ListGamesArgs>
    | [<CliPrefix(CliPrefix.None)>] List_Lessons of ParseResults<ListLessonsArgs>
    | [<CliPrefix(CliPrefix.None)>] Delete_Game of ParseResults<DeleteGameArgs>
    | [<CliPrefix(CliPrefix.None)>] Delete_Lessons of ParseResults<DeleteLessonsArgs>
    | [<CliPrefix(CliPrefix.None)>] Dump_Text of ParseResults<DumpTextArgs>
    | [<CliPrefix(CliPrefix.None)>] Export_Anki of ParseResults<ExportAnkiArgs>
    | [<CliPrefix(CliPrefix.None)>] Scan_For_Text of ParseResults<ScanForTextArgs>
    | [<MainCommand>] Plugin_Arguments of string
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Database_Path _ -> "Path to the SQLite database file to use"
                | Command_File _ -> "File from which arguments should be read"
                | Log_File _ -> "Optional log file to which output should be redirected"
                | Import _ -> "Import localized content from a game"
                | List_Supported_Games _ -> "List all games supported for extraction"
                | List_Games _ -> "List all imported games"
                | List_Lessons _ -> "List lessons, filtering by game and lesson names"
                | Delete_Game _ -> "Delete a single game"
                | Delete_Lessons _ -> "Delete lessons for a game, filtered by name"
                | Dump_Text _ -> "Dumps extracted strings for inspection."
                | Export_Anki _ -> "Exports extracted text for use with the Anki spaced repetition program"
                | Scan_For_Text _ -> "Used to scan arbitrary binary data for strings, to locate localized content"
                | Plugin_Arguments _ -> ""

let private makeGameRegexFilter(reOpt: string option) = 
    match reOpt with
    | Some(fr) -> 
        let re = new Regex(fr)
        fun (t: GameRecord) -> re.IsMatch(t.Name)
    | _ -> fun (_: GameRecord) -> true
    
let private makeLessonRegexFilter(reOpt: string option) = 
    match reOpt with
    | Some(fr) ->
        let re = new Regex(fr)
        fun (t: LessonRecord) -> re.IsMatch(t.Name)
    | _ -> fun (_: LessonRecord) -> true

let private makeGameNameFilter(nameOpt: string option) = 
    match nameOpt with
    | Some(name) -> fun (t: GameRecord) -> t.Name = name
    | _ -> fun (_: GameRecord) -> true

let private makeLessonNameFilter(nameOpt: string option) =
    match nameOpt with
    | Some(name) -> fun (t: LessonRecord) -> t.Name = name
    | _ -> fun (_: LessonRecord) -> true

let runImportAction(iPluginManager: IPluginManager, 
                    _: TextWriter, llDatabase: LLDatabase, argv: string array)(vc: ParseResults<ImportArgs>) = 
    let pluginOpt = iPluginManager.GetPluginForGame(vc.GetResult(<@ ImportArgs.Game @>))
    match pluginOpt with
    | Some(plugin) -> 
        // run the import action with the plugin
        plugin.ExtractAll(vc.GetResult(<@ ImportArgs.Game @>), vc.GetResult(<@ ImportArgs.Game_Dir @>), llDatabase, argv)
    | _ ->
        failwith("Could not find installed plugin for '" + vc.GetResult(<@ ImportArgs.Game @>) + "'")

let runExportAnkiAction(iPluginManager: IPluginManager, 
                        outputTextWriter: TextWriter, llDatabase: LLDatabase)(vc: CardExport.AnkiExporterConfiguration) = 
    let exporter = new CardExport.AnkiExporter(iPluginManager, outputTextWriter, llDatabase, vc)
    exporter.RunExportAction()

let runScanForTextAction(otw: TextWriter)(vc: ParseResults<ScanForTextArgs>) = 
    let config = 
        {
            DebugTools.TextScannerConfiguration.Path = vc.GetResult(<@ ScanForTextArgs.Path @>)
            DebugTools.TextScannerConfiguration.MinimumLength = vc.GetResult(<@ ScanForTextArgs.Minimum_Length @>)
            DebugTools.TextScannerConfiguration.MaximumLength = vc.GetResult(<@ ScanForTextArgs.Maximum_Length @>)
            DebugTools.TextScannerConfiguration.DictionaryFile = vc.GetResult(<@ ScanForTextArgs.Dictionary_File @>)
        }
    let scanner = new DebugTools.StringScanner(config)
    let results = scanner.Scan()
    let writeFoundStrings(fn: string, strs: DebugTools.FoundString array) =
        let writeOffsetAndValue(s: DebugTools.FoundString) = 
            otw.WriteLine(s.offset.ToString("X8") + ": " + s.value)

        otw.WriteLine("Found strings in " + fn + ":")
        strs |> Array.iter writeOffsetAndValue

    results |> Array.iter writeFoundStrings

/// <summary>
/// Runs the 'list-games' action, using the optional filter regex.
/// </summary>
/// <param name="otw">output channel</param>
/// <param name="db">database</param>
/// <param name="vc">configuration for this verb handler</param>
let runListGamesAction(otw: TextWriter, db: LLDatabase)(vc: ParseResults<ListGamesArgs>) = 
    let games = db.Games
    let regexFilter = makeGameRegexFilter(vc.TryGetResult(<@ ListGamesArgs.Filter_Regex @>))
    let languagesFilter(g: GameRecord, languages: string array) = 
        vc.GetResult(<@ ListGamesArgs.Languages @>)
        |> Seq.forall(fun l -> languages |> Array.contains(l)) 

    let languagesForGame(g: GameRecord) = 
        db.Lessons 
        |> Array.filter (fun l -> l.GameID = g.ID)
        |> Array.collect(db.LanguagesForLesson >> Array.ofList >> Array.sort)
        |> Array.distinct
    
    games 
    |> Array.filter regexFilter
    |> Array.sortBy (fun g -> g.Name)
    |> Array.map(fun g -> (g, languagesForGame(g)))
    |> Array.filter languagesFilter
    |> Array.map(fun (g, languages) -> "[" + g.Name + "], [" + String.Join(", ", languages) + "]")
    |> Array.iter otw.WriteLine

/// <summary>
/// Runs the 'list-supported-games' action.
/// </summary>
/// <param name="iPluginManager">plugin manager</param>
/// <param name="otw">output writer</param>
/// <param name="_">configuration for this verb handler</param>
let runListSupportedGamesAction(iPluginManager: IPluginManager, otw: TextWriter) = 
    otw.WriteLine("Supported games:")
    iPluginManager.SupportedGames
    |> Array.sort
    |> Array.iter otw.WriteLine

let runListLessonsAction(otw: TextWriter, db: LLDatabase)(vc: ParseResults<ListLessonsArgs>) = 
    let allowedGameIds = 
        db.Games
        |> Array.filter(makeGameRegexFilter(vc.TryGetResult(<@ ListLessonsArgs.Game_Regex @>)))
        |> Array.map(fun t -> t.ID)

    let checkGameFilterForLesson(l: LessonRecord) = 
        allowedGameIds |> Array.contains(l.GameID)

    let lessonFilter = makeLessonRegexFilter(vc.TryGetResult(<@ ListLessonsArgs.Filter_Regex @>))

    db.Lessons
    |> Array.filter checkGameFilterForLesson
    |> Array.filter lessonFilter
    |> Array.map (fun l -> l.Name)
    |> Array.sort
    |> Array.iter otw.WriteLine

let runDeleteGameAction(otw: TextWriter, db: LLDatabase)(vc: ParseResults<DeleteGameArgs>) = 
    let deleteGame(g: GameRecord) = 
        db.DeleteGame(g)
        otw.WriteLine("deleted game [" + g.Name + "]")

    db.Games
    |> Array.filter(makeGameNameFilter(Some(vc.GetResult(<@ DeleteGameArgs.Game @>))))
    |> Array.tryHead
    |> Option.iter deleteGame

let runDeleteLessonsAction(otw: TextWriter, db: LLDatabase)(vc: ParseResults<DeleteLessonsArgs>) = 
    let gameOpt = 
        db.Games
        |> Array.filter(makeGameNameFilter(Some(vc.GetResult(<@ DeleteLessonsArgs.Game @>))))
        |> Array.tryHead

    let lessonsForGameFilter(id: int)(l: LessonRecord) = 
        l.GameID = id

    let lessonFilter = 
        let f1 = makeLessonRegexFilter(vc.TryGetResult(<@ DeleteLessonsArgs.Filter_Regex @>))
        let f2 = makeLessonNameFilter(vc.TryGetResult(<@ DeleteLessonsArgs.Lesson_Name @>))
        fun (l: LessonRecord) -> f1(l) || f2(l)

    let deleteLesson(l: LessonRecord) = 
        db.DeleteLesson(l)
        otw.WriteLine("Deleted lesson [" + l.Name + "]")

    gameOpt
    |> Option.map(fun t -> db.Lessons |> Array.filter(lessonsForGameFilter(t.ID)) |> Array.filter lessonFilter)
    |> Option.iter(Array.iter deleteLesson)

/// <summary>
/// Runs the 'dump-text' action.
/// </summary>
/// <param name="otw">output destination</param>
/// <param name="db">content database</param>
/// <param name="vc">configuration for the action</param>
let runDumpTextAction(otw: TextWriter, db: LLDatabase)(vc: ParseResults<DumpTextArgs>) = 
    let gameOpt = 
        db.Games
        |> Array.filter(makeGameNameFilter(Some(vc.GetResult(<@ DumpTextArgs.Game @>))))
        |> Array.tryHead

    let lessonsForGameFilter(id: int)(l: LessonRecord) = 
        l.GameID = id

    let lessonNameFilter = makeLessonRegexFilter(Some(vc.GetResult(<@ DumpTextArgs.Lesson_Filter_Regex @>)))
    let contentFilter = 
        let regex = new Regex(vc.GetResult(<@ DumpTextArgs.Content_Filter_Regex @>))
        (fun (c: CardRecord) -> regex.IsMatch(c.Text))

    let languagesFilter(c: CardRecord) = 
        vc.GetResult(<@ DumpTextArgs.Languages @>) |> Seq.contains(c.LanguageTag)

    let dumpCard(c: CardRecord) = 
        let includeKeyPrefix = 
            if vc.GetResult(<@ DumpTextArgs.Include_Key @>) then
                c.Key + "\t"
            else
                ""

        let includeLanguagePrefix = 
            if vc.GetResult(<@ DumpTextArgs.Include_Language @>) then
                c.LanguageTag + "\t"
            else
                ""

        let includeLessonPrefix = 
            if vc.GetResult(<@ DumpTextArgs.Include_Lesson @>) then
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

        let sampleSize = vc.GetResult(<@ DumpTextArgs.Sample_Size @>)
        if sampleSize = 0 then
            cards
        else
            sampledIndices(Math.Min(sampleSize, cardsByLessonAndKey.Length), cardsByLessonAndKey.Length)
            |> Array.map(fun i -> cardsByLessonAndKey |> Array.item(i)) 
            |> Array.collect(fun (_, c) -> c)

    gameOpt
    |> Option.map(fun g -> db.Lessons |> Array.filter(lessonsForGameFilter(g.ID)) |> Array.filter lessonNameFilter)
    |> Option.map(fun lessons -> (lessons |> Array.collect getCardsForLesson) |> sampleCards)
    |> Option.iter(fun cards -> cards |> Array.iter dumpCard)

let rec parseCommands(cs: string array) = 
    let parser = ArgumentParser.Create<BaseArgs>(errorHandler = new ProcessExiter())
    let results = parser.Parse(cs)

    if (results.IsUsageRequested) || (results.GetAllResults() |> List.isEmpty) then
        Console.WriteLine(parser.PrintUsage())
        results
    else
        match results.TryGetResult(<@ Command_File @>) with
        | Some(commandFile) ->
            let commands = File.ReadAllText(commandFile)
            let newArgs = commands.Split([| '\n'; '\r'; ' ' |], StringSplitOptions.RemoveEmptyEntries)
            parseCommands(newArgs)
        | _ -> results
    
let private instantiatePluginType(iPluginManager: IPluginManager, otw: TextWriter, otherArgs: string array)(t: Type) = 
    try
        iPluginManager.Instantiate(otw, t, otherArgs)
    with
    | ex -> 
        otw.WriteLine("Failed to load plugin " + t.AssemblyQualifiedName + ":" + Environment.NewLine + ex.ToString())
        ()

let private loadAndInstantiatePlugin(iPluginManager: IPluginManager, otw: TextWriter, otherArgs: string array)(pluginFilename: string) = 
    try
        let loadedAssembly = Assembly.LoadFile(pluginFilename)
        iPluginManager.Discover(loadedAssembly) 
        |> Array.ofList 
        |> Array.iter(instantiatePluginType(iPluginManager, otw, otherArgs))
    with
        | _ -> ()

let private loadAllPlugins(iPluginManager: IPluginManager, otw: TextWriter, otherArgs: string array) = 
    // try and load all plugins that are alongside this executable
    let bundledPluginsPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let bundledPlugins = Directory.GetFiles(bundledPluginsPath, "*.dll", SearchOption.AllDirectories)
    try
        bundledPlugins |> Array.iter(loadAndInstantiatePlugin(iPluginManager, otw, otherArgs))
    with
    | ex ->
        otw.WriteLine("Failed to load plugins: " + Environment.NewLine + ex.ToString())
        ()


[<EntryPoint>]
let main argv = 
    let results = parseCommands(argv)

    let pluginManager = new PluginManager()
    let iPluginManager = pluginManager :> IPluginManager

    let otw = 
        match results.TryGetResult(<@ Log_File @>) with
        | Some(lf) -> new StreamWriter(lf, false, Encoding.UTF8) :> TextWriter
        | _ -> System.Console.Out

    let fldbPath = 
        match results.TryGetResult(<@ Database_Path @>) with
        | Some(dbPath) -> dbPath
        | _ -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"LudumLinguarum\LudumLinguarum.db3")

    if (not(Directory.Exists(Path.GetDirectoryName(fldbPath)))) then
        Directory.CreateDirectory(Path.GetDirectoryName(fldbPath)) |> ignore

    let lldb = new LLDatabase(fldbPath)

    let remainderOpt = results.TryGetResult(<@ Plugin_Arguments @>)
    let remainderArgs = 
        match remainderOpt with
        | Some(remainder) -> remainder.Split([| '\r'; '\n'; ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
        | _ -> [||]

    loadAllPlugins(iPluginManager, otw, remainderArgs)

    match results.TryGetSubCommand() with
    | Some(Import ia) -> runImportAction(iPluginManager, otw, lldb, remainderArgs)(ia)
    | Some(List_Supported_Games _) -> runListSupportedGamesAction(iPluginManager, otw)
    | Some(List_Games lga) -> runListGamesAction(otw, lldb)(lga)
    | Some(List_Lessons lla) -> runListLessonsAction(otw, lldb)(lla)
    | Some(Delete_Game dga) -> runDeleteGameAction(otw, lldb)(dga)
    | Some(Delete_Lessons dla) -> runDeleteLessonsAction(otw, lldb)(dla)
    | Some(Dump_Text dta) -> runDumpTextAction(otw, lldb)(dta)
    | None -> ()
    | _ -> failwith "unrecognized subcommand"

    0
