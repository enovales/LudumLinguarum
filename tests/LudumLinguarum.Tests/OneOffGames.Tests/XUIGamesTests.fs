module XUIGamesTests

open Expecto
open System.Globalization

[<Tests>]
let tests = 
  testList "XUI games tests" [
    testCase "The languageForXUITag function returns the tag for all language tags except 'jp'" <|
      fun () ->
        Expect.equal "en" (XUIGames.languageForXUITag "en") ""
        Expect.equal "es" (XUIGames.languageForXUITag "es") ""
        Expect.equal "de" (XUIGames.languageForXUITag "de") ""
        Expect.equal "fr" (XUIGames.languageForXUITag "fr") ""
        Expect.equal "it" (XUIGames.languageForXUITag "it") ""

    testCase "The languageForXUITag function returns 'ja' for 'jp'" <|
      fun () -> Expect.equal "ja" (XUIGames.languageForXUITag "jp") ""

    testCase "The languageTagsForHeaderRow function skips the first two entries in the row" <|
      fun () -> Expect.equal [| "foo"; "bar" |] (XUIGames.languageTagsForHeaderRow [| "1"; "2"; "foo"; "bar" |]) ""

    testCase "The languageTagsForHeaderRow function skips blank entries" <|
      fun () -> Expect.equal [| "foo" |] (XUIGames.languageTagsForHeaderRow [| "1"; "2"; ""; " "; "foo" |]) ""

    testCase "The generateCardsForRow function creates the appropriate number of card entries" <|
      fun () -> 
        let languageTags = [| "en"; "de" |]
        let text = [| "key"; "category"; "enstring"; "destring" |]
        let result = XUIGames.generateCardsForRow(0, languageTags)(0)(text)
        Expect.equal 2 result.Length ""

    testCase "The generateCardsForRow function creates a card with a key equal to the row index, key, and category concatenated" <|
      fun () ->
        let languageTags = [| "en" |]
        let text = [| "key"; "category"; "enstring" |]
        let result = XUIGames.generateCardsForRow(0, languageTags)(0)(text)
        Expect.equal "0keycategory" result.[0].Key ""

    testCase "The extractXUITabDelimited function creates a set of cards for all data lines" <|
      fun () ->
        let lines =
            [|
                "key\tcategory\ten\tjp"
                "a\t1\tenstring1\tjpstring1"
                "b\t2\tenstring2\tjpstring2"
            |]

        let result = XUIGames.extractXUITabDelimited(lines, 0)
        Expect.equal 4 result.Length ""

        // check that there are 2 of each language
        result 
        |> Array.groupBy(fun c -> c.LanguageTag) 
        |> Array.iter(fun (_, cardsPerLanguage) -> Expect.equal 2 cardsPerLanguage.Length "")
  ]
