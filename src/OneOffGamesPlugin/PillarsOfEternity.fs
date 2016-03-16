module PillarsOfEternity

open ICSharpCode.SharpZipLib.Zip
open LLDatabase
open System
open System.IO
open System.Text
open System.Xml.Linq

type internal ExtractedData = 
    {
        Id: int
        DefaultText: string option
        FemaleText: string option
    }

/// <summary>
/// Generates a tuple containing the entry ID, default/masculine text, and female text,
/// which will later be transformed into a card.
/// </summary>
/// <param name="el">the XML element</param>
let internal extractDataFromEntry(entryElement: XElement): ExtractedData = 
    let nonWhitespaceFilter s = not(String.IsNullOrWhiteSpace(s))
    let id = Int32.Parse((entryElement.Descendants(XName.Get("ID")) |> Seq.head).Value)
    let defaultText = entryElement.Descendants(XName.Get("DefaultText")) |> Seq.map (fun t -> t.Value) |> Seq.filter nonWhitespaceFilter |> Seq.tryHead
    let femaleText = entryElement.Descendants(XName.Get("FemaleText")) |> Seq.map (fun t -> t.Value) |> Seq.filter nonWhitespaceFilter |> Seq.tryHead

    { Id = id; DefaultText = defaultText; FemaleText = femaleText }

let internal generateCardsForEntry(keyRoot: string, lessonID: int, language: string)(exData: ExtractedData): CardRecord array =
    let defaultCard =
        exData.DefaultText
        |> Option.map (fun t -> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "masculine")([| (exData.Id.ToString(), t) |] |> Map.ofArray))
        |> Option.toArray
        |> Array.collect id

    let femaleCard =
        exData.FemaleText
        |> Option.map (fun t -> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "feminine")([| (exData.Id.ToString(), t) |] |> Map.ofArray))
        |> Option.toArray
        |> Array.collect id

    Array.concat([| defaultCard; femaleCard |])

let internal generateCardsForXElement(lessonID: int, language: string, keyRoot: string)(xel: XElement) = 
    // get the Name element under StringTableFile, and bolt that onto the key root
    let name = (xel.Descendants(XName.Get("Name")) |> Seq.head).Value

    // then, get each Entry descendant. for each Entry, get ID, DefaultText, and FemaleText, and generate
    // cards for the latter two.
    let entries = (xel.Descendants(XName.Get("Entry")))
    let entryData = entries |> Seq.map extractDataFromEntry

    // the element is the TextLibrary node
    entries
    |> Seq.map extractDataFromEntry
    |> Seq.collect(generateCardsForEntry(keyRoot, lessonID, language))

let internal generateCardsForXmlStream(lessonID: int, language: string, keyRoot: string)(stream: Stream) = 
    let xel = XElement.Load(stream)
    generateCardsForXElement(lessonID, language, keyRoot)(xel)

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
    generateCardsForXElement(lessonID, language, keyRoot)(xel)

/// <summary>
/// Generates a set of cards for a single asset path from Pillars of Eternity.
/// </summary>
/// <param name="languageMap">the map of directory names to language codes</param>
/// <param name="lessonsMap">the map of directory paths (inside the zip) to lesson records</param>
/// <param name="zipPath">path to the zip file to process</param>
let internal generateCardsForAssetPath(languageMap: Map<string, string>, lessonsMap: Map<string, LessonRecord>)(assetPath: string): CardRecord array = 
    let generateCardsForLessons(language: string)(lessonDir: string, lesson: LessonRecord) = 
        // open all XML files under the directory, and read the contents
        let stringTableFiles = 
            Directory.GetFiles(lessonDir, "*.stringtable", SearchOption.AllDirectories)
            |> Seq.ofArray

        let getStreamForFile(f: string) = 
            new MemoryStream(File.ReadAllBytes(f))

        // For each lesson directory, get all string table files, and zip them with a stream containing their contents.
        // Then, generate cards, providing a key root of the lesson name, plus the relative path (from the game directory) to
        // the string table file.
        stringTableFiles
        |> Seq.zip(stringTableFiles |> Seq.map getStreamForFile)
        |> Seq.collect(fun (str, u) -> 
            let result = generateCardsForXmlStream(lesson.ID, language, lesson.Name + u.Substring(assetPath.Length))(str)
            str.Dispose()
            result
            )
        |> Array.ofSeq

    let generateCardsForLanguage(dir: string, language: string): CardRecord array = 
        lessonsMap
        |> Map.toArray
        |> Array.map (fun (lessonDir, lesson) -> (Path.Combine(Path.Combine(assetPath, dir), lessonDir), lesson))
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

let ExtractPillarsOfEternity(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)
    let languageMap = 
        [|
            (@"localized\en", "en")
            (@"localized\fr", "fr")
            (@"localized\de", "de")
            (@"localized\it", "it")
            (@"localized\es", "es")
            (@"localized\ko", "ko")
            (@"localized\pl", "pl")
            (@"localized\ru", "ru")
        |]
        |> Map.ofArray

    // create lessons for each of the subdirectories in the asset zips
    let lessonsMap = 
        [|
            (@"text\game", "Game Text")
            (@"text\conversations", "Conversations")
            (@"text\quests", "Quests")
        |]
        |> Array.map(fun (k, v) -> (k, configuredLessonCreator(v)))
        |> Map.ofArray

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key
    [|
        @"PillarsOfEternity_Data\data_expansion2"
        @"PillarsOfEternity_Data\data_expansion1"
        @"PillarsOfEternity_Data\data"
    |]
    |> Array.map(fun p -> Path.Combine(path, p))
    |> Array.filter Directory.Exists
    |> Array.collect(generateCardsForAssetPath(languageMap, lessonsMap))
    |> Array.distinctBy cardKeyAndLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()