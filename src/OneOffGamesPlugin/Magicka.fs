module Magicka

open LLDatabase
open System
open System.IO

let ExtractMagicka(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let threeCharLanguages = Directory.GetDirectories(Path.Combine(path, @"Content\Languages"))

    let generateCardsForLanguage(languagePath: string) = 
        let generateCardsForWorksheet(worksheetPath: string) = 
            [||]

        // TODO: map three character languages to two

        Directory.GetFiles(languagePath, "*.xml")
        |> Array.collect generateCardsForWorksheet

    threeCharLanguages
    |> Array.collect generateCardsForLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()