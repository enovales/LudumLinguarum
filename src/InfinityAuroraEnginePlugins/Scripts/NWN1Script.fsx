#I __SOURCE_DIRECTORY__
#load "load-project-debug.fsx"

open InfinityAuroraEnginePlugins
open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFF
open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.NWN1Context
open InfinityAuroraEnginePlugins.SerializedGFF
open InfinityAuroraEnginePlugins.TalkTable
open LLDatabase
open LudumLinguarumPlugins
open System.Diagnostics
open System.IO

let rootDataPathGerman = @"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\"
let keyFilePathGerman = Path.Combine(rootDataPathGerman, "chitin.key")
let rootDataPathEnglish = @"C:\GOG Games\Neverwinter Nights Diamond Edition"
let keyFilePathEnglish = Path.Combine(rootDataPathEnglish, "chitin.key")
let outputPath = @"C:\TestNWNExtract"
let twoDAExtractPath = @"c:\NWN1Extract2DAs"

let extractNWNFiles() = 
    let keyFile = KEYFile.FromFilePath(keyFilePathGerman)
    let bifFiles = [for i in 0..(keyFile.BIFFilenames.Length - 1) -> BIFFile.FromKEYFile(keyFile, rootDataPathGerman, i)]
    let bifSet = BIFSet.FromKEYAndBifs(keyFile, bifFiles)

    ignore(Directory.CreateDirectory(outputPath))

    ignore(keyFile.ResourceEntries |> List.map(fun kte -> bifSet.Extract(kte, outputPath)))
    ()

let extract2DAs() = 
    let contextEnglish = new NWN1Context(rootDataPathEnglish)
    let twoDAResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Twoda)

    if (not(Directory.Exists(twoDAExtractPath))) then
        Directory.CreateDirectory(twoDAExtractPath) |> ignore

    twoDAResources |> Array.iter (fun t ->
        let twoDAPath = Path.Combine(twoDAExtractPath, t.Name.Value + ".2da")
        use br = t.GetBinaryReader
        File.WriteAllBytes(twoDAPath, br.ReadBytes(int br.BaseStream.Length))
    )


let readDialogues() = 
    let keyFile = KEYFile.FromFilePath(keyFilePathGerman)
    let bifFiles = [for i in 0..(keyFile.BIFFilenames.Length - 1) -> BIFFile.FromKEYFile(keyFile, rootDataPathGerman, i)]
    let bifSet = BIFSet.FromKEYAndBifs(keyFile, bifFiles)

    let dialogues = keyFile.ResourceEntries |> List.filter(fun r -> r.resource.ResType = ResType.Dlg )
    ignore (dialogues |> List.map(fun d -> bifSet.GetBinaryReader(d).Dispose()))

let readErf() = 
    let erfFile = ERFFile.FromFilePath(Path.Combine(rootDataPathGerman, @"nwm\Chapter1.nwm"))
    Trace.Assert(erfFile.Resources.Length > 0)
    erfFile.Resources |> Array.map (fun t -> System.Console.WriteLine(t.resRef.Value + "." + t.resType.ToString())) |> ignore

let extractNWN1Cards() = 
    let plugin = new AuroraPlugin()
    let db = new LLDatabase(":memory:")
    (plugin :> IGameExtractorPlugin).Load(System.Console.Out)
    (plugin :> IGameExtractorPlugin).ExtractAll("Neverwinter Nights", rootDataPathEnglish, db)

    Trace.Assert(db.Games.Length > 0)
    Trace.Assert(db.Lessons.Length > 0)
    Trace.Assert(db.Cards.Length > 0)

    System.Console.WriteLine("extracted " + db.Cards.Length.ToString() + " cards from NWN1") |> ignore
    ()

