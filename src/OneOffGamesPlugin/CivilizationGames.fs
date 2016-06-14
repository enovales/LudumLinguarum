module CivilizationGames

open LLDatabase
open System
open System.IO
open System.Xml.Linq

let internal nodeNameToLanguage = 
    [|
        ("English", "en")
        ("French", "fr")
        ("German", "de")
        ("Italian", "it")
        ("Spanish", "es")
    |]
    |> Map.ofArray

/// <summary>
/// Generates a set of key-value pairs for a text element, one for each language.
/// </summary>
/// <param name="xel">the parent TEXT element</param>
let internal generateKVsForTextElement(xel: XElement) = 
    let tagName = XName.Get("Tag", "http://www.firaxis.com")
    let tag = (xel.Elements(tagName) |> Seq.head).Value
    let others = xel.Elements() |> Seq.filter(fun e -> e.Name <> tagName)
    let getValueForLanguageElement(langEl: XElement) = 
        if String.IsNullOrWhiteSpace(langEl.Value) then
            // get "Text" descendant
            let textName = XName.Get("Text", "http://www.firaxis.com")
            (langEl.Elements(textName) |> Seq.head).Value
        else
            langEl.Value

    others |> Seq.map(fun e -> (nodeNameToLanguage.[e.Name.LocalName], (tag, getValueForLanguageElement(e))))

/// <summary>
/// Generates cards for the top-level node in a Puzzle Quest-engine localization file.
/// </summary>
/// <param name="lessonID">lesson ID to use</param>
/// <param name="keyRoot">key root for generating cards</param>
/// <param name="xel">the XML root element</param>
let internal generateCardsForXElement(lessonID: int, keyRoot: string)(xel: XElement) = 
    let createLtvMap(ltv: string * (string * string)) = 
        let (_, (t, v)) = ltv
        (t, v)

    // the element is the TextLibrary node
    xel.Elements(XName.Get("TEXT", "http://www.firaxis.com"))
    |> Seq.collect generateKVsForTextElement
    |> Seq.groupBy(fun (l, (t, v)) -> l)
    |> Seq.collect(fun (language, ltvs) -> ltvs |> Seq.map createLtvMap |> Map.ofSeq |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "masculine"))
    |> Array.ofSeq

let internal generateCardsForXmlStream(lessonID: int, keyRoot: string)(stream: Stream) = 
    let xel = XElement.Load(stream)
    generateCardsForXElement(lessonID, keyRoot)(xel)

/// <summary>
/// Generates a set of cards for a single localization XML file.
/// </summary>
/// <param name="lessonID">lesson ID to use for generated cards</param>
/// <param name="language">the language of this content</param>
/// <param name="keyRoot">root to use for keys in the generated cards -- should correspond to the file name from which the content was pulled</param>
/// <param name="xmlContent">the XML content to parse</param>
let internal generateCardsForXml(lessonID: int, keyRoot: string)(xmlContent: string) = 
    use stringReader = new StringReader(xmlContent)
    let xel = XElement.Load(stringReader)
    generateCardsForXElement(lessonID, keyRoot)(xel)

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let ExtractCiv4(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)

    // create lessons for each of the localization files
    let contentPathsToLessons = 
        [|
            ("CIV4DiplomacyText.xml", "Diplomacy Text")
            ("CIV4GameText_Civilopedia_Bonuses.xml", "Civilopedia Bonuses")
            ("CIV4GameText_Civilopedia_BuildingsProjects.xml", "Civilopedia Buildings and Projects")
            ("CIV4GameText_Civilopedia_CivicsReligion.xml", "Civilopedia Civics and Religions")
            ("CIV4GameText_Civilopedia_CivLeaders.xml", "Civilopedia Leaders")
            ("CIV4GameText_Civilopedia_Concepts.xml", "Civilopedia Concepts")
            ("CIV4GameText_Civilopedia_Techs.xml", "Civilopedia Techs")
            ("CIV4GameText_Civilopedia_Units.xml", "Civilopedia Units")
            ("CIV4GameText_Help.xml", "Help")
            ("CIV4GameText_Misc1.xml", "Misc")
            ("CIV4GameText_New.xml", "New")
            ("CIV4GameText_Strategy.xml", "Strategy")
            ("CIV4GameTextInfos.xml", "Infos")
            ("CIV4GameTextInfos_Cities.xml", "Infos Cities")
            ("CIV4GameTextInfos_GreatPeople.xml", "Infos Great People")
            ("CIV4GameTextInfos_Objects.xml", "Infos Objects")
        |]
        |> Array.map(fun (k, v) -> (Path.Combine(path, @"Assets\XML\Text\" + k), configuredLessonCreator(v)))

    let cardsForContent(p: string, l: LessonRecord) = 
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)

    contentPathsToLessons
    |> Array.collect cardsForContent
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
