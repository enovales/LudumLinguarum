module SjsonToolsTests

open Expecto
open FParsec.CharParsers
open SjsonTools
open System.IO

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
                let expected = "\"test\""
                match FParsec.CharParsers.run SjsonGrammar.quotedStringLiteral input with
                | FParsec.CharParsers.ParserResult.Success (result, _, _) ->
                    Expect.equal result expected "parsed string had unexpected value"
                | _ -> failwith "couldn't parse quoted string"

        testCase "quoted string literal parser works with triple quotes" <|
            fun () ->
                let input = "\"\"\"test\"\"\""
                let expected = "test"
                let result = FParsec.CharParsers.run SjsonGrammar.quotedStringLiteral input
                match result with
                | FParsec.CharParsers.ParserResult.Success (result, _, _) ->
                    Expect.equal result expected "parsed string had unexpected value"
                | _ -> failwith "couldn't parse quoted string"

        testCase "triple quoted strings end at the last 3 quotes" <|
            fun () ->
                let input = "\"\"\"test\"\"\"\" "
                let expected = "test\""
                let result = FParsec.CharParsers.run SjsonGrammar.tripleQuotedStringLiteral input
                match result with
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

        testCase "Parse a SJSON file with trailing commas in an array into equivalent JSON" <|
            fun () ->
                let input = "test_array = [1, 2, 3, 4, 5,]"
                let expected = "{\n\"test_array\": [\n1,\n2,\n3,\n4,\n5\n]\n\n}\n".ReplaceLineEndings()
                let result = sjsonToJSON input
                Expect.equal result expected "unexpected parse result"

        testCase "Replace comments with a single space" <|
            fun () ->
                let input = "foo = /* c-style comment */ 123\nbar = 456 // line comment\nbaz = 789".ReplaceLineEndings()

                let expected = "foo =   123\nbar = 456  \nbaz = 789".ReplaceLineEndings()
                let result = (stripComments input).ReplaceLineEndings()
                Expect.equal result expected "unexpected parse result"

        testCase "comment stripping works when a multiline comment contains an asterisk" <|
            fun () ->
                let input = """
/* * */
foo = "bar"
"""
                let expected = "\n \nfoo = \"bar\"\n".ReplaceLineEndings()
                let result = (stripComments input).ReplaceLineEndings()
                Expect.equal result expected "unexpected multi-line comment stripping behavior"

        testCase "properly handle escaping quotes in strings when writing SJSON as JSON, when the quotes are at the start of the string" <|
            fun () ->
                let input = SjsonString "\""
                let expected = "\"\\\"\""
                let writer = new StringWriter()
                SjsonGrammar.printJson(input) |> ignore
                let result = writer.ToString()
                Expect.equal result expected "string did not have quotes escaped correctly"
                ()

        testCase "properly handle escaping quotes in strings when writing SJSON as JSON, when the quotes are somewhere in the middle of the string" <|
            fun () ->
                let input = SjsonString "abc\""
                let expected = "\"abc\\\"\""
                let writer = new StringWriter()
                SjsonGrammar.printJson(input) |> ignore
                let result = writer.ToString()
                Expect.equal result expected "string did not have quotes escaped correctly"
                ()
    ]