let testLoadNWN1TalkTable() = 
    let talkTable = TalkTableV3.FromFilePath(Path.Combine(rootDataPathGerman, "dialog.tlk"))
    let fTalkTable = TalkTableV3.FromFilePath(Path.Combine(rootDataPathGerman, "dialogF.tlk"))

    Trace.Assert((talkTable :> ITalkTable<TalkTableV3String>).Strings.Length > 0)
    Trace.Assert((fTalkTable :> ITalkTable<TalkTableV3String>).Strings.Length > 0)

let testLoadGFF() = 
    let bifSet = BIFSet.FromKEYPath(keyFilePathEnglish)
    let resOpt = bifSet.GetBinaryReader({ ResRef.Value = "nw_hen_bod" }, ResType.Dlg)
    let gffFile = GFFFile.FromStream(resOpt.Value.BaseStream)
    let gff = GFF.FromSerializedGFF(gffFile)
    Trace.Assert(gff.Members.Fields.Length > 0)

let dumpResourceTypes() = 
    let bifSet = BIFSet.FromKEYPath(keyFilePathEnglish)
    let resourceGroups = bifSet.KEY.ResourceEntries |> List.toArray |> Array.groupBy (fun t -> t.resource.ResType)
    resourceGroups 
    |> Array.iter (fun (restype, l) -> System.Console.WriteLine(restype.ToString() + ": " + l.Length.ToString()))

let tryCreatingContext() = 
    let contextGerman = new NWN1Context(rootDataPathGerman)
    let contextEnglish = new NWN1Context(rootDataPathEnglish)

    Trace.Assert(contextGerman.Resources.Length > 0)
    Trace.Assert(contextEnglish.Resources.Length > 0)

let extractOneLanguageDialoguesToDatabase() = 
    let contextGerman = new NWN1Context(rootDataPathGerman)
    let englishTalkTable = TalkTableV3.FromFilePath(Path.Combine(rootDataPathEnglish, "dialog.tlk"))
    let dialogueResources = contextGerman.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Dlg)
    let dialoguesAndResources = dialogueResources |> Array.map (fun t -> (LoadDialogue(t.Name, contextGerman.Resources), t))

    System.Console.WriteLine("loaded " + dialoguesAndResources.Length.ToString() + " dialogues")

    let stringsAndKeys = 
        dialoguesAndResources |> 
        Array.collect(fun (t, dialogueResource) -> 
            let extracted = ExtractStringsFromDialogue(t, LanguageType.English, Gender.MasculineOrNeutral, englishTalkTable, englishTalkTable)
            AugmentExtractedStringKeys(extracted, dialogueResource.Name, dialogueResource.OriginDesc, Gender.MasculineOrNeutral) |> Array.ofSeq
            )

    let lldb = new LLDatabase(":memory:")
    stringsAndKeys |> Array.iter (fun (t, k, genderlessKey, g) -> 
        let newCard = {
            CardRecord.Gender = g;
            ID = 0;
            LanguageTag = "de";
            LessonID = 0;
            Text = t;
            Key = k;
            GenderlessKey = genderlessKey;
            KeyHash = k.GetHashCode();
            GenderlessKeyHash = genderlessKey.GetHashCode();
            Reversible = true;
            SoundResource = ""
        }
        lldb.AddCard(newCard) |> ignore)

    Trace.Assert(lldb.Cards |> Array.ofSeq |> Array.length = stringsAndKeys.Length)
    
