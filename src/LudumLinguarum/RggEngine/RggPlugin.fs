namespace RggEngine

open LudumLinguarumPlugins
open RggEngine.RggGames
open System
open System.IO

/// Plugin providing extraction support for RGG Studio games (Yakuza / Like a Dragon / Judgment series).
type RggPlugin() =
    let mutable outStream: TextWriter option = None

    let gameMap =
        supportedGames
        |> Array.map (fun spec -> (spec.Name, spec))
        |> Map.ofArray

    member private _.LogWriteLine(s: string) =
        outStream |> Option.iter (fun tw -> tw.WriteLine(s))

    interface IPlugin with
        member _.Load(tw: TextWriter, [<ParamArray>] _args: string[]) =
            outStream <- Some tw
        member _.Name = "rgg"
        member _.Parameters = [||]

    interface IGameExtractorPlugin with
        member _.SupportedGames: GameMetadata array =
            supportedGames
            |> Array.map (fun spec -> GameMetadata.TextOnly(spec.Name, spec.SupportedLanguages))

        member this.ExtractAll(game: string, path: string, _args: string[]) : ExtractedContent =
            this.LogWriteLine($"Extracting RGG Studio game '{game}' from '{path}'")
            match gameMap |> Map.tryFind game with
            | Some spec -> extractGame spec path
            | None -> raise (UnknownGameException("unknown game " + game))
