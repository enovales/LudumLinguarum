﻿module CardExport

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Text.RegularExpressions

type AnkiExporterConfiguration =
    {
        GameToExport: string
        LessonToExport: string option
        LessonRegexToExport: string option
        ExportPath: string
        RecognitionLanguage: string
        ProductionLanguage: string
        RecognitionLengthLimit: int option
        ProductionLengthLimit: int option
        RecognitionWordLimit: int option
        ProductionWordLimit: int option
    }

let internal mkLengthFilter(lo: int option) = 
    match lo with
    | Some(l) -> (fun (c: CardRecord) -> c.Text.Length <= l)
    | _ -> (fun (_: CardRecord) -> true)

let internal mkWordCountFilter(lo: int option) = 
    match lo with
    | Some(l) -> (fun (c: CardRecord) -> (c.Text.ToCharArray() |> Array.sumBy(fun c -> if Char.IsWhiteSpace(c) then 1 else 0)) <= (l - 1))
    | _ -> (fun (_: CardRecord) -> true)

type AnkiExporter(iPluginManager: IPluginManager, outputTextWriter: TextWriter, 
                  llDatabase: LLDatabase, config: AnkiExporterConfiguration) = 

    let recognitionLengthFilter = mkLengthFilter(config.RecognitionLengthLimit)
    let productionLengthFilter = mkLengthFilter(config.ProductionLengthLimit)
    let recognitionWordCountFilter = mkWordCountFilter(config.RecognitionWordLimit)
    let productionWordCountFilter = mkWordCountFilter(config.ProductionWordLimit)

    member private this.GenerateRecognitionAndProductionCardSets
        (lesson: LessonRecord, recognitionLanguage: string, productionLanguage: string) = 
        // get the recognition and production cards for this lesson
        let recognitionCards = llDatabase.CardsFromLessonAndLanguageTag(lesson, recognitionLanguage)
        let productionCards = llDatabase.CardsFromLessonAndLanguageTag(lesson, productionLanguage)

        // Check if one of the card sets has populated genders, and the other has none. If this
        // is the case, then replicate the non-gendered cards using the populated gender set. This
        // will make it easier to match up the recognition and production cards later.
        let recognitionGenders = recognitionCards |> Array.distinctBy(fun t -> t.Gender) |> Array.map(fun t -> t.Gender)
        let productionGenders = productionCards |> Array.distinctBy(fun t -> t.Gender) |> Array.map(fun t -> t.Gender)

        let duplicateIntoGenders(c: CardRecord array, g: string array) = 
            c |> Array.collect(fun t -> recognitionGenders |> Array.map(fun u -> { t with Gender = u }))

        // Only support unpacking many-to-1 or 1-to-many. Anything else
        // can't be disambiguated without actual domain knowledge about
        // these languages.
        match (recognitionGenders.Length, productionGenders.Length) with
        | (a, b) when a > b -> 
            (recognitionCards, duplicateIntoGenders(productionCards, recognitionGenders))
        | (a, b) when a < b -> 
            (duplicateIntoGenders(recognitionCards, productionGenders), productionCards)
        | (a, b) when a = b -> (recognitionCards, productionCards)
        | _ -> failwith "can't disambiguate m-to-n language pairs"

    member private this.RunLessonExport(game: GameRecord, sw: StreamWriter)(lesson: LessonRecord) = 
        let writeCard(r: CardRecord, p: CardRecord) = 
            let recogText = r.Text.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")
            let prodText = p.Text.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")
            let reverseText = 
                if (r.Reversible && p.Reversible) then
                    "\ty"
                else
                    "\t"
            sw.WriteLine(recogText + "\t" + prodText + reverseText)

        let (recognitionCards, productionCards) = 
            this.GenerateRecognitionAndProductionCardSets(lesson, config.RecognitionLanguage, config.ProductionLanguage)

        // filter and then group cards by their key hash and gender, and then match them up with each other.
        let recognitionByHashKeys = 
            recognitionCards 
            |> Array.filter(recognitionLengthFilter)
            |> Array.filter(recognitionWordCountFilter)
            |> Array.groupBy(fun t -> (t.GenderlessKeyHash, t.Gender))
        let productionByHashKeys = 
            productionCards 
            |> Array.filter(productionLengthFilter)
            |> Array.filter(productionWordCountFilter)
            |> Array.groupBy(fun t -> (t.GenderlessKeyHash, t.Gender)) |> Map.ofArray

        // Match up recognition cards with the same key and gender string with
        // production cards with the same key and gender string.
        let pairsByHashKeys = 
            recognitionByHashKeys |> 
            Array.map(fun (k, cards) -> 
                (cards, productionByHashKeys |> Map.tryFind(k))) |> 
            Array.filter(fun (_, prod) -> prod.IsSome) |> 
            Array.map(fun (cards, prod) -> (cards, prod.Value))
        
        pairsByHashKeys
        |> Array.iter(fun (rs, ps) -> 
            Array.zip rs ps
            |> Array.iter(fun (r, p) -> writeCard(r, p)))
        ()

    member private this.RunGameExport(game: string, lesson: string option, lessonRegex: string option) = 
        let filterGame(gid: int)(l: LessonRecord) = 
            (l.GameID = gid)
        let filterLessonsNoWildcard(gid: int, name: string)(l: LessonRecord) = 
            filterGame(gid)(l) && (l.Name = name)
        let filterLessonsWildcard(gid: int, name: string)(l: LessonRecord) = 
            let rootName = name.Substring(0, name.IndexOf('*'))
            filterGame(gid)(l) && (l.Name.StartsWith(rootName, StringComparison.CurrentCultureIgnoreCase))
        let filterLessonsRegex(gid: int, regex: Regex)(l: LessonRecord) = 
            filterGame(gid)(l) && (regex.IsMatch(l.Name))

        match (llDatabase.Games |> Array.tryFind(fun t -> t.Name = game)) with
        | Some(g) -> 
            // Prefer the regex over using the name. If no name is specified, then 
            // export all lessons for the game.
            let lessonFilter = 
                match (lesson, lessonRegex) with
                | (_, Some(regex)) -> filterLessonsRegex(g.ID, new Regex(regex))
                | (Some(l), _) when not(l.Contains("*")) -> filterLessonsNoWildcard(g.ID, l)
                | (Some(l), _) -> filterLessonsWildcard(g.ID, l)
                | _ -> filterGame(g.ID)

            let lessonsToExport = llDatabase.Lessons |> Array.filter lessonFilter
            let reportLessonExport(l: LessonRecord) =
                outputTextWriter.WriteLine("Exporting lesson [" + l.Name + "]")
                l

            use outStream = new StreamWriter(config.ExportPath)
            lessonsToExport |> Array.iter(reportLessonExport >> this.RunLessonExport(g, outStream))
        | _ -> 
            outputTextWriter.WriteLine("Couldn't find entry for game '" + game + "'")
        ()

    member this.RunExportAction() = 
        this.RunGameExport(config.GameToExport, config.LessonToExport, config.LessonRegexToExport)
        ()