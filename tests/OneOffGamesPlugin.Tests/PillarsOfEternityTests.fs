module PillarsOfEternityTests

open LLDatabase
open NUnit.Framework
open PillarsOfEternity
open System.Xml.Linq

[<TestFixture>]
type PillarsOfEternityTests() = 
    [<Test>]
    member this.``Calling extractDataFromEntry returns a tuple of the ID, DefaultText, and FemaleText``() = 
        let idElement = new XElement(XName.Get("ID"), "1")
        let defaultTextElement = new XElement(XName.Get("DefaultText"), "TestText")
        let femaleTextElement = new XElement(XName.Get("FemaleText"), "TestTextF")

        let contents: obj array = [| idElement; defaultTextElement; femaleTextElement |]
        let el = new XElement(XName.Get("Entry"), contents)
        let expected = 
            {
                ExtractedData.Id = 1
                DefaultText = Some("TestText")
                FemaleText = Some("TestTextF")
            }
        Assert.AreEqual(expected, PillarsOfEternity.extractDataFromEntry(el))

    [<Test>]
    member this.``Calling extractDataFromEntry returns a tuple of the ID, DefaultText if FemaleText is empty``() = 
        let idElement = new XElement(XName.Get("ID"), "1")
        let defaultTextElement = new XElement(XName.Get("DefaultText"), "TestText")
        let femaleTextElement = new XElement(XName.Get("FemaleText"))

        let contents: obj array = [| idElement; defaultTextElement; femaleTextElement |]
        let el = new XElement(XName.Get("Entry"), contents)
        let expected = 
            {
                ExtractedData.Id = 1
                DefaultText = Some("TestText")
                FemaleText = None
            }
        Assert.AreEqual(expected, PillarsOfEternity.extractDataFromEntry(el))


    [<Test>]
    member this.``Calling generateCardsForXml returns a card for an XML document with a single localized string``() = 
        let xml = """<?xml version="1.0" encoding="utf-8"?>
<StringTableFile xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Name>testconversation</Name>
  <NextEntryID>1</NextEntryID>
  <EntryCount>1</EntryCount>
  <Entries>
    <Entry>
      <ID>1</ID>
      <DefaultText>TestText</DefaultText>
      <FemaleText>TestTextF</FemaleText>
    </Entry>
  </Entries>
</StringTableFile>
"""
        let lessonID = 0
        let generated = (PillarsOfEternity.generateCardsForXml(lessonID, "en", "keyroot")(xml)) |> Set.ofSeq
        let expected =
            [|
                {
                    CardRecord.ID = 0
                    LessonID = lessonID
                    Text = "TestText"
                    Gender = "masculine"
                    Key = "keyroot1masculine"
                    GenderlessKey = "keyroot1"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
                {
                    CardRecord.ID = 0
                    LessonID = lessonID
                    Text = "TestTextF"
                    Gender = "feminine"
                    Key = "keyroot1feminine"
                    GenderlessKey = "keyroot1"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            |]
            |> Set.ofArray

        Assert.AreEqual(expected, generated)
