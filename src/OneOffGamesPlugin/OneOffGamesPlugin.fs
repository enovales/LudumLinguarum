﻿module OneOffGames

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Reflection

type OneOffGamesPlugin() = 
    let mutable outStream: TextWriter option = None
    let kof2002Name = "The King of Fighters 2002 Unlimited Match"
    let kof98Name = "The King of Fighters '98 Ultimate Match"
    let jetSetRadioName = "Jet Set Radio"
    let skullsOfTheShogunName = "Skulls of the Shogun"
    let magicalDropVName = "Magical Drop V"
    let audiosurfName = "Audiosurf"
    let bastionName = "Bastion"
    let magickaName = "Magicka"
    let wormsArmageddonName = "Worms Armageddon"
    let puzzleChroniclesName = "Puzzle Chronicles"
    let puzzleQuest2Name = "Puzzle Quest 2"
    let pillarsOfEternityName = "Pillars of Eternity"
    let orcsMustDieName = "Orcs Must Die!"
    let hatofulBoyfriendName = "Hatoful Boyfriend"
    let hatofulBoyfriendHolidayStarName = "Hatoful Boyfriend: Holiday Star"
    let hellYeahName = "Hell Yeah!"
    let madballsBaboInvasionName = "Madballs: Babo Invasion"

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            ()
        member this.Name = "oneoffgames"
        member this.Parameters = [||]
    interface IGameExtractorPlugin with
        member this.SupportedGames: string array = 
            [|
                kof2002Name
                kof98Name
                jetSetRadioName
                skullsOfTheShogunName
                magicalDropVName
                audiosurfName
                bastionName
                magickaName
                wormsArmageddonName
                puzzleChroniclesName
                puzzleQuest2Name
                pillarsOfEternityName
                orcsMustDieName
                hatofulBoyfriendName
                hatofulBoyfriendHolidayStarName
                hellYeahName
                madballsBaboInvasionName
            |]
        member this.ExtractAll(game: string, path: string, db: LLDatabase, [<ParamArray>] args: string[]) = 
            this.LogWriteLine("Searching for game handler for '" + game + "'") |> ignore
            let handlerMapping = 
                [|
                    (kof2002Name, XUIGames.ExtractKOF2002);
                    (kof98Name, XUIGames.ExtractKOF98);
                    (jetSetRadioName, JetSetRadio.JetSetRadio.ExtractJetSetRadio)
                    (skullsOfTheShogunName, SimpleGames.ExtractSkullsOfTheShogun)
                    (magicalDropVName, SimpleGames.ExtractMagicalDropV)
                    (audiosurfName, SimpleGames.ExtractAudiosurf)
                    (bastionName, SimpleGames.ExtractBastion)
                    (magickaName, Magicka.ExtractMagicka)
                    (wormsArmageddonName, WormsArmageddon.ExtractWormsArmageddon)
                    (puzzleChroniclesName, PuzzleQuestGames.ExtractPuzzleChronicles)
                    (puzzleQuest2Name, PuzzleQuestGames.ExtractPuzzleQuest2)
                    (pillarsOfEternityName, PillarsOfEternity.ExtractPillarsOfEternity)
                    (orcsMustDieName, OrcsMustDie.ExtractOrcsMustDie)
                    (hatofulBoyfriendName, SimpleGames.ExtractHatofulBoyfriend)
                    (hatofulBoyfriendHolidayStarName, SimpleGames.ExtractHatofulBoyfriendHolidayStar)
                    (hellYeahName, SimpleGames.ExtractHellYeah)
                    (madballsBaboInvasionName, MadballsBaboInvasion.ExtractMadballsBaboInvasion)
                |] |> Map.ofArray

            if (handlerMapping |> Map.containsKey(game)) then
                // create game entry, and then run handler
                let gameEntry = {
                    GameRecord.Name = game;
                    ID = 0
                }
                let gameEntryWithId = { gameEntry with ID = db.CreateOrUpdateGame(gameEntry) }
                this.LogWriteLine("Game entry for " + game + " updated.") |> ignore

                handlerMapping.[game](path, db, gameEntryWithId, args)
            else
                raise(UnknownGameException("unknown game " + game))
            ()                

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))
