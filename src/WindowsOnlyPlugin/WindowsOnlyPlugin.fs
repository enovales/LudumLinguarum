﻿module OneOffGames

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO

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
            ("Pillars of Eternity II: Deadfire", PillarsOfEternity.ExtractPillarsOfEternity2)
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
            ("Crusader Kings II", ParadoxStrategyGames.ExtractCrusaderKings2)
            ("Age of Empires II: HD Edition", AgeOfEmpiresGames.ExtractAOE2HD)
            ("Age of Empires III", AgeOfEmpiresGames.ExtractAOE3)
            ("Sonic Adventure DX", SonicAdventureDX.ExtractSonicAdventureDX)
            ("Braid", SimpleGames.ExtractBraid)
            ("Transistor", SimpleGames.ExtractTransistor)
            ("IHF Handball Challenge 12", SimpleGames.ExtractIHFHandballChallenge12)
            ("IHF Handball Challenge 14", SimpleGames.ExtractIHFHandballChallenge14)
            ("Torment: Tides of Numenera", PillarsOfEternity.ExtractTormentTidesOfNumenera)
            ("Tyranny", PillarsOfEternity.ExtractTyranny)
            ("F1 Race Stars", CodemastersGames.ExtractF1RaceStars)
            ("F1 2011", CodemastersGames.ExtractF12011)
            ("F1 2012", CodemastersGames.ExtractF12012)
            ("F1 2014", CodemastersGames.ExtractF12014)
            ("F1 2015", CodemastersGames.ExtractF12015)
            ("F1 2016", CodemastersGames.ExtractF12016)
            ("DiRT", CodemastersGames.ExtractDirt)
            ("DiRT 2", CodemastersGames.ExtractDirt2)
            ("GRID", CodemastersGames.ExtractGrid)
            ("GRID 2", CodemastersGames.ExtractGrid2)
            ("GRID Autosport", CodemastersGames.ExtractGridAutosport)
            ("Defcon", SimpleGames.ExtractDefcon)
            ("Darwinia", SimpleGames.ExtractDarwinia)
            ("Multiwinia", SimpleGames.ExtractMultiwinia)
        |] |> Map.ofArray

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            ()
        member this.Name = "oneoffgames"
        member this.Parameters = [||]
    interface IGameExtractorPlugin with
        member this.SupportedGames: string array = 
            handlerMapping |> Map.toArray |> Array.map (fun (k, _) -> k)

        member this.ExtractAll(game: string, path: string, args: string[]): ExtractedContent = 
            this.LogWriteLine("Searching for game handler for '" + game + "'") |> ignore

            if (handlerMapping |> Map.containsKey(game)) then
                handlerMapping.[game](path)
            else
                raise(UnknownGameException("unknown game " + game))

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))