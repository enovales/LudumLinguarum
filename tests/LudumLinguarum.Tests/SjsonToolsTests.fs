module SjsonToolsTests

open System
open System.Text

open Expecto
open SjsonTools

[<Tests>]
let tests =
    testList "SjsonTools tests" [
        testCase "Parse a simple SJSON file into equivalent JSON" <|
            fun () ->
                let input = "test = \"foo\""
                let expected = "{\n\"test\": \"foo\"\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"

        testCase "Parse a more complex SJSON file into equivalent JSON" <|
            fun () ->
                let input = "test_struct = { foo = \"bar\" }"
                let expected = "{\n\"test_struct\": {\n\"foo\": \"bar\"\n}\n\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"
    ]
