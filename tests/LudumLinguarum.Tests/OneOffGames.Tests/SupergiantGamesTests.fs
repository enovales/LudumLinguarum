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

        testCase "regex to remove formatting braces from card text works" <|
            fun () ->
                let input = """{#SomeDirective}{!AnotherDirective}foo{!AThirdDirective}"""
                let result = Hades.formattingBraceRemovalRegex.Replace(input, "")
                let expected = "foo"
                Expect.equal result expected "formatting removal regex did not work as intended"

        testCase "regex to remove escaped whitespace from card text works" <|
            fun () ->
                let input = """foo\rbar\nbaz\tboo"""
                let result = Hades.formattingEscapedWhitespaceRemovalRegex.Replace(input, " ")
                let expected = "foo bar baz boo"
                Expect.equal result expected "formatting removal regex did not work as intended"

        testCase "regex to remove at-directives from card text works" <|
            fun () ->
                let input = """@Directive\u1234Blah foo"""
                let result = Hades.formattingAtDirectiveRemovalRegex.Replace(input, "")
                let expected = "foo"
                Expect.equal result expected "formatting removal regex did not work as intended"
    ]
