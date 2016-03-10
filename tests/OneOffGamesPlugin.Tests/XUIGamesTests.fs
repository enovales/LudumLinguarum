module XUIGamesTests

open NUnit.Framework
open System.Globalization

[<TestFixture>]
type XUIGamesTests() = 
    [<Test>]
    member this.``The languageForXUITag function returns the tag for all language tags except 'jp'``() = 
        Assert.AreEqual("en", XUIGames.languageForXUITag("en"))
        Assert.AreEqual("es", XUIGames.languageForXUITag("es"))
        Assert.AreEqual("de", XUIGames.languageForXUITag("de"))
        Assert.AreEqual("fr", XUIGames.languageForXUITag("fr"))
        Assert.AreEqual("it", XUIGames.languageForXUITag("it"))
        ()

    [<Test>]
    member this.``The languageForXUITag function returns 'ja' for 'jp'``() = 
        Assert.AreEqual("ja", XUIGames.languageForXUITag("jp"))

    [<Test>]
    member this.``The languageTagsForHeaderRow function skips the first two entries in the row``() = 
        Assert.AreEqual([| "foo"; "bar" |], XUIGames.languageTagsForHeaderRow([| "1"; "2"; "foo"; "bar" |]))

    [<Test>]
    member this.``The languageTagsForHeaderRow function skips blank entries``() = 
        Assert.AreEqual([| "foo" |], XUIGames.languageTagsForHeaderRow([| "1"; "2"; ""; " "; "foo" |]))

    [<Test>]
    member this.``The generateCardsForRow function creates the appropriate number of card entries``() = 
        let languageTags = [| "en"; "de" |]
        let text = [| "key"; "category"; "enstring"; "destring" |]
        let result = XUIGames.generateCardsForRow(0, languageTags)(0)(text)
        Assert.AreEqual(2, result.Length)

    [<Test>]
    member this.``The generateCardsForRow function creates a card with a key equal to the row index, key, and category concatenated``() = 
        let languageTags = [| "en" |]
        let text = [| "key"; "category"; "enstring" |]
        let result = XUIGames.generateCardsForRow(0, languageTags)(0)(text)
        Assert.AreEqual("0keycategory", result.[0].Key)

    [<Test>]
    member this.``The extractXUITabDelimited function creates a set of cards for all data lines``() = 
        let lines =
            [|
                "key\tcategory\ten\tjp"
                "a\t1\tenstring1\tjpstring1"
                "b\t2\tenstring2\tjpstring2"
            |]

        let result = XUIGames.extractXUITabDelimited(lines, 0)
        Assert.AreEqual(4, result.Length)

        // check that there are 2 of each language
        result 
        |> Array.groupBy(fun c -> c.LanguageTag) 
        |> Array.iter(fun (_, cardsPerLanguage) -> Assert.AreEqual(2, cardsPerLanguage.Length))