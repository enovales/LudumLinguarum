module AgeOfEmpiresGames

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

let private aoe2HDLanguages = [| "br"; "de"; "en"; "es"; "fr"; "it"; "jp"; "ko"; "nl"; "ru"; "zh" |]

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let private extractAOE2HDHistoryFile(fn: string, lid: int, lang: string) = 
    AssemblyResourceTools.createCardRecordForStrings(lid, "", lang, "masculine")([| ("card", File.ReadAllText(fn, Encoding.UTF8)) |] |> Map.ofArray)

let private getHistoryLessonName(fn: string) = 
    Path.GetFileNameWithoutExtension(fn).Replace("-utf8", "")

let private extractAOE2HDHistoryFiles(path: string, db: LLDatabase, g: GameRecord) = 
    aoe2HDLanguages
    |> Array.collect (fun l -> Directory.GetFiles(Path.Combine(path, @"resources\" + l + @"\strings\history")) |> Array.map (fun p -> (l, p)))
    |> Array.groupBy (fun (_, p) -> getHistoryLessonName(p))
    |> Array.map (fun (lessonName, lp) -> (createLesson(g.ID, db)(lessonName), lp))
    |> Array.collect (fun (lesson, lps) -> lps |> Array.map(fun (lang, path) -> extractAOE2HDHistoryFile(path, lesson.ID, lang)))
    |> Array.collect id

let private aoe2HDCampaignStringRegex = new Regex(@"^(\S+)\s+(.+)$")
let private extractAOE2HDCampaignStrings(path: string, db: LLDatabase, g: GameRecord) = 
    let lesson = createLesson(g.ID, db)("Game Text")
    let cardsForLanguage(lang: string) = 
        let p = Path.Combine(path, @"resources\" + lang + @"\strings\key-value\key-value-strings-utf8.txt")
        let lines = 
            File.ReadAllLines(p, Encoding.UTF8)
            |> Array.map(fun l -> l.Trim())
            |> Array.filter(fun l -> not(l.StartsWith("//")) && not(String.IsNullOrWhiteSpace(l)))
        let kvPairForLine(l: string) = 
            let m = aoe2HDCampaignStringRegex.Match(l)
            if m.Success then
                [| (m.Groups.[1].Value, m.Groups.[2].Value) |]
            else
                [||]

        lines
        |> Array.collect kvPairForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, "", lang, "masculine")


    aoe2HDLanguages
    |> Array.collect cardsForLanguage

let private aoe2HDLauncherStringRegex = new Regex(@"^([^=]+)=(.+)$")
let private extractAOE2HDLauncherStrings(path: string, db: LLDatabase, g: GameRecord) = 
    let lesson = createLesson(g.ID, db)("Launcher Text")
    let cardsForLocaleIni(lang: string) = 
        let p = Path.Combine(path, @"launcher_res\" + lang + @"\locale.ini")
        let lines =
            File.ReadAllLines(p)
            |> Array.map (fun l -> l.Trim())
            |> Array.filter(fun l -> not(l.StartsWith("[")) && not (String.IsNullOrWhiteSpace(l)))
        let kvPairForLine(l: string) = 
            let m = aoe2HDLauncherStringRegex.Match(l)
            if m.Success then
                [| (m.Groups.[1].Value, m.Groups.[2].Value) |]
            else
                [||]

        lines
        |> Array.collect kvPairForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, "", lang, "masculine")

    aoe2HDLanguages
    |> Array.collect cardsForLocaleIni

let ExtractAOE2HD(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    // Extract strings from the "history" files, each of which is just a single long passage.
    [|
        extractAOE2HDHistoryFiles(path, db, g)
        extractAOE2HDCampaignStrings(path, db, g)
        extractAOE2HDLauncherStrings(path, db, g)
    |]
    |> Array.collect id
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
