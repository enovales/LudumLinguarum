module LudumLinguarumConsole

open CommandLine
open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions

/// <summary>
/// Generic configuration for game imports.
/// </summary>
[<CommandLine.Verb("import", HelpText = "Import localized content from a game")>]
type ImportConfiguration() = 
    [<CommandLine.Option(Required = true, HelpText = "The name of the game from the supported games list (list-supported-games) that you wish to extract.")>]
    member val Game = "" with get, set

    [<CommandLine.Option("game-dir", Required = true, HelpText = "The root directory of the game.")>]
    member val GameDir = "" with get, set

/// <summary>
/// Configuration for the 'list-games' verb.
/// </summary>
[<CommandLine.Verb("list-games", HelpText = "List all imported games")>]
type ListGamesConfiguration() = 
    [<CommandLine.Option("filter-regex", Required = false, HelpText = "An optional regular expression to filter the list of games")>]
    member val FilterRegex = "" with get, set

    [<CommandLine.Option("language", Required = false, HelpText = "Specifies a language that the game must support. Apply this multiple times if desired.")>]
    member val Languages = Seq.empty<string> with get, set

/// <summary>
/// Configuration for the 'list-supported-games' verb.
/// </summary>
[<CommandLine.Verb("list-supported-games", HelpText = "List all games supported for extraction")>]
type ListSupportedGamesConfiguration() = 
    class
    end

/// <summary>
/// Configuration for the 'list-lessons' verb.
/// </summary>
[<CommandLine.Verb("list-lessons", HelpText = "List lessons, filtering by game and lesson names")>]
type ListLessonsConfiguration() = 
    [<CommandLine.Option("game-regex", Required = false, HelpText = "An optional regular expression to filter the list of games searched")>]
    member val GameRegex = "" with get, set

    [<CommandLine.Option("filter-regex", Required = false, HelpText = "An optional regular expression to filter the list of lessons returned")>]
    member val FilterRegex = "" with get, set

/// <summary>
/// Configuration for the 'delete-game' verb.
/// </summary>
[<CommandLine.Verb("delete-game", HelpText = "Delete a single game")>]
type DeleteGameConfiguration() = 
    [<CommandLine.Option(Required = true, HelpText = "The name of the game to delete")>]
    member val Game = "" with get, set

/// <summary>
/// Configuration for the 'delete-lessons' verb.
/// </summary>
[<CommandLine.Verb("delete-lessons", HelpText = "Delete lessons for a game, filtered by name")>]
type DeleteLessonsConfiguration() = 
    [<CommandLine.Option(Required = true, HelpText = "The name of the game for which lessons are to be deleted.")>]
    member val Game = "" with get, set

    [<CommandLine.Option("filter-regex", Required = false, HelpText = "An optional regular expression filter for the name of lessons to delete. Either this or lesson-name must be specified.")>]
    member val FilterRegex = "" with get, set

    [<CommandLine.Option("lesson-name", Required = false, HelpText = "The name of the lesson to delete. Either this or filter-regex must be specified.")>]
    member val LessonName = "" with get, set

/// <summary>
/// Root configuration for the program.
/// </summary>
type LudumLinguarumConfiguration() = 
    [<CommandLine.Option("database-path", Required = false, HelpText = "Path to the SQLite database file to use")>]
    member val DatabasePath = "" with get, set

    [<CommandLine.Option("command-file", Required = false, HelpText = "File from which arguments should be read")>]
    member val CommandFile = "" with get, set

    [<CommandLine.Option("log-file", Required = false, HelpText = "Optional log file to which output should be redirected")>]
    member val LogFile = "" with get, set

let runImportAction(iPluginManager: IPluginManager, 
                    _: TextWriter, llDatabase: LLDatabase, argv: string array)(vc: ImportConfiguration) = 
    let pluginOpt = iPluginManager.GetPluginForGame(vc.Game)
    match pluginOpt with
    | Some(plugin) -> 
        // run the import action with the plugin
        plugin.ExtractAll(vc.Game, vc.GameDir, llDatabase, argv)
    | _ ->
        failwith("Could not find installed plugin for '" + vc.Game + "'")

let runExportAnkiAction(iPluginManager: IPluginManager, 
                        outputTextWriter: TextWriter, llDatabase: LLDatabase)(vc: CardExport.AnkiExporterConfiguration) = 
    let exporter = new CardExport.AnkiExporter(iPluginManager, outputTextWriter, llDatabase, vc)
    exporter.RunExportAction()

