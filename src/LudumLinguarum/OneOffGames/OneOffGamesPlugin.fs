module OneOffGames

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO

type private OneOffGameHandler =
    | TextOnly of (string -> ExtractedContent)
    | WithMedia of (string -> string array -> MediaExtractionContext -> ExtractedContent)

type OneOffGamesPlugin() = 
    let mutable outStream: TextWriter option = None
    let textGame (name: string) (languages: string array) (handler: string -> ExtractedContent) =
        (GameMetadata.TextOnly(name, languages), TextOnly handler)

    let mediaGame
        (name: string)
        (languages: string array)
        (audioLanguages: string array)
        (imageLanguages: string array)
        (handler: string -> string array -> MediaExtractionContext -> ExtractedContent) =
        let meta =
            {
                GameMetadata.name = name
                supportedLanguages = languages
                supportedAudioLanguages = audioLanguages
                supportedImageLanguages = imageLanguages
            }
        (meta, WithMedia handler)

    let handlerEntries = 
        [|
            textGame "The King of Fighters 2002 Unlimited Match" [| "en"; "ja" |] XUIGames.ExtractKOF2002
            textGame "The King of Fighters '98 Ultimate Match" [| "en"; "ja" |] XUIGames.ExtractKOF98
            textGame "Jet Set Radio" [| "de"; "en"; "es"; "fr"; "it"; "ja" |] JetSetRadio.JetSetRadio.ExtractJetSetRadio
            textGame "Skulls of the Shogun" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt"; "ru"; "zh" |] SimpleGames.ExtractSkullsOfTheShogun
            textGame "Magical Drop V" [| "de"; "en"; "es"; "fr"; "it"; "ja" |] SimpleGames.ExtractMagicalDropV
            textGame "Audiosurf" [| "en"; "ru" |] SimpleGames.ExtractAudiosurf
            textGame "Bastion" [| "de"; "en"; "es"; "fr"; "it" |] SupergiantGames.ExtractBastion
            textGame "Magicka" [| "de"; "en"; "es"; "fr"; "hu"; "it"; "pl"; "ru" |] Magicka.ExtractMagicka
            textGame "Worms Armageddon" [| "de"; "en"; "es"; "fr"; "it"; "nl"; "pt"; "ru"; "sv" |] WormsGames.Armageddon.ExtractWormsArmageddon
            textGame "Puzzle Chronicles" [| "de"; "en"; "en-GB"; "es"; "es-MX"; "fr"; "fr-CA"; "it" |] PuzzleQuestGames.ExtractPuzzleChronicles
            textGame "Puzzle Kingdoms" [| "de"; "en"; "es"; "fr"; "it" |] PuzzleQuestGames.ExtractPuzzleKingdoms
            textGame "Puzzle Quest 2" [| "de"; "en"; "es"; "fr"; "it" |] PuzzleQuestGames.ExtractPuzzleQuest2
            textGame "Pillars of Eternity" [| "de"; "en"; "es"; "fr"; "it"; "ko"; "pl"; "ru" |] PillarsOfEternity.ExtractPillarsOfEternity
            textGame "Pillars of Eternity II: Deadfire" [| "de"; "en"; "es"; "fr"; "it"; "ko"; "pl"; "pt"; "ru"; "zh-CN" |] PillarsOfEternity.ExtractPillarsOfEternity2
            textGame "Orcs Must Die!" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt"; "ru" |] OrcsMustDie.ExtractOrcsMustDie
            textGame "Orcs Must Die! 2" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt"; "ru" |] OrcsMustDie.ExtractOrcsMustDie2
            textGame "Hatoful Boyfriend" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ru" |] SimpleGames.ExtractHatofulBoyfriend
            textGame "Hatoful Boyfriend: Holiday Star" [| "de"; "en"; "fr"; "ja" |] SimpleGames.ExtractHatofulBoyfriendHolidayStar
            textGame "Hell Yeah!" [| "en"; "de"; "es"; "fr"; "it"; "ja" |] SimpleGames.ExtractHellYeah
            textGame "Madballs: Babo Invasion" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt"; "ru"; "zh" |] MadballsBaboInvasion.ExtractMadballsBaboInvasion
            textGame "Space Channel 5: Part 2" [| "en"; "de"; "es"; "fr"; "it"; "ja" |] SpaceChannel5.ExtractSpaceChannel5Part2
            mediaGame "Sid Meier's Civilization IV" [| "de"; "en"; "es"; "fr"; "it" |] [| "de"; "en"; "es"; "fr"; "it" |] [||] CivilizationGames.ExtractCiv4WithMedia
            mediaGame "Sid Meier's Civilization IV: Warlords" [| "de"; "en"; "es"; "fr"; "it" |] [| "de"; "en"; "es"; "fr"; "it" |] [||] CivilizationGames.ExtractCiv4WarlordsWithMedia
            mediaGame "Sid Meier's Civilization IV: Beyond the Sword" [| "de"; "en"; "es"; "fr"; "it" |] [| "de"; "en"; "es"; "fr"; "it" |] [||] CivilizationGames.ExtractCiv4BeyondTheSwordWithMedia
            textGame "Sid Meier's Civilization IV: Colonization" [| "de"; "en"; "es"; "fr"; "it" |] CivilizationGames.ExtractCiv4Colonization
            textGame "Europa Universalis III" [| "de"; "en" |] ParadoxStrategyGames.ExtractEU3
            textGame "Hearts of Iron 3" [| "de"; "en"; "fr" |] ParadoxStrategyGames.ExtractHOI3
            textGame "Victoria 2" [| "de"; "en"; "fr" |] ParadoxStrategyGames.ExtractVictoria2
            textGame "Europa Universalis IV" [| "de"; "en"; "es"; "fr" |] ParadoxStrategyGames.ExtractEU4
            textGame "Crusader Kings II" [| "de"; "en"; "es"; "fr" |] ParadoxStrategyGames.ExtractCrusaderKings2
            textGame "Age of Empires II: HD Edition" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "nl"; "pt"; "ru"; "zh" |] AgeOfEmpiresGames.ExtractAOE2HD
            mediaGame "Age of Empires III" [| "de"; "en"; "es"; "fr"; "it" |] [| "de"; "en"; "es"; "fr"; "it" |] [||] AgeOfEmpiresGames.ExtractAOE3WithMedia
            textGame "Sonic Adventure DX" [| "de"; "en"; "es"; "fr"; "it"; "ja" |] SonicAdventureDX.ExtractSonicAdventureDX
            textGame "Braid" [| "cs"; "de"; "en"; "es"; "fr"; "it"; "ja"; "ka"; "ko"; "pl"; "pt"; "ru"; "zh" |] SimpleGames.ExtractBraid
            textGame "Transistor" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt"; "ru"; "zh" |] SupergiantGames.ExtractTransistor
            textGame "IHF Handball Challenge 12" [| "da"; "de"; "en"; "es"; "hu"; "it"; "pt" |] SimpleGames.ExtractIHFHandballChallenge12
            textGame "IHF Handball Challenge 14" [| "da"; "de"; "en"; "es"; "fr"; "pl"; "sv" |] SimpleGames.ExtractIHFHandballChallenge14
            textGame "Torment: Tides of Numenera" [| "de"; "en"; "es"; "fr"; "it"; "pl"; "ru" |] PillarsOfEternity.ExtractTormentTidesOfNumenera
            textGame "Tyranny" [| "de"; "en"; "es"; "fr"; "pl"; "ru" |] PillarsOfEternity.ExtractTyranny
            textGame "F1 Race Stars" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR" |] CodemastersGames.ExtractF1RaceStars
            textGame "F1 2011" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "pt-PT"; "ru" |] CodemastersGames.ExtractF12011
            textGame "F1 2012" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru" |] CodemastersGames.ExtractF12012
            textGame "F1 2014" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR" |] CodemastersGames.ExtractF12014
            textGame "F1 2015" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] CodemastersGames.ExtractF12015
            textGame "F1 2016" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] CodemastersGames.ExtractF12016
            textGame "DiRT" [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it" |] CodemastersGames.ExtractDirt
            textGame "DiRT 2" [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja"; "pl"; "ru" |] CodemastersGames.ExtractDirt2
            textGame "GRID" [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja" |] CodemastersGames.ExtractGrid
            textGame "GRID 2" [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR" |] CodemastersGames.ExtractGrid2
            textGame "GRID Autosport" [| "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "ja"; "pl"; "pt-BR"; "ru" |] CodemastersGames.ExtractGridAutosport
            textGame "Defcon" [| "de"; "en"; "es"; "fr"; "it" |] SimpleGames.ExtractDefcon
            textGame "Darwinia" [| "de"; "en"; "es"; "fr"; "it" |] SimpleGames.ExtractDarwinia
            textGame "Multiwinia" [| "de"; "en"; "es"; "fr"; "it" |] SimpleGames.ExtractMultiwinia
            textGame "The Witness" [| "ar"; "de"; "en"; "es-ES"; "es-LA"; "fr"; "hu"; "id"; "it"; "ja"; "ko"; "pl"; "pt-BR"; "pt-PT"; "ru"; "zh-CN"; "zh-TW" |] SimpleGames.ExtractTheWitness
            textGame "Prison Architect" [| "bg"; "cs"; "da"; "de"; "el"; "en"; "es"; "fi"; "fr"; "hu"; "it"; "ja"; "ko"; "nl"; "no"; "pl"; "pt-PT"; "pt-BR"; "ro"; "ru"; "sv"; "th"; "tr"; "uk"; "zh-CN"; "zh-TW" |] SimpleGames.ExtractPrisonArchitect
            textGame "The Escapists" [| "de"; "en"; "es"; "fr"; "it"; "pl"; "ru" |] SimpleGames.ExtractTheEscapists
            textGame "Super Meat Boy" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "kr"; "pl"; "pt"; "ru"; "zh-CN"; "zh-TW" |] SimpleGames.ExtractSuperMeatBoy
            textGame "Mega Man Legacy Collection" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pt-BR"; "ru" |] SimpleGames.ExtractMegaManLegacyCollection
            textGame "Worms Crazy Golf" [| "cs"; "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "pl" |] WormsGames.Common.ExtractWormsCrazyGolf
            textGame "Worms W.M.D" [| "cs"; "de"; "en-GB"; "en-US"; "es"; "fr"; "it"; "pl"; "pt-BR"; "ru"; "zh-CN" |] WormsGames.Common.ExtractWormsWMD
            textGame "Dicey Dungeons" [| "ar"; "br"; "ca"; "cy"; "de"; "en"; "es"; "fr"; "ie"; "it"; "ja"; "ko"; "nl"; "pl"; "pt"; "ru"; "sc"; "sz"; "tc"; "tr" |] DiceyDungeons.ExtractDiceyDungeons
            textGame "Pyre" [| "de"; "en"; "es"; "fr"; "ru"; "zh-CN" |] SupergiantGames.ExtractPyre
            mediaGame "Hades" [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pl"; "pt-BR"; "ru"; "zh-CN" |] [| "en" |] [||] SupergiantGames.ExtractHadesWithMedia
            mediaGame "Hades II" [| "de"; "el"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pl"; "pt-BR"; "ru"; "tr"; "uk"; "zh-CN"; "zh-TW" |] [| "en" |] [||] SupergiantGames.ExtractHades2WithMedia
            textGame "Nubby's Number Factory" [| "ca"; "da"; "en"; "es"; "es-LA"; "fr"; "gl"; "hu"; "it"; "nl"; "no"; "pl"; "pt-BR"; "pt-PT"; "ru"; "sk"; "tr"; "uk" |] NubbysNumberFactory.ExtractNubbysNumberFactory
            textGame "Balatro" [| "de"; "en-US"; "es-419"; "es-ES"; "fr"; "id"; "it"; "ja"; "ko"; "nl"; "pl"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] Balatro.ExtractBalatro
        |]

    let gameNamesToHandlers =
        handlerEntries
        |> Array.map (fun (gmd, handler) -> (gmd.name, handler))
        |> Map.ofArray

    member private this.RunHandler(game: string, path: string, args: string[], mediaCtx: MediaExtractionContext) =
        this.LogWriteLine("Searching for game handler for '" + game + "'") |> ignore
        match gameNamesToHandlers |> Map.tryFind game with
        | Some (TextOnly handler) -> handler path
        | Some (WithMedia handler) -> handler path args mediaCtx
        | None -> raise(UnknownGameException("unknown game " + game))

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            outStream <- Some(tw)
        member this.Name = "oneoffgames"
        member this.Parameters = [||]

    interface IGameExtractorPlugin with
        member this.SupportedGames: GameMetadata array = 
            handlerEntries |> Array.map fst

        member this.ExtractAll(game: string, path: string, args: string[]): ExtractedContent = 
            this.RunHandler(game, path, args, MediaExtractionContext.None)

    interface IMediaExtractorPlugin with
        member this.ExtractAllWithMedia(game: string, path: string, args: string[], mediaCtx: MediaExtractionContext): ExtractedContent =
            this.RunHandler(game, path, args, mediaCtx)

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))
