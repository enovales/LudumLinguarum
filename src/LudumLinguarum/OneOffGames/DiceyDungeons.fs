module DiceyDungeons

open LLDatabase
open LLUtils
open System.Text
open System.Text.RegularExpressions
open System.IO

let private trimString(s: string) = s.Trim()
let private replaceEscapeChars(s: string) = s.Replace(@"\r", " ").Replace(@"\n", " ").Replace('|', ' ')
let private consolidateWhitespace(s: string) = Regex.Replace(s, @"\s+", " ")

let private sanitizePipeline =
    replaceEscapeChars >> consolidateWhitespace >> trimString


let private ExtractLanguageCards(l: LessonRecord)(language: string, path: string): CardRecord list =
    // We need to autodetect the column pairs for localized text.
    use s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    use r = new StreamReader(s, Encoding.UTF8)

    let header = r.ReadLine()
    let extractor = CsvTools.extractFieldsForLine(None)
    let fields = extractor(header)

    // For each field, check to see if there is another field with the same name + a "_<language>" suffix.
    // Track the field indices for both of these, if found.
    let fieldsWithIndices = fields |> List.ofArray |> List.indexed
    let localizedFieldIndexesAndNames =
        fieldsWithIndices
        |> List.allPairs fieldsWithIndices
        |> List.filter (fun ((i1, fn1), (i2, fn2)) -> fn2 = fn1 + "_" + language)
        |> List.map (fun ((i1, _), (i2, _)) -> (i1, i2))

    let cardPairForFields(fields: string array, lineCount: int)((i1: int, i2: int)) =
        try
            let key = lineCount.ToString() + fields[i1]
            let nonEnglishValue = sanitizePipeline(fields[i2])
            let englishValue = sanitizePipeline(fields[i1])

            [
                {
                    CardRecord.ID = 0
                    LessonID = l.ID
                    Text = nonEnglishValue
                    Gender = "masculine"
                    Key = key
                    GenderlessKey = key
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = language
                    Reversible = true
                }
                {
                    CardRecord.ID = 0
                    LessonID = l.ID
                    Text = englishValue
                    Gender = "masculine"
                    Key = key
                    GenderlessKey = key
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            ]
        with
        | _ -> []

    let cardsForLine(lineCount: int, line: string) =
        let lineFields = extractor(line)
        localizedFieldIndexesAndNames
        |> List.collect (cardPairForFields(lineFields, lineCount))

    File.ReadAllLines(path)
    |> Array.mapi (fun i l -> (i, l))
    |> Array.skip 1
    |> List.ofArray
    |> List.collect cardsForLine
    |> List.distinct

let private ExtractLessonCards(languages: string list, localizedRootPath: string)(l: LessonRecord): CardRecord list =
    // Check if a CSV with the lesson name exists - not all languages have the same
    // set of content.
    let paths =
        languages
        |> List.map (fun lang -> (lang, Path.Combine(localizedRootPath, FixPathSeps(lang + @"\" + l.Name + ".csv"))))
        |> List.filter (fun (_, p) -> File.Exists(p))

    paths
    |> List.collect(ExtractLanguageCards(l))
    |> List.distinct

let ExtractDiceyDungeons(path: string) =
    let localizedRoot = FixPathSeps @"data\text\locale"

    let localizedRootPath = Path.Combine(path, localizedRoot)
    let languages =
        Directory.GetDirectories(localizedRootPath)
        |> Array.toList
        |> List.map (fun languagePath -> Path.GetFileNameWithoutExtension(languagePath))

    let lessonNames =
        languages
        |> List.collect (fun l -> Directory.GetFiles(Path.Combine(localizedRootPath, l), "*.csv") |> Array.toList)
        |> List.map (fun n -> Path.GetFileNameWithoutExtension(n))
        |> List.distinct

    let lessonRecords =
        lessonNames
        |> List.mapi (fun i n -> { LessonRecord.ID = i; Name = n })

    let cards =
        lessonRecords
        |> List.collect(ExtractLessonCards(languages, localizedRootPath))

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessonRecords |> Array.ofList
        LudumLinguarumPlugins.ExtractedContent.cards = cards |> Array.ofList
    }
