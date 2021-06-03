module MagickaTests

open Expecto
open LLDatabase
open Magicka
open System.Xml.Linq

[<Tests>]
let tests = 
  testList "Magicka tests" [
    testCase "Calling getStringForCell() returns the value of the Data descendant" <|
      fun () ->
        let contents: obj array = [| new XElement(XName.Get("Data", officeNs), "content") |]
        let el = new XElement(XName.Get("Cell", officeNs), contents)

        Expect.equal "content" (getStringForCell el) ""

    testCase "Calling getStringForCell() returns an empty string if there is no Data descendant" <|
      fun () ->
        let el = new XElement(XName.Get("Cell", officeNs))
        Expect.equal "" (getStringForCell el) ""
    
    testCase "Calling getFirstTwoCellsForRow() returns the first two XElements of Cell descendants" <|
      fun () ->
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

        Expect.equal 2 results.Length ""
        Expect.equal [ cell1; cell2 ] results ""

    testCase "Calling getFirstTwoColumnsForWorksheet() returns an array of tuples of the strings of the first two columns of each row after the first" <|
      fun () ->
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
        Expect.equal expected.Length results.Length ""
        Expect.equal expected.[0] results.[0] ""
        Expect.equal expected.[1] results.[1] ""

    testCase "Calling generateCardsForXml() returns an array of CardRecords, based on the contents of the first two columns of each Row" <|
      fun () ->
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
        Expect.equal expected results ""

    testCase "Calling stripFormattingCodes has no effect on a tuple with no formatting codes" <|
      fun () ->
        let testValue = ("key", "value")
        let expected = testValue
        Expect.equal expected (stripFormattingCodes testValue) ""

    testCase "Calling stripFormattingCodes removes formatting codes if present" <|
      fun () ->
        let testValue = ("key", "val[c foo]u[/c]e")
        let expected = ("key", "value")
        Expect.equal expected (stripFormattingCodes testValue) ""
  ]
