namespace InfinityAuroraEnginePlugins

open LLDatabase
open LudumLinguarumPlugins
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.JadeEmpireContext
open InfinityAuroraEnginePlugins.NWN1Context
open InfinityAuroraEnginePlugins.TalkTable
open InfinityAuroraEnginePlugins.TwoDA
open System
open System.IO

type AuroraPluginSettings() = 
    [<CommandLine.Option("language-tag", DefaultValue = "en", Required = false)>]
    member val LanguageTag = "en" with get, set

type NWN1PluginSettings() = 
    inherit AuroraPluginSettings()

    [<CommandLine.Option("extract-dialogues", DefaultValue = "false", Required = false)>]
    member val ExtractDialogues = "false" with get, set
    member this.ExtractDialoguesBoolean = bool.Parse(this.ExtractDialogues)

    [<CommandLine.Option("extract-2das", DefaultValue = "true", Required = false)>]
    member val Extract2DAs = "true" with get, set
    member this.Extract2DAsBoolean = bool.Parse(this.Extract2DAs)

type private ExtractionContext<'TalkTableString when 'TalkTableString :> ITalkTableString> = {
    gameResources: IGenericResource seq;
    pluginSettings: NWN1PluginSettings;
    db: LLDatabase;
    masculineOrNeuterTalkTable: ITalkTable<'TalkTableString>;
    feminineTalkTable: ITalkTable<'TalkTableString>;
    gameEntry: GameRecord
}

/// <summary>
/// Function that generates a set of cards for a provided 2DA file.
/// </summary>
type TwoDAExtractor<'TalkTableString when 'TalkTableString :> ITalkTableString> = 
    ExtractionContext<'TalkTableString> * TwoDAFile * LessonRecord * string -> CardRecord array

/// <summary>
/// 2DA resref, extractor method, lesson name
/// </summary>
type TwoDAExtractionOrder<'TalkTableString when 'TalkTableString :> ITalkTableString> = string * TwoDAExtractor<'TalkTableString> * string

