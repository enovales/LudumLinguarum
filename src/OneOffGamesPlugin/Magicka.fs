module Magicka

open DocumentFormat.OpenXml
open DocumentFormat.OpenXml.Packaging
open DocumentFormat.OpenXml.Spreadsheet
open LLDatabase
open OpenXmlPowerTools
open System
open System.IO

let internal getFirstTwoColumnsForWorksheet(doc: SpreadsheetDocument)(ws: WorksheetPart): (string * string) array = 
    ws.Rows() 
    |> Seq.skip(1) 
    |> Seq.mapi(fun i r -> 
        let idCell = WorksheetAccessor.GetCellValue(doc, ws, 1, i + 1)
        let valueCell = WorksheetAccessor.GetCellValue(doc, ws, 2, i + 1)
        (idCell.ToString(), valueCell.ToString())
    )
    |> Array.ofSeq

let internal generateCardsForWorkbook(language: string)(lessonID: int, worksheetPath: string) = 
    let spreadsheet = SpreadsheetDocument.Open(worksheetPath, false)
    
    spreadsheet.WorkbookPart.WorksheetParts 
    |> Seq.collect (getFirstTwoColumnsForWorksheet(spreadsheet))
    |> Map.ofSeq
    |> AssemblyResourceTools.createCardRecordForStrings(lessonID, Path.GetFileNameWithoutExtension(worksheetPath), language, "masculine")

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
    let makeLessonForFile(fp: string): (int * string) = 
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
        (createdLessonRecord.ID, fp)

    Directory.GetFiles(languagePath, "*.loctable.xml")
    |> Array.map makeLessonForFile
    |> Array.collect(generateCardsForWorkbook(language))

let ExtractMagicka(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let threeCharLanguages = Directory.GetDirectories(Path.Combine(path, @"Content\Languages"))

    threeCharLanguages
    |> Array.collect(generateCardsForLanguage(db, g.ID))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()