module MadballsBaboInvasionTests

open Expecto
open MadballsBaboInvasion

[<Tests>]
let tests = 
  testList "Madballs: Babo Invasion tests" [
    testCase "Calling stripFormattingTags doesn't affect strings without formatting tags" <|
      fun () ->
        let input = "This is a test string with no formatting tags."
        Expect.equal input (stripFormattingTags input) ""

    testCase "Calling stripFormattingTags removes tags of the format ^###" <|
      fun () ->
        let input1 = "Here's a caret tag: ^123 Isn't it great?"
        let expected = "Here's a caret tag:  Isn't it great?"
        let input2 = "Here's a caret tag: ^123456789 Isn't it great?"
        let input3 = "Here's a caret tag: ^1 Isn't it great?"
        Expect.equal expected (stripFormattingTags input1) ""
        Expect.equal expected (stripFormattingTags input2) ""
        Expect.equal expected (stripFormattingTags input3) ""

    testCase "Calling stripFormattingTags removes substitutions of the format {#}" <|
      fun () ->
        let input = "Here's a substitution tag: {0} Isn't it great?"
        let expected = "Here's a substitution tag:  Isn't it great?"
        Expect.equal expected (stripFormattingTags input) ""

    testCase "Brace removal is non-greedy" <|
      fun () ->
        let input = "Here's one brace {12345} and then another {67890}"
        let expected = "Here's one brace  and then another "
        Expect.equal expected (stripFormattingTags input) ""

    testCase "Calling stripFormattingTags removes all tags and substitutions" <|
      fun () ->
        let input = "Here's a string with both ^456 and {9} Isn't it great?"
        let expected = "Here's a string with both  and  Isn't it great?"
        Expect.equal expected (stripFormattingTags input) ""

    testCase "Calling stripFormattingTags removes contents inside brackets" <|
      fun () ->
        let input = "Here's stuff in brackets [abc/def/ghi ^123] Isn't it great?"
        let expected = "Here's stuff in brackets  Isn't it great?"
        Expect.equal expected (stripFormattingTags input) ""

    testCase "Bracket removal is non-greedy" <|
      fun () ->
        let input = "Here's one bracket [12345] and then another [67890]"
        let expected = "Here's one bracket  and then another "
        Expect.equal expected (stripFormattingTags input) ""
  ]
