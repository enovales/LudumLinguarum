module PuzzleQuest2

open LLDatabase
open System
open System.IO
open System.Xml.Linq

/// <summary>
/// Generates a key-value pair from a Text XML element in a PQ2 localization
/// file, which will later be transformed into a card.
/// </summary>
/// <param name="keyRoot">root to add onto key</param>
/// <param name="el">the XML element</param>
let internal generateKVForTextElement(keyRoot: string)(el: XElement) = 
    (keyRoot + el.Attribute(XName.Get("tag")).Value, el.Value)

/// <summary>
/// Generates a set of cards for a single localization XML file.
/// </summary>
/// <param name="lessonID">lesson ID to use for generated cards</param>
/// <param name="language">the language of this content</param>
/// <param name="keyRoot">root to use for keys in the generated cards -- should correspond to the file name from which the content was pulled</param>
/// <param name="xmlContent">the XML content to parse</param>
let internal generateCardsForXml(lessonID: int, language: string, keyRoot: string)(xmlContent: string) = 
    use stringReader = new StringReader(xmlContent)
    let xel = XElement.Load(stringReader)

    xel.Descendants(XName.Get("TextLibrary"))
    |> Array.ofSeq
    |> Array.collect(fun t -> t.Descendants(XName.Get("Text")) |> Array.ofSeq)
    |> Array.map(generateKVForTextElement(keyRoot))
    |> Map.ofArray
    |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language)

/// <summary>
/// Generates a set of cards for a single asset zip from PQ2.
/// </summary>
/// <param name="languageMap">the map of directory names to language codes</param>
/// <param name="lessonsMap">the map of directory paths (inside the zip) to lesson records</param>
/// <param name="zipPath">path to the zip file to process</param>
let internal generateCardsForAssetZip(languageMap: Map<string, string>, lessonsMap: Map<string, LessonRecord>)(zipPath: string): CardRecord array = 
    let generateCardsForLessons(language: string)(lessonDir: string, lesson: LessonRecord) = 
        // TODO: open all XML files under the directory, and read the contents
        let files: string array = [| |]
        files
        |> Seq.map(fun f -> "")
        |> Seq.collect(generateCardsForXml(lesson.ID, language, lesson.Name))
        |> Array.ofSeq

    let generateCardsForLanguage(dir: string, language: string): CardRecord array = 
        lessonsMap
        |> Map.toArray
        |> Array.map (fun (lessonDir, lesson) -> (Path.Combine(dir, lessonDir), lesson))
        |> Array.collect(generateCardsForLessons(language))

    languageMap
    |> Map.toArray
    |> Array.collect generateCardsForLanguage

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let ExtractPuzzleQuest2(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)
    let languageMap = 
        [|
            ("English", "en")
            ("French", "fr")
            ("German", "de")
            ("Italian", "it")
            ("Spanish", "es")
        |]
        |> Map.ofArray

    // create lessons for each of the subdirectories in the asset zips
    let lessonsMap = 
        [|
            ("", "Game Text")
            ("Conversations", "Conversations")
            ("Levels", "Levels")
            ("NIS", "NIS")
            ("pc", "PC")
            ("Tutorials", "Tutorials")
        |]
        |> Array.map(fun (k, v) -> (k, configuredLessonCreator(v)))
        |> Map.ofArray

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let cardKey(c: CardRecord) = c.Key
    [|
        "Patch1.zip"
        "Assets.zip"
    |]
    |> Array.collect((fun p -> Path.Combine(path, p)) >> generateCardsForAssetZip(languageMap, lessonsMap))
    |> Array.distinctBy cardKey
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()