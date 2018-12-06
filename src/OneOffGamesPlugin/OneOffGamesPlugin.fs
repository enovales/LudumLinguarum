module OneOffGames

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO

type OneOffGamesPlugin() = 
    let mutable outStream: TextWriter option = None
    let handlerMapping = 
        [|
            ({ GameMetadata.name = "The King of Fighters 2002 Unlimited Match"; supportedLanguages = [| "en"; "ja" |] }, XUIGames.ExtractKOF2002)
            ({ GameMetadata.name = "The King of Fighters '98 Ultimate Match"; supportedLanguages = [| "en"; "ja" |] }, XUIGames.ExtractKOF98)
            ({ GameMetadata.name = "Jet Set Radio"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja" |] }, JetSetRadio.JetSetRadio.ExtractJetSetRadio)
            ({ GameMetadata.name = "Skulls of the Shogun"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt"; "ru"; "zh" |] }, SimpleGames.ExtractSkullsOfTheShogun)
            ({ GameMetadata.name = "Magical Drop V"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja" |] }, SimpleGames.ExtractMagicalDropV)
            ({ GameMetadata.name = "Audiosurf"; supportedLanguages = [| "en"; "ru" |] }, SimpleGames.ExtractAudiosurf)
            ({ GameMetadata.name = "Bastion"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, SimpleGames.ExtractBastion)
            ({ GameMetadata.name = "Magicka"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "hu"; "it"; "pl"; "ru" |] }, Magicka.ExtractMagicka)
            ({ GameMetadata.name = "Worms Armageddon"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "nl"; "pt"; "ru"; "sv" |] }, WormsArmageddon.ExtractWormsArmageddon)
            ({ GameMetadata.name = "Puzzle Chronicles"; supportedLanguages = [| "de"; "en"; "en-GB"; "es"; "es-MX"; "fr"; "fr-CA"; "it" |] }, PuzzleQuestGames.ExtractPuzzleChronicles)
            ({ GameMetadata.name = "Puzzle Kingdoms"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, PuzzleQuestGames.ExtractPuzzleKingdoms)
            ({ GameMetadata.name = "Puzzle Quest 2"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, PuzzleQuestGames.ExtractPuzzleQuest2)
            ({ GameMetadata.name = "Pillars of Eternity"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ko"; "pl"; "ru" |] }, PillarsOfEternity.ExtractPillarsOfEternity)
            ({ GameMetadata.name = "Pillars of Eternity II: Deadfire"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ko"; "pl"; "pt"; "ru"; "zh-CN" |] }, PillarsOfEternity.ExtractPillarsOfEternity2)
            ({ GameMetadata.name = "Orcs Must Die!"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt"; "ru" |] }, OrcsMustDie.ExtractOrcsMustDie)
            ({ GameMetadata.name = "Orcs Must Die! 2"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt"; "ru" |] }, OrcsMustDie.ExtractOrcsMustDie2)
            ({ GameMetadata.name = "Hatoful Boyfriend"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ru" |] }, SimpleGames.ExtractHatofulBoyfriend)
            ({ GameMetadata.name = "Hatoful Boyfriend: Holiday Star"; supportedLanguages = [| "de"; "en"; "fr"; "ja" |] }, SimpleGames.ExtractHatofulBoyfriendHolidayStar)
            ({ GameMetadata.name = "Hell Yeah!"; supportedLanguages = [| "en"; "de"; "es"; "fr"; "it"; "ja" |] }, SimpleGames.ExtractHellYeah)
            ({ GameMetadata.name = "Madballs: Babo Invasion"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt"; "ru"; "zh" |] }, MadballsBaboInvasion.ExtractMadballsBaboInvasion)
            ({ GameMetadata.name = "Space Channel 5: Part 2"; supportedLanguages = [| "en"; "de"; "es"; "fr"; "it"; "jp" |] }, SpaceChannel5.ExtractSpaceChannel5Part2)
            ({ GameMetadata.name = "Sid Meier's Civilization IV"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, CivilizationGames.ExtractCiv4)
            ({ GameMetadata.name = "Sid Meier's Civilization IV: Warlords"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, CivilizationGames.ExtractCiv4Warlords)
            ({ GameMetadata.name = "Sid Meier's Civilization IV: Beyond the Sword"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, CivilizationGames.ExtractCiv4BeyondTheSword)
            ({ GameMetadata.name = "Sid Meier's Civilization IV: Colonization"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, CivilizationGames.ExtractCiv4Colonization)
            ({ GameMetadata.name = "Europa Universalis III"; supportedLanguages = [| "de"; "en" |] }, ParadoxStrategyGames.ExtractEU3)
            ({ GameMetadata.name = "Hearts of Iron 3"; supportedLanguages = [| "de"; "en"; "fr" |] }, ParadoxStrategyGames.ExtractHOI3)
            ({ GameMetadata.name = "Victoria 2"; supportedLanguages = [| "de"; "en"; "fr" |] }, ParadoxStrategyGames.ExtractVictoria2)
            ({ GameMetadata.name = "Europa Universalis IV"; supportedLanguages = [| "de"; "en"; "es"; "fr" |] }, ParadoxStrategyGames.ExtractEU4)
            ({ GameMetadata.name = "Crusader Kings II"; supportedLanguages = [| "de"; "en"; "es"; "fr" |] }, ParadoxStrategyGames.ExtractCrusaderKings2)
            ({ GameMetadata.name = "Age of Empires II: HD Edition"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "nl"; "pt"; "ru"; "zh" |] }, AgeOfEmpiresGames.ExtractAOE2HD)
            ({ GameMetadata.name = "Age of Empires III"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, AgeOfEmpiresGames.ExtractAOE3)
            ({ GameMetadata.name = "Sonic Adventure DX"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja" |] }, SonicAdventureDX.ExtractSonicAdventureDX)
            ({ GameMetadata.name = "Braid"; supportedLanguages = [| "cs"; "de"; "en"; "es"; "fr"; "it"; "ja"; "ka"; "ko"; "pl"; "pt"; "ru"; "zh" |] }, SimpleGames.ExtractBraid)
            ({ GameMetadata.name = "Transistor"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt"; "ru"; "zh" |] }, SimpleGames.ExtractTransistor)
            ({ GameMetadata.name = "IHF Handball Challenge 12"; supportedLanguages = [| "da"; "de"; "en"; "es"; "hu"; "it"; "pt" |] }, SimpleGames.ExtractIHFHandballChallenge12)
            ({ GameMetadata.name = "IHF Handball Challenge 14"; supportedLanguages = [| "da"; "de"; "en"; "es"; "fr"; "pl"; "sv" |] }, SimpleGames.ExtractIHFHandballChallenge14)
            ({ GameMetadata.name = "Torment: Tides of Numenera"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "pl"; "ru" |] }, PillarsOfEternity.ExtractTormentTidesOfNumenera)
            ({ GameMetadata.name = "Tyranny"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "pl"; "ru" |] }, PillarsOfEternity.ExtractTyranny)
            ({ GameMetadata.name = "F1 Race Stars"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR" |] }, CodemastersGames.ExtractF1RaceStars)
            ({ GameMetadata.name = "F1 2011"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "pt-PT"; "ru" |] }, CodemastersGames.ExtractF12011)
            ({ GameMetadata.name = "F1 2012"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru" |] }, CodemastersGames.ExtractF12012)
            ({ GameMetadata.name = "F1 2014"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR" |] }, CodemastersGames.ExtractF12014)
            ({ GameMetadata.name = "F1 2015"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }, CodemastersGames.ExtractF12015)
            ({ GameMetadata.name = "F1 2016"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }, CodemastersGames.ExtractF12016)
            ({ GameMetadata.name = "DiRT"; supportedLanguages = [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it" |] }, CodemastersGames.ExtractDirt)
            ({ GameMetadata.name = "DiRT 2"; supportedLanguages = [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja"; "pl"; "ru" |] }, CodemastersGames.ExtractDirt2)
            ({ GameMetadata.name = "GRID"; supportedLanguages = [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja" |] }, CodemastersGames.ExtractGrid)
            ({ GameMetadata.name = "GRID 2"; supportedLanguages = [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR" |] }, CodemastersGames.ExtractGrid2)
            ({ GameMetadata.name = "GRID Autosport"; supportedLanguages = [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru" |] }, CodemastersGames.ExtractGridAutosport)
            ({ GameMetadata.name = "Defcon"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, SimpleGames.ExtractDefcon)
            ({ GameMetadata.name = "Darwinia"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, SimpleGames.ExtractDarwinia)
            ({ GameMetadata.name = "Multiwinia"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }, SimpleGames.ExtractMultiwinia)
            ({ GameMetadata.name = "The Witness"; supportedLanguages = [| "ar"; "de"; "en"; "es-ES"; "es-LA"; "fr"; "hu"; "id"; "it"; "ja"; "ko"; "pl"; "pt-BR"; "pt-PT"; "ru"; "zh-CN"; "zh-TW" |] }, SimpleGames.ExtractTheWitness)
            ({ GameMetadata.name = "Prison Architect"; supportedLanguages = [| "bg"; "cs"; "da"; "de"; "el"; "en"; "es"; "fi"; "fr"; "hu"; "it"; "ja"; "ko"; "nl"; "no"; "pl"; "pt-PT"; "pt-BR"; "ro"; "ru"; "sv"; "th"; "tr"; "uk"; "zh-CN"; "zh-TW" |] }, SimpleGames.ExtractPrisonArchitect)
            ({ GameMetadata.name = "The Escapists"; supportedLanguages = [| "en"; "de"; "es"; "fr"; "it"; "pl"; "ru" |]}, SimpleGames.ExtractTheEscapists)
        |] |> Map.ofArray

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            ()
        member this.Name = "oneoffgames"
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

            if (gameNamesToHandlers |> Map.containsKey(game)) then
                gameNamesToHandlers.[game](path)
            else
                raise(UnknownGameException("unknown game " + game))

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))
