module RGGStudio.RGGStudioPlugin

open LLDatabase
open LudumLinguarumPlugins
open RGGStudio.DragonEngineGames
open RGGStudio.OldEngineGames
open System
open System.IO

type RGGStudioPlugin() =
    let mutable outStream: TextWriter option = None
    let handlerMapping =
        [|
            ({ GameMetadata.name = "Yakuza 0"; supportedLanguages = [| "en"; "ja" |] }, OldEngineGames.ExtractYakuza0)
            ({ GameMetadata.name = "Yakuza Kiwami"; supportedLanguages = [| "en"; "ja" |] }, OldEngineGames.ExtractYakuzaKiwami)
            ({ GameMetadata.name = "Yakuza Kiwami 2"; supportedLanguages = [| "en"; "ja"; "zh-CN"; "zh-TW" |] }, DragonEngineGames.ExtractYakuzaKiwami2)
            ({ GameMetadata.name = "Yakuza 6: The Song of Life"; supportedLanguages = [| "en"; "ja"; "zh-CN"; "zh-TW" |] }, DragonEngineGames.ExtractYakuza6)
            ({ GameMetadata.name = "Judgment"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }, DragonEngineGames.ExtractJudgment)
            ({ GameMetadata.name = "Lost Judgment"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }, DragonEngineGames.ExtractLostJudgment)
            ({ GameMetadata.name = "Yakuza: Like a Dragon"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }, DragonEngineGames.ExtractYakuzaLikeADragon)
        |] |> Map.ofArray

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]): unit =
            outStream <- Some(tw)
        member this.Name = "rggstudiogames"
        member this.Parameters = [||]
    interface IGameExtractorPlugin with
        member this.SupportedGames: GameMetadata array =
            handlerMapping |> Map.toArray |> Array.map (fun (k, _) -> k)

        member this.ExtractAll(game: string, path: string, args: string[]): ExtractedContent =
            this.LogWriteLine("Searching for game handler for '" + game + "'") |> ignore

            let gameNamesToHandlers =
                handlerMapping
                |> Map.toArray
                |> Array.map (fun (gmd, handler) -> (gmd.name, handler))
                |> Map.ofArray

            match gameNamesToHandlers |> Map.tryFind game with
            | Some(handler) -> handler(path)
            | None -> raise(UnknownGameException("unknown game " + game))

    member private this.LogWrite(s: string) =
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) =
        outStream |> Option.map(fun t -> t.WriteLine(s))
