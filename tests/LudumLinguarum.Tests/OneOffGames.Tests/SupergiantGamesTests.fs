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
            
    ]
