module LudumLinguarumConsole

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions

/// <summary>
/// Generic configuration for game imports.
/// </summary>
type ImportConfiguration() = 
    [<CommandLine.Option(Required = true)>]
    member val Game = "" with get, set

    [<CommandLine.Option("game-dir", Required = true)>]
    member val GameDir = "" with get, set

/// <summary>
/// Configuration for the 'list-games' verb.
/// </summary>
type ListGamesConfiguration() = 
    [<CommandLine.Option(Required = false)>]
    member val FilterRegex = "" with get, set

/// <summary>
/// Configuration for the 'list-lessons' verb.
/// </summary>
type ListLessonsConfiguration() = 
    [<CommandLine.Option(Required = false)>]
    member val GameRegex = "" with get, set

    [<CommandLine.Option(Required = false)>]
    member val FilterRegex = "" with get, set

/// <summary>
/// Configuration for the 'delete-game' verb.
/// </summary>
type DeleteGameConfiguration() = 
    [<CommandLine.Option(Required = true)>]
    member val Game = "" with get, set

/// <summary>
/// Configuration for the 'delete-lessons' verb.
/// </summary>
type DeleteLessonsConfiguration() = 
    [<CommandLine.Option(Required = true)>]
    member val Game = "" with get, set

    [<CommandLine.Option(Required = false)>]
    member val FilterRegex = "" with get, set

    [<CommandLine.Option(Required = false)>]
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

    [<CommandLine.VerbOption("import", HelpText = "Import localized content from a game")>]
    member val ImportOptions = new ImportConfiguration() with get, set

    [<CommandLine.VerbOption("export-anki", HelpText = "Export content as Anki flashcards")>]
    member val ExportAnkiOptions = new CardExport.AnkiExporterConfiguration() with get, set

    [<CommandLine.VerbOption("scan-for-text", HelpText = "Scan for text in files in a path")>]
    member val TextScannerOptions = new DebugTools.TextScannerConfiguration() with get, set

    [<CommandLine.VerbOption("list-games", HelpText = "List all imported games")>]
    member val ListGamesOptions = new ListGamesConfiguration() with get, set

    [<CommandLine.VerbOption("list-lessons", HelpText = "List lessons, filtering by game and lesson names")>]
    member val ListLessonsOptions = new ListLessonsConfiguration() with get, set

    [<CommandLine.VerbOption("delete-game", HelpText = "Delete a single game")>]
    member val DeleteGameOptions = new DeleteGameConfiguration() with get, set

    [<CommandLine.VerbOption("delete-lessons", HelpText = "Delete lessons for a game, filtered by name")>]
    member val DeleteLessonsOptions = new DeleteLessonsConfiguration() with get, set

let runImportAction(baseConfiguration: LudumLinguarumConfiguration, iPluginManager: IPluginManager, 
                    outputTextWriter: TextWriter, llDatabase: LLDatabase, argv: string array) = 
    let pluginOpt = iPluginManager.GetPluginForGame(baseConfiguration.ImportOptions.Game)
    match pluginOpt with
    | Some(plugin) -> 
        // run the import action with the plugin
        plugin.ExtractAll(baseConfiguration.ImportOptions.Game, baseConfiguration.ImportOptions.GameDir, llDatabase, argv)
        0
    | _ ->
        outputTextWriter.WriteLine("Could not find installed plugin for '" + baseConfiguration.ImportOptions.Game + "'")

        // error code since we couldn't actually find the plugin
        1

let runExportAnkiAction(baseConfiguration: LudumLinguarumConfiguration, iPluginManager: IPluginManager, 
                        outputTextWriter: TextWriter, llDatabase: LLDatabase, argv: string array) = 
    let exporter = new CardExport.AnkiExporter(iPluginManager, outputTextWriter, llDatabase, baseConfiguration.ExportAnkiOptions)
    exporter.RunExportAction()
    0

let runScanForTextAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter) = 
    let scanner = new DebugTools.StringScanner(baseConfiguration.TextScannerOptions)
    let results = scanner.Scan()
    results |> Array.iter(fun (fn, strs) ->
        otw.WriteLine("Found strings in " + fn + ":")
        strs |> Array.iter(fun s -> otw.WriteLine(s.offset.ToString("X8") + ": " + s.value))
        )
    0

/// <summary>
/// Runs the 'list-games' action, using the optional filter regex.
/// </summary>
/// <param name="baseConfiguration">configuration for running the action</param>
/// <param name="otw">output channel</param>
/// <param name="db">database</param>
let runListGamesAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase) = 
    let games = db.Games |> Array.map(fun t -> t.Name)
    let filter = 
        if (String.IsNullOrWhiteSpace(baseConfiguration.ListGamesOptions.FilterRegex)) then
            (fun _ -> true)
        else
            let regex = new Regex(baseConfiguration.ListGamesOptions.FilterRegex)
            (fun t -> regex.IsMatch(t))

    games
    |> Array.filter filter
    |> Array.iter otw.WriteLine
    0

let runListLessonsAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase) = 
    let gameFilter = 
        if (String.IsNullOrWhiteSpace(baseConfiguration.ListLessonsOptions.GameRegex)) then
            (fun _ -> true)
        else
            let regex = new Regex(baseConfiguration.ListLessonsOptions.GameRegex)
            (fun (t: GameRecord) -> regex.IsMatch(t.Name))

    let allowedGameIds = 
        db.Games
        |> Array.filter gameFilter
        |> Array.map(fun t -> t.ID)

    let checkGameFilterForLesson(l: LessonRecord) = 
        allowedGameIds |> Array.contains(l.GameID)

    let lessonFilter = 
        if (String.IsNullOrWhiteSpace(baseConfiguration.ListLessonsOptions.FilterRegex)) then
            (fun _ -> true)
        else
            let regex = new Regex(baseConfiguration.ListLessonsOptions.FilterRegex)
            (fun (t: LessonRecord) -> regex.IsMatch(t.Name))
        
    db.Lessons
    |> Array.filter checkGameFilterForLesson
    |> Array.filter lessonFilter
    |> Array.map (fun l -> l.Name)
    |> Array.iter otw.WriteLine
    0

let runDeleteGameAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase) = 
    0

let runDeleteLessonsAction(baseConfiguration: LudumLinguarumConfiguration, otw: TextWriter, db: LLDatabase) = 
    0

let processConfiguration(verbConfiguration: LudumLinguarumConfiguration, iPluginManager: IPluginManager, outputTextWriter: TextWriter, selectedVerb: string, argv: string array) = 
    let effectiveFLDBPath = 
        match verbConfiguration.DatabasePath with
        | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"LudumLinguarum\LudumLinguarum.db3")
        | nonDefaultPath -> nonDefaultPath

    if (not(Directory.Exists(Path.GetDirectoryName(effectiveFLDBPath)))) then
        Directory.CreateDirectory(Path.GetDirectoryName(effectiveFLDBPath)) |> ignore

    // Set up the database.
    let LLDatabase = new LLDatabase(effectiveFLDBPath)

#if DEBUG
    if true then
#else
    try
#endif
        match selectedVerb with
        | "import" ->
            runImportAction(verbConfiguration, iPluginManager, outputTextWriter, LLDatabase, argv)
        | "export-anki" ->
            runExportAnkiAction(verbConfiguration, iPluginManager, outputTextWriter, LLDatabase, argv)
        | "scan-for-text" ->
            runScanForTextAction(verbConfiguration, outputTextWriter)
        | "list-games" ->
            runListGamesAction(verbConfiguration, outputTextWriter, LLDatabase)
        | "list-lessons" ->
            runListLessonsAction(verbConfiguration, outputTextWriter, LLDatabase)
        | "delete-game" ->
            runDeleteGameAction(verbConfiguration, outputTextWriter, LLDatabase)
        | "delete-lessons" ->
            runDeleteLessonsAction(verbConfiguration, outputTextWriter, LLDatabase)
        | _ ->
            System.Console.WriteLine("Error: no action specified")
            1
#if DEBUG
    else
        0
#else
    with
    | e -> 
        System.Console.WriteLine("Exception thrown:" + e.ToString())

        // return error code since an exception was thrown
        1
#endif


[<EntryPoint>]
let main argv = 
    let pluginManager = new PluginManager()
    let iPluginManager = pluginManager :> IPluginManager

    // try and load all plugins that are alongside this executable
    let bundledPluginsPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let bundledPlugins = Directory.GetFiles(bundledPluginsPath, "*.dll", SearchOption.AllDirectories)

    let commandLineParserSetupAction(t: CommandLine.ParserSettings) = 
        t.HelpWriter <- System.Console.Out
        t.IgnoreUnknownArguments <- true

    let commandLineParser = new CommandLine.Parser(commandLineParserSetupAction)

    // This is kind of convoluted. First, we parse the base configuration with the verb removed, to pick up common options.
    // Then, we parse the regular configuration to decide which verb to use (and to pick up verb-specific options).

    let verbConfiguration = new LudumLinguarumConfiguration()
    let mutable selectedVerb = ""
    let chooseVerb = fun(c: string)(o: obj) -> selectedVerb <- c
    let verbSuccess = commandLineParser.ParseArguments(argv, verbConfiguration, new Action<string, obj>(chooseVerb))

    match verbSuccess with
    | true ->
        // if there was a command line file specified, read that file and use its contents as
        // the real set of command line arguments.
        let finalVerbConfiguration = 
            if (not(String.IsNullOrWhiteSpace(verbConfiguration.CommandFile))) then
                let commandFileConfigurationText = File.ReadAllText(verbConfiguration.CommandFile).Trim()
                let commandFileArgv = commandFileConfigurationText.Split(' ')
                let commandFileVerbConfiguration = new LudumLinguarumConfiguration()
                let commandVerbSuccess = 
                    commandLineParser.ParseArguments(commandFileArgv, commandFileVerbConfiguration, new Action<string, obj>(chooseVerb))

                match commandVerbSuccess with
                | true -> commandFileVerbConfiguration
                | _ -> raise(exn("Failed to parse configuration file"))
            else
                verbConfiguration

        let outputTextWriter = 
            if (String.IsNullOrWhiteSpace(finalVerbConfiguration.LogFile)) then
                System.Console.Out
            else
                new StreamWriter(finalVerbConfiguration.LogFile) :> TextWriter

        let instantiatePluginType(t: Type) = 
            try
                iPluginManager.Instantiate(outputTextWriter, t, argv)
            with
            | ex -> 
                outputTextWriter.WriteLine("Failed to load plugin " + t.AssemblyQualifiedName + ":" + Environment.NewLine + ex.ToString())
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
            outputTextWriter.WriteLine("Failed to load plugins: " + Environment.NewLine + ex.ToString())
            ()

        let retVal = processConfiguration(finalVerbConfiguration, iPluginManager, outputTextWriter, selectedVerb, argv)
        outputTextWriter.Flush() |> ignore
        retVal

    | _ -> raise(exn("Failed to parse configuration"))
