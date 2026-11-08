module NubbysNumberFactoryTests

open Expecto
open LLDatabase
open NubbysNumberFactory
open System
open System.IO
open System.Text

let private withTempGameDir(files: (string * string * string) array)(f: string -> unit): unit =
    let dir = Path.Combine(Path.GetTempPath(), "nnf-tests-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(dir) |> ignore
    try
        files
        |> Array.iter(fun (name, contents, encoding) ->
            let enc = if encoding = "bom" then new UTF8Encoding(true) else new UTF8Encoding(false)
            File.WriteAllText(Path.Combine(dir, name), contents, enc))
        f(dir)
    finally
        Directory.Delete(dir, true)

[<Tests>]
let sanitizeTextTests =
  testList "Nubby's Number Factory sanitizeText tests" [
    testCase "Line-break placeholders are replaced and whitespace is collapsed" <|
      fun () ->
        Expect.equal "Alpha Beta {b}!" (sanitizeText "Alpha{a}{a}Beta  {b}!\n") ""

    testCase "Substitution placeholders other than {a} are kept" <|
      fun () ->
        Expect.equal "Made for {b} in {c}" (sanitizeText "Made for {b} in {c}") ""

    testCase "Plain text is unchanged" <|
      fun () ->
        Expect.equal "Foo bar" (sanitizeText "Foo bar") ""
  ]

[<Tests>]
let parseLocalizationCsvTests =
  testList "Nubby's Number Factory parseLocalizationCsv tests" [
    testCase "Quoted, unquoted, blank, header, and multi-line rows are all parsed" <|
      fun () ->
        let csv = """Column1,Column2,Column3
"key_one","Source one","Translation one",

key_two,Unquoted source,Unquoted translation,
"key_three","Source line one
Source line two","Translation line one
Translation line two"
"""
        withTempGameDir [| ("NNF_LocalizationFR.csv", csv, "nobom") |] (fun dir ->
            let rows = parseLocalizationCsv(Path.Combine(dir, "NNF_LocalizationFR.csv"))
            let expected =
                [|
                    ("key_one", "Source one", "Translation one")
                    ("key_two", "Unquoted source", "Unquoted translation")
                    ("key_three", "Source line one\nSource line two", "Translation line one\nTranslation line two")
                |]
            Expect.equal expected rows "")

    testCase "A UTF-8 BOM is handled" <|
      fun () ->
        let csv = "\"key_one\",\"Source one\",\"Translation one\"\r\n"
        withTempGameDir [| ("NNF_LocalizationHU.csv", csv, "bom") |] (fun dir ->
            let rows = parseLocalizationCsv(Path.Combine(dir, "NNF_LocalizationHU.csv"))
            Expect.equal [| ("key_one", "Source one", "Translation one") |] rows "")
  ]

[<Tests>]
let findLocalizationFilesTests =
  testList "Nubby's Number Factory findLocalizationFiles tests" [
    testCase "Localization files are mapped to language tags, unknown files are skipped" <|
      fun () ->
        withTempGameDir
            [|
                ("NNF_Full_LocalizationEN.csv", "", "nobom")
                ("NNF_LocalizationFR.csv", "", "nobom")
                ("NNF_LocalizationBR.csv", "", "nobom")
                ("NNF_LocalizationDU.csv", "", "nobom")
                ("NNF_LocalizationPT.csv", "", "nobom")
                ("NNF_LocalizationXX.csv", "", "nobom")
                ("something-else.csv", "", "nobom")
            |]
            (fun dir ->
                let expected =
                    [|
                        ("en", Path.Combine(dir, "NNF_Full_LocalizationEN.csv"))
                        ("fr", Path.Combine(dir, "NNF_LocalizationFR.csv"))
                        ("pt-BR", Path.Combine(dir, "NNF_LocalizationBR.csv"))
                        ("nl", Path.Combine(dir, "NNF_LocalizationDU.csv"))
                        ("pt-PT", Path.Combine(dir, "NNF_LocalizationPT.csv"))
                    |] |> Array.sortBy fst
                Expect.equal expected (findLocalizationFiles dir |> Array.sortBy fst) "")
  ]

[<Tests>]
let extractTests =
  // The third column of the master English file is an unused placeholder in
  // the real game; any value works here, since it is ignored for "en".
  let enCsv = """
"test_greeting","Alpha{a}{a}Beta {b}!","unused placeholder",
"test_blank","   ","unused placeholder",
"""

  let frCsv = """Column1,Column2,Column3
"test_greeting","Alpha{a}{a}Beta {b}!","Gamma{a}{a}Delta {b}",
"test_multiline","Source line one
Source line two","Translation line one
Translation line two",
test_unquoted,Unquoted source,Unquoted translation,
"test_blank_translation","Some source text","",
"""

  let expectedEnCard =
    {
        CardRecord.ID = 0
        LessonID = 0
        Text = "Alpha Beta {b}!"
        Gender = "masculine"
        Key = "test_greeting"
        GenderlessKey = "test_greeting"
        KeyHash = 0
        GenderlessKeyHash = 0
        SoundResource = ""
        LanguageTag = "en"
        Reversible = true
    }

  testList "Nubby's Number Factory extraction tests" [
    testCase "Extraction produces a single lesson and language-pairable cards" <|
      fun () ->
        withTempGameDir
            [|
                ("NNF_Full_LocalizationEN.csv", enCsv, "nobom")
                ("NNF_LocalizationFR.csv", frCsv, "nobom")
            |]
            (fun dir ->
                let result = ExtractNubbysNumberFactory(dir)
                Expect.equal [| { LessonRecord.ID = 0; Name = "Game Text" } |] result.lessons ""

                let expectedCards =
                    [|
                        expectedEnCard
                        { expectedEnCard with Text = "Gamma Delta {b}"; LanguageTag = "fr" }
                        { expectedEnCard with Text = "Translation line one Translation line two"; Key = "test_multiline"; GenderlessKey = "test_multiline"; LanguageTag = "fr" }
                        { expectedEnCard with Text = "Unquoted translation"; Key = "test_unquoted"; GenderlessKey = "test_unquoted"; LanguageTag = "fr" }
                    |] |> Array.sortBy(fun c -> (c.LanguageTag, c.Key))
                Expect.equal expectedCards (result.cards |> Array.sortBy(fun c -> (c.LanguageTag, c.Key))) "")

    testCase "Extraction fails with a useful error when no localization files are present" <|
      fun () ->
        withTempGameDir [| ("readme.txt", "nothing here", "nobom") |] (fun dir ->
            Expect.throws (fun () -> ExtractNubbysNumberFactory(dir) |> ignore) "")
  ]
