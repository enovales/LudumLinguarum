module CardExportTests

open Expecto
open LLDatabase
open System
open System.IO

let private sampleText = "Really long text"
let private cr = 
    {
        CardRecord.ID = 0
        LessonID = 0
        Text = sampleText
        Gender = ""
        Key = ""
        GenderlessKey = ""
        KeyHash = 0
        GenderlessKeyHash = 0
        SoundResource = ""
        LanguageTag = ""
        Reversible = true
    }

// Mirrors the sanitization that CommonExporter applies to card text before
// handing it to a card writer.
let private sanitizeCardText(s: string) = 
    s.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")

let private writeBrainscapeCsv(cardLines: string list) = 
    let path = Path.GetTempFileName()
    try
        let writeAll() = 
            use writer = new CardExport.BrainscapeCsvWriter(Console.Out, path)
            let cardWriter = writer :> CardExport.ICardWriter
            cardLines |> List.iter cardWriter.WriteCard
        writeAll()
        File.ReadAllText(path)
    finally
        File.Delete(path)

let private parseCsvRecords(contents: string) = 
    contents.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (CsvTools.extractFieldsForLine(Some ","))

[<Tests>]
let tests = 
  testList "CardExportTests" [
    testCase "Length filter without a limit specified always allows the card" <|
      fun () -> Expect.isTrue (CardExport.mkLengthFilter(None)(cr)) "card should be allowed"

    testCase "Length filter allows cards up to the length limit" <|
      fun () -> Expect.isTrue (CardExport.mkLengthFilter(Some(sampleText.Length))(cr)) "card should be allowed"

    testCase "Length filter disallows cards beyond the length limit" <|
      fun () -> Expect.isFalse (CardExport.mkLengthFilter(Some(sampleText.Length - 1))(cr)) "card should be disallowed"

    testCase "Word count filter without a limit specified always allows the card" <|
      fun () -> Expect.isTrue (CardExport.mkWordCountFilter(None)(cr)) "card should be allowed"
    
    testCase "Word count filter allows cards up to the word count limit" <|
      fun () -> Expect.isTrue (CardExport.mkWordCountFilter(Some(3))(cr)) "card should be allowed"

    testCase "Word count filter disallows cards beyond the word count limit" <|
      fun () -> Expect.isFalse (CardExport.mkWordCountFilter(Some(2))(cr)) "card should be disallowed"

    testCase "Brainscape CSV writer writes one question/answer record per card" <|
      fun () -> 
        let records = writeBrainscapeCsv(["alpha\tbeta"; "gamma\tdelta"]) |> parseCsvRecords
        Expect.equal records [| [| "alpha"; "beta" |]; [| "gamma"; "delta" |] |] "records should round-trip"

    testCase "Brainscape CSV writer quotes and escapes fields containing commas and quotes" <|
      fun () -> 
        let contents = writeBrainscapeCsv(["say \"hi\", loudly\tadieu, mon ami"])
        Expect.isTrue (contents.Contains("\"say \"\"hi\"\", loudly\"")) "question field should be quoted and escaped"
        let records = parseCsvRecords contents
        Expect.equal records [| [| "say \"hi\", loudly"; "adieu, mon ami" |] |] "fields should round-trip"

    testProperty "Brainscape CSV writer round-trips arbitrary card text" <|
      fun (question: string, answer: string) ->
        let q = sanitizeCardText question
        let a = sanitizeCardText answer
        let records = writeBrainscapeCsv([q + "\t" + a]) |> parseCsvRecords
        records.Length = 1 && records.[0].[0] = q && records.[0].[1] = a
  ]
