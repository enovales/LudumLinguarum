module WormsArmageddonTests

open NUnit.Framework
open WormsArmageddon

type WormsArmageddonTests() = 
    let emptyState = 
        {
            StringExtractState.AccumulatingKey = None
            Accumulated = []
            Complete = []
        }
    [<Test>]
    member this.``Calling getAccumulatedString if there is no accumulating key returns an empty list``() = 
        Assert.AreEqual([], WormsArmageddon.getAccumulatedString(emptyState))

    [<Test>]
    member this.``Calling getAccumulatedString returns a tuple of the accumulating key and the reversed accumulated strings``() = 
        let populated = 
            {
                StringExtractState.AccumulatingKey = Some("key")
                Accumulated = [ "baz"; "bar"; "foo" ]
                Complete = []
            }            
        let expected = [("key", "foo bar baz")]
        Assert.AreEqual(expected, WormsArmageddon.getAccumulatedString(populated))

    [<Test>]
    member this.``Calling foldStringLines reads one single-line string mapping``() = 
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

        Assert.AreEqual(expected, lines |> Array.fold foldStringLines emptyState)

    [<Test>]
    member this.``Calling foldStringLines reads two single-line string mappings``() =
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

        Assert.AreEqual(expected, lines |> Array.fold foldStringLines emptyState)

    [<Test>]
    member this.``Calling foldStringLines reads one partial mapping``() = 
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

        Assert.AreEqual(expected, lines |> Array.fold foldStringLines emptyState)

    [<Test>]
    member this.``Calling foldStringLines with one partial mapping containing two strings``() = 
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

        Assert.AreEqual(expected, lines |> Array.fold foldStringLines emptyState)

    [<Test>]
    member this.``A partial mapping followed by a single-line string mapping are both recognized``() = 
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

        Assert.AreEqual(expected, lines |> Array.fold foldStringLines emptyState)
