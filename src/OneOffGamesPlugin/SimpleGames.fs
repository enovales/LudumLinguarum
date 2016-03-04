module SimpleGames

open AssemblyResourceTools
open LLDatabase
open OneOffGamesUtils
open System
open System.IO
open System.Xml.Linq

(***************************************************************************)
(************************** Skulls of the Shogun ***************************)
(***************************************************************************)
let ExtractSkullsOfTheShogun(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
    OneOffGamesUtils.ExtractStringsFromAssemblies(path, Path.Combine(path, "SkullsOfTheShogun.exe"), "SkullsOfTheShogun.resources.dll", "TBS.Properties.AllResources", "gametext", lessonEntryWithId.ID)
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

(***************************************************************************)
(***************************** Magical Drop V ******************************)
(***************************************************************************)
let internal sanitizeMagicalDropVXml(x: string) = 
    x.Replace("&", "&amp;").Replace("<string placeholder>", "")

let internal sanitizeMagicalDropVFormatStrings(x: string) = 
    x.Replace("\\n", " ")

let internal generateMagicalDropVStringMap(cleanedXml: string): Map<string, string> = 
    use stringReader = new StringReader(cleanedXml)
    let xel = XElement.Load(stringReader)
    xel.Descendants(XName.Get("content")) 
    |> Array.ofSeq 
    |> Array.collect(fun t -> t.Descendants() |> Array.ofSeq) 
    |> Array.map (fun t -> (t.Name.LocalName, t.Value |> sanitizeMagicalDropVFormatStrings)) |> Map.ofArray

let ExtractMagicalDropV(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let filePaths = 
        [|
            @"localization\localization_de-DE.xml"
            @"localization\localization_eng-US.xml"
            @"localization\localization_es-ES.xml"
            @"localization\localization_fr-FR.xml"
            @"localization\localization_it-IT.xml"
            @"localization\localization_ja-JP.xml"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let filePathsAndLanguages = 
        [| "de"; "en"; "es"; "fr"; "it"; "ja" |] |> Array.zip(filePaths)

    let lessonStoryEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Story Text"
    }
    let lessonUIEntry = { lessonStoryEntry with Name = "UI Text" }
    let lessonStoryEntryWithId = { lessonStoryEntry with ID = db.CreateOrUpdateLesson(lessonStoryEntry) }
    let lessonUIEntryWithId = { lessonUIEntry with ID = db.CreateOrUpdateLesson(lessonUIEntry) }

    let generateCardsForLocalization(locPath: string, lang: string) = 
        let cleanedXml = File.ReadAllText(locPath) |> sanitizeMagicalDropVXml
        let (storyStrings, nonStoryStrings) = 
            generateMagicalDropVStringMap(cleanedXml)
            |> Map.partition(fun k t -> (k.StartsWith("story", StringComparison.InvariantCultureIgnoreCase) || k.StartsWith("ID_storyintro")))

        [|
            storyStrings |> AssemblyResourceTools.createCardRecordForStrings(lessonStoryEntryWithId.ID, "storytext", lang)
            nonStoryStrings |> AssemblyResourceTools.createCardRecordForStrings(lessonUIEntryWithId.ID, "uitext", lang)
        |] |> Array.concat

    filePathsAndLanguages |> Array.collect generateCardsForLocalization
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
