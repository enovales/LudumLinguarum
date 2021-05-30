module WormsArmageddonTests

open Expecto
open WormsGames.Armageddon

let private emptyState = 
  {
      StringExtractState.AccumulatingKey = None
      Accumulated = []
      Complete = []
  }

[<Tests>]
let tests =     
  testList "Worms Armageddon tests" [
    testCase "Calling getAccumulatedString if there is no accumulating key returns an empty list" <|
      fun () -> Expect.equal [] (getAccumulatedString emptyState) ""

    testCase "Calling getAccumulatedString returns a tuple of the accumulating key and the reversed accumulated strings" <|
      fun () -> 
        let populated = 
            {
                StringExtractState.AccumulatingKey = Some("key")
                Accumulated = [ "baz"; "bar"; "foo" ]
                Complete = []
            }            
        let expected = [("key", "foo bar baz")]
        Expect.equal expected (getAccumulatedString populated) ""

    testCase "Calling foldStringLines reads one single-line string mapping" <|
      fun () -> 
        let lines = 
            [|
                "KEY \"Contained string\""
            |]

        let expected = 
            {
                StringExtractState.AccumulatingKey = None
                Accumulated = []
                Complete = [("KEY", "Contained string")]
            }

        Expect.equal expected (lines |> Array.fold foldStringLines emptyState) ""

    testCase "Calling foldStringLines reads two single-line string mappings" <|
      fun () -> 
        let lines = 
            [|
                "KEY \"Contained string\""
                "KEY2 \"Contained string 2\""
            |]

        let expected = 
            {
                StringExtractState.AccumulatingKey = None
                Accumulated = []
                Complete = [("KEY2", "Contained string 2"); ("KEY", "Contained string")]
            }

        Expect.equal expected (lines |> Array.fold foldStringLines emptyState) ""

    testCase "Calling foldStringLines reads one partial mapping" <|
      fun () ->
        let lines = 
            [|
                "KEY"
                " \"Contained string\""
            |]

        let expected = 
            {
                StringExtractState.AccumulatingKey = Some("KEY")
                Accumulated = ["Contained string"]
                Complete = []
            }

        Expect.equal expected (lines |> Array.fold foldStringLines emptyState) ""

    testCase "Calling foldStringLines with one partial mapping containing two strings" <|
      fun () ->
        let lines = 
            [|
                "KEY"
                " \"Contained string\""
                " \"Contained string 2\""
            |]

        let expected = 
            {
                StringExtractState.AccumulatingKey = Some("KEY")
                Accumulated = ["Contained string 2"; "Contained string"]
                Complete = []
            }

        Expect.equal expected (lines |> Array.fold foldStringLines emptyState) ""

    testCase "A partial mapping followed by a single-line string mapping are both recognized" <|
      fun () ->
        let lines = 
            [|
                "KEY"
                " \"Contained string\""
                " \"Contained string 2\""
                "SIMPLE1 \"Simple string\""
            |]

        let expected = 
            {
                StringExtractState.AccumulatingKey = None
                Accumulated = []
                Complete = [("SIMPLE1", "Simple string"); ("KEY", "Contained string Contained string 2")]
            }

        Expect.equal expected (lines |> Array.fold foldStringLines emptyState) ""
  ]
