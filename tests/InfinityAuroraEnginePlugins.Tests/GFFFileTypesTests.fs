module GFFFileTypesTests

open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.SerializedGFF
open NUnit.Framework

[<TestFixture>]
type GatherStringsTests() = 
    let testStrings = 
        [|
            for i in 0..9 do 
            yield { 
                GFFRawCExoLocString.sizeOfOtherData = uint32 0; 
                stringRef = uint32 i; 
                stringCount = uint32 0; 
                substrings = [] 
            }
        |]

    let testNodes = 
        [|
            for i in 0..9 do 
            yield SyncStructDialogueNode.Node {
                DialogueStruct.Animation = None
                AnimLoop = None
                Comment = None
                Delay = None
                Quest = None
                QuestEntry = None
                Script = None
                Sound = None
                Text = Some(testStrings.[i])
                Next = []
                Speaker = None
            }
        |]

    let syncStructs = 
        [|
            for i in 0..9 do
            yield {
                AugmentedSyncStruct.Active = None
                DialogueNode = Some(testNodes.[i])
                IsLink = false
                LinkComment = None
            }
        |]

    let connectSyncStructs(a: AugmentedSyncStruct, b: AugmentedSyncStruct) = 
        let aNewDialogueNode = 
            match (a.DialogueNode.Value, b.DialogueNode.Value) with
            | (Node(na), Node(nb)) -> 
                {
                    na with Next = b :: na.Next
                }
            | _ -> failwith "invalid pairing"

        {
            a with DialogueNode = Some(SyncStructDialogueNode.Node(aNewDialogueNode))
        }

    [<Test>]
    member this.TestInvalidDialogue() =
        let d = 
            {
                AugmentedSyncStruct.Active = None
                DialogueNode = None
                IsLink = false
                LinkComment = None
            }
        Assert.IsEmpty(GatherStrings([], d, 0, 0))

    [<Test>]
    member this.TestSingleLineDialogue() = 
        let expected = 
            [
                (testStrings.[0], dialogueNodeKey(0, 0))
            ]
        Assert.AreEqual(expected, GatherStrings([], syncStructs.[0], 0, 0))

    [<Test>]
    member this.TestTwoLineDialogue() = 
        let expected = 
            [
                (testStrings.[0], dialogueNodeKey(0, 0));
                (testStrings.[1], dialogueNodeKey(1, 0))
            ]
        Assert.AreEqual(expected, GatherStrings([], connectSyncStructs(syncStructs.[0], syncStructs.[1]), 0, 0))

    [<Test>]
    member this.TestMultipleLineDialogue() = 
        let expected = 
            [
                (testStrings.[0], dialogueNodeKey(0, 0))
                (testStrings.[1], dialogueNodeKey(1, 0))
                (testStrings.[2], dialogueNodeKey(2, 0))
            ]

        let connected = connectSyncStructs(syncStructs.[0], connectSyncStructs(syncStructs.[1], syncStructs.[2]))
        Assert.AreEqual(expected, GatherStrings([], connected, 0, 0))

    [<Test>]
    member this.TestMultipleAnswerDialogue() = 
        let expected = 
            [
                (testStrings.[0], dialogueNodeKey(0, 0))
                (testStrings.[1], dialogueNodeKey(1, 0))
                (testStrings.[2], dialogueNodeKey(1, 1))
            ]

        // connect answers in reverse order, because of the prepending
        let connected = connectSyncStructs(syncStructs.[0], syncStructs.[2])
        let bothConnected = connectSyncStructs(connected, syncStructs.[1])
        Assert.AreEqual(expected, GatherStrings([], bothConnected, 0, 0))

    [<Test>]
    member this.TestLinkedDialogue() = 
        let expected = 
            [
                (testStrings.[0], dialogueNodeKey(0, 0))
            ]

        // connect first sync struct to a link.
        let link = 
            {
                syncStructs.[1] with IsLink = true
            }
        let connected = connectSyncStructs(syncStructs.[0], link)
        Assert.AreEqual(expected, GatherStrings([], connected, 0, 0))

    [<Test>]
    member this.TestBranchingDialogue() = 
        ()