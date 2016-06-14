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
    let others = xel.Elements() |> Seq.filter(fun e -> (e.Name <> tagName) && (nodeNameToLanguage.ContainsKey(e.Name.LocalName)))
    let getValueForLanguageElement(langEl: XElement): string option = 
        if String.IsNullOrWhiteSpace(langEl.Value) then
            // get "Text" descendant, if available
            let textName = XName.Get("Text", "http://www.firaxis.com")
            langEl.Elements(textName) |> Seq.tryHead |> Option.map(fun o -> o.Value)
        else
            Some(langEl.Value)

    let kvSeqForTextElement(e: XElement) = 
        match getValueForLanguageElement(e) with
        | Some(v) -> [| (nodeNameToLanguage.[e.Name.LocalName], (tag, v)) |]
        | _ -> [||]

    others |> Seq.collect kvSeqForTextElement

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

let internal civ4Content = 
    [|
        (@"Assets\XML\Text\CIV4DiplomacyText.xml", "Diplomacy Text")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_Bonuses.xml", "Civilopedia Bonuses")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_BuildingsProjects.xml", "Civilopedia Buildings and Projects")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_CivicsReligion.xml", "Civilopedia Civics and Religions")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_CivLeaders.xml", "Civilopedia Leaders")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_Concepts.xml", "Civilopedia Concepts")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_Techs.xml", "Civilopedia Techs")
        (@"Assets\XML\Text\CIV4GameText_Civilopedia_Units.xml", "Civilopedia Units")
        (@"Assets\XML\Text\CIV4GameText_Help.xml", "Help")
        (@"Assets\XML\Text\CIV4GameText_Misc1.xml", "Misc")
        (@"Assets\XML\Text\CIV4GameText_New.xml", "New")
        (@"Assets\XML\Text\CIV4GameText_Strategy.xml", "Strategy")
        (@"Assets\XML\Text\CIV4GameTextInfos.xml", "Infos")
        (@"Assets\XML\Text\CIV4GameTextInfos_Cities.xml", "Infos Cities")
        (@"Assets\XML\Text\CIV4GameTextInfos_GreatPeople.xml", "Infos Great People")
        (@"Assets\XML\Text\CIV4GameTextInfos_Objects.xml", "Infos Objects")
    |]

let internal civ4WarlordsContent = 
    [|
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords.xml", "Warlords")
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Changed.xml", "Warlords Changed")
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Civ4Changed.xml", "Warlords Civ 4 Changed")
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Civilopedia.xml", "Warlords Civilopedia")
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Diplomacy.xml", "Warlords Diplomacy")
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Objects.xml", "Warlords Objects")
        (@"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Strategy.xml", "Warlords Strategy")
    |]

let internal civ4BtsContent = 
    [|
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS.xml", "Beyond the Sword")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS_Fixed.xml", "Beyond the Sword Fixed")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS_FourthRoundTranslation.xml", "Beyond the Sword Fourth Round Translation")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS_PatchText.xml", "Beyond the Sword Patch Text")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_Cities_BTS.xml", "Beyond the Sword Cities")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_Civilopedia_BTS.xml", "Beyond the Sword Civilopedia")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_DiplomacyText_BTS.xml", "Beyond the Sword Diplomacy")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_Events_BTS.xml", "Beyond the Sword Events")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameText_Objects_BTS.xml", "Beyond the Sword Objects")
        (@"Beyond the Sword\Assets\XML\Text\CIV4GameTextChanged_BTS.xml", "Beyond the Sword Changed")
    |]

let internal civ4ColonizationContent = 
    [|
        (@"Assets\XML\Text\CIV4GameText_BTS_PatchText.xml", "Patch Text")
        (@"Assets\XML\Text\CIV4GameText_Colonization.xml", "Colonization")
        (@"Assets\XML\Text\CIV4GameText_Colonization_DiplomacyText.xml", "Diplomacy Text")
        (@"Assets\XML\Text\CIV4GameText_Colonization_Events.xml", "Events")
        (@"Assets\XML\Text\CIV4GameText_Colonization_LastMinute.xml", "Last Minute")
        (@"Assets\XML\Text\CIV4GameText_Colonization_Objects.xml", "Objects")
        (@"Assets\XML\Text\CIV4GameText_Colonization_PatchMod.xml", "Patch Mod")
        (@"Assets\XML\Text\CIV4GameText_Colonization_Pedia.xml", "Civilopedia")
        (@"Assets\XML\Text\CIV4GameText_Colonization_Strategy.xml", "Strategy")
        (@"Assets\XML\Text\CIV4GameTextInfos_Objects_Original.xml", "Infos Objects Original")
        (@"Assets\XML\Text\CIV4GameTextInfos_Original.xml", "Infos Original")
    |]

let ExtractCiv4(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)

    // create lessons for each of the localization files
    let contentPathsToLessons = 
        civ4Content
        |> Array.map(fun (k, v) -> (Path.Combine(path, k), configuredLessonCreator(v)))

    let cardsForContent(p: string, l: LessonRecord) = 
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)

    contentPathsToLessons
    |> Array.collect cardsForContent
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractCiv4Warlords(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)

    // create lessons for each of the localization files
    let contentPathsToLessons = 
        Array.concat([| civ4Content; civ4WarlordsContent |])
        |> Array.map(fun (k, v) -> (Path.Combine(path, k), configuredLessonCreator(v)))

    let cardsForContent(p: string, l: LessonRecord) = 
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)

    contentPathsToLessons
    |> Array.collect cardsForContent
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractCiv4BeyondTheSword(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)

    // create lessons for each of the localization files
    let contentPathsToLessons = 
        Array.concat([| civ4Content; civ4WarlordsContent; civ4BtsContent |])
        |> Array.map(fun (k, v) -> (Path.Combine(path, k), configuredLessonCreator(v)))

    let cardsForContent(p: string, l: LessonRecord) = 
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)

    contentPathsToLessons
    |> Array.collect cardsForContent
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractCiv4Colonization(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)

    // create lessons for each of the localization files
    let contentPathsToLessons = 
        civ4ColonizationContent
        |> Array.map(fun (k, v) -> (Path.Combine(path, k), configuredLessonCreator(v)))

    let cardsForContent(p: string, l: LessonRecord) = 
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)

    contentPathsToLessons
    |> Array.collect cardsForContent
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
