module OneOffGames

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Reflection

type OneOffGamesPlugin() = 
    let mutable outStream: TextWriter option = None
    let handlerMapping = 
        [|
            ("The King of Fighters 2002 Unlimited Match", XUIGames.ExtractKOF2002)
            ("The King of Fighters '98 Ultimate Match", XUIGames.ExtractKOF98)
            ("Jet Set Radio", JetSetRadio.JetSetRadio.ExtractJetSetRadio)
            ("Skulls of the Shogun", SimpleGames.ExtractSkullsOfTheShogun)
            ("Magical Drop V", SimpleGames.ExtractMagicalDropV)
            ("Audiosurf", SimpleGames.ExtractAudiosurf)
            ("Bastion", SimpleGames.ExtractBastion)
            ("Magicka", Magicka.ExtractMagicka)
            ("Worms Armageddon", WormsArmageddon.ExtractWormsArmageddon)
            ("Puzzle Chronicles", PuzzleQuestGames.ExtractPuzzleChronicles)
            ("Puzzle Kingdoms", PuzzleQuestGames.ExtractPuzzleKingdoms)
            ("Puzzle Quest 2", PuzzleQuestGames.ExtractPuzzleQuest2)
            ("Pillars of Eternity", PillarsOfEternity.ExtractPillarsOfEternity)
            ("Orcs Must Die!", OrcsMustDie.ExtractOrcsMustDie)
            ("Orcs Must Die! 2", OrcsMustDie.ExtractOrcsMustDie2)
            ("Hatoful Boyfriend", SimpleGames.ExtractHatofulBoyfriend)
            ("Hatoful Boyfriend: Holiday Star", SimpleGames.ExtractHatofulBoyfriendHolidayStar)
            ("Hell Yeah!", SimpleGames.ExtractHellYeah)
            ("Madballs: Babo Invasion", MadballsBaboInvasion.ExtractMadballsBaboInvasion)
            ("Space Channel 5: Part 2", SpaceChannel5.ExtractSpaceChannel5Part2)
            ("Sid Meier's Civilization IV", CivilizationGames.ExtractCiv4)
            ("Sid Meier's Civilization IV: Warlords", CivilizationGames.ExtractCiv4Warlords)
            ("Sid Meier's Civilization IV: Beyond the Sword", CivilizationGames.ExtractCiv4BeyondTheSword)
            ("Sid Meier's Civilization IV: Colonization", CivilizationGames.ExtractCiv4Colonization)
            ("Europa Universalis III", ParadoxStrategyGames.ExtractEU3)
            ("Hearts of Iron 3", ParadoxStrategyGames.ExtractHOI3)
            ("Victoria 2", ParadoxStrategyGames.ExtractVictoria2)
            ("Europa Universalis IV", ParadoxStrategyGames.ExtractEU4)
            ("Age of Empires II: HD Edition", AgeOfEmpiresGames.ExtractAOE2HD)
            ("Sonic Adventure DX", SonicAdventureDX.ExtractSonicAdventureDX)
        |] |> Map.ofArray

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            ()
        member this.Name = "oneoffgames"
        member this.Parameters = [||]
    interface IGameExtractorPlugin with
        member this.SupportedGames: string array = 

            handlerMapping |> Map.toArray |> Array.map (fun (k, _) -> k)

        member this.ExtractAll(game: string, path: string, db: LLDatabase, [<ParamArray>] args: string[]) = 
            this.LogWriteLine("Searching for game handler for '" + game + "'") |> ignore

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
