module XUIGames

open LLDatabase
open System
open System.Globalization
open System.IO

let internal languageForXUITag(c: string) = 
    match c with
    | "jp" -> "ja"
    | x -> x

let internal languageTagsForHeaderRow(headerRow: string array) = 
    headerRow 
    |> Array.skip(2) 
    |> Array.filter(String.IsNullOrWhiteSpace >> not)
    |> Array.map languageForXUITag    

let internal makeCardsForLanguageTags(cardKey: string, lessonId: int)(language: string, text: string) = 
    {
        CardRecord.Gender = "";
        GenderlessKey = cardKey;
        GenderlessKeyHash = 0;
        CardRecord.ID = 0;
        Key = cardKey;
        KeyHash = 0;
        LanguageTag = language;
        LessonID = lessonId;
        Reversible = true;
        SoundResource = "";
        Text = text
    }

let internal generateCardsForRow(lessonId: int, languageTags: string array)(rowIndex: int)(t: string array) = 
    let key = t.[0]
    let category = t.[1]
    let cardKey = rowIndex.ToString() + key + category;
    let localizedStrings = t |> Array.skip(2)
    let tagsAndStrings = localizedStrings |> Array.zip languageTags

    tagsAndStrings
    |> Array.map(makeCardsForLanguageTags(cardKey, lessonId))

let internal extractXUITabDelimited(stringLines: string array, lessonId: int): CardRecord array = 
    let tabDelimited = stringLines |> Array.map(fun t -> t.Split('\t'))
    let dataRows = tabDelimited |> Array.tail

    // get country tags, and convert them into language tags
    let languageTags = languageTagsForHeaderRow(tabDelimited |> Array.head)

    dataRows
    |> Array.mapi(generateCardsForRow(lessonId, languageTags))
    |> Array.collect id

let ExtractKOF2002(path: string, db: LLDatabase, gameEntryWithId: GameRecord, args: string[]) = 
    let lessonEntry = {
        LessonRecord.GameID = gameEntryWithId.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

    let stringFilePath = Path.Combine(path, @"data\strings.txt")
    let extractedCards = 
        extractXUITabDelimited(File.ReadAllLines(stringFilePath, Text.Encoding.UTF8), lessonEntryWithId.ID)

    // filter out empty cards.
    let allCards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

    db.CreateOrUpdateCards(allCards)
    ()

let ExtractKOF98(path: string, db: LLDatabase, gameEntryWithId: GameRecord, args: string[]) = 
    let lessonEntry = {
        LessonRecord.GameID = gameEntryWithId.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

    let stringFilePath = Path.Combine(path, @"data\strings.txt")
    let extractedCards = 
        extractXUITabDelimited(File.ReadAllLines(stringFilePath, Text.Encoding.UTF8), lessonEntryWithId.ID)

    // filter out empty cards.
    let allCards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

    db.CreateOrUpdateCards(allCards)
    ()
