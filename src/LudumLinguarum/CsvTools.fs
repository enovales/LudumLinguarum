module CsvTools

open System
open System.Globalization
open System.IO

let extractFieldsForLine(delimiterOpt: string option)(line: string): string array = 
    use sr = new StringReader(line + Environment.NewLine + line)
    
    let config = CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
    config.BadDataFound <- (fun a -> ())

    match delimiterOpt with
    | Some(delimiter) -> config.Delimiter <- delimiter
    | _ -> config.DetectDelimiter <- true

    let parser = new CsvHelper.CsvParser(sr, config)
    if parser.Read() then
        parser.Record
    else
        [||]

let extractCsv<'T>(delimiterOpt: string option)(contents: string): 'T array = 
    use sr = new StringReader(contents)
    let config = CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
    config.BadDataFound <- (fun a -> ())

    match delimiterOpt with
    | Some(delimiter) -> config.Delimiter <- delimiter
    | _ -> config.DetectDelimiter <- true

    let parser = new CsvHelper.CsvReader(sr, config)

    parser.GetRecords<'T>() |> Array.ofSeq
