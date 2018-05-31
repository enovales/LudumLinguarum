module PuzzleQuestGamesTests

open Expecto
open LLDatabase
open System.Xml.Linq

[<Tests>]
let tests = 
  testList "Puzzle Quest games tests" [
    testCase "Calling generateKVForTextElement returns a pair of the 'tag' element, and the value" <|
      fun () ->
        let contents: obj array = [| new XAttribute(XName.Get("tag"), "[GAME_TAG]"); "content" |]
        let el = new XElement(XName.Get("Text"), contents)
        let expected = ("[GAME_TAG]", "content")
        Expect.equal expected (PuzzleQuestGames.generateKVForTextElement el) ""

    testCase "Calling generateCardsForXml returns a card for an XML document with a single localized string" <|
      fun () ->
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

        Expect.equal expected generated ""
  ]