let runScanForTextAction(otw: TextWriter)(vc: DebugTools.TextScannerConfiguration) = 
    let scanner = new DebugTools.StringScanner(vc)
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
let runListGamesAction(otw: TextWriter, db: LLDatabase)(vc: ListGamesConfiguration) = 
    let games = db.Games
    let regexFilter = 
        if (String.IsNullOrWhiteSpace(vc.FilterRegex)) then
            fun _ -> true
        else
            let regex = new Regex(vc.FilterRegex)
            fun (t: GameRecord) -> regex.IsMatch(t.Name)

    let languagesFilter(g: GameRecord, languages: string array) = 
        vc.Languages
        |> Seq.forall(fun l -> languages |> Array.contains(l)) 

    let eligibleGames = games |> Array.filter regexFilter
    let languagesForGame(g: GameRecord) = 
        db.Lessons 
        |> Array.filter (fun l -> l.GameID = g.ID)
        |> Array.collect(db.LanguagesForLesson >> Array.ofList >> Array.sort)
        |> Array.distinct
    
    eligibleGames
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
let runListSupportedGamesAction(iPluginManager: IPluginManager, otw: TextWriter)(_: ListSupportedGamesConfiguration) = 
    otw.WriteLine("Supported games:")
    iPluginManager.SupportedGames
    |> Array.sort
    |> Array.iter otw.WriteLine

let runListLessonsAction(otw: TextWriter, db: LLDatabase)(vc: ListLessonsConfiguration) = 
    let gameFilter = 
        if (String.IsNullOrWhiteSpace(vc.GameRegex)) then
            (fun _ -> true)
        else
            let regex = new Regex(vc.GameRegex)
            (fun (t: GameRecord) -> regex.IsMatch(t.Name))

    let allowedGameIds = 
        db.Games
        |> Array.filter gameFilter
        |> Array.map(fun t -> t.ID)

    let checkGameFilterForLesson(l: LessonRecord) = 
        allowedGameIds |> Array.contains(l.GameID)

    let lessonFilter = 
        if (String.IsNullOrWhiteSpace(vc.FilterRegex)) then
            (fun _ -> true)
        else
            let regex = new Regex(vc.FilterRegex)
            (fun (t: LessonRecord) -> regex.IsMatch(t.Name))
        
    db.Lessons
    |> Array.filter checkGameFilterForLesson
    |> Array.filter lessonFilter
    |> Array.map (fun l -> l.Name)
    |> Array.sort
    |> Array.iter otw.WriteLine

let runDeleteGameAction(otw: TextWriter, db: LLDatabase)(vc: DeleteGameConfiguration) = 
    let games = db.Games
    let filter(g: GameRecord) = vc.Game = g.Name
    let deleteGame(g: GameRecord) = 
        db.DeleteGame(g)
        otw.WriteLine("deleted game [" + g.Name + "]")

    games
    |> Array.filter filter
    |> Array.tryHead
    |> Option.iter deleteGame

let runDeleteLessonsAction(otw: TextWriter, db: LLDatabase)(vc: DeleteLessonsConfiguration) = 
    let games = db.Games
    let filter(g: GameRecord) = vc.Game = g.Name
    let gameOpt = 
        games
        |> Array.filter filter
        |> Array.tryHead

    let lessonsForGameFilter(id: int)(l: LessonRecord) = 
        l.GameID = id

    let lessonNameFilter = 
        if (String.IsNullOrWhiteSpace(vc.FilterRegex)) then
            (fun t -> t.Name = vc.LessonName)
        else
            let regex = new Regex(vc.FilterRegex)
            (fun (t: LessonRecord) -> regex.IsMatch(t.Name))

    let deleteLesson(l: LessonRecord) = 
        db.DeleteLesson(l)
        otw.WriteLine("Deleted lesson [" + l.Name + "]")

    gameOpt
    |> Option.map(fun t -> db.Lessons |> Array.filter(lessonsForGameFilter(t.ID)) |> Array.filter lessonNameFilter)
    |> Option.iter(Array.iter deleteLesson)

