module Magicka

open DocumentFormat.OpenXml
open DocumentFormat.OpenXml.Spreadsheet
open LLDatabase
open System
open System.IO

let internal getFirstTwoColumnsForWorksheet()

let internal generateCardsForWorkbook(worksheetPath: string) = 
    let workbook = new Workbook(File.ReadAllText(worksheetPath))
    workbook.Sheets.GetEnumerator() |> 
    [||]

let internal generateCardsForLanguage(languagePath: string) = 

    // TODO: map three character languages to two

    Directory.GetFiles(languagePath, "*.xml")
    |> Array.collect generateCardsForWorkbook

let ExtractMagicka(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let threeCharLanguages = Directory.GetDirectories(Path.Combine(path, @"Content\Languages"))

    threeCharLanguages
    |> Array.collect generateCardsForLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()