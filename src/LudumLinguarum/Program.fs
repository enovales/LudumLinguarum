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
    [<CommandLine.Option(Required = true)>]
    member val Game = "" with get, set

    [<CommandLine.Option("game-dir", Required = true)>]
    member val GameDir = "" with get, set

/// <summary>
/// Configuration for the 'list-games' verb.
/// </summary>
[<CommandLine.Verb("list-games", HelpText = "List all imported games")>]
type ListGamesConfiguration() = 
    [<CommandLine.Option("filter-regex", Required = false)>]
    member val FilterRegex = "" with get, set

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
    [<CommandLine.Option("game-regex", Required = false)>]
    member val GameRegex = "" with get, set

    [<CommandLine.Option("filter-regex", Required = false)>]
    member val FilterRegex = "" with get, set

/// <summary>
/// Configuration for the 'delete-game' verb.
/// </summary>
[<CommandLine.Verb("delete-game", HelpText = "Delete a single game")>]
type DeleteGameConfiguration() = 
    [<CommandLine.Option(Required = true)>]
    member val Game = "" with get, set

/// <summary>
/// Configuration for the 'delete-lessons' verb.
/// </summary>
[<CommandLine.Verb("delete-lessons", HelpText = "Delete lessons for a game, filtered by name")>]
type DeleteLessonsConfiguration() = 
    [<CommandLine.Option(Required = true)>]
    member val Game = "" with get, set

    [<CommandLine.Option("game-regex", Required = false)>]
    member val FilterRegex = "" with get, set

    [<CommandLine.Option("lesson-name", Required = false)>]
    member val LessonName = "" with get, set

/// <summary>
/// Root configuration for the program.
/// </summary>
type LudumLinguarumConfiguration() = 
    [<CommandLine.Option("database-path", Required = false)>]
    member val DatabasePath = "" with get, set

    [<CommandLine.Option("command-file", Required = false)>]
    member val CommandFile = "" with get, set

    [<CommandLine.Option("log-file", Required = false)>]
    member val LogFile = "" with get, set

let runImportAction(baseConfiguration: LudumLinguarumConfiguration, iPluginManager: IPluginManager, 
                    outputTextWriter: TextWriter, llDatabase: LLDatabase, argv: string array)(vc: ImportConfiguration) = 
    let pluginOpt = iPluginManager.GetPluginForGame(vc.Game)
    match pluginOpt with
    | Some(plugin) -> 
        // run the import action with the plugin
        plugin.ExtractAll(vc.Game, vc.GameDir, llDatabase, argv)
    | _ ->
        failwith("Could not find installed plugin for '" + vc.Game + "'")

let runExportAnkiAction(baseConfiguration: LudumLinguarumConfiguration, iPluginManager: IPluginManager, 
                        outputTextWriter: TextWriter, llDatabase: LLDatabase, argv: string array)(vc: CardExport.AnkiExporterConfiguration) = 
    let exporter = new CardExport.AnkiExporter(iPluginManager, outputTextWriter, llDatabase, vc)
    exporter.RunExportAction()

let runScanForTextAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter)(vc: DebugTools.TextScannerConfiguration) = 
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
/// <param name="baseConfiguration">configuration for running the action</param>
/// <param name="otw">output channel</param>
/// <param name="db">database</param>
let runListGamesAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase)(vc: ListGamesConfiguration) = 
    let games = db.Games
    let filter = 
        if (String.IsNullOrWhiteSpace(vc.FilterRegex)) then
            fun _ -> true
        else
            let regex = new Regex(vc.FilterRegex)
            fun (t: GameRecord) -> regex.IsMatch(t.Name)

    let eligibleGames = games |> Array.filter filter
    let languagesForGame(g: GameRecord) = 
        db.Lessons 
        |> Array.filter (fun l -> l.GameID = g.ID)
        |> Array.collect(db.LanguagesForLesson >> Array.ofList)
        |> Array.distinct
    
    eligibleGames
    |> Array.map(fun t -> "[" + t.Name + "], [" + String.Join(", ", languagesForGame(t)) + "]")
    |> Array.iter otw.WriteLine

let runListSupportedGamesAction(baseConfiguration: LudumLinguarumConfiguration,  iPluginManager: IPluginManager, otw: TextWriter)(vc: ListSupportedGamesConfiguration) = 
    otw.WriteLine("Supported games:")
    iPluginManager.SupportedGames
    |> Array.sort
    |> Array.iter otw.WriteLine

let runListLessonsAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase)(vc: ListLessonsConfiguration) = 
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
    |> Array.iter otw.WriteLine

let runDeleteGameAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase)(vc: DeleteGameConfiguration) = 
    let games = db.Games
    let filter(g: GameRecord) = vc.Game = g.Name
    let deleteGame(g: GameRecord) = 
        db.DeleteGame(g)
        otw.WriteLine("deleted game [" + g.Name + "]")

    games
    |> Array.filter filter
    |> Array.tryHead
    |> Option.iter deleteGame

let runDeleteLessonsAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase)(vc: DeleteLessonsConfiguration) = 
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

    try

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
         .WithParsed<ImportConfiguration>(new Action<ImportConfiguration>(runImportAction(c, iPluginManager, otw, lldb, argv)))
         .WithParsed<CardExport.AnkiExporterConfiguration>(new Action<CardExport.AnkiExporterConfiguration>(runExportAnkiAction(c, iPluginManager, otw, lldb, argv)))
         .WithParsed<DebugTools.TextScannerConfiguration>(new Action<DebugTools.TextScannerConfiguration>(runScanForTextAction(c, otw)))
         .WithParsed<ListGamesConfiguration>(new Action<ListGamesConfiguration>(runListGamesAction(c, otw, lldb)))
         .WithParsed<ListSupportedGamesConfiguration>(new Action<ListSupportedGamesConfiguration>(runListSupportedGamesAction(c, iPluginManager, otw)))
         .WithParsed<ListLessonsConfiguration>(new Action<ListLessonsConfiguration>(runListLessonsAction(c, otw, lldb)))
         .WithParsed<DeleteGameConfiguration>(new Action<DeleteGameConfiguration>(runDeleteGameAction(c, otw, lldb)))
         .WithParsed<DeleteLessonsConfiguration>(new Action<DeleteLessonsConfiguration>(runDeleteLessonsAction(c, otw, lldb)))
         |> ignore
    finally
        otw.Flush() |> ignore

    ()


let runGenerateFinalConfigurationStep(clp: CommandLine.Parser, argv: string array)(c: LudumLinguarumConfiguration) =
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
        runGenerateFinalConfigurationStep(commandLineParser, argv)(c)

    commandLineParser
        .ParseArguments<LudumLinguarumConfiguration>(argv)
        .WithParsed<LudumLinguarumConfiguration>(new Action<LudumLinguarumConfiguration>(runFn))
        .WithNotParsed(fun _ -> failwith "Failed to parse configuration")
    |> ignore

    0
