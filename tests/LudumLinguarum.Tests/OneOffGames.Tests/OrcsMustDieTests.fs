module OrcsMustDieTests

open Expecto
open LLDatabase
open System.Xml.Linq

[<Tests>]
let tests = 
  testList "Orcs Must Die! tests" [
    testCase "Calling generateKVForTextElement returns a pair of the '_locID' element, and the value" <|
      fun () ->
        let contents: obj array = [| new XAttribute(XName.Get("_locID"), "12345"); "content" |]
        let el = new XElement(XName.Get("String"), contents)
        let expected = ("12345", "content")
        Expect.equal expected (OrcsMustDie.generateKVForTextElement el) ""

    testCase "Calling generateCardsForXml returns a card for an XML document with a single localized string" <|
      fun () ->
        let xml = """<?xml version="1.0" encoding="UTF-16"?>
<StringTable version ='0'>
   <Language name ='Unused'>
      <String _locID ='12345'>content</String>
   </Language>
</StringTable>
"""
        let lessonID = 0
        let generated = OrcsMustDie.generateCardsForXml(lessonID, Some("en"), "keyroot")(xml)
        let expected =
            [|
                {
                    CardRecord.ID = 0
                    LessonID = lessonID
                    Text = "content"
                    Gender = "masculine"
                    Key = "keyroot12345masculine"
                    GenderlessKey = "keyroot12345"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            |]

        Expect.equal expected generated ""

    testCase "Calling generateCardsForXml autodetects the language to use from the XML string, if one is not specified" <|
      fun () ->
        let xml = """<?xml version="1.0" encoding="UTF-16"?>
<StringTable version ='0'>
   <Language name ='English'>
      <String _locID ='12345'>content</String>
   </Language>
</StringTable>
"""
        let lessonID = 0
        let generated = OrcsMustDie.generateCardsForXml(lessonID, None, "keyroot")(xml)
        let expected =
            [|
                {
                    CardRecord.ID = 0
                    LessonID = lessonID
                    Text = "content"
                    Gender = "masculine"
                    Key = "keyroot12345masculine"
                    GenderlessKey = "keyroot12345"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            |]

        Expect.equal expected generated ""
  ]