let checkLoadedDialogueGFF() = 
    let contextEnglish = new NWN1Context(rootDataPathEnglish)
    let gff = LoadGFF({ ResRef.Value = "nw_hen_bod"}, ResType.Dlg, contextEnglish.Resources)
    Trace.Assert(gff.Members.GetDword(SerializedDialogue.DelayEntryName).IsSome, "didn't have DelayEntry")
    Trace.Assert(gff.Members.GetDword(SerializedDialogue.DelayReplyName).IsSome, "didn't have DelayReply")
    Trace.Assert(gff.Members.GetResRef(SerializedDialogue.EndConverAbortName).IsSome, "didn't have EndConverAbort")
    Trace.Assert(gff.Members.GetResRef(SerializedDialogue.EndConversationName).IsSome, "didn't have EndConversation")
    Trace.Assert(gff.Members.GetList(SerializedDialogue.EntryListName).IsSome, "didn't have EntryList")
    Trace.Assert(gff.Members.GetDword(SerializedDialogue.NumWordsName).IsSome, "didn't have NumWords")
    //Assert.IsTrue(gff.Members.GetByte(SerializedDialogue.PreventZoomInName).IsSome, "didn't have PreventZoomIn")
    Trace.Assert(gff.Members.GetList(SerializedDialogue.ReplyListName).IsSome, "didn't have ReplyList")
    Trace.Assert(gff.Members.GetList(SerializedDialogue.StartingListName).IsSome, "didn't have StartingList")

let testLoadSerializedDialogue() = 
    let contextEnglish = new NWN1Context(rootDataPathEnglish)
    let gff = LoadGFF({ ResRef.Value = "nw_hen_bod"}, ResType.Dlg, contextEnglish.Resources)
    let serializedDialogue = SerializedDialogue.FromGFF(gff)
    Trace.Assert(serializedDialogue.EntryList.IsSome, "didn't have entry list")
    Trace.Assert(serializedDialogue.StartingList.IsSome, "didn't have starting list")
    Trace.Assert(serializedDialogue.ReplyList.IsSome, "didn't have reply list")

let testLoadDialogue() = 
    let contextEnglish = new NWN1Context(rootDataPathEnglish)
    let gff = LoadGFF({ ResRef.Value = "nw_hen_bod"}, ResType.Dlg, contextEnglish.Resources)
    let serializedDialogue = SerializedDialogue.FromGFF(gff)
    let dialogue = Dialogue.FromSerialized(serializedDialogue)

    Trace.Assert(dialogue.EntryList.Length > 0)
    Trace.Assert(dialogue.ReplyList.Length > 0)
    Trace.Assert(dialogue.StartingList.Length > 0)

let testWalkDialogueTree() = 
    let contextEnglish = new NWN1Context(rootDataPathEnglish)
    let dialogue = LoadDialogue({ ResRef.Value = "nw_hen_bod"}, contextEnglish.Resources)
    let strings = dialogue.StartingList |> Array.mapi (fun i t -> GatherStrings(t) |> List.toArray) |> Array.concat
    let englishTalkTable = TalkTableV3.FromFilePath(Path.Combine(rootDataPathEnglish, "dialog.tlk"))
    let germanTalkTable = TalkTableV3.FromFilePath(Path.Combine(rootDataPathGerman, "dialog.tlk"))
    let germanTalkTableF = TalkTableV3.FromFilePath(Path.Combine(rootDataPathGerman, "dialogF.tlk"))
    strings |> Array.map (fun (t, k) -> 
        System.Console.WriteLine(EvaluateString(t, englishTalkTable, englishTalkTable, LanguageType.English, Gender.MasculineOrNeutral))
        System.Console.WriteLine(EvaluateString(t, germanTalkTable, germanTalkTableF, LanguageType.German, Gender.MasculineOrNeutral))) |> ignore

let testWalkAllDialogueTrees() = 
    let contextEnglish = new NWN1Context(rootDataPathEnglish)
    let englishTalkTable = TalkTableV3.FromFilePath(Path.Combine(rootDataPathEnglish, "dialog.tlk"))
    let dialogueResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Dlg)
    let dialogues = dialogueResources |> Array.map (fun t -> LoadDialogue(t.Name, contextEnglish.Resources))

    System.Console.WriteLine("loaded " + dialogues.Length.ToString() + " dialogues")

    dialogues |> 
        Array.collect(fun t -> ExtractStringsFromDialogue(t, LanguageType.English, Gender.MasculineOrNeutral, englishTalkTable, englishTalkTable) |> Array.ofList) |> 
        Array.map(fun t -> System.Console.WriteLine(t)) |>
        ignore
