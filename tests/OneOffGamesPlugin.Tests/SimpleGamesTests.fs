module SimpleGamesTests

open Expecto
open SimpleGames
open System
open System.IO
open System.Text

[<Tests>]
let magicalDropVTests = 
  let testXml = """<?xml version="1.0" encoding="utf-8"?>
<golgoth>
  <content>
    <ID_UnitTest1>Testing</ID_UnitTest1>
    <ID_UnitTest2>Unescaped &</ID_UnitTest2>
    <ID_UnitTest3><string placeholder></ID_UnitTest3>
  </content>
</golgoth>"""

  testList "Magical Drop V tests" [
    testCase "XmlAmpersandsEscapedBySanitization" <|
      fun () ->
        let source = "<fakecontent>Foo & Bar</fakecontent>"
        let expected = "<fakecontent>Foo &amp; Bar</fakecontent>"
        Expect.equal expected (SimpleGames.sanitizeMagicalDropVXml source) ""

    testCase "XmlStringPlaceholdersRemovedBySanitization" <|
      fun () ->
        let source = "<fakecontent><string placeholder></fakecontent>"
        let expected = "<fakecontent></fakecontent>"
        Expect.equal expected (SimpleGames.sanitizeMagicalDropVXml source) ""

    testCase "WellFormedXmlUnchangedBySanitization" <|
      fun () ->
        let source = "<fakecontent>Foo and Bar</fakecontent>"
        Expect.equal source (SimpleGames.sanitizeMagicalDropVXml source) ""

    testCase "GeneratedStringMapFromXml" <|
      fun () ->
        let expected = 
            [| 
                ("ID_UnitTest1", "Testing")
                ("ID_UnitTest2", "Unescaped &")
                ("ID_UnitTest3", "")
            |]
            |> Map.ofArray

        Expect.equal expected (SimpleGames.generateMagicalDropVStringMap(SimpleGames.sanitizeMagicalDropVXml(testXml))) ""
  ]

[<Tests>]
let hatofulBoyfriendTests = 
  testList "Hatoful Boyfriend tests" [
    testCase "Non-format tokens are ignored by stripHbFormattingTokens" <|
      fun () -> Expect.equal "Foo bar" (stripHbFormattingTokens "Foo bar") ""

    testCase "Format tokens are stripped from the string by stripHbFormattingTokens" <|
      fun () -> Expect.equal "Foo bar" (stripHbFormattingTokens "Foo [p]bar") ""

    testCase "Newline tokens are replaced with a space by stripHbFormattingTokens" <|
      fun () -> Expect.equal "Foo bar" (stripHbFormattingTokens @"Foo\nbar") ""
  ]
