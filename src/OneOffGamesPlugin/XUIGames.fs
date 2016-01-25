﻿namespace XUIGames

open LLDatabase
open System
open System.IO

type XUI = 
    static member private ExtractXUITabDelimited(path: string, l: LessonRecord): CardRecord array = 
        let stringLines = File.ReadAllLines(path, Text.Encoding.UTF8)
        let tabDelimited = stringLines |> Array.map(fun t -> t.Split('\t'))
        let headerRow = tabDelimited |> Array.head
        let dataRows = tabDelimited |> Array.tail
        // get language tags
        let languageTags = headerRow |> Array.skip(2) |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t)))
        dataRows |> Array.mapi(fun rowIndex t ->
            let key = t.[0]
            let category = t.[1]
            languageTags |> Array.mapi(fun i u -> 
                if (t.Length > (2 + i)) then
                    let cardKey = rowIndex.ToString() + key + category;
                    Some({
                            CardRecord.Gender = "";
                            GenderlessKey = cardKey;
                            GenderlessKeyHash = 0;
                            CardRecord.ID = 0;
                            Key = cardKey;
                            KeyHash = 0;
                            LanguageTag = languageTags.[i];
                            LessonID = l.ID;
                            Reversible = true;
                            SoundResource = "";
                            Text = t.[2 + i]
                    })
                else
                    None
                )) |> Array.collect(fun t -> t) |> Array.collect(fun t -> t |> Option.toArray)

    static member ExtractKOF2002(path: string, db: LLDatabase, gameEntryWithId: GameRecord, args: string[]) = 
        let lessonEntry = {
            LessonRecord.GameID = gameEntryWithId.ID;
            ID = 0;
            Name = "Game Text"
        }
        let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

        let stringFilePath = Path.Combine(path, @"data\strings.txt")
        let extractedCards = XUI.ExtractXUITabDelimited(stringFilePath, lessonEntryWithId)

        // filter out empty cards.
        let allCards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

        db.CreateOrUpdateCards(allCards)

        ()

    static member ExtractKOF98(path: string, db: LLDatabase, gameEntryWithId: GameRecord, args: string[]) = 
        let lessonEntry = {
            LessonRecord.GameID = gameEntryWithId.ID;
            ID = 0;
            Name = "Game Text"
        }
        let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

        let stringFilePath = Path.Combine(path, @"data\strings.txt")
        let extractedCards = XUI.ExtractXUITabDelimited(stringFilePath, lessonEntryWithId)

        // filter out empty cards.
        let allCards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

        db.CreateOrUpdateCards(allCards)

        ()