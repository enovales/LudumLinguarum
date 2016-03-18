module OrcsMustDieTests

open LLDatabase
open NUnit.Framework
open System.Xml.Linq

[<TestFixture>]
type OrcsMustDieTests() = 
    [<Test>]
    member this.``Calling generateKVForTextElement returns a pair of the '_locID' element, and the value``() = 
        let contents: obj array = [| new XAttribute(XName.Get("_locID"), "12345"); "content" |]
        let el = new XElement(XName.Get("String"), contents)
        let expected = ("12345", "content")
        Assert.AreEqual(expected, OrcsMustDie.generateKVForTextElement(el))

    [<Test>]
    member this.``Calling generateCardsForXml returns a card for an XML document with a single localized string``() = 
        let xml = """<?xml version="1.0" encoding="UTF-16"?>
<StringTable version ='0'>
   <Language name ='English'>
      <String _locID ='12345'>content</String>
   </Language>
</StringTable>
"""
        let lessonID = 0
        let generated = OrcsMustDie.generateCardsForXml(lessonID, "en", "keyroot")(xml)
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

        Assert.AreEqual(expected, generated)