type AuroraPlugin() = 
    let mutable outStream: TextWriter option = None
    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            outStream <- Some(tw)
            ()
        member this.Name = "aurora"
        member this.Parameters = [| new AuroraPluginSettings() :> Object; new NWN1PluginSettings() :> Object |]
    end
    interface IGameExtractorPlugin with
        member this.SupportedGames = [| "Neverwinter Nights"; "Jade Empire" |]
        member this.ExtractAll(game: string, path: string, db: LLDatabase, [<ParamArray>] args: string[]) = 
            let parser = new CommandLine.Parser(fun t -> 
                t.HelpWriter <- System.Console.Out
                t.IgnoreUnknownArguments <- true)
            let pluginSettings = new AuroraPluginSettings()

            // parse arguments, skipping the verb from the command line
            if (parser.ParseArguments(args |> Array.skip(1), pluginSettings)) then
                this.LogWriteLine("Searching for game handler for '" + game + "'") |> ignore
                match game with
                | "Neverwinter Nights" -> this.ExtractNWN1(path, db, args)
                | "Jade Empire" -> this.ExtractJadeEmpire(path, db, args)
                | g -> raise(UnknownGameException("unknown game " + g))
                ()                
            else
                raise(exn("failed to parse args"))

    end

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))

    member private this.ExtractDialogues<'TalkTableString when 'TalkTableString :> ITalkTableString>(xc: ExtractionContext<'TalkTableString>) = 
        this.LogWriteLine("Starting dialogue extraction.") |> ignore
        // extract dialogues, and create lessons for each one
        let dialogueResources = xc.gameResources |> Seq.filter(fun t -> t.ResourceType = ResType.Dlg)
        let dialoguesAndResourcesAndLessons = 
            dialogueResources |> 
                Seq.map (fun t -> 
                    let lessonEntry = { 
                        LessonRecord.Name = "Dialogue: " + t.Name.Value;
                        GameID = xc.gameEntry.ID;
                        ID = 0
                    }

                    let lessonEntryWithId = { lessonEntry with ID = xc.db.CreateOrUpdateLesson(lessonEntry) }
                    (LoadDialogue(t.Name, xc.gameResources), t, lessonEntryWithId))

        let languageType = LanguageTypeFromIETFLanguageTag(xc.pluginSettings.LanguageTag)

        this.LogWriteLine("Dialogues loaded.") |> ignore

        let stringsAndKeys = 
            dialoguesAndResourcesAndLessons |> 
            Seq.collect(fun (t, dialogueResource, lessonEntry) -> 
                let extractedM = ExtractStringsFromDialogue(t, languageType, Gender.MasculineOrNeutral, xc.masculineOrNeuterTalkTable, xc.feminineTalkTable)
                let extractedF = 
                    if (xc.masculineOrNeuterTalkTable = xc.feminineTalkTable) then 
                        [| |]
                    else
                        ExtractStringsFromDialogue(t, languageType, Gender.Feminine, xc.masculineOrNeuterTalkTable, xc.feminineTalkTable)

                let augmentedM = 
                    AugmentExtractedStringKeys(extractedM, dialogueResource.Name, dialogueResource.OriginDesc, Gender.MasculineOrNeutral) |>
                    Array.map(fun (t, k, genderlessK, g) -> (t, k, genderlessK, lessonEntry, g))
                let augmentedF = 
                    AugmentExtractedStringKeys(extractedF, dialogueResource.Name, dialogueResource.OriginDesc, Gender.Feminine) |>
                    Array.map(fun (t, k, genderlessK, g) -> (t, k, genderlessK, lessonEntry, g))
                Array.concat([|augmentedM; augmentedF|] |> Array.toSeq))

        this.LogWriteLine("Dialogue strings extracted.") |> ignore
        let retVal = stringsAndKeys |> Seq.map (fun (t, k, genderlessKey, l, g) -> 
            {
                CardRecord.Gender = g;
                ID = 0;
                KeyHash = 0;
                GenderlessKeyHash = 0;
                LanguageTag = xc.pluginSettings.LanguageTag;
                LessonID = l.ID;
                Reversible = true;
                SoundResource = String.Empty;
                Text = t;
                Key = k;
                GenderlessKey = genderlessKey;
            })

        this.LogWriteLine("Dialogue cards generated. Dialogue extraction complete.") |> ignore
        retVal

    /// <summary>
    /// Function that takes a 2DA file, and extracts its contents into a lesson.
    /// </summary>
    /// <param name="columns">the 2DA columns to extract as string refs</param>
    /// <param name="xc">extraction context</param>
    /// <param name="twoDA">the 2DA file representation</param>
    /// <param name="l">the lesson to which the cards should be added</param>
    /// <param name="languageTag">the language being extracted</param>
    member private this.ExtractGeneric2DA<'TalkTableString when 'TalkTableString :> ITalkTableString>
        (columns: string array)
        (xc: ExtractionContext<'TalkTableString>, twoDA: TwoDAFile, l: LessonRecord, languageTag: string): CardRecord array =
        columns |> Array.collect(fun column ->
            // find all rows with a valid string ref
            let validRows = twoDA.RowsWithValueInt(column) |> Array.filter(fun (_, strRef) -> strRef >= 0)
            validRows |> Array.map (fun (row, strRef) -> 
                let key = "2DA " + l.Name + " " + column + " " + row.ToString()
                {
                    CardRecord.Gender = "MasculineOrNeutral";
                    ID = 0;
                    Key = key;
                    KeyHash = 0;
                    GenderlessKey = key;
                    GenderlessKeyHash = 0;
                    LanguageTag = languageTag;
                    LessonID = l.ID;
                    Reversible = true;
                    SoundResource = String.Empty;
                    Text = (xc.masculineOrNeuterTalkTable.Strings.[strRef] :> ITalkTableString).Value
                }
            )
        )

    /// <summary>
    /// Generic handler for extracting string refs from 2DAs in an Aurora game.
    /// </summary>
    /// <param name="xc">the extraction context</param>
    /// <param name="orders">array of tuples describing what to extract, and how</param>
    member private this.Extract2DAs<'TalkTableString when 'TalkTableString :> ITalkTableString>
        (xc: ExtractionContext<'TalkTableString>, orders: TwoDAExtractionOrder<'TalkTableString> array) = 
        this.LogWriteLine("Starting 2DA extraction.") |> ignore

        let languageType = LanguageTypeFromIETFLanguageTag(xc.pluginSettings.LanguageTag)
        let allGeneratedCards = orders |> Array.collect(fun (twoDAName, action, lessonName) -> 
            let twoDARes = xc.gameResources |> Seq.tryFind(fun t -> (t.ResourceType = ResType.Twoda) && (t.Name.Value.ToLower() = twoDAName.ToLower()))
            match twoDARes with
            | Some(tdr) ->
                let twoDAFile = TwoDAFile.FromStream(tdr.GetStream)
                let lessonEntry = {
                    LessonRecord.Name = "2DA: " + lessonName;
                    GameID = xc.gameEntry.ID;
                    ID = 0
                }

                let lessonEntryWithId = {
                    lessonEntry with ID = xc.db.CreateOrUpdateLesson(lessonEntry)
                }
                
                let generatedCards = action(xc, twoDAFile, lessonEntryWithId, xc.pluginSettings.LanguageTag)
                generatedCards
            | _ -> [||])

        this.LogWriteLine("2DAs loaded.") |> ignore
        allGeneratedCards
        
    member private this.ExtractNWN12DAs(xc: ExtractionContext<TalkTableV3String>) = 
        let extractionList = [|
            ("actions",             this.ExtractGeneric2DA([|"STRING_REF"|]), "actions");
            ("ambientmusic",        this.ExtractGeneric2DA([|"Description"|]), "ambient music");
            ("ambientsound",        this.ExtractGeneric2DA([|"Description"|]), "ambient sound");
            ("appearance",          this.ExtractGeneric2DA([|"STRING_REF"|]), "creature appearance");
            ("armor",               this.ExtractGeneric2DA([|"DESCRIPTIONS"|]), "armor");
            ("baseitems",           this.ExtractGeneric2DA([|"Name"; "Description"|]), "base items")
            ("bodybag",             this.ExtractGeneric2DA([|"Name"|]), "body bags");
            ("classes",             this.ExtractGeneric2DA([|"Name"; "Plural"; "Lower"; "Description"|]), "actions");
            ("creaturespeed",       this.ExtractGeneric2DA([|"Name"|]), "creature speed");
            ("crtemplates",         this.ExtractGeneric2DA([|"STRREF"|]), "cr templates");
            ("damagelevels",        this.ExtractGeneric2DA([|"STRING_REF"|]), "damage levels");
            ("des_crft_armor",      this.ExtractGeneric2DA([|"Label0"; "Label1"; "Label2"; "Label3"; "Label4"|]), "crafting armor");
            ("des_crft_wewapon",    this.ExtractGeneric2DA([|"Label0"; "Label1"; "Label2"; "Label3"; "Label4"|]), "crafting weapons");
            ("des_crft_props",      this.ExtractGeneric2DA([|"SuccessText"|]), "crafting properties");
            ("des_restsystem",      this.ExtractGeneric2DA([|"FeedBackStrRefSuccess"; "FeedBackStrRefFail"|]), "rest system");
            ("disease",             this.ExtractGeneric2DA([|"Name"|]), "disease");
            ("domains",             this.ExtractGeneric2DA([|"Name"; "Description"|]), "spell domains");
            ("doortypes",           this.ExtractGeneric2DA([|"StringRefGame"|]), "door types");
            ("effecticons",         this.ExtractGeneric2DA([|"StrRef"|]), "effect icons");
            ("encdifficulty",       this.ExtractGeneric2DA([|"STRREF"|]), "encounter difficulty");
            ("environment",         this.ExtractGeneric2DA([|"STRREF"|]), "environments");
            ("feat",                this.ExtractGeneric2DA([|"FEAT";"DESCRIPTION"|]), "feats");
            ("gamespyrooms",        this.ExtractGeneric2DA([|"STR_REF"|]), "GameSpy rooms");
            ("gender",              this.ExtractGeneric2DA([|"NAME"|]), "gender");
            ("genericdoors",        this.ExtractGeneric2DA([|"StrRef"; "Name"|]), "generic doors");
            ("hen_companion",       this.ExtractGeneric2DA([|"STRREF";"DESCRIPTION"|]), "animal companions");
            ("hen_familiar",        this.ExtractGeneric2DA([|"STRREF";"DESCRIPTION"|]), "familiars");
            ("iprp_abilities",      this.ExtractGeneric2DA([|"Name"|]), "item properties: abilities");
            ("iprp_acmodtype",      this.ExtractGeneric2DA([|"Name"|]), "item properties: AC mod");
            ("iprp_addcost",        this.ExtractGeneric2DA([|"Name"|]), "item properties: additional cost");
            ("iprp_additional",     this.ExtractGeneric2DA([|"Name"|]), "item properties: additional");
            ("iprp_aligngrp",       this.ExtractGeneric2DA([|"Name"|]), "item properties: alignment groups");
            ("iprp_alignment",      this.ExtractGeneric2DA([|"Name"|]), "item properties: alignment");
            ("iprp_ammocost",       this.ExtractGeneric2DA([|"Name"|]), "item properties: ammo cost");
            ("iprp_ammotype",       this.ExtractGeneric2DA([|"Name"|]), "item properties: ammo type");
            ("iprp_amount",         this.ExtractGeneric2DA([|"Name"|]), "item properties: amount");
            ("iprp_arcspell",       this.ExtractGeneric2DA([|"Name"|]), "item properties: arc spell");
            ("iprp_bladecost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: blade cost");
            ("iprp_bonuscost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: bonus cost");
            ("iprp_chargecost",     this.ExtractGeneric2DA([|"Name"|]), "item properties: charge cost");
            ("iprp_color",          this.ExtractGeneric2DA([|"Name"|]), "item properties: color");
            ("iprp_combatdam",      this.ExtractGeneric2DA([|"Name"|]), "item properties: combat damage");
            ("iprp_damagecost",     this.ExtractGeneric2DA([|"Name";"GameString"|]), "item properties: damage cost");
            ("iprp_damagetype",     this.ExtractGeneric2DA([|"Name"|]), "item properties: damage type");
            ("iprp_damvulcost",     this.ExtractGeneric2DA([|"Name"|]), "item properties: damage vulnerability cost");
            ("iprp_feats",          this.ExtractGeneric2DA([|"Name"|]), "item properties: feats");
            ("iprp_immuncost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: immunity cost");
            ("iprp_immunity",       this.ExtractGeneric2DA([|"Name"|]), "item properties: immunity");
            ("iprp_kitcost",        this.ExtractGeneric2DA([|"Name"|]), "item properties: kit cost");
            ("iprp_lightcost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: light cost");
            ("iprp_material",       this.ExtractGeneric2DA([|"Name"|]), "item properties: material");
            ("iprp_matcost",        this.ExtractGeneric2DA([|"Name"|]), "item properties: material cost");
            ("iprp_meleecost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: melee cost");
            ("iprp_monstcost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: monster cost");
            ("iprp_monsterdam",     this.ExtractGeneric2DA([|"Name"|]), "item properties: monster damage");
            ("iprp_monsterhit",     this.ExtractGeneric2DA([|"Name"|]), "item properties: monster hit");
            ("iprp_neg10cost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: negative 10 penalty cost");
            ("iprp_neg5cost",       this.ExtractGeneric2DA([|"Name"|]), "item properties: negative 5 penalty cost");
            ("iprp_onhit",          this.ExtractGeneric2DA([|"Name"|]), "item properties: on hit");
            ("iprp_onhitcost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: on hit cost");
            ("iprp_onhitdur",       this.ExtractGeneric2DA([|"Name"|]), "item properties: on hit duration");
            ("iprp_onhitspell",     this.ExtractGeneric2DA([|"Name"|]), "item properties: on hit spell");
            ("iprp_paramtable",     this.ExtractGeneric2DA([|"Name"|]), "item properties: parameter table");
            ("iprp_poison",         this.ExtractGeneric2DA([|"Name"|]), "item properties: poison");
            ("iprp_protection",     this.ExtractGeneric2DA([|"Name"|]), "item properties: protection");
            ("iprp_qualcost",       this.ExtractGeneric2DA([|"Name"|]), "item properties: quality cost");
            ("iprp_quality",        this.ExtractGeneric2DA([|"Name"|]), "item properties: quality");
            ("iprp_redcost",        this.ExtractGeneric2DA([|"Name"|]), "item properties: reduced cost");
            ("iprp_resistcost",     this.ExtractGeneric2DA([|"Name"|]), "item properties: resistance cost");
            ("iprp_saveelement",    this.ExtractGeneric2DA([|"Name"|]), "item properties: save vs. element");
            ("iprp_savingthrow",    this.ExtractGeneric2DA([|"Name"|]), "item properties: saving throw");
            ("iprp_skillcost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: skill cost");
            ("iprp_soakcost",       this.ExtractGeneric2DA([|"Name"|]), "item properties: soak cost");
            ("iprp_spellcost",      this.ExtractGeneric2DA([|"Name"|]), "item properties: spell cost");
            ("iprp_spellcstr",      this.ExtractGeneric2DA([|"Name"|]), "item properties: spell caster");
            ("iprp_spells",         this.ExtractGeneric2DA([|"Name"|]), "item properties: spells");
            ("iprp_spellshl",       this.ExtractGeneric2DA([|"Name"|]), "item properties: spell shield");
            ("iprp_spellvcost",     this.ExtractGeneric2DA([|"Name"|]), "item properties: spell v cost");
            ("iprp_spellvlimm",     this.ExtractGeneric2DA([|"Name"|]), "item properties: spell v limm");
            ("iprp_srcost",         this.ExtractGeneric2DA([|"Name"|]), "item properties: sr cost");
            ("iprp_trapcost",       this.ExtractGeneric2DA([|"Name"|]), "item properties: trap cost");
            ("iprp_traps",          this.ExtractGeneric2DA([|"Name"|]), "item properties: traps");
            ("iprp_trapsize",       this.ExtractGeneric2DA([|"Name"|]), "item properties: trap size");
            ("iprp_visualfx",       this.ExtractGeneric2DA([|"Name"|]), "item properties: visual effects");
            ("iprp_walk",           this.ExtractGeneric2DA([|"Name"|]), "item properties: walk speed");
            ("iprp_weightcost",     this.ExtractGeneric2DA([|"Name"|]), "item properties: weight cost");
            ("iprp_weightinc",      this.ExtractGeneric2DA([|"Name"|]), "item properties: weight increase");
            ("itempropdef",         this.ExtractGeneric2DA([|"Name";"GameStrRef";"Description"|]), "item property definitions");
            ("itemprops",           this.ExtractGeneric2DA([|"StringRef"|]), "item properties");
            ("keymap",              this.ExtractGeneric2DA([|"ActionStrRef"|]), "key mappings");
            ("loadhints",           this.ExtractGeneric2DA([|"HINT"|]), "loading screen hints");
            ("loadscreens",         this.ExtractGeneric2DA([|"StrRef"|]), "loading screens");
            ("masterfeats",         this.ExtractGeneric2DA([|"STRREF";"DESCRIPTION"|]), "master feats");
            ("metamagic",           this.ExtractGeneric2DA([|"Name"|]), "metamagic");
            ("nwconfig",            this.ExtractGeneric2DA([|"TITLESTRREF"|]), "NWConfig strings");
            ("packages",            this.ExtractGeneric2DA([|"Name";"Description"|]), "packages");
            ("phenotype",           this.ExtractGeneric2DA([|"Name"|]), "phenotypes");
            ("placeables",          this.ExtractGeneric2DA([|"StrRef"|]), "placeables");
            ("placeabletypes",      this.ExtractGeneric2DA([|"StrRef"|]), "placeable types");
            ("poison",              this.ExtractGeneric2DA([|"Name"|]), "poison");
            ("pvpsettings",         this.ExtractGeneric2DA([|"strref"|]), "player vs. player settings");
            ("racialtypes",         this.ExtractGeneric2DA([|"Name";"ConverName";"ConverNameLower";"NamePlural";"Description";"Biography"|]), "racial types");
            ("ranges",              this.ExtractGeneric2DA([|"Name"|]), "ranges");
            ("skills",              this.ExtractGeneric2DA([|"Name";"Description"|]), "skills");
            ("soundeax",            this.ExtractGeneric2DA([|"Description"|]), "sound: EAX");
            ("soundset",            this.ExtractGeneric2DA([|"STRREF"|]), "sound sets");
            ("soundsettype",        this.ExtractGeneric2DA([|"STRREF"|]), "sound set types");
            ("spells",              this.ExtractGeneric2DA([|"Name";"SpellDesc";"AltMessage"|]), "spells");
            ("spellschools",        this.ExtractGeneric2DA([|"StringRef";"Description"|]), "spell schools");
            ("stringtokens",        this.ExtractGeneric2DA([|"Default";"StrRef1";"StrRef2";"StrRef3";"StrRef4"|]), "string tokens");
            ("traps",               this.ExtractGeneric2DA([|"TrapName"|]), "traps");
            ("treasurescale",       this.ExtractGeneric2DA([|"STRREF"|]), "treasure scale");
            ("waypoint",            this.ExtractGeneric2DA([|"STRREF"|]), "waypoints");
        |]
        this.Extract2DAs(xc, extractionList)

    member private this.ExtractNWN1(path: string, db: LLDatabase, args: string[]) = 
        let parser = new CommandLine.Parser(fun t ->
            t.HelpWriter <- System.Console.Out
            t.IgnoreUnknownArguments <- true)
        let pluginSettings = new NWN1PluginSettings()
        if (not(parser.ParseArguments(args, pluginSettings))) then
            raise(exn("failed to parse NWN1 configuration"))

        this.LogWriteLine("Creating NWN1 context from " + path) |> ignore
        let context = new NWN1Context(path)

        // find the talk tables
        let masculineOrNeuterTalkTable = TalkTableV3.FromFilePath(Path.Combine(path, "dialog.tlk"))
        let feminineTalkTablePath = Path.Combine(path, "dialogF.tlk")
        let feminineTalkTable = 
            if (File.Exists(feminineTalkTablePath)) then
                TalkTableV3.FromFilePath(feminineTalkTablePath)
            else
                masculineOrNeuterTalkTable

        let gameEntry = {
            GameRecord.Name = "Neverwinter Nights";
            ID = 0
        }
        let gameEntryWithId = { gameEntry with ID = db.CreateOrUpdateGame(gameEntry) }
        this.LogWriteLine("Game entry updated.") |> ignore

        let extractionContext = {
            ExtractionContext.gameResources = context.Resources;
            pluginSettings = pluginSettings;
            db = db;
            masculineOrNeuterTalkTable = masculineOrNeuterTalkTable;
            feminineTalkTable = feminineTalkTable;
            gameEntry = gameEntryWithId
        }
        
        let extractedDialogueCards = 
            if (pluginSettings.ExtractDialoguesBoolean) then
                this.ExtractDialogues(extractionContext) |> Array.ofSeq
            else
                [||]

        let extracted2DACards = 
            if (pluginSettings.Extract2DAsBoolean) then
                this.ExtractNWN12DAs(extractionContext)
            else
                [||]

        // filter out empty cards.
        let allCards = Array.concat([| extractedDialogueCards; extracted2DACards |]) |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

        this.LogWriteLine("Adding extracted cards to DB.") |> ignore
        db.CreateOrUpdateCards(allCards)

        this.LogWriteLine("NWN1 extraction complete.") |> ignore
        ()

    member private this.ExtractJadeEmpire2DAs(xc: ExtractionContext<TalkTableV4String>) = 
        let makeCreatureEntries(creatureName: string) = 
            [|
                (creatureName + "lv", this.ExtractGeneric2DA([|"label"|]), creatureName + "lv")
                (creatureName + "mv", this.ExtractGeneric2DA([|"nameref"; "descref"|]), creatureName + "mv")
            |]

        let creaturesExtraction = [| 
                "cannibal"; "chaika"; "clay_golem"; "deaths_hand"; "demon_bull"; "demon_elephant"; "demon_fox"; 
                "demon_horse"; "demon_rat"; "demon_toad"; "drunkenmaster"; "gimp_martial"; "gimp_spear";
                "heavenlywave"; "hiddenfist"; "imp_ham"; "imp_legs"; "ironpalm"; "jade_warrior"; "leapingtiger";
                "legendaryfist"; "magic_air"; "magic_earth"; "magic_fire"; "magic_water"; "mask_spirit";
                "masterli"; "monkeypaw"; "mother"; "mummy"; "ogre"; "paralyzepalm"; "red_minister"; "spiritwell";
                "stormdragon"; "trans_golem"; "trans_horse"; "trans_minister"; "viper"; "weapon_1sword";
                "weapon_bigaxe"; "weapon_gun"; "weapon_staff"; "weapon_swords"; "whitedemon" |] |> Array.collect(fun t -> makeCreatureEntries(t))

        let standaloneExtraction = [|
                ("1000cutslv",          this.ExtractGeneric2DA([|"Label"|]), "1000cutslv");
                ("books",               this.ExtractGeneric2DA([|"title"; "text"|]), "books");
                ("centipede",           this.ExtractGeneric2DA([|"nameref"; "descref"|]), "centipede");
                ("confusioneffects",    this.ExtractGeneric2DA([|"floatystrref"|]), "confusion effects");
                ("creaturespeed",       this.ExtractGeneric2DA([|"name"|]), "creature speed");
                ("cscreen",             this.ExtractGeneric2DA([|"title"; "text"|]), "cscreen");
                ("deathcounts",         this.ExtractGeneric2DA([|"strref"|]), "death counts");
                ("deathhints",          this.ExtractGeneric2DA([|"strref"|]), "death hints");
                ("debilitation",        this.ExtractGeneric2DA([|"floatystrref"|]), "debilitating effects");
                ("drunken",             this.ExtractGeneric2DA([|"nameref"; "descref"|]), "drunkenness");
                ("focuseffects",        this.ExtractGeneric2DA([|"display"; "desc"|]), "focus effects");
                ("gems",                this.ExtractGeneric2DA([|"alignmentsrtref"|]), "gems");
                ("henchdefaultdata",    this.ExtractGeneric2DA([|"descstrref"; "lockstrref"|]), "henchman default data");
                ("henchmenaidata",      this.ExtractGeneric2DA([|"displaystrref"|]), "henchman AI data");
                ("henchstates",         this.ExtractGeneric2DA([|"state_strref"|]), "henchman states");
                ("hintsarea",           this.ExtractGeneric2DA([|"hint_strref01"; "hint_strref02"; "hint_strref03"; "hint_strref"|]), "hints area");
                ("hintsgeneral",        this.ExtractGeneric2DA([|"chapter1"; "chapter2"; "chapter3"; "chapter4"; "chapter5"; "chapter6"; "chapter7"|]), "general hints");
                ("hintstech",           this.ExtractGeneric2DA([|"gameplayhint"|]), "technical hints");
                ("improvements",        this.ExtractGeneric2DA([|"storewarningstrref"|]), "improvements");
                ("items",               this.ExtractGeneric2DA([|"strref_name"; "strref_desc"|]), "items");
                ("jades",               this.ExtractGeneric2DA([|"title"; "descstrref"|]), "jades");
                ("jdk_players",         this.ExtractGeneric2DA([|"name"; "description"; "oneworddesc"|]), "predefined characters");
                ("magicstance0",        this.ExtractGeneric2DA([|"nameref"; "desc"|]), "magic stance 0");
                ("mappins",             this.ExtractGeneric2DA([|"strref"|]), "map pins");
                ("mggroupdata",         this.ExtractGeneric2DA([|"message"|]), "mg group data");
                ("mggroups",            this.ExtractGeneric2DA([|"titlestrref"; "descstrref"|]), "mg groups");
                ("mgweapons",           this.ExtractGeneric2DA([|"namestrref"; "descstrref"|]), "mg weapons");
                ("movie",               this.ExtractGeneric2DA(seq { for i in 0..14 do yield "subtitle" + i.ToString() + "strref" } |> Array.ofSeq), "movie");
                ("multipleeffects",     this.ExtractGeneric2DA([|"floatystrref"|]), "multiple effects");
                ("namelist",            this.ExtractGeneric2DA([|"male"; "female"|]), "name list");
                ("other",               this.ExtractGeneric2DA([|"descstrref"|]), "other");
                ("placeables",          this.ExtractGeneric2DA([|"strref"|]), "placeables");
                ("players",             this.ExtractGeneric2DA([|"name"; "description"; "oneworddesc"|]), "placeables");
                ("poison",              this.ExtractGeneric2DA([|"name"; "floatystrref"|]), "poison");
                ("polymorph",           this.ExtractGeneric2DA([|"name"; "description"; "oneworddesc"|]), "polymorph forms");
                ("reward",              this.ExtractGeneric2DA([|"rewardstrref"|]), "rewards");
                ("romanceindex",        this.ExtractGeneric2DA([|"strref"|]), "romance index");
                ("scriptedprogress",    this.ExtractGeneric2DA([|"strref"|]), "scripted progress");
                ("spiritelements",      this.ExtractGeneric2DA([|"nameref"; "desc"|]), "spirit elements");
                ("spiritmoves",         this.ExtractGeneric2DA([|"nameref"; "desc"; "longdesc"|]), "spirit moves");
                ("stringtokens",        this.ExtractGeneric2DA(seq { for i in 1..4 do yield "strref" + i.ToString() } |> Array.ofSeq), "string tokens");
                ("styleadvance",        this.ExtractGeneric2DA([|"descref"; "nameoverride"; "descoverride"|]), "style advancement");
                ("stylesuperlist",      this.ExtractGeneric2DA([|"nameref"; "descref"|]), "style super list");
                ("styletypes",          this.ExtractGeneric2DA([|"name"; "filtertext"; "favoritetext"|]), "style types");
                ("synergy",             this.ExtractGeneric2DA([|"name"; "desc"|]), "synergies");
                ("unique",              this.ExtractGeneric2DA([|"descstrref"|]), "unique");
                ("worldmapinfo",        this.ExtractGeneric2DA([|"areaname"|]), "world map info");
                ("zdc_impoffset",       this.ExtractGeneric2DA([|"label"|]), "zdc_impoffset");
                ("zdc_vfxoffset",       this.ExtractGeneric2DA([|"label"|]), "zdc_vfxoffset");
                ("zz_scoreboard",       this.ExtractGeneric2DA([|"titlestrref"; "namestrref"; "scorestrref"|]), "zdc_vfxoffset");
            |]

        let extractionList = Array.concat([| creaturesExtraction; standaloneExtraction |])
        this.Extract2DAs(xc, extractionList)

    member private this.ExtractJadeEmpire(path: string, db: LLDatabase, args: string[]) = 
        let parser = new CommandLine.Parser(fun t ->
            t.HelpWriter <- System.Console.Out
            t.IgnoreUnknownArguments <- true)
        let pluginSettings = new NWN1PluginSettings()
        if (not(parser.ParseArguments(args, pluginSettings))) then
            raise(exn("failed to parse Jade Empire configuration"))

        this.LogWriteLine("Creating Jade Empire context from " + path) |> ignore
        let context = new JadeEmpireContext(path)

        // find the talk tables
        let masculineOrNeuterTalkTable = TalkTableV4.FromFilePath(Path.Combine(path, "dialog.tlk"))
        let feminineTalkTablePath = Path.Combine(path, "dialogF.tlk")
        let feminineTalkTable = 
            if (File.Exists(feminineTalkTablePath)) then
                TalkTableV4.FromFilePath(feminineTalkTablePath)
            else
                masculineOrNeuterTalkTable

        let gameEntry = {
            GameRecord.Name = "Jade Empire";
            ID = 0
        }
        let gameEntryWithId = { gameEntry with ID = db.CreateOrUpdateGame(gameEntry) }
        this.LogWriteLine("Game entry updated.") |> ignore

        let extractionContext = {
            ExtractionContext.gameResources = context.Resources;
            pluginSettings = pluginSettings;
            db = db;
            masculineOrNeuterTalkTable = masculineOrNeuterTalkTable;
            feminineTalkTable = feminineTalkTable;
            gameEntry = gameEntryWithId
        }
        
        let extractedDialogueCards = 
            if (pluginSettings.ExtractDialoguesBoolean) then
                this.ExtractDialogues(extractionContext) |> Array.ofSeq
            else
                [||]

        let extracted2DACards = 
            if (pluginSettings.Extract2DAsBoolean) then
                this.ExtractJadeEmpire2DAs(extractionContext)
            else
                [||]

        // filter out empty cards.
        let allCards = Array.concat([| extractedDialogueCards; extracted2DACards |]) |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

        this.LogWriteLine("Adding extracted cards to DB.") |> ignore
        db.CreateOrUpdateCards(allCards)

        this.LogWriteLine("Jade Empire extraction complete.") |> ignore
        ()
