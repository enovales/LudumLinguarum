module SjsonToolsTests

open Expecto
open FParsec.CharParsers
open SjsonTools

[<Tests>]
let tests =
    testList "SjsonTools tests" [
        testCase "quoted string literal parser works with no escaped quotes" <|
            fun () ->
                let input = "\"test\""
                let expected = "test"
                match run SjsonGrammar.quotedStringLiteral input with
                | ParserResult.Success (result, _, _) ->
                    Expect.equal result expected "parsed string had unexpected value"
                | _ -> failwith "couldn't parse quoted string"

        testCase "quoted string literal parser works with escaped quotes" <|
            fun () ->
                let input = "\"\\\"test\\\"\""
                let expected = "\\\"test\\\""
                match FParsec.CharParsers.run SjsonGrammar.quotedStringLiteral input with
                | FParsec.CharParsers.ParserResult.Success (result, _, _) ->
                    Expect.equal result expected "parsed string had unexpected value"
                | _ -> failwith "couldn't parse quoted string"

        testCase "Parse a simple SJSON file into equivalent JSON" <|
            fun () ->
                let input = "test = \"foo\""
                let expected = "{\n\"test\": \"foo\"\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"

        testCase "Parse escaped quotes" <|
            fun () ->
                let input = "test = \"\\\"foo\\\"\""
                let expected = "{\n\"test\": \"\\\"foo\\\"\"\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"

        testCase "Parse a SJSON file with a struct into equivalent JSON" <|
            fun () ->
                let input = "test_struct = { foo = \"bar\" }"
                let expected = "{\n\"test_struct\": {\n\"foo\": \"bar\"\n}\n\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"

        testCase "Parse a SJSON file with an array into equivalent JSON" <|
            fun () ->
                let input = "test_array = [1 2 3 4 5]"
                let expected = "{\n\"test_array\": [\n1,\n2,\n3,\n4,\n5\n]\n\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"

        testCase "Replace comments with a single space" <|
            fun () ->
                let input = "foo = /* c-style comment */ 123\nbar = 456 // line comment\nbaz = 789".ReplaceLineEndings()

                let expected = "foo =   123\nbar = 456  \nbaz = 789".ReplaceLineEndings()
                let result = (stripComments input).ReplaceLineEndings()
                Expect.equal result expected "unexpected parse result"
    ]
