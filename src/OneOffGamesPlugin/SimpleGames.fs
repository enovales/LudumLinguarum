module SimpleGames

open AssemblyResourceTools
open LLDatabase
open OneOffGamesUtils
open System
open System.IO
open System.Xml.Linq

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

let private generateMagicalDropVStringMap(cleanedXml: string): Map<string, string> = 
    Map.empty

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

    // replace unescaped entities and other garbage
    let fixupRawXml(x: string) = 
        x.Replace("&", "&amp;").Replace("<string placeholder>", "")

    let generateCardsForLocalization(locPath: string, lang: string) = 
        let cleanedXml = File.ReadAllText(locPath) |> fixupRawXml
        use stringReader = new StringReader(cleanedXml)
        let xel = XElement.Load(stringReader)
        xel.DescendantNodes
        [||]

    ()