module MagickaTests

open LLDatabase
open Magicka
open NUnit.Framework
open System.Xml.Linq

[<TestFixture>]
type MagickaTests() = 
    [<Test>]
    member this.``Calling getStringForCell() returns the value of the Data descendant``() = 
        let contents: obj array = [| new XElement(XName.Get("Data", officeNs), "content") |]
        let el = new XElement(XName.Get("Cell", officeNs), contents)

        Assert.AreEqual("content", getStringForCell(el))

    [<Test>]
    member this.``Calling getStringForCell() returns an empty string if there is no Data descendant``() = 
        let el = new XElement(XName.Get("Cell", officeNs))

        Assert.AreEqual("", getStringForCell(el))

    [<Test>]
    member this.``Calling getFirstTwoCellsForRow() returns the first two XElements of Cell descendants``() = 
        let cellContents1: obj array = [| new XElement(XName.Get("Data", officeNs), "content1") |]
        let cellContents2: obj array = [| new XElement(XName.Get("Data", officeNs), "content2") |]
        let cellContents3: obj array = [| new XElement(XName.Get("Data", officeNs), "should not be captured") |]
        let cell1 = new XElement(XName.Get("Cell", officeNs), cellContents1)
        let cell2 = new XElement(XName.Get("Cell", officeNs), cellContents2)
        let cell3 = new XElement(XName.Get("Cell", officeNs), cellContents3)
        let rowContents: obj array = 
            [|
                cell1
                cell2
                cell3
            |]
        let rowEl = new XElement(XName.Get("Row", officeNs), rowContents)
        let results = getFirstTwoCellsForRow(rowEl)

        Assert.AreEqual(2, results.Length)
        Assert.AreEqual([ cell1; cell2 ], results)

    [<Test>]
    member this.``Calling getFirstTwoColumnsForWorksheet() returns an array of tuples of the strings of the first two columns of each row after the first``() = 
        let worksheetEl = 
            new XElement(XName.Get("Worksheet", officeNs),
                new XElement(XName.Get("Table", officeNs),
                    [| 
                        new XElement(XName.Get("Column", officeNs))
                        new XElement(XName.Get("Column", officeNs))
                        new XElement(XName.Get("Column", officeNs))
                        new XElement(XName.Get("Row", officeNs),
                            [|
                                new XElement(XName.Get("Cell", officeNs), 
                                    new XElement(XName.Get("Data", officeNs), "ColumnHeader1")
                                )
                                new XElement(XName.Get("Cell", officeNs),
                                    new XElement(XName.Get("Data", officeNs), "ColumnHeader2")
                                )    
                                new XElement(XName.Get("Cell", officeNs),
                                    new XElement(XName.Get("Data", officeNs), "ColumnHeader3")
                                )
                            |]
                        )
                        new XElement(XName.Get("Row", officeNs),
                            [|
                                new XElement(XName.Get("Cell", officeNs), 
                                    new XElement(XName.Get("Data", officeNs), "row1column1")
                                )
                                new XElement(XName.Get("Cell", officeNs),
                                    new XElement(XName.Get("Data", officeNs), "row1column2")
                                )    
                                new XElement(XName.Get("Cell", officeNs),
                                    new XElement(XName.Get("Data", officeNs), "row1column3")
                                )
                            |]
                        )
                        new XElement(XName.Get("Row", officeNs),
                            [|
                                new XElement(XName.Get("Cell", officeNs), 
                                    new XElement(XName.Get("Data", officeNs), "row2column1")
                                )
                                new XElement(XName.Get("Cell", officeNs),
                                    new XElement(XName.Get("Data", officeNs), "row2column2")
                                )    
                                new XElement(XName.Get("Cell", officeNs),
                                    new XElement(XName.Get("Data", officeNs), "row2column3")
                                )
                            |]
                        )
                    |]
                )
            )

        let results = getFirstTwoColumnsForWorksheet(worksheetEl)
        let expected = 
            [|
                ("row1column1", "row1column2")
                ("row2column1", "row2column2")
            |]
        Assert.AreEqual(expected.Length, results.Length)
        Assert.AreEqual(expected.[0], results.[0])
        Assert.AreEqual(expected.[1], results.[1])

    [<Test>]
    member this.``Calling generateCardsForXml() returns an array of CardRecords, based on the contents of the first two columns of each Row``() = 
        let testXml = """<?xml version="1.0"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:o="urn:schemas-microsoft-com:office:office"
 xmlns:x="urn:schemas-microsoft-com:office:excel"
 xmlns:dt="uuid:C2F41010-65B3-11d1-A29F-00AA00C14882"
 xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:html="http://www.w3.org/TR/REC-html40">
 <Worksheet ss:Name="ABC">
  <Table ss:ExpandedColumnCount="3" ss:ExpandedRowCount="4">
   <Column />
   <Column />
   <Column />
   <Column />
   <Row>
    <Cell><Data ss:Type="String">ColumnHeader1</Data></Cell>
    <Cell><Data ss:Type="String">ColumnHeader2</Data></Cell>
    <Cell><Data ss:Type="String">ColumnHeader3</Data></Cell>
   </Row>
   <Row>
    <Cell><Data ss:Type="String">row1column1</Data></Cell>
    <Cell><Data ss:Type="String">row1column2</Data></Cell>
    <Cell><Data ss:Type="String">row1column3</Data></Cell>
   </Row>
   <Row>
    <Cell><Data ss:Type="String">row2column1</Data></Cell>
    <Cell><Data ss:Type="String">row2column2</Data></Cell>
    <Cell><Data ss:Type="String">row2column3</Data></Cell>
   </Row>
  </Table>
 </Worksheet>
</Workbook>"""

        let expected = 
            [|
                {
                    CardRecord.ID = 0
                    LessonID = 1
                    Text = "row1column2"
                    Gender = "masculine"
                    Key = "ABCrow1column1masculine"
                    GenderlessKey = "ABCrow1column1"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
                {
                    CardRecord.ID = 0
                    LessonID = 1
                    Text = "row2column2"
                    Gender = "masculine"
                    Key = "ABCrow2column1masculine"
                    GenderlessKey = "ABCrow2column1"
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            |]

        let results = generateCardsForXml(1, "en")(testXml)
        Assert.AreEqual(expected, results)

    [<Test>]
    member this.``Calling stripFormattingCodes has no effect on a tuple with no formatting codes``() = 
        let testValue = ("key", "value")
        let expected = testValue
        Assert.AreEqual(expected, stripFormattingCodes(testValue))

    [<Test>]
    member this.``Calling stripFormattingCodes removes formatting codes if present``() = 
        let testValue = ("key", "val[c foo]u[/c]e")
        let expected = ("key", "value")
        Assert.AreEqual(expected, stripFormattingCodes(testValue))
