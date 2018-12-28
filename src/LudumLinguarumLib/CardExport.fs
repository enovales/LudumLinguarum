module CardExport

open LLDatabase
open LudumLinguarumPlugins
open System
open System.IO
open System.Text.RegularExpressions

type CommonExportTarget = 
    | Anki
    | SuperMemo
    | Mnemosyne
    | AnyMemo

type CommonExporterConfiguration =
    {
        Target: CommonExportTarget
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

type ICardWriter = 
    interface
        abstract member WriteCard: string -> unit
    end

type private NonPaginatingWriter(otw: TextWriter, exportBasePath: string) = 
    let streamWriter = new StreamWriter(exportBasePath)

    do otw.WriteLine("Writing to [" + exportBasePath + "]")
    interface IDisposable with
        member this.Dispose() = streamWriter.Dispose()

    interface ICardWriter with
        member this.WriteCard(line: String) = streamWriter.WriteLine(line)

type private SuperMemoPaginatingWriter(otw: TextWriter, exportBasePath: string) = 
    let mutable lineCounter = 0
    let mutable fileCounter = 1
    let mutable streamWriter = new StreamWriter(exportBasePath)

    do otw.WriteLine("Writing to [" + exportBasePath + "]")
    interface IDisposable with
        member this.Dispose() = streamWriter.Dispose()

    interface ICardWriter with
        member this.WriteCard(line: String) = 
            if (lineCounter >= 99) then
                streamWriter.Dispose()

                let newFileName = 
                    Path.GetDirectoryName(exportBasePath) + 
                    new string([|Path.DirectorySeparatorChar|]) + 
                    Path.GetFileNameWithoutExtension(exportBasePath) + 
                    "_" + 
                    fileCounter.ToString("000") + 
                    Path.GetExtension(exportBasePath)

                streamWriter <- new StreamWriter(newFileName)
                otw.WriteLine("Writing to [" + newFileName + "]")

                fileCounter <- fileCounter + 1
                lineCounter <- 0

            streamWriter.WriteLine(line)
            lineCounter <- lineCounter + 1

type CommonExporter(iPluginManager: IPluginManager, outputTextWriter: TextWriter, 
                    llDatabase: LLDatabase, config: CommonExporterConfiguration) = 

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

    member private this.RunLessonExport(cardWriter: ICardWriter, applicationTarget: CommonExportTarget)(lesson: LessonRecord) = 
        let writeCard(r: CardRecord, p: CardRecord) = 
            let recogText = r.Text.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")
            let prodText = p.Text.Replace("\t", "    ").Replace("\r", "").Replace("\n", "")
            let reverseText = 
                match (applicationTarget, r.Reversible, p.Reversible) with
                | (CommonExportTarget.Anki, true, true) -> "\ty"
                | (CommonExportTarget.Anki, _, _) -> "\t"
                | (CommonExportTarget.SuperMemo, _, _) -> ""
                | (CommonExportTarget.Mnemosyne, _, _) -> "\t"
                | (CommonExportTarget.AnyMemo, _, _) -> "\t"

            cardWriter.WriteCard(recogText + "\t" + prodText + reverseText)

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

    member private this.RunGameExport(lesson: string option, lessonRegex: string option) = 
        let filterLessonsNoWildcard(name: string)(l: LessonRecord) = 
            (l.Name = name)
        let filterLessonsWildcard(name: string)(l: LessonRecord) = 
            let rootName = name.Substring(0, name.IndexOf('*'))
            (l.Name.StartsWith(rootName, StringComparison.CurrentCultureIgnoreCase))
        let filterLessonsRegex(regex: Regex)(l: LessonRecord) = 
            (regex.IsMatch(l.Name))

        // Prefer the regex over using the name. If no name is specified, then 
        // export all lessons for the game.
        let lessonFilter = 
            match (lesson, lessonRegex) with
            | (_, Some(regex)) -> filterLessonsRegex(new Regex(regex))
            | (Some(l), _) when not(l.Contains("*")) -> filterLessonsNoWildcard(l)
            | (Some(l), _) -> filterLessonsWildcard(l)
            | _ -> (fun _ -> true)

        let lessonsToExport = llDatabase.Lessons |> Array.filter lessonFilter
        let reportLessonExport(l: LessonRecord) =
            outputTextWriter.WriteLine("Exporting lesson [" + l.Name + "]")
            l

        let cardWriter: ICardWriter = 
            match config.Target with
            | CommonExportTarget.SuperMemo -> new SuperMemoPaginatingWriter(outputTextWriter, config.ExportPath) :> ICardWriter
            | CommonExportTarget.Anki 
            | CommonExportTarget.Mnemosyne 
            | CommonExportTarget.AnyMemo -> new NonPaginatingWriter(outputTextWriter, config.ExportPath) :> ICardWriter
        let disposableCardWriter = cardWriter :?> IDisposable

        lessonsToExport |> Array.iter(reportLessonExport >> this.RunLessonExport(cardWriter, config.Target))
        disposableCardWriter.Dispose() |> ignore

    member this.RunExportAction() = 
        this.RunGameExport(config.LessonToExport, config.LessonRegexToExport)
        ()