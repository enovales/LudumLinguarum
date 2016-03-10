module DialogueTests

open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFF
open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.NWN1Context
open InfinityAuroraEnginePlugins.SerializedGFF
open InfinityAuroraEnginePlugins.TalkTable
open NUnit.Framework
open System.IO

[<TestFixture>]
type DialogueTests() = 
    let rootPathGerman = @"C:\GOG Games\Neverwinter Nights Diamond Edition (German)"
    let rootPathEnglish = @"C:\GOG Games\Neverwinter Nights Diamond Edition"
    let contextGerman = new NWN1Context(rootPathGerman)
    let contextEnglish = new NWN1Context(rootPathEnglish)

    let englishTalkTable = TalkTableV3.FromFilePath(@"C:\GOG Games\Neverwinter Nights Diamond Edition\dialog.tlk")
    let germanTalkTable = TalkTableV3.FromFilePath(@"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\dialog.tlk")
    let germanTalkTableF = TalkTableV3.FromFilePath(@"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\dialogF.tlk")

    [<Test; Explicit>]
    member this.CheckLoadedGFF(): Unit = 
        let gff = LoadGFF({ ResRef.Value = "nw_hen_bod"}, ResType.Dlg, contextGerman.Resources)
        Assert.IsTrue(gff.Members.GetDword(SerializedDialogue.DelayEntryName).IsSome, "didn't have DelayEntry")
        Assert.IsTrue(gff.Members.GetDword(SerializedDialogue.DelayReplyName).IsSome, "didn't have DelayReply")
        Assert.IsTrue(gff.Members.GetResRef(SerializedDialogue.EndConverAbortName).IsSome, "didn't have EndConverAbort")
        Assert.IsTrue(gff.Members.GetResRef(SerializedDialogue.EndConversationName).IsSome, "didn't have EndConversation")
        Assert.IsTrue(gff.Members.GetList(SerializedDialogue.EntryListName).IsSome, "didn't have EntryList")
        Assert.IsTrue(gff.Members.GetDword(SerializedDialogue.NumWordsName).IsSome, "didn't have NumWords")
        //Assert.IsTrue(gff.Members.GetByte(SerializedDialogue.PreventZoomInName).IsSome, "didn't have PreventZoomIn")
        Assert.IsTrue(gff.Members.GetList(SerializedDialogue.ReplyListName).IsSome, "didn't have ReplyList")
        Assert.IsTrue(gff.Members.GetList(SerializedDialogue.StartingListName).IsSome, "didn't have StartingList")
        ()

    [<Test; Explicit>]
    member this.TestLoadSerializedDialogue(): Unit = 
        let gff = LoadGFF({ ResRef.Value = "nw_hen_bod"}, ResType.Dlg, contextGerman.Resources)
        let serializedDialogue = SerializedDialogue.FromGFF(gff)
        Assert.IsTrue(serializedDialogue.EntryList.IsSome, "didn't have entry list")
        Assert.IsTrue(serializedDialogue.StartingList.IsSome, "didn't have starting list")
        Assert.IsTrue(serializedDialogue.ReplyList.IsSome, "didn't have reply list")
        ()

    [<Test; Explicit>]
    member this.TestLoadDialogue(): Unit = 
        let gff = LoadGFF({ ResRef.Value = "nw_hen_bod"}, ResType.Dlg, contextGerman.Resources)
        let serializedDialogue = SerializedDialogue.FromGFF(gff)
        let dialogue = Dialogue.FromSerialized(serializedDialogue)

        Assert.IsNotEmpty(dialogue.EntryList)
        Assert.IsNotEmpty(dialogue.ReplyList)
        Assert.IsNotEmpty(dialogue.StartingList)

    [<Test; Explicit>]
    member this.TestWalkDialogueTree(): Unit = 
        let dialogue = LoadDialogue({ ResRef.Value = "nw_hen_bod"}, contextGerman.Resources)
        let strings = dialogue.StartingList |> Array.mapi (fun i t -> GatherStrings(t) |> List.toArray) |> Array.concat
        strings |> Array.map (fun (t, k) -> 
            System.Console.WriteLine(EvaluateString(t, englishTalkTable, englishTalkTable, LanguageType.English, Gender.MasculineOrNeutral))
            System.Console.WriteLine(EvaluateString(t, germanTalkTable, germanTalkTableF, LanguageType.German, Gender.MasculineOrNeutral))) |> ignore
        ()

    [<Test; Explicit>]
    member this.TestWalkAllDialogueTrees(): Unit = 
        let dialogueResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Dlg)
        let dialogues = dialogueResources |> Array.map (fun t -> LoadDialogue(t.Name, contextEnglish.Resources))

        System.Console.WriteLine("loaded " + dialogues.Length.ToString() + " dialogues")

        dialogues |> 
            Array.collect(fun t -> ExtractStringsFromDialogue(t, LanguageType.English, Gender.MasculineOrNeutral, englishTalkTable, englishTalkTable) |> Array.ofList) |> 
            Array.map(fun t -> System.Console.WriteLine(t)) |>
            ignore
        ()