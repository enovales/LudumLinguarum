module PillarsOfEternity

open ICSharpCode.SharpZipLib.Zip
open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq

type internal ExtractedData = 
    {
        Id: int
        DefaultText: string option
        FemaleText: string option
    }

let formattingCodeRegexes = 
    [|
        new Regex(@"\[[^\]]+?\]")
        new Regex(@"\{[^\d\}][^\}]*?\}")
    |]

let rec internal stripFormattingCodes(v: string): string = 
    let firstMatch = formattingCodeRegexes |> Array.tryFind(fun r -> r.IsMatch(v))

    match firstMatch with
    | Some(fm) -> 
        let m = fm.Match(v)
        stripFormattingCodes(v.Remove(m.Index, m.Length))
    | _ -> v

/// <summary>
/// Generates a tuple containing the entry ID, default/masculine text, and female text,
/// which will later be transformed into a card.
/// </summary>
/// <param name="el">the XML element</param>
let internal extractDataFromEntry(entryElement: XElement): ExtractedData = 
    let nonWhitespaceFilter(s: string) = 
        let trimmed = s.Trim()
        not(String.IsNullOrWhiteSpace(trimmed)) && (trimmed <> "\"\"")

    let id = Int32.Parse((entryElement.Descendants(XName.Get("ID")) |> Seq.head).Value)
    let defaultText = entryElement.Descendants(XName.Get("DefaultText")) |> Seq.map (fun t -> t.Value |> stripFormattingCodes) |> Seq.filter nonWhitespaceFilter |> Seq.tryHead
    let femaleText = entryElement.Descendants(XName.Get("FemaleText")) |> Seq.map (fun t -> t.Value |> stripFormattingCodes) |> Seq.filter nonWhitespaceFilter |> Seq.tryHead

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
let internal generateCardsForAssetPath(languageMap: Map<string, string>, lessonsMap: Map<string, LessonRecord>, excludeFiles: string seq)(assetPath: string): CardRecord array = 
    let generateCardsForLessons(language: string)(lessonDir: string, lesson: LessonRecord) = 
        // open all XML files under the directory, and read the contents
        let stringTableFiles = 
            Directory.GetFiles(lessonDir, "*.stringtable", SearchOption.AllDirectories)
            |> Array.filter (fun fn -> not(excludeFiles |> Seq.contains(Path.GetFileNameWithoutExtension(fn))))
            |> Seq.ofArray

        let getStreamForFile(f: string) = 
            new MemoryStream(File.ReadAllBytes(f))

        // For each lesson directory, get all string table files, and zip them with a stream containing their contents.
        // Then, generate cards, providing a key root of the lesson name, plus the relative path (from the lesson directory) to
        // the string table file.
        stringTableFiles
        |> Seq.zip(stringTableFiles |> Seq.map getStreamForFile)
        |> Seq.collect(fun (str, u) -> 
            let result = generateCardsForXmlStream(lesson.ID, language, lesson.Name + u.Substring(lessonDir.Length))(str)
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

let internal createLesson(db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let ExtractPillarsOfEternity(path: string, db: LLDatabase, args: string array) = 
    let configuredLessonCreator = createLesson(db)
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

    let fileExclusions = 
        [|
        |]

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key
    [|
        @"PillarsOfEternity_Data\data_expansion2"
        @"PillarsOfEternity_Data\data_expansion1"
        @"PillarsOfEternity_Data\data"
    |]
    |> Array.map(fun p -> Path.Combine(path, p))
    |> Array.filter Directory.Exists
    |> Array.collect(generateCardsForAssetPath(languageMap, lessonsMap, fileExclusions))
    |> Array.distinctBy cardKeyAndLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()

let ExtractTormentTidesOfNumenera(path: string, db: LLDatabase, args: string array) = 
    let configuredLessonCreator = createLesson(db)
    let languageMap = 
        [|
            (@"localized\en", "en")
            (@"localized\fr", "fr")
            (@"localized\de", "de")
            (@"localized\it", "it")
            (@"localized\es", "es")
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

    let fileExclusions = 
        [|
            // from 'game'
            "backercontent"; "backerepitaphnames"; "backerepitaphs"; "backertombstonenames"

            // from 'conversations'
            "adam_test"; "adams_test"
            "dafdsdfa"; "debugging"; "dumb"; "follow-up_error_test"; "follow-up_test"; "letstry"; "loop_test"
            "mytest"; "newrednode"; "newtest"; "outoftestfolder"; "paola_test"; "paola_test2"; "paolatest"; "paolatest140327"; "performtasktest"
            "question_node_test"; "seconddebugging"; "thirddebugging"; "thomas_test"; "tidenodetest"; "tidetest"; "whathappenswithperforce"

            // from 'quests'
            "paola_questendnodetest"; "questendnodetest"; "test_kutallu_gate"; "test_quest"; "test_quest_02_crucial"
            // from 'quests\test_quests'
            "end_state_example"; "end_state_example_with_end_nodes"; "i_am_a_quest"; "jesse_test_quest"; "stupid_quest"
        |]

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key
    [|
        @"WIN\TidesOfNumenera_Data\StreamingAssets\data"
    |]
    |> Array.map(fun p -> Path.Combine(path, p))
    |> Array.filter Directory.Exists
    |> Array.collect(generateCardsForAssetPath(languageMap, lessonsMap, fileExclusions))
    |> Array.distinctBy cardKeyAndLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()

let ExtractTyranny(path: string, db: LLDatabase, args: string array) = 
    let configuredLessonCreator = createLesson(db)
    let languageMap = 
        [|
            (@"localized\en", "en")
            (@"localized\fr", "fr")
            (@"localized\de", "de")
            (@"localized\es", "es")
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

    let fileExclusions = 
        [|
            // from 'conversations\test'
            "test_conversation_anims_female_base"
            "test_conversation_anims_male_disfavored_salute"
            "test_conversation_anims_male_generic_greeter"
            "test_conversation_anims_scarlet_chorus_salute"
            "test_conversation_anims_tied_up"
            "test_skill_trainer"
        |]

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key
    [|
        @"Data\data\exported"
    |]
    |> Array.map(fun p -> Path.Combine(path, p))
    |> Array.filter Directory.Exists
    |> Array.collect(generateCardsForAssetPath(languageMap, lessonsMap, fileExclusions))
    |> Array.distinctBy cardKeyAndLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()
