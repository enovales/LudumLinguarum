module ParadoxStrategyGames

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq

let internal localizationsAndEncodings = 
    [|
        ("en", Encoding.GetEncoding("Windows-1252"))
        ("fr", Encoding.GetEncoding("Windows-1252"))
        ("de", Encoding.GetEncoding("Windows-1252"))
        ("pl", Encoding.GetEncoding("windows-1250"))
        ("es", Encoding.GetEncoding("Windows-1252"))
        ("it", Encoding.GetEncoding("Windows-1252"))
        //("sv", Encoding.GetEncoding("Windows-1252"))
        ("cz", Encoding.GetEncoding("Windows-1250"))
        ("hu", Encoding.GetEncoding("Windows-1250"))
        ("nl", Encoding.GetEncoding("Windows-1252"))
        ("pt", Encoding.GetEncoding("Windows-1252"))
        ("ru", Encoding.GetEncoding("koi8-r"))
        ("fi", Encoding.GetEncoding("Windows-1252"))
    |]

let internal generateCardsForSSVContent(lessonID: int, keyRoot: string)(ssvBytes: byte array) = 
    let extractor = CsvTools.extractFieldsForLine(";")
    let cardsForLine(i: int, lang: string)(fields: string array) = 
        let tag = fields |> Array.head
        let langValue = fields.[i].Trim()
        match langValue with
        | v when not(String.IsNullOrWhiteSpace(v)) && v <> "x" && v <> "*" -> 
            [|
                {
                    CardRecord.ID = 0
                    LessonID = lessonID
                    Text = v
                    Gender = "masculine"
                    Key = keyRoot + tag + "masculine"
                    GenderlessKey = keyRoot + tag
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    LanguageTag = lang
                    Reversible = true
                }
            |]
        | _ -> [||]

    // for each available language, extract the whole file again with that language's encoding.
    let createExtractionTuple(i: int)(lang: string, enc: Encoding) = 
        (i + 1, lang, enc.GetString(ssvBytes).Split([| Environment.NewLine |], StringSplitOptions.None))
    let cardsForLocalizationAndEncoding(i: int, lang: string, lines: string array) = 
        let nonCommentLine(l: string) = not(l.StartsWith("#")) && not(String.IsNullOrWhiteSpace(l))
        lines 
        |> Array.filter nonCommentLine 
        |> Array.map extractor
        |> Array.collect(cardsForLine(i, lang))

    localizationsAndEncodings
    |> Array.mapi createExtractionTuple
    |> Array.collect cardsForLocalizationAndEncoding

let internal generateCardsForSSVs(lid: int, ssvDir: string) = 
    let files = Directory.GetFiles(ssvDir, "*.csv")
    let cardsForFile(p: string) = 
        File.ReadAllBytes(p) 
        |> generateCardsForSSVContent(lid, Path.GetFileNameWithoutExtension(p))

    files
    |> Seq.collect cardsForFile
    |> Array.ofSeq

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let ExtractEU3(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lesson = createLesson(g.ID, db)("Game Text")

    generateCardsForSSVs(lesson.ID, Path.Combine(path, "localisation"))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
