module NubbysNumberFactory

open CsvHelper
open CsvHelper.Configuration
open LLDatabase
open System
open System.Globalization
open System.IO
open System.Text
open System.Text.RegularExpressions

(***************************************************************************)
(************************* Nubby's Number Factory **************************)
(***************************************************************************)
// Nubby's Number Factory stores all of its localized text in CSV files in the
// game's root directory: NNF_Full_LocalizationEN.csv holds the master English
// strings, and NNF_Localization<XX>.csv holds each additional language. Every
// file has (at least) three columns: a string key, the English source text,
// and the translated text. In the English file, the translation column just
// contains a placeholder, so the English text is used directly for it.
//
// The files are not consistently formatted: some quote every field and some
// quote none, some have a UTF-8 BOM, blank lines, a header row, trailing empty
// columns, and occasionally quoted fields with embedded newlines. A real CSV
// parser is therefore used instead of line-based splitting.

/// Maps the language code used in the localization file names to an IETF
/// language tag.
let internal languageCodeToTag =
    [|
        ("BR", "pt-BR")
        ("CA", "ca")
        ("DA", "da")
        ("DU", "nl")
        ("EN", "en")
        ("ES", "es")
        ("FR", "fr")
        ("GZ", "gl")
        ("HU", "hu")
        ("IT", "it")
        ("LA", "es-LA")
        ("NO", "no")
        ("PO", "pl")
        ("PT", "pt-PT")
        ("RU", "ru")
        ("SK", "sk")
        ("TU", "tr")
        ("UK", "uk")
    |] |> Map.ofArray

/// Finds the localization CSV files in the game directory, paired with the
/// language tag each one represents.
let internal findLocalizationFiles(gamePath: string): (string * string) array =
    Directory.GetFiles(gamePath, "NNF_*.csv")
    |> Array.choose(fun p ->
        let name = Path.GetFileNameWithoutExtension(p)
        // Files are named NNF_Full_LocalizationEN.csv (the master English file)
        // or NNF_LocalizationXX.csv, where XX is the language code.
        let code =
            if name.StartsWith("NNF_Full_Localization") then name.Substring("NNF_Full_Localization".Length)
            elif name.StartsWith("NNF_Localization") then name.Substring("NNF_Localization".Length)
            else ""
        match languageCodeToTag |> Map.tryFind(code) with
        | Some(tag) -> Some(tag, p)
        | None -> None)

/// Replaces the game's line-break placeholder ({a}) with whitespace and
/// collapses all whitespace runs into single spaces. Other {x} placeholders
/// are substitution slots (names, numbers, etc.) and are left alone.
let internal sanitizeText(s: string): string =
    let withoutLineBreaks = s.Replace("{a}", " ")
    Regex.Replace(withoutLineBreaks, @"\s+", " ").Trim()

/// Parses a localization CSV file into (key, English text, translated text)
/// triples. Blank rows, the occasional header row, and rows without a key are
/// skipped.
let internal parseLocalizationCsv(path: string): (string * string * string) array =
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    use sr = new StreamReader(fs, Encoding.UTF8, true)

    let config = new CsvConfiguration(CultureInfo.InvariantCulture)
    config.Delimiter <- ","
    config.HasHeaderRecord <- false
    config.BadDataFound <- (fun _ -> ())
    config.MissingFieldFound <- (fun _ -> ())
    // Skip completely blank rows (e.g. the ",,," separators in some files).
    config.ShouldSkipRecord <- (fun args -> args.Row.Parser.Record |> Array.forall(fun f -> String.IsNullOrWhiteSpace(f)))

    use reader = new CsvReader(sr, config)
    let mutable rows = []
    while reader.Read() do
        let record = reader.Parser.Record
        if record.Length >= 3 && not(String.IsNullOrWhiteSpace(record.[0])) && record.[0] <> "Column1" then
            rows <- (record.[0], record.[1], record.[2]) :: rows
    rows |> List.rev |> Array.ofList

let private createCard(lessonId: int)(languageTag: string)(key: string)(text: string): CardRecord =
    {
        CardRecord.ID = 0
        LessonID = lessonId
        Text = text
        Gender = "masculine"
        Key = key
        GenderlessKey = key
        KeyHash = 0
        GenderlessKeyHash = 0
        SoundResource = ""
        LanguageTag = languageTag
        Reversible = true
    }

let internal generateCardsForLanguage(lessonId: int)(languageTag: string)(rows: (string * string * string) array): CardRecord array =
    rows
    |> Array.choose(fun (key, english, translated) ->
        // For English, the translation column is a placeholder, so the source
        // text is the card text. For other languages, the translated text is.
        let text = if languageTag = "en" then english else translated
        let sanitized = sanitizeText(text)
        if String.IsNullOrWhiteSpace(sanitized) then None
        else Some(createCard lessonId languageTag key sanitized))

let ExtractNubbysNumberFactory(path: string): LudumLinguarumPlugins.ExtractedContent =
    let lessonEntry = {
        LessonRecord.ID = 0
        Name = "Game Text"
    }

    let languageFiles = findLocalizationFiles(path)
    if languageFiles.Length = 0 then
        raise(ArgumentException("No NNF_*.csv localization files found in '" + path + "'. Is this the root directory of Nubby's Number Factory?"))

    // The master English file is always present in a canonical installation,
    // so English cards come only from it. All language files share the same
    // keys, so the cards still pair up for export.
    let cards =
        languageFiles
        |> Array.collect(fun (tag, file) -> generateCardsForLanguage lessonEntry.ID tag (parseLocalizationCsv file))

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
