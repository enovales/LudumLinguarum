module CsvTools

open System
open System.IO

let extractFieldsForLine(delimiter: string)(line: string): string array = 
    use sr = new StringReader(line + Environment.NewLine + line)
    let config = CsvHelper.Configuration.Configuration(Delimiter = delimiter)
    let parser = new CsvHelper.CsvParser(sr, config)
    match parser.Read() with
    | null -> [||]
    | a -> a

let extractCsv<'T>(delimiter: string)(contents: string): 'T array = 
    use sr = new StringReader(contents)
    let parser = new CsvHelper.CsvReader(sr)
    parser.Configuration.Delimiter <- delimiter

    parser.GetRecords<'T>() |> Array.ofSeq