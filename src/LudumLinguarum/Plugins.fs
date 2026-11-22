module LudumLinguarumPlugins
    
open AudioPipeline
open LLDatabase
open MediaDatabase

open System
open System.IO
open System.Reflection

exception UnknownGameException of string

[<Interface>]
type IPlugin = 
    abstract member Load: TextWriter * [<ParamArray>] args: string[] -> unit
    abstract member Name: string
    abstract member Parameters: Object array

type NullPlugin() = 
    interface IPlugin with
        member this.Load(_: TextWriter, [<ParamArray>] args: string[]): unit = ()
        member this.Name = "Null"
        member this.Parameters = [| new Object() |]

type ExtractedContent = 
    {
        lessons: LessonRecord array
        cards: CardRecord array
    }

// Arguments are intended to be game name, path, and command line arguments
type ExtractAllFunc = string -> string -> string array -> ExtractedContent

type GameMetadata = 
    {
        name: string
        supportedLanguages: string array
        supportedAudioLanguages: string array
        supportedImageLanguages: string array
    }
    with
        static member TextOnly(name: string, supportedLanguages: string array) =
            {
                name = name
                supportedLanguages = supportedLanguages
                supportedAudioLanguages = [||]
                supportedImageLanguages = [||]
            }

        member this.SupportsAudio = this.supportedAudioLanguages.Length > 0
        member this.SupportsImages = this.supportedImageLanguages.Length > 0

type MediaExtractionContext =
    {
        ExtractAudio: bool
        ExtractImages: bool
        MediaStore: IMediaStore option
        AudioTools: ExternalToolConfig
        AudioOptions: AudioIngestOptions
        Log: string -> unit
    }
    static member None =
        {
            ExtractAudio = false
            ExtractImages = false
            MediaStore = None
            AudioTools = ExternalToolConfig.Default
            AudioOptions = AudioIngestOptions.Default
            Log = fun _ -> ()
        }

[<Interface>]
type IGameExtractorPlugin = 
    inherit IPlugin

    abstract member SupportedGames: GameMetadata array

    /// <summary>
    /// Generic call to extract all localized resources from the game and path specified
    /// into the provided database.
    /// </summary>
    abstract member ExtractAll: string * string * string array -> ExtractedContent

/// <summary>
/// Opt-in plugin interface for extractors that can also extract localized audio and/or
/// visual assets and stream them directly into the companion IMediaStore.
/// </summary>
[<Interface>]
type IMediaExtractorPlugin =
    inherit IGameExtractorPlugin

    abstract member ExtractAllWithMedia: string * string * string array * MediaExtractionContext -> ExtractedContent

[<Interface>]
type IPluginManager = 
    abstract member Discover: Assembly -> list<Type>
    abstract member Instantiate: TextWriter * Type * string[] -> unit
    abstract member InstantiateArgv: TextWriter * Type * [<ParamArray>] args: string[] -> unit
    abstract member Plugins: list<IPlugin> with get
    abstract member GetPluginForGame: string -> IGameExtractorPlugin option
    abstract member SupportedGames: GameMetadata array

type PluginManager() = 
    let mutable plugins: list<IPlugin> = []
    interface IPluginManager with
        member this.Discover(a: Assembly): list<Type> = 
            let types = a.GetExportedTypes()
            let isTypeAPlugin(t: Type) = 
                t.IsClass && 
                (t.GetInterfaces() |> 
                 Seq.exists(fun u -> u.FullName = "LudumLinguarumPlugins+IPlugin" && (u.GetConstructors() |> Seq.isEmpty)))

            types
            |> Array.filter isTypeAPlugin
            |> List.ofArray
        member this.Instantiate(tw: TextWriter, t: Type, args: string[]): unit = 
            let newPluginObject = t.GetConstructor([||]).Invoke([||])
            match newPluginObject with
            | :? IPlugin as newPlugin ->
                newPlugin.Load(tw, args)
                plugins <- newPlugin :: plugins
            | _ -> ()
        member this.InstantiateArgv(tw: TextWriter, t: Type, [<ParamArray>] args: string[]): unit = 
            (this :> IPluginManager).Instantiate(tw, t, args)
        member this.Plugins: list<IPlugin> = plugins

        member this.GetPluginForGame(g: string): IGameExtractorPlugin option = 
            plugins |> List.tryPick(fun p ->
                match p with
                | :? IGameExtractorPlugin as gep when (gep.SupportedGames |> Array.exists(fun gmd -> gmd.name = g)) ->
                    Some(gep)
                | _ -> None)

        member this.SupportedGames: GameMetadata array = 
            let geps = plugins |> List.filter(fun p ->
                match p with
                | :? IGameExtractorPlugin as gep -> true
                | _ -> false) |> List.map(fun t -> t :?> IGameExtractorPlugin)

            geps 
            |> List.map(fun t -> t.SupportedGames) 
            |> Array.concat
