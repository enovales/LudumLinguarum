namespace InfinityAuroraEngine

open Argu
open LLDatabase
open InfinityAuroraEngine.CommonTypes
open InfinityAuroraEngine.GFFFileTypes
open InfinityAuroraEngine.IAResourceManager
open InfinityAuroraEngine.JadeEmpireContext
open InfinityAuroraEngine.KOTOR1Context
open InfinityAuroraEngine.KOTOR2Context
open InfinityAuroraEngine.NWN1Context
open InfinityAuroraEngine.TalkTable
open InfinityAuroraEngine.TwoDA
open LudumLinguarumPlugins
open System
open System.IO
open System.Text.RegularExpressions

type AuroraPluginArgs = 
    | [<Mandatory; EqualsAssignment>] Language_Tag of string
    | [<EqualsAssignment>] Extract_Dialogues of bool
    | [<EqualsAssignment>] Extract_2DAs of bool
    | [<EqualsAssignment>] Extract_All of bool
    with
        interface IArgParserTemplate with
            member this.Usage =
                match this with
                | Language_Tag _ -> "The language of the installed version of the game"
                | Extract_Dialogues _ -> "Whether or not content from dialogues should be extracted (default: false)"
                | Extract_2DAs _ -> "Whether or not content from 2DAs should be extracted (default: true)"
                | Extract_All _ -> "Shortcut for extracting all types of content"

