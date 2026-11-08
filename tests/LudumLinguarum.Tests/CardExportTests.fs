module CardExportTests

open Expecto
open LLDatabase
open LudumLinguarumPlugins
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

let private writeBrainscapeCsvWithPageSize(pageSize: int)(cardLines: string list) = 
    let path = Path.GetTempFileName()
    try
        let writeAll() = 
            use writer = new CardExport.BrainscapeCsvWriter(Console.Out, path, pageSize)
            let cardWriter = writer :> CardExport.ICardWriter
            cardLines |> List.iter cardWriter.WriteCard
        writeAll()
        File.ReadAllText(path)
    finally
        File.Delete(path)

let private writeBrainscapeCsv(cardLines: string list) = writeBrainscapeCsvWithPageSize(500)(cardLines)

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

    testCase "Brainscape CSV writer splits output into numerically suffixed files at the page limit" <|
      fun () -> 
        let dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(dir) |> ignore
        let exportPath = Path.Combine(dir, "deck.csv")
        try
            let writeAll() = 
                use writer = new CardExport.BrainscapeCsvWriter(Console.Out, exportPath, 2)
                let cardWriter = writer :> CardExport.ICardWriter
                ["a1\tb1"; "a2\tb2"; "a3\tb3"; "a4\tb4"; "a5\tb5"] |> List.iter cardWriter.WriteCard
            writeAll()

            let page1 = parseCsvRecords (File.ReadAllText(exportPath))
            let page2 = parseCsvRecords (File.ReadAllText(Path.Combine(dir, "deck_001.csv")))
            let page3 = parseCsvRecords (File.ReadAllText(Path.Combine(dir, "deck_002.csv")))
            Expect.equal page1 [| [| "a1"; "b1" |]; [| "a2"; "b2" |] |] "first page should hold 2 records"
            Expect.equal page2 [| [| "a3"; "b3" |]; [| "a4"; "b4" |] |] "second page should hold 2 records"
            Expect.equal page3 [| [| "a5"; "b5" |] |] "third page should hold the remaining record"
            Expect.isFalse (File.Exists(Path.Combine(dir, "deck_003.csv"))) "no fourth page should be created"
        finally
            Directory.Delete(dir, true)

    testCase "Brainscape export writes reversible card pairs in both directions" <|
      fun () -> 
        let exportPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv")
        try
            let db = new LLDatabase(":memory:")
            let lessonId = db.AddLesson({ LessonRecord.Name = "Test lesson"; ID = 0 })
            let mkCard key text lang = {
                CardRecord.ID = 0
                LessonID = lessonId
                Text = text
                Gender = ""
                Key = key
                GenderlessKey = key
                KeyHash = key.GetHashCode()
                GenderlessKeyHash = key.GetHashCode()
                SoundResource = ""
                LanguageTag = lang
                Reversible = true
            }
            db.AddCard(mkCard "k1" "alpha" "en") |> ignore
            db.AddCard(mkCard "k1" "eins" "de") |> ignore
            db.AddCard(mkCard "k2" "beta" "en") |> ignore
            db.AddCard(mkCard "k2" "zwei" "de") |> ignore

            let config = {
                CardExport.CommonExporterConfiguration.Target = CardExport.CommonExportTarget.Brainscape
                CardExport.CommonExporterConfiguration.LessonToExport = None
                CardExport.CommonExporterConfiguration.LessonRegexToExport = None
                CardExport.CommonExporterConfiguration.ExportPath = exportPath
                CardExport.CommonExporterConfiguration.RecognitionLanguage = "en"
                CardExport.CommonExporterConfiguration.ProductionLanguage = "de"
                CardExport.CommonExporterConfiguration.RecognitionLengthLimit = None
                CardExport.CommonExporterConfiguration.ProductionLengthLimit = None
                CardExport.CommonExporterConfiguration.RecognitionWordLimit = None
                CardExport.CommonExporterConfiguration.ProductionWordLimit = None
            }
            let exporter = new CardExport.CommonExporter(Unchecked.defaultof<IPluginManager>, Console.Out, db, config)
            exporter.RunExportAction()

            let records = parseCsvRecords (File.ReadAllText(exportPath)) |> Array.sort
            Expect.equal records [| [| "alpha"; "eins" |]; [| "beta"; "zwei" |]; [| "eins"; "alpha" |]; [| "zwei"; "beta" |] |] "each pair should appear forwards and backwards"
        finally
            File.Delete(exportPath)
  ]
