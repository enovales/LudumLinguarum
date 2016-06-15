module ParadoxStrategyGames

open LLDatabase
open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

let internal localizationSsvColumnMapping = 
    [|
        Some("en")
        Some("fr")
        Some("de")
        Some("pl")
        Some("es")
        Some("it")
        Some("sv")
        Some("cz")
        Some("hu")
        Some("nl")
        Some("pt")
        Some("ru")
        Some("fi")
        None
    |]

let internal generateCardsForSSVContent(lessonID: int, keyRoot: string)(ssv: string) = 
    let extractor = CsvTools.extractFieldsForLine(";")
    let cardsForLine(fields: string array) = 
        let cardsForPair(key: string)(langOpt: string option, value: string) = 
            match (langOpt, value) with
            | (Some(language), v) -> 
                [|
                    {
                        CardRecord.ID = 0
                        LessonID = lessonID
                        Text = v
                        Gender = "masculine"
                        Key = keyRoot + key + "masculine"
                        GenderlessKey = keyRoot + key
                        KeyHash = 0
                        GenderlessKeyHash = 0
                        SoundResource = ""
                        LanguageTag = language
                        Reversible = true
                    }
                |]
            | _ -> [||]
        Seq.zip localizationSsvColumnMapping (fields |> Seq.skip(1))
        |> Seq.collect(cardsForPair(fields |> Seq.head))
        |> Seq.toArray

    ssv.Split([| Environment.NewLine |], StringSplitOptions.None) 
    |> Array.filter (fun s -> not(s.StartsWith("#")))
    |> Array.collect(extractor >> cardsForLine)

let internal generateCardsForSSVs(lid: int, ssvDir: string) = 
    let files = Directory.GetFiles(ssvDir, "*.csv")
    let cardsForFile(p: string) = 
        File.ReadAllText(p) 
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
