module CsvTools

open System
open System.Globalization
open System.IO

let extractFieldsForLine(delimiter: string)(line: string): string array = 
    use sr = new StringReader(line + Environment.NewLine + line)
    
    let config = CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
    config.Delimiter <- delimiter

    let parser = new CsvHelper.CsvParser(sr, config)
    if parser.Read() then
        parser.Record
    else
        [||]

let extractCsv<'T>(delimiter: string)(contents: string): 'T array = 
    use sr = new StringReader(contents)
    let config = CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
    config.Delimiter <- delimiter
    let parser = new CsvHelper.CsvReader(sr, config)

    parser.GetRecords<'T>() |> Array.ofSeq
