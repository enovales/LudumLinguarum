module CsvTools

open CsvHelper
open System
open System.IO

let extractFieldsForLine(delimiter: string)(line: string): string array = 
    use sr = new StringReader(line + Environment.NewLine + line)
    let config = new CsvHelper.Configuration.CsvConfiguration(Delimiter = delimiter)
    let parser = new CsvParser(sr, config)
    match parser.Read() with
    | null -> [||]
    | a -> a

