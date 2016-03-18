module Magicka

open LLDatabase
open System
open System.IO
open System.Xml.Linq

let internal getStringForCell(cellElement: XElement): string = 
    let dataOption = 
        cellElement.Descendants(XName.Get("Data")) 
        |> Seq.tryHead 
        |> Option.map (fun e -> e.Value)

    match dataOption with
    | Some(d) -> d
    | _ -> ""

let internal getFirstTwoCellsForRow(rowElement: XElement): XElement list = 
    rowElement.Descendants(XName.Get("Cell")) |> Seq.take(2) |> List.ofSeq

let internal getFirstTwoColumnsForWorksheet(worksheetElement: XElement): (string * string) array = 
    // skip header row
    let rows = worksheetElement.Descendants(XName.Get("Row")) |> Seq.skip(1)

    let cellsToStrings(cells: XElement list) =
        match cells with
        | [idCell; valueCell] -> (getStringForCell(idCell), getStringForCell(valueCell))
        | _ -> failwith "unexpected number of cells"

    rows
    |> Seq.map(getFirstTwoCellsForRow >> cellsToStrings)
    |> Array.ofSeq

let internal generateCardsForWorkbook(language: string)(lessonID: int, doc: XElement) = 
    let makeTupleOfWorksheetAndName(worksheetElement: XElement) = 
        (worksheetElement, worksheetElement.Attribute(XName.Get("ss:Name")).Value)

    doc.Descendants(XName.Get("Worksheet"))
    |> Seq.map makeTupleOfWorksheetAndName
    |> Seq.collect (fun (ws, name) -> AssemblyResourceTools.createCardRecordForStrings(lessonID, name, language, "masculine")(getFirstTwoColumnsForWorksheet(ws) |> Map.ofSeq))
    |> Array.ofSeq

let internal generateCardsForXmlStream(lesson: LessonRecord, language: string, stream: Stream) = 
    let xel = XElement.Load(stream)
    generateCardsForWorkbook(language)(lesson.ID, xel)

/// <summary>
/// Generates a set of cards for a single localization XML file.
/// </summary>
/// <param name="lessonID">lesson ID to use for generated cards</param>
/// <param name="language">the language of this content</param>
/// <param name="xmlContent">the XML content to parse</param>
let internal generateCardsForXml(lessonID: int, language: string)(xmlContent: string) = 
    use stringReader = new StringReader(xmlContent)
    let xel = XElement.Load(stringReader)
    generateCardsForWorkbook(language)(lessonID, xel)

let internal generateCardsForLanguage(db: LLDatabase, gameID: int)(languagePath: string) = 
    // TODO: map three character languages to two
    let languageMap =
        [|
            ("deu", "de")
            ("eng", "en")
            ("fra", "fr")
            ("hun", "hu")
            ("ita", "it")
            ("pol", "pl")
            ("rus", "ru")
            ("spa", "es")
        |]
        |> Map.ofArray

    let language = languageMap.Item(Path.GetFileName(languagePath))
    let makeLessonForFile(fp: string): (LessonRecord * string * Stream) = 
        let rootName = Path.GetFileNameWithoutExtension(fp)
        let prefixToRemove = "Magicka_"
        let lessonName = 
            if rootName.StartsWith(prefixToRemove, StringComparison.InvariantCultureIgnoreCase) then
                rootName.Substring(prefixToRemove.Length)
            else
                rootName

        let lessonRecord = 
            {
                LessonRecord.GameID = gameID
                ID = 0
                Name = lessonName
            }
        let createdLessonRecord = { lessonRecord with ID = db.CreateOrUpdateLesson(lessonRecord) }
        (createdLessonRecord, fp, new MemoryStream(File.ReadAllBytes(fp)) :> Stream)

    let wrappedGenerateCardsForXmlStream(lesson: LessonRecord, language: string, stream: Stream) = 
        try
            try
                generateCardsForXmlStream(lesson, language, stream)
            with
            | ex -> [||]
        finally
            stream.Dispose()

    Directory.GetFiles(languagePath, "*.loctable.xml")
    |> Array.map makeLessonForFile
    |> Array.collect(wrappedGenerateCardsForXmlStream)

let ExtractMagicka(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let threeCharLanguages = Directory.GetDirectories(Path.Combine(path, @"Content\Languages"))

    threeCharLanguages
    |> Array.collect(generateCardsForLanguage(db, g.ID))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()