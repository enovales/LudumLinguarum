module PuzzleQuestGamesTests

open LLDatabase
open NUnit.Framework
open System.Xml.Linq

[<TestFixture>]
type PuzzleQuestGamesTests() = 
    [<Test>]
    member this.``Calling generateKVForTextElement returns a pair of the 'tag' element, and the value``() = 
        let contents: obj array = [| new XAttribute(XName.Get("tag"), "[GAME_TAG]"); "content" |]
        let el = new XElement(XName.Get("Text"), contents)
        let expected = ("[GAME_TAG]", "content")
        Assert.AreEqual(expected, PuzzleQuestGames.generateKVForTextElement(el))

    [<Test>]
    member this.``Calling generateCardsForXml returns a card for an XML document with a single localized string``() = 
        let xml = """<TextLibrary><Text tag="[GAME_TAG]">content</Text></TextLibrary>"""
        let lessonID = 0
        let generated = PuzzleQuestGames.generateCardsForXml(lessonID, "en", "keyroot")(xml)
        let expected =
            [|
                {
                    CardRecord.ID = 0
                    LessonID = lessonID
                    Text = "content"
                    Gender = "masculine"
                    Key = "keyroot[GAME_TAG]masculine"
                    GenderlessKey = "keyroot[GAME_TAG]"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            |]

        Assert.AreEqual(expected, generated)