let runConfiguration(clp: CommandLine.Parser, argv: string array)(c: LudumLinguarumConfiguration) = 
    let pluginManager = new PluginManager()
    let iPluginManager = pluginManager :> IPluginManager

    // try and load all plugins that are alongside this executable
    let bundledPluginsPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let bundledPlugins = Directory.GetFiles(bundledPluginsPath, "*.dll", SearchOption.AllDirectories)

    let otw = 
        if (String.IsNullOrWhiteSpace(c.LogFile)) then
            System.Console.Out
        else
            new StreamWriter(c.LogFile) :> TextWriter

    let instantiatePluginType(t: Type) = 
        try
            iPluginManager.Instantiate(otw, t, argv)
        with
        | ex -> 
            otw.WriteLine("Failed to load plugin " + t.AssemblyQualifiedName + ":" + Environment.NewLine + ex.ToString())
            ()

    let loadAndInstantiatePlugin(pluginFilename: string) = 
        try
            let loadedAssembly = Assembly.LoadFile(pluginFilename)
            iPluginManager.Discover(loadedAssembly) 
            |> Array.ofList 
            |> Array.iter instantiatePluginType
        with
        | _ -> ()
            
    try
        try
            bundledPlugins |> Array.iter loadAndInstantiatePlugin
        with
        | ex ->
            otw.WriteLine("Failed to load plugins: " + Environment.NewLine + ex.ToString())
            ()

        let effectiveFLDBPath = 
            match c.DatabasePath with
            | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"LudumLinguarum\LudumLinguarum.db3")
            | nonDefaultPath -> nonDefaultPath

        if (not(Directory.Exists(Path.GetDirectoryName(effectiveFLDBPath)))) then
            Directory.CreateDirectory(Path.GetDirectoryName(effectiveFLDBPath)) |> ignore

        // Set up the database.
        let lldb = new LLDatabase(effectiveFLDBPath)

        // Run the parser, but with handlers for each possible configuration type.
        clp.ParseArguments<
            ImportConfiguration, 
            CardExport.AnkiExporterConfiguration, 
            DebugTools.TextScannerConfiguration, 
            ListGamesConfiguration, 
            ListSupportedGamesConfiguration, 
            ListLessonsConfiguration, 
            DeleteGameConfiguration, 
            DeleteLessonsConfiguration
         >(argv)
         .WithParsed<ImportConfiguration>(new Action<ImportConfiguration>(runImportAction(iPluginManager, otw, lldb, argv)))
         .WithParsed<CardExport.AnkiExporterConfiguration>(new Action<CardExport.AnkiExporterConfiguration>(runExportAnkiAction(iPluginManager, otw, lldb)))
         .WithParsed<DebugTools.TextScannerConfiguration>(new Action<DebugTools.TextScannerConfiguration>(runScanForTextAction(otw)))
         .WithParsed<ListGamesConfiguration>(new Action<ListGamesConfiguration>(runListGamesAction(otw, lldb)))
         .WithParsed<ListSupportedGamesConfiguration>(new Action<ListSupportedGamesConfiguration>(runListSupportedGamesAction(iPluginManager, otw)))
         .WithParsed<ListLessonsConfiguration>(new Action<ListLessonsConfiguration>(runListLessonsAction(otw, lldb)))
         .WithParsed<DeleteGameConfiguration>(new Action<DeleteGameConfiguration>(runDeleteGameAction(otw, lldb)))
         .WithParsed<DeleteLessonsConfiguration>(new Action<DeleteLessonsConfiguration>(runDeleteLessonsAction(otw, lldb)))
         |> ignore
    finally
        otw.Flush() |> ignore

    ()


let internal generateFinalConfigurationAndRun(clp: CommandLine.Parser, argv: string array)(c: LudumLinguarumConfiguration) =
    // if there was a command line file specified, read that file and use its contents as
    // the real set of command line arguments.
    if (not(String.IsNullOrWhiteSpace(c.CommandFile))) then
        let commandFileConfigurationText = File.ReadAllText(c.CommandFile).Trim()
        let commandFileArgv = commandFileConfigurationText.Split(' ')
        clp
            .ParseArguments<LudumLinguarumConfiguration>(commandFileArgv)
            .WithParsed(runConfiguration(clp, commandFileArgv))
            .WithNotParsed(fun _ -> failwith("failed to parse configuration file [" + c.CommandFile + "]"))
        |> ignore
    else
        runConfiguration(clp, argv)(c)

[<EntryPoint>]
let main argv = 
    let commandLineParserSetupAction(t: CommandLine.ParserSettings) = 
        t.HelpWriter <- System.Console.Out
        t.EnableDashDash <- true
        t.IgnoreUnknownArguments <- true

    let commandLineParser = new CommandLine.Parser(new Action<CommandLine.ParserSettings>(commandLineParserSetupAction))
    let runFn(c: LudumLinguarumConfiguration) = 
        generateFinalConfigurationAndRun(commandLineParser, argv)(c)

    commandLineParser
        .ParseArguments<LudumLinguarumConfiguration>(argv)
        .WithParsed<LudumLinguarumConfiguration>(new Action<LudumLinguarumConfiguration>(runFn))
    |> ignore

    0
