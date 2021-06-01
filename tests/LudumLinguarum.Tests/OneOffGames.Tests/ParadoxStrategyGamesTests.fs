module ParadoxStrategyGamesTests

open Expecto
open ParadoxStrategyGames

[<Tests>]
let tests = 
  testList "Paradox strategy games tests" [
    testCase "Regex for stripping numeric key annotations for EU4 YAMLs doesn't modify strings without annotations" <|
      fun () ->
        let s = "  key: value"
        let expected = s
        Expect.equal expected (eu4StripNumericKeyAnnotations s) ""

    testCase "Regex for stripping numeric key annotations for EU4 YAMLs removes single-digit annotations" <|
      fun () ->
        let s = "  key:1 value"
        let expected = "  key: value"
        Expect.equal expected (eu4StripNumericKeyAnnotations s) ""

    testCase "Regex for stripping numeric key annotations for EU4 YAMLs removes multiple-digit annotations" <|
      fun () ->
        let s = "  key:123 value"
        let expected = "  key: value"
        Expect.equal expected (eu4StripNumericKeyAnnotations s) ""

    testCase "Escaping quoted values for EU4 YAMLs doesn't modify strings without quoted values" <|
      fun () ->
        let s = "  key: value"
        let expected = s
        Expect.equal expected (eu4EscapeQuotedValues s) ""

    testCase "Escaping quoted values for EU4 YAMLs doesn't modify strings with quoted values without interior quotes" <|
      fun () ->
        let s = "  key: \"value\""
        let expected = s
        Expect.equal expected (eu4EscapeQuotedValues s) ""

    testCase "Escaping quoted values for EU4 YAMLs converts embedded double quotes to single quotes" <|
      fun () ->
        let s = "  key: \"here are some \"embedded\" quotes\""
        let expected = "  key: \"here are some 'embedded' quotes\""
        Expect.equal expected (eu4EscapeQuotedValues s) ""

    testCase "Escaping double sets of double quotes for EU4 YAMLs only converts the inner set of double quotes" <|
      fun () ->
        let s = "  key: \"\"friends\"\""
        let expected = "  key: \"'friends'\""
        Expect.equal expected (eu4EscapeQuotedValues s) ""

    testCase "Escaping quoted strings for EU4 YAMLs trims the interior" <|
      fun () ->
        let s = "  key: \" leading space\""
        let expected = "  key: \"leading space\""
        Expect.equal expected (eu4EscapeQuotedValues s) ""
  ]