type private ExtractionContext<'TalkTableString when 'TalkTableString :> ITalkTableString> = {
    gameResources: IGenericResource seq;
    pluginSettings: ParseResults<AuroraPluginArgs>;
    masculineOrNeuterTalkTable: ITalkTable<'TalkTableString>;
    feminineTalkTable: ITalkTable<'TalkTableString>;
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

type private ExtractedContentWithIndex = 
    {
        ecs: ExtractedContent array
        i: int
    }

type AuroraPlugin() = 
    let mutable outStream: TextWriter option = None
    let remapAndMergeExtractedContents(ecs: ExtractedContent array): ExtractedContent = 
        let folder(ecwi: ExtractedContentWithIndex)(ec: ExtractedContent): ExtractedContentWithIndex = 
            // remap all lessons in the new content, and then all cards.
            let newLessons = 
                ec.lessons |> Array.mapi (fun i l -> { l with ID = i + ecwi.i })
            let lessonIDMap = 
                newLessons 
                |> Array.zip ec.lessons
                |> Array.map (fun (oldLesson, newLesson) -> (oldLesson.ID, newLesson.ID))
                |> Map.ofArray
            let newCards = 
                ec.cards |> Array.map (fun c -> { c with LessonID = lessonIDMap |> Map.find(c.LessonID) })

            let newContent = 
                {
                    LudumLinguarumPlugins.ExtractedContent.lessons = newLessons
                    LudumLinguarumPlugins.ExtractedContent.cards = newCards
                }

            {
                ExtractedContentWithIndex.ecs = [| newContent |] |> Array.append ecwi.ecs
                i = ecwi.i + (ec.lessons |> Array.length)
            } 

        let remapped = 
            ecs 
            |> Array.fold folder { ExtractedContentWithIndex.ecs = [||]; i = 0 }

        {
            LudumLinguarumPlugins.ExtractedContent.lessons = remapped.ecs |> Array.collect (fun ec -> ec.lessons)
            LudumLinguarumPlugins.ExtractedContent.cards = remapped.ecs |> Array.collect(fun ec -> ec.cards)
        }

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            outStream <- Some(tw)
            ()
        member this.Name = "aurora"
        member this.Parameters = 
            [| 
            |]
    end
    interface IGameExtractorPlugin with
        member this.SupportedGames = 
            [| 
                { GameMetadata.name = "Neverwinter Nights"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "pl" |] }
                { GameMetadata.name = "Jade Empire"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }
                { GameMetadata.name = "Star Wars: Knights of the Old Republic"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }
                { GameMetadata.name = "Star Wars: Knights of the Old Republic II"; supportedLanguages = [| "de"; "en"; "es"; "fr"; "it" |] }
            |]
        member this.ExtractAll(game: string, path: string, args: string[]): ExtractedContent = 
            let parser = ArgumentParser.Create<AuroraPluginArgs>(errorHandler = new ProcessExiter())
            let results = parser.Parse(args)

            if (results.IsUsageRequested) || (results.GetAllResults() |> List.isEmpty) then
                Console.WriteLine(parser.PrintUsage())
                failwith "couldn't extract game"
            else
                match game with
                | "Neverwinter Nights" -> this.ExtractNWN1(path, results)
                | "Jade Empire" -> this.ExtractJadeEmpire(path, results)
                | "Star Wars: Knights of the Old Republic" -> this.ExtractKOTOR1(path, results)
                | "Star Wars: Knights of the Old Republic II" -> this.ExtractKOTOR2(path, results)
                | g -> raise(UnknownGameException("unknown game " + g))

    end

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))

    member private this.ExtractDialogues<'TalkTableString when 'TalkTableString :> ITalkTableString>(xc: ExtractionContext<'TalkTableString>): ExtractedContent = 
        this.LogWriteLine("Starting dialogue extraction.") |> ignore
        // extract dialogues, and create lessons for each one
        let dialogueResources = xc.gameResources |> Seq.filter(fun t -> t.ResourceType = ResType.Dlg) |> Array.ofSeq
        let zipResourcesAndLessons(i: int)(t: IGenericResource) = 
            let lessonEntry = 
                { 
                    LessonRecord.Name = "Dialogue: " + t.Name.Value;
                    ID = i
                }

            (LoadDialogue(t.Name, xc.gameResources), t, lessonEntry)
                                        
        let dialoguesAndResourcesAndLessons = 
            dialogueResources 
            |> Array.mapi zipResourcesAndLessons

        let languageType = LanguageTypeFromIETFLanguageTag(xc.pluginSettings.GetResult(AuroraPluginArgs.Language_Tag))

        this.LogWriteLine("Dialogues loaded.") |> ignore

        let generateStringsAndKeys(r: Dialogue * IGenericResource * LessonRecord) = 
            let (t, dialogueResource, lessonEntry) = r
            let extractedM = ExtractStringsFromDialogue(t, languageType, Gender.MasculineOrNeutral, xc.masculineOrNeuterTalkTable, xc.feminineTalkTable)
            let extractedF = 
                if (xc.masculineOrNeuterTalkTable = xc.feminineTalkTable) then 
                    []
                else
                    ExtractStringsFromDialogue(t, languageType, Gender.Feminine, xc.masculineOrNeuterTalkTable, xc.feminineTalkTable)

            let zipWithLessonEntry(tuple: string * string * string * string) = 
                let (t, k, genderlessK, g) = tuple
                (t, k, genderlessK, lessonEntry, g)

            let augmentedM = 
                AugmentExtractedStringKeys(extractedM, dialogueResource.Name, dialogueResource.OriginDesc, Gender.MasculineOrNeutral) 
                |> Seq.map zipWithLessonEntry
                |> Array.ofSeq
            let augmentedF = 
                AugmentExtractedStringKeys(extractedF, dialogueResource.Name, dialogueResource.OriginDesc, Gender.Feminine) 
                |> Seq.map zipWithLessonEntry
                |> Array.ofSeq

            Array.concat([|augmentedM; augmentedF|] |> Array.toSeq)

        let stringsAndKeys = 
            dialoguesAndResourcesAndLessons |> 
            Array.collect(generateStringsAndKeys)

        this.LogWriteLine("Dialogue strings extracted.") |> ignore

        let generateCardRecordForTuple(tuple: string * string * string * LessonRecord * string) = 
            let (t, k, genderlessKey, l, g) = tuple
            {
                CardRecord.Gender = g;
                ID = 0;
                KeyHash = 0;
                GenderlessKeyHash = 0;
                LanguageTag = xc.pluginSettings.GetResult(AuroraPluginArgs.Language_Tag);
                LessonID = l.ID;
                Reversible = true;
                SoundResource = String.Empty;
                Text = t;
                Key = k;
                GenderlessKey = genderlessKey;
            }
            
        let allCards = stringsAndKeys |> Array.map generateCardRecordForTuple
        let (_, _, allLessons) = dialoguesAndResourcesAndLessons |> Array.unzip3

        {
            LudumLinguarumPlugins.ExtractedContent.lessons = allLessons
            LudumLinguarumPlugins.ExtractedContent.cards = allCards
        }

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
        (xc: ExtractionContext<'TalkTableString>, orders: TwoDAExtractionOrder<'TalkTableString> array): ExtractedContent = 
        this.LogWriteLine("Starting 2DA extraction.") |> ignore

        let languageType = LanguageTypeFromIETFLanguageTag(xc.pluginSettings.GetResult(AuroraPluginArgs.Language_Tag))
        let allGeneratedCards = orders |> Array.mapi(fun i (twoDAName, action, lessonName) -> 
            let twoDARes = xc.gameResources |> Seq.tryFind(fun t -> (t.ResourceType = ResType.Twoda) && (t.Name.Value.ToLower() = twoDAName.ToLower()))
            let lessonEntry = {
                LessonRecord.Name = "2DA: " + lessonName;
                ID = i
            }
            match twoDARes with
            | Some(tdr) ->
                let twoDAFile = TwoDAFile.FromStream(tdr.GetStream)
                let generatedCards = action(xc, twoDAFile, lessonEntry, xc.pluginSettings.GetResult(AuroraPluginArgs.Language_Tag))
                (lessonEntry, generatedCards)
            | _ -> (lessonEntry, [||]))

        this.LogWriteLine("2DAs loaded.") |> ignore

        let (finalLessons, finalCardGroups) = 
            allGeneratedCards
            |> Array.filter(fun (_, cs) -> not(cs |> Array.isEmpty))
            |> Array.unzip

        {
            LudumLinguarumPlugins.ExtractedContent.lessons = finalLessons
            LudumLinguarumPlugins.ExtractedContent.cards = finalCardGroups |> Array.collect id
        }

        
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

    member private this.ExtractNWN1(path: string, args: ParseResults<AuroraPluginArgs>) = 
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

        let extractionContext = {
            ExtractionContext.gameResources = context.Resources;
            pluginSettings = args;
            masculineOrNeuterTalkTable = masculineOrNeuterTalkTable;
            feminineTalkTable = feminineTalkTable;
        }
        
        let extractedDialogueContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.Contains(AuroraPluginArgs.Extract_Dialogues)) then
                [| this.ExtractDialogues(extractionContext) |]
            else
                [||]

        let extracted2DAContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.GetResult(AuroraPluginArgs.Extract_2DAs, defaultValue = true)) then
                [| this.ExtractNWN12DAs(extractionContext) |]
            else
                [||]

        let allExtractedContent = Array.concat([| extractedDialogueContent; extracted2DAContent |])
        remapAndMergeExtractedContents(allExtractedContent)

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

    member private this.ExtractJadeEmpire(path: string, args: ParseResults<AuroraPluginArgs>) = 
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

        let extractionContext = {
            ExtractionContext.gameResources = context.Resources;
            pluginSettings = args;
            masculineOrNeuterTalkTable = masculineOrNeuterTalkTable;
            feminineTalkTable = feminineTalkTable;
        }
        
        let extractedDialogueContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.Contains(AuroraPluginArgs.Extract_Dialogues)) then
                [| this.ExtractDialogues(extractionContext) |]
            else
                [||]

        let extracted2DAContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.GetResult(AuroraPluginArgs.Extract_2DAs, defaultValue = true)) then
                [| this.ExtractJadeEmpire2DAs(extractionContext) |]
            else
                [||]

        let allExtractedContent = Array.concat([| extractedDialogueContent; extracted2DAContent |])
        remapAndMergeExtractedContents(allExtractedContent)

    member private this.ExtractKOTOR12DAs(xc: ExtractionContext<TalkTableV3String>) = 
        let standaloneExtraction = [|
                ("aiscripts",          this.ExtractGeneric2DA([|"name_strref";"description_strref"|]), "AI scripts");
                ("ambientmusic",       this.ExtractGeneric2DA([|"description"|]), "ambient music");
                ("ambientsound",       this.ExtractGeneric2DA([|"description"|]), "ambient sound");
                ("appearance",         this.ExtractGeneric2DA([|"string_ref"|]), "appearance");
                ("baseitems",          this.ExtractGeneric2DA([|"name"|]), "base items");
                ("bindablekeys",       this.ExtractGeneric2DA([|"keynamestrref"|]), "bindable keys");
                ("bodybag",            this.ExtractGeneric2DA([|"name"|]), "bodybags");
                ("classes",            this.ExtractGeneric2DA([|"name"; "description"|]), "base items");
                ("credits",            this.ExtractGeneric2DA([|"name"|]), "credits");
                ("difficultyopt",      this.ExtractGeneric2DA([|"name"|]), "difficulty options");
                ("encdifficulty",      this.ExtractGeneric2DA([|"strref"|]), "encounter difficulty");
                ("feat",               this.ExtractGeneric2DA([|"name"; "description"|]), "feat");
                ("feedbacktext",       this.ExtractGeneric2DA([|"strref"|]), "feedback text");
                ("gender",             this.ExtractGeneric2DA([|"name"|]), "gender");
                ("genericdoors",       this.ExtractGeneric2DA([|"strref"|]), "generic doors");
                ("iprp_abilities",     this.ExtractGeneric2DA([|"name"|]), "item property: abilities");
                ("iprp_acmodtype",     this.ExtractGeneric2DA([|"name"|]), "item property: AC mod types");
                ("iprp_aligngrp",      this.ExtractGeneric2DA([|"name"|]), "item property: alignment group");
                ("iprp_amount",        this.ExtractGeneric2DA([|"name"|]), "item property: amount");
                ("iprp_bladecost",     this.ExtractGeneric2DA([|"name"|]), "item property: blade cost");
                ("iprp_bonuscost",     this.ExtractGeneric2DA([|"name"|]), "item property: bonus cost");
                ("iprp_chargecost",    this.ExtractGeneric2DA([|"name"|]), "item property: charge cost");
                ("iprp_color",         this.ExtractGeneric2DA([|"name"|]), "item property: color");
                ("iprp_combatdam",     this.ExtractGeneric2DA([|"name"|]), "item property: combat damage");
                ("iprp_damagecost",    this.ExtractGeneric2DA([|"name"|]), "item property: damage cost");
                ("iprp_damagetype",    this.ExtractGeneric2DA([|"name"|]), "item property: damage type");
                ("iprp_damvulcost",    this.ExtractGeneric2DA([|"name"|]), "item property: damage vulnerability cost");
                ("iprp_immuncost",     this.ExtractGeneric2DA([|"name"|]), "item property: immunity cost");
                ("iprp_immunity",      this.ExtractGeneric2DA([|"name"|]), "item property: immunity");
                ("iprp_lightcost",     this.ExtractGeneric2DA([|"name"|]), "item property: light cost");
                ("iprp_meleecost",     this.ExtractGeneric2DA([|"name"|]), "item property: melee cost");
                ("iprp_monsterhit",    this.ExtractGeneric2DA([|"name"|]), "item property: monster hit");
                ("iprp_monstcost",     this.ExtractGeneric2DA([|"name"|]), "item property: monster cost");
                ("iprp_neg5cost",      this.ExtractGeneric2DA([|"name"|]), "item property: negative 5 cost");
                ("iprp_neg10cost",     this.ExtractGeneric2DA([|"name"|]), "item property: negative 10 cost");
                ("iprp_onhit",         this.ExtractGeneric2DA([|"name"|]), "item property: on hit effect");
                ("iprp_onhitdc",       this.ExtractGeneric2DA([|"name"|]), "item property: on hit DC");
                ("iprp_onhitcost",     this.ExtractGeneric2DA([|"name"|]), "item property: on hit cost");
                ("iprp_onhitdur",      this.ExtractGeneric2DA([|"name"|]), "item property: on hit duration");
                ("iprp_paramtable",    this.ExtractGeneric2DA([|"name"|]), "item property: parameter table");
                ("iprp_poison",        this.ExtractGeneric2DA([|"name"|]), "item property: poison");
                ("iprp_protection",    this.ExtractGeneric2DA([|"name"|]), "item property: protection");
                ("iprp_redcost",       this.ExtractGeneric2DA([|"name"|]), "item property: reduced cost");
                ("iprp_resistcost",    this.ExtractGeneric2DA([|"name"|]), "item property: resistance cost");
                ("iprp_saveelement",   this.ExtractGeneric2DA([|"name"|]), "item property: save against element");
                ("iprp_savingthrow",   this.ExtractGeneric2DA([|"name"|]), "item property: saving throw");
                ("iprp_soakcost",      this.ExtractGeneric2DA([|"name"|]), "item property: soak cost");
                ("iprp_srcost",        this.ExtractGeneric2DA([|"name"|]), "item property: SR cost");
                ("iprp_walk",          this.ExtractGeneric2DA([|"name"|]), "item property: walk");
                ("iprp_weightinc",     this.ExtractGeneric2DA([|"name"|]), "item property: weight increase");
                ("iprp_weightcost",    this.ExtractGeneric2DA([|"name"|]), "item property: weight cost");
                ("itemprops",          this.ExtractGeneric2DA([|"stringref"|]), "item properties");
                ("itempropdef",        this.ExtractGeneric2DA([|"name"|]), "item property definitions");
                ("keymap",             this.ExtractGeneric2DA([|"actionstrref"; "descstrref"|]), "keymap");
                ("loadscreenhints",    this.ExtractGeneric2DA([|"gameplayhint"; "storyhint"|]), "loading screen hints");
                ("masterfeats",        this.ExtractGeneric2DA([|"strref"|]), "master feats");
                ("modulesave",         this.ExtractGeneric2DA([|"areaname"|]), "module save data");
                ("movies",             this.ExtractGeneric2DA([|"strrefname"|]), "movies");
                ("placeables",         this.ExtractGeneric2DA([|"strref"|]), "placeables");
                ("planetary",          this.ExtractGeneric2DA([|"name"; "description"|]), "planetary map");
                ("poison",             this.ExtractGeneric2DA([|"name"|]), "poison");
                ("racialtypes",        this.ExtractGeneric2DA([|"name"|]), "racial types");
                ("skills",             this.ExtractGeneric2DA([|"name"; "description"|]), "skills");
                ("soundset",           this.ExtractGeneric2DA([|"strref"|]), "sound sets");
                ("spells",             this.ExtractGeneric2DA([|"name"; "spelldesc"|]), "spells");
                ("stringtokens",       this.ExtractGeneric2DA([|"default"; "strref1"; "strref2"|]), "string tokens");
                ("texpacks",           this.ExtractGeneric2DA([|"strrefname"|]), "texture packs");
                ("traps",              this.ExtractGeneric2DA([|"trapname"; "name"|]), "traps");
                ("tutorial",           this.ExtractGeneric2DA([|"message0"; "message1"; "message2"|]), "tutorial");
                ("tutorial_old",       this.ExtractGeneric2DA([|"message0"; "message1"; "message2"|]), "tutorial (old)");
            |]

        this.Extract2DAs(xc, standaloneExtraction)

    member private this.ExtractKOTOR1(path: string, args: ParseResults<AuroraPluginArgs>) = 
        this.LogWriteLine("Creating KOTOR1 context from " + path) |> ignore
        let context = new KOTOR1Context(path)

        // find the talk tables
        let masculineOrNeuterTalkTable = TalkTableV3.FromFilePath(Path.Combine(path, "dialog.tlk"))
        let extractionContext = {
            ExtractionContext.gameResources = context.Resources;
            pluginSettings = args
            masculineOrNeuterTalkTable = masculineOrNeuterTalkTable;
            feminineTalkTable = masculineOrNeuterTalkTable;
        }
        
        let extractedDialogueContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.Contains(AuroraPluginArgs.Extract_Dialogues)) then
                [| this.ExtractDialogues(extractionContext) |]
            else
                [||]

        let extracted2DAContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.GetResult(AuroraPluginArgs.Extract_2DAs, defaultValue = true)) then
                [| this.ExtractKOTOR12DAs(extractionContext) |]
            else
                [||]

        let allExtractedContent = Array.concat([| extractedDialogueContent; extracted2DAContent |])
        remapAndMergeExtractedContents(allExtractedContent)


    member private this.ExtractKOTOR22DAs(xc: ExtractionContext<TalkTableV3String>) = 
        let standaloneExtraction = [|
                ("aiscripts",          this.ExtractGeneric2DA([|"name_strref"|]), "AI scripts");
                ("ambientmusic",       this.ExtractGeneric2DA([|"description"|]), "ambient music");
                ("ambientsound",       this.ExtractGeneric2DA([|"description"|]), "ambient sound");
                ("baseitems",          this.ExtractGeneric2DA([|"name"|]), "base items");
                ("bindablekeys",       this.ExtractGeneric2DA([|"keynamestrref"|]), "bindable keys");
                ("bodybag",            this.ExtractGeneric2DA([|"name"|]), "bodybags");
                ("classes",            this.ExtractGeneric2DA([|"name"; "description"|]), "classes");
                ("credits",            this.ExtractGeneric2DA([|"name"|]), "credits");
                ("difficultyopt",      this.ExtractGeneric2DA([|"name"|]), "difficulty options");
                ("effecticon",         this.ExtractGeneric2DA([|"namestrref"|]), "effect icons");
                ("encdifficulty",      this.ExtractGeneric2DA([|"strref"|]), "encounter difficulty");
                ("feat",               this.ExtractGeneric2DA([|"name"; "description"|]), "feats");
                ("feedbacktext",       this.ExtractGeneric2DA([|"strref"|]), "feedback text");
                ("genericdoors",       this.ExtractGeneric2DA([|"strref"|]), "generic doors");
                ("gender",             this.ExtractGeneric2DA([|"name"|]), "gender");
                ("iprp_abilities",     this.ExtractGeneric2DA([|"name"|]), "item property: abilities");
                ("iprp_acmodtype",     this.ExtractGeneric2DA([|"name"|]), "item property: AC mod types");
                ("iprp_aligngrp",      this.ExtractGeneric2DA([|"name"|]), "item property: alignment group");
                ("iprp_amount",        this.ExtractGeneric2DA([|"name"|]), "item property: amount");
                ("iprp_attribcost",    this.ExtractGeneric2DA([|"name"|]), "item property: attribute cost");
                ("iprp_bladecost",     this.ExtractGeneric2DA([|"name"|]), "item property: blade cost");
                ("iprp_bonuscost",     this.ExtractGeneric2DA([|"name"|]), "item property: bonus cost");
                ("iprp_chargecost",    this.ExtractGeneric2DA([|"name"|]), "item property: charge cost");
                ("iprp_color",         this.ExtractGeneric2DA([|"name"|]), "item property: color");
                ("iprp_combatdam",     this.ExtractGeneric2DA([|"name"|]), "item property: combat damage");
                ("iprp_damagecost",    this.ExtractGeneric2DA([|"name"|]), "item property: damage cost");
                ("iprp_damagetype",    this.ExtractGeneric2DA([|"name"|]), "item property: damage type");
                ("iprp_damvulcost",    this.ExtractGeneric2DA([|"name"|]), "item property: damage vulnerability cost");
                ("iprp_immuncost",     this.ExtractGeneric2DA([|"name"|]), "item property: immunity cost");
                ("iprp_immunity",      this.ExtractGeneric2DA([|"name"|]), "item property: immunity");
                ("iprp_lightcost",     this.ExtractGeneric2DA([|"name"|]), "item property: light cost");
                ("iprp_meleecost",     this.ExtractGeneric2DA([|"name"|]), "item property: melee cost");
                ("iprp_monsterhit",    this.ExtractGeneric2DA([|"name"|]), "item property: monster hit");
                ("iprp_monstcost",     this.ExtractGeneric2DA([|"name"|]), "item property: monster cost");
                ("iprp_neg5cost",      this.ExtractGeneric2DA([|"name"|]), "item property: negative 5 cost");
                ("iprp_neg10cost",     this.ExtractGeneric2DA([|"name"|]), "item property: negative 10 cost");
                ("iprp_onhit",         this.ExtractGeneric2DA([|"name"|]), "item property: on hit effect");
                ("iprp_onhitdc",       this.ExtractGeneric2DA([|"name"|]), "item property: on hit DC");
                ("iprp_onhitcost",     this.ExtractGeneric2DA([|"name"|]), "item property: on hit cost");
                ("iprp_onhitdur",      this.ExtractGeneric2DA([|"name"|]), "item property: on hit duration");
                ("iprp_paramtable",    this.ExtractGeneric2DA([|"name"|]), "item property: parameter table");
                ("iprp_pc",            this.ExtractGeneric2DA([|"name"|]), "item property: player character");
                ("iprp_poison",        this.ExtractGeneric2DA([|"name"|]), "item property: poison");
                ("iprp_protection",    this.ExtractGeneric2DA([|"name"|]), "item property: protection");
                ("iprp_redcost",       this.ExtractGeneric2DA([|"name"|]), "item property: reduced cost");
                ("iprp_resistcost",    this.ExtractGeneric2DA([|"name"|]), "item property: resistance cost");
                ("iprp_saveelement",   this.ExtractGeneric2DA([|"name"|]), "item property: save against element");
                ("iprp_savingthrow",   this.ExtractGeneric2DA([|"name"|]), "item property: saving throw");
                ("iprp_soakcost",      this.ExtractGeneric2DA([|"name"|]), "item property: soak cost");
                ("iprp_srcost",        this.ExtractGeneric2DA([|"name"|]), "item property: SR cost");
                ("iprp_walk",          this.ExtractGeneric2DA([|"name"|]), "item property: walk");
                ("iprp_weightinc",     this.ExtractGeneric2DA([|"name"|]), "item property: weight increase");
                ("iprp_weightcost",    this.ExtractGeneric2DA([|"name"|]), "item property: weight cost");
                ("itempropdef",        this.ExtractGeneric2DA([|"name"|]), "item property definitions");
                ("itemprops",          this.ExtractGeneric2DA([|"stringref"|]), "item properties");
                ("keymap",             this.ExtractGeneric2DA([|"actionstrref"; "descstrref"|]), "keymap");
                ("loadscreenhints",    this.ExtractGeneric2DA([|"gameplayhint"; "gameplayhintxbox"|]), "loading screen hints");
                ("loadstoryhints",     this.ExtractGeneric2DA([|"storyhint"|]), "loading screen story hints");
                ("masterfeats",        this.ExtractGeneric2DA([|"strref"|]), "master feats");
                ("modulesave",         this.ExtractGeneric2DA([|"areaname"|]), "module save data");
                ("movies",             this.ExtractGeneric2DA([|"strrefname"|]), "movies");
                ("musictable",         this.ExtractGeneric2DA([|"strrefname"|]), "music table");
                ("placeables",         this.ExtractGeneric2DA([|"strref"|]), "placeables");
                ("planetary",          this.ExtractGeneric2DA([|"name"; "description"; "lockedoutreason"|]), "planetary map");
                ("racialtypes",        this.ExtractGeneric2DA([|"name"|]), "racial types");
                ("skills",             this.ExtractGeneric2DA([|"name"; "description"|]), "skills");
                ("soundset",           this.ExtractGeneric2DA([|"strref"|]), "sound sets");
                ("spells",             this.ExtractGeneric2DA([|"name"; "spelldesc"|]), "spells");
                ("stringtokens",       this.ExtractGeneric2DA([|"default"; "strref1"; "strref2"|]), "string tokens");
                ("subrace",            this.ExtractGeneric2DA([|"name"|]), "subrace");
                ("swoopupgrade",       this.ExtractGeneric2DA([|"desc_strref"|]), "swoop racing upgrades");
                ("texpacks",           this.ExtractGeneric2DA([|"strrefname"|]), "texture packs");
                ("traps",              this.ExtractGeneric2DA([|"trapname"; "name"|]), "traps");
                ("tutorial",           this.ExtractGeneric2DA([|"message_pc0"; "message_pc1"; "message_pc2"; "message_xbox0"; "message_xbox1"; "message_xbox2"|]), "tutorial");
            |]

        this.Extract2DAs(xc, standaloneExtraction)

    member private this.ExtractKOTOR2(path: string, args: ParseResults<AuroraPluginArgs>) = 
        this.LogWriteLine("Creating KOTOR2 context from " + path) |> ignore
        let context = new KOTOR2Context(path)

        // find the talk tables
        let masculineOrNeuterTalkTable = TalkTableV3.FromFilePath(Path.Combine(path, "dialog.tlk"))
        let extractionContext = {
            ExtractionContext.gameResources = context.Resources;
            pluginSettings = args;
            masculineOrNeuterTalkTable = masculineOrNeuterTalkTable;
            feminineTalkTable = masculineOrNeuterTalkTable;
        }

        let findImplementationCommentsRegex = new Regex("\{.*\}")
        let rec textWithoutImplementationComments(t: string): string = 
            let rmatch = findImplementationCommentsRegex.Match(t)
            if (rmatch.Success) then
                textWithoutImplementationComments(t.Remove(rmatch.Index, rmatch.Length).Trim())
            else
                t

        let removeImplementationComments(c: CardRecord): CardRecord = 
            { c with Text = textWithoutImplementationComments(c.Text) }
        
        let extractedDialogueContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.Contains(AuroraPluginArgs.Extract_Dialogues)) then
                [| this.ExtractDialogues(extractionContext) |]
            else
                [||]

        let extracted2DAContent = 
            if (args.Contains(AuroraPluginArgs.Extract_All) || args.GetResult(AuroraPluginArgs.Extract_2DAs, defaultValue = true)) then
                [| this.ExtractKOTOR22DAs(extractionContext) |]
            else
                [||]

        let allExtractedContent = Array.concat([| extractedDialogueContent; extracted2DAContent |])
        let mergedContent = remapAndMergeExtractedContents(allExtractedContent)

        {
            mergedContent with cards = mergedContent.cards |> Array.map removeImplementationComments
        }
