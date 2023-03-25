module SupergiantGamesTests

open Expecto
open SupergiantGames

[<Tests>]
let supergiantGamesTests =
    testList "Supergiant Games" [
        testCase "single-line Lua comments are properly removed" <|
            fun () ->
                let input = "abc -- def"
                let expected = "abc  "
                let result = Lua.stripComments input
                Expect.equal result expected "single line comment was not removed correctly"

        testCase "multi-line Lua comments are properly removed" <|
            fun () ->
                let input = "abc --[[\nfoo\n]]"
                let expected = "abc  "
                let result = Lua.stripComments input
                Expect.equal result expected "multi-line comment was not removed correctly"

        testCase "cue regex extraction works as expected" <|
            fun () ->
                let input = """Foo, Cue = "Blah", Meep = "Moop", Text = "Boo" """
                let result = Lua.hadesCueRegex.Match(input)
                Expect.equal result.Success true "regex should match against input"
                Expect.equal result.Groups.[1].Value "Blah" "cue should be extracted successfully"

        testCase "text regex extraction works as expected" <|
            fun () ->
                let input = """Foo, Cue = "Blah", Meep = "Moop", Text = "Boo" """
                let result = Lua.hadesTextRegex.Match(input)
                Expect.equal result.Success true "regex should match against input"
                Expect.equal result.Groups.[1].Value "Boo" "text should be extracted successfully"

        testCase "cue and text regex extraction works as expected" <|
            fun () ->
                let input = """{ blah = { foo = { Cue = "cue1", Text = "text1" }, }, }"""
                let result = Lua.hadesCueAndTextRegex.Match(input)
                Expect.equal result.Success true "regex should match against input"
                Expect.equal result.Groups.[1].Value "cue1" "cue should be extracted correctly"
                Expect.equal result.Groups.[2].Value "text1" "text should be extracted correctly"

        testCase "cue and text regex extraction works with other fields in the braces" <|
            fun () ->
                let input = """{ blah = { foo = { Cue = "cue1", Baz = Boo, Text = "text1" }, }, }"""
                let result = Lua.hadesCueAndTextRegex.Match(input)
                Expect.equal result.Success true "regex should match against input"
                Expect.equal result.Groups.[1].Value "cue1" "cue should be extracted correctly"
                Expect.equal result.Groups.[2].Value "text1" "text should be extracted correctly"

        testCase "cue and text regex extraction does not match if there is only a cue value" <|
            fun () ->
                let input = """{ blah = { foo = { Cue = "cue1" }, }, }"""
                let result = Lua.hadesCueAndTextRegex.Match(input)
                Expect.equal result.Success false "regex should not match against input"
    ]
