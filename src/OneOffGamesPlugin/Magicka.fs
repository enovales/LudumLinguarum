module Magicka

open LLDatabase
open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

let officeNs = "urn:schemas-microsoft-com:office:spreadsheet"
let ssNs = "urn:schemas-microsoft-com:office:spreadsheet"

let internal getStringForCell(cellElement: XElement): string = 
    let dataOption = 
        cellElement.Descendants(XName.Get("Data", officeNs)) 
        |> Seq.tryHead 
        |> Option.map (fun e -> e.Value)

    match dataOption with
    | Some(d) -> d
    | _ -> ""

let internal getFirstTwoCellsForRow(rowElement: XElement): XElement list = 
    rowElement.Descendants(XName.Get("Cell", officeNs)) |> Seq.take(2) |> List.ofSeq

let internal getFirstTwoColumnsForWorksheet(worksheetElement: XElement): (string * string) array = 
    // skip header row
    let rows = 
        worksheetElement.Descendants(XName.Get("Row", officeNs)) 
        |> Seq.skip(1)
        |> Array.ofSeq

    let cellsToStrings(cells: XElement list) =
        match cells with
        | [idCell; valueCell] -> (getStringForCell(idCell), getStringForCell(valueCell))
        | _ -> failwith "unexpected number of cells"

    rows
    |> Array.map(getFirstTwoCellsForRow >> cellsToStrings)

let formattingCodeRegex = new Regex("\[[^\]]+\]")
let rec internal stripFormattingCodes(key: string, value: string): (string * string) = 
    let r = formattingCodeRegex.Match(value)
    match r.Success with
    | true -> stripFormattingCodes(key, value.Remove(r.Index, r.Length))
    | _ -> (key, value)

let internal generateCardsForWorkbook(language: string, keyRoot: string)(lessonID: int, doc: XElement) = 
    let makeTupleOfWorksheetAndName(worksheetElement: XElement) = 
        let worksheetName = worksheetElement.Attribute(XName.Get("Name", ssNs)).Value
        (worksheetElement, worksheetName)

    doc.Descendants(XName.Get("Worksheet", officeNs))
    |> Seq.map makeTupleOfWorksheetAndName
    |> Seq.collect (fun (ws, name) -> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot + name, language, "masculine")(getFirstTwoColumnsForWorksheet(ws) |> Seq.map stripFormattingCodes |> Map.ofSeq))
    |> Array.ofSeq

let internal generateCardsForXmlStream(lesson: LessonRecord, language: string, keyRoot: string, stream: Stream) = 
    let xel = XElement.Load(stream)
    generateCardsForWorkbook(language, keyRoot)(lesson.ID, xel)

/// <summary>
/// Generates a set of cards for a single localization XML file.
/// </summary>
/// <param name="lessonID">lesson ID to use for generated cards</param>
/// <param name="language">the language of this content</param>
/// <param name="xmlContent">the XML content to parse</param>
let internal generateCardsForXml(lessonID: int, language: string)(xmlContent: string) = 
    use stringReader = new StringReader(xmlContent)
    let xel = XElement.Load(stringReader)
    generateCardsForWorkbook(language, "")(lessonID, xel)

let internal languageMap =
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

let internal generateLessonsForLanguage(acc: LessonRecord array)(languagePath: string) = 
    let language = languageMap.Item(Path.GetFileName(languagePath))
    let makeLessonForFile(i: int)(fp: string): LessonRecord = 
        let rootName = Path.GetFileNameWithoutExtension(fp)
        let prefixToRemove = "Magicka_"
        let lessonName = 
            if rootName.StartsWith(prefixToRemove, StringComparison.InvariantCultureIgnoreCase) then
                rootName.Substring(prefixToRemove.Length)
            else
                rootName

        let lessonRecord = 
            {
                LessonRecord.ID = i
                Name = lessonName
            }
        lessonRecord

    let newLessons = 
        Directory.GetFiles(languagePath, "*.loctable.xml")
        |> Array.mapi makeLessonForFile

    // Append the newly-created lessons, remove duplicates by name, and then reindex.
    newLessons
    |> Array.append(acc)
    |> Array.distinctBy(fun l -> l.Name)
    |> Array.mapi(fun i l -> { l with ID = i })

let internal generateCardsAndLessonsForLanguage(lessons: LessonRecord array)(languagePath: string) = 
    let language = languageMap.Item(Path.GetFileName(languagePath))
    let getLessonForFile(fp: string): (LessonRecord * string * Stream) = 
        let rootName = Path.GetFileNameWithoutExtension(fp)
        let prefixToRemove = "Magicka_"
        let lessonName = 
            if rootName.StartsWith(prefixToRemove, StringComparison.InvariantCultureIgnoreCase) then
                rootName.Substring(prefixToRemove.Length)
            else
                rootName

        let lessonRecord = lessons |> Array.find(fun l -> l.Name = lessonName)
        (lessonRecord, fp.Substring(languagePath.Length), new MemoryStream(File.ReadAllBytes(fp)) :> Stream)

    let wrappedGenerateCardsForXmlStream(language: string)(lesson: LessonRecord, keyRoot: string, stream: Stream) = 
        try
            try
                generateCardsForXmlStream(lesson, language, keyRoot, stream)
            with
            | _ -> [||]
        finally
            stream.Dispose()

    Directory.GetFiles(languagePath, "*.loctable.xml")
    |> Array.map getLessonForFile
    |> Array.collect(wrappedGenerateCardsForXmlStream(language))

let ExtractMagicka(path: string) = 
    let threeCharLanguages = Directory.GetDirectories(Path.Combine(path, @"Content\Languages"))

    let lessons = 
        threeCharLanguages
        |> Array.fold generateLessonsForLanguage [||]

    let cards = 
        threeCharLanguages
        |> Array.collect(generateCardsAndLessonsForLanguage(lessons))

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
