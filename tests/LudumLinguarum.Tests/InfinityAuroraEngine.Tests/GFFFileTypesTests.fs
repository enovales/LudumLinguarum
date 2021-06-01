module GFFFileTypesTests

open InfinityAuroraEngine.GFFFileTypes
open InfinityAuroraEngine.SerializedGFF
open Expecto

let private testStrings = 
    [|
        for i in 0..9 do 
        yield { 
            GFFRawCExoLocString.sizeOfOtherData = uint32 0; 
            stringRef = uint32 i; 
            stringCount = uint32 0; 
            substrings = [] 
        }
    |]

let private testNodes = 
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

let private syncStructs = 
    [|
        for i in 0..9 do
        yield {
            AugmentedSyncStruct.Active = None
            DialogueNode = Some(testNodes.[i])
            IsLink = false
            LinkComment = None
        }
    |]

let private connectSyncStructs(a: AugmentedSyncStruct, b: AugmentedSyncStruct) = 
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

[<Tests>]
let tests = 
  testList "GFFFileTypes" [
      testCase "GatherStrings() on invalid dialogue returns nothing" <|
        fun () -> 
          let d = 
              {
                  AugmentedSyncStruct.Active = None
                  DialogueNode = None
                  IsLink = false
                  LinkComment = None
              }
          Expect.isEmpty (GatherStrings d) "invalid dialogue should return empty seq"
      
      testCase "GatherStrings() on a single-line dialogue" <|
        fun () ->
          let expected = 
              [
                  (testStrings.[0], "0")
              ]
              |> Set.ofList
          Expect.equal expected (GatherStrings(syncStructs.[0]) |> Set.ofList) "expecting a single entry"

      testCase "GatherStrings() on a two-line dialogue" <|
        fun () ->
          let expected = 
              [
                  (testStrings.[0], "0");
                  (testStrings.[1], "1")
              ]
              |> Set.ofList
          Expect.equal expected ((GatherStrings (connectSyncStructs(syncStructs.[0], syncStructs.[1]))) |> Set.ofList) "expecting two entries"

      testCase "GatherStrings() on a multiple line dialogue" <|
        fun () -> 
          let expected = 
              [
                  (testStrings.[0], "0")
                  (testStrings.[1], "1")
                  (testStrings.[2], "2")
              ]
              |> Set.ofList

          let connected = connectSyncStructs(syncStructs.[0], connectSyncStructs(syncStructs.[1], syncStructs.[2]))
          Expect.equal expected (GatherStrings(connected) |> Set.ofList) "expecting three entries"

      testCase "GatherStrings() on a dialogue with multiple answers on a node" <|
        fun () ->
          let expected = 
              [
                  (testStrings.[0], "0")
                  (testStrings.[1], "1")
                  (testStrings.[2], "2")
              ]
              |> Set.ofList

          // connect answers in reverse order, because of the prepending
          let connected = connectSyncStructs(syncStructs.[0], syncStructs.[2])
          let bothConnected = connectSyncStructs(connected, syncStructs.[1])
          Expect.equal expected (GatherStrings(bothConnected) |> Set.ofList) "expecting multiple answers"

      testCase "GatherStrings() doesn't follow links in the dialogue" <|
        fun () ->
          let expected = 
              [
                  (testStrings.[0], "0")
              ]
              |> Set.ofList

          // connect first sync struct to a link.
          let link = 
              {
                  syncStructs.[1] with IsLink = true
              }
          let connected = connectSyncStructs(syncStructs.[0], link)
          Expect.equal expected (GatherStrings(connected) |> Set.ofList) "expecting a single element response"
  ]
