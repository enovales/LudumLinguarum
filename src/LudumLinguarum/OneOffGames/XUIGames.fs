module XUIGames

open LLDatabase
open LLUtils
open System
open System.Globalization
open System.IO

let internal languageForXUITag(c: string) = 
    match c with
    | "jp" -> "ja"
    | x -> x

let internal languageTagsForHeaderRow(headerRow: string array) = 
    if ((headerRow |> Array.length) > 2) then
        headerRow 
        |> Array.skip(2) 
        |> Array.filter(String.IsNullOrWhiteSpace >> not)
        |> Array.map languageForXUITag    
    else
        [||]

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
    let localizedStrings = 
        t 
        |> Array.skip(2)
        |> Array.truncate(languageTags.Length)

    if (localizedStrings.Length = languageTags.Length) then
        localizedStrings 
        |> Array.truncate(languageTags.Length) 
        |> Array.zip languageTags
        |> Array.map(makeCardsForLanguageTags(cardKey, lessonId))
    else
        [||]

let internal extractXUITabDelimited(stringLines: string array, lessonId: int): CardRecord array = 
    let tabDelimited = stringLines |> Array.map(fun t -> t.Split('\t'))
    let dataRows = tabDelimited |> Array.tail

    // get country tags, and convert them into language tags
    let languageTags = languageTagsForHeaderRow(tabDelimited |> Array.head)

    dataRows
    |> Array.mapi(generateCardsForRow(lessonId, languageTags))
    |> Array.collect id

let ExtractKOF2002(path: string) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }

    let stringFilePath = Path.Combine(path, FixPathSeps @"data\strings.txt")
    let extractedCards = 
        extractXUITabDelimited(File.ReadAllLines(stringFilePath, Text.Encoding.UTF8), lessonEntry.ID)

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    }

let ExtractKOF98(path: string) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }

    let stringFilePath = Path.Combine(path, FixPathSeps @"data\strings.txt")
    let extractedCards = 
        extractXUITabDelimited(File.ReadAllLines(stringFilePath, Text.Encoding.UTF8), lessonEntry.ID)

    // filter out empty cards.
    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    }
