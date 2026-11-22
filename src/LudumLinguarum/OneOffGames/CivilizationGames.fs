module CivilizationGames

open AudioPipeline
open LLDatabase
open LLUtils
open LudumLinguarumPlugins
open System
open System.Collections.Generic
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

let private nodeNameToLanguage = 
    [|
        ("English", "en")
        ("French", "fr")
        ("German", "de")
        ("Italian", "it")
        ("Spanish", "es")
    |]
    |> Map.ofArray

let private civ4formattingCodeRegex = new Regex("\[[^\]]+?\]")
let rec internal stripCiv4FormattingCodes(value: string): string = 
    let r = civ4formattingCodeRegex.Match(value)
    match r.Success with
    | true -> stripCiv4FormattingCodes(value.Remove(r.Index, r.Length))
    | _ -> value


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
        | Some(v) -> [| (nodeNameToLanguage.[e.Name.LocalName], (tag, stripCiv4FormattingCodes(v))) |]
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

let internal createLesson(i: int)(title: string): LessonRecord = 
    {
        LessonRecord.ID = i;
        Name = title
    }

let internal civ4Content = 
    [|
        (FixPathSeps @"Assets\XML\Text\CIV4DiplomacyText.xml", "Diplomacy Text")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_Bonuses.xml", "Civilopedia Bonuses")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_BuildingsProjects.xml", "Civilopedia Buildings and Projects")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_CivicsReligion.xml", "Civilopedia Civics and Religions")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_CivLeaders.xml", "Civilopedia Leaders")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_Concepts.xml", "Civilopedia Concepts")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_Techs.xml", "Civilopedia Techs")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Civilopedia_Units.xml", "Civilopedia Units")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Help.xml", "Help")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Misc1.xml", "Misc")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_New.xml", "New")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Strategy.xml", "Strategy")
        (FixPathSeps @"Assets\XML\Text\CIV4GameTextInfos.xml", "Infos")
        (FixPathSeps @"Assets\XML\Text\CIV4GameTextInfos_Cities.xml", "Infos Cities")
        (FixPathSeps @"Assets\XML\Text\CIV4GameTextInfos_GreatPeople.xml", "Infos Great People")
        (FixPathSeps @"Assets\XML\Text\CIV4GameTextInfos_Objects.xml", "Infos Objects")
    |]

let internal civ4WarlordsContent = 
    [|
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords.xml", "Warlords")
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Changed.xml", "Warlords Changed")
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Civ4Changed.xml", "Warlords Civ 4 Changed")
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Civilopedia.xml", "Warlords Civilopedia")
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Diplomacy.xml", "Warlords Diplomacy")
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Objects.xml", "Warlords Objects")
        (FixPathSeps @"Warlords\Assets\XML\Text\CIV4GameText_Warlords_Strategy.xml", "Warlords Strategy")
    |]

let internal civ4BtsContent = 
    [|
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS.xml", "Beyond the Sword")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS_Fixed.xml", "Beyond the Sword Fixed")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS_FourthRoundTranslation.xml", "Beyond the Sword Fourth Round Translation")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_BTS_PatchText.xml", "Beyond the Sword Patch Text")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_Cities_BTS.xml", "Beyond the Sword Cities")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_Civilopedia_BTS.xml", "Beyond the Sword Civilopedia")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_DiplomacyText_BTS.xml", "Beyond the Sword Diplomacy")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_Events_BTS.xml", "Beyond the Sword Events")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameText_Objects_BTS.xml", "Beyond the Sword Objects")
        (FixPathSeps @"Beyond the Sword\Assets\XML\Text\CIV4GameTextChanged_BTS.xml", "Beyond the Sword Changed")
    |]

let internal civ4ColonizationContent = 
    [|
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_BTS_PatchText.xml", "Patch Text")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization.xml", "Colonization")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_DiplomacyText.xml", "Diplomacy Text")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_Events.xml", "Events")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_LastMinute.xml", "Last Minute")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_Objects.xml", "Objects")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_PatchMod.xml", "Patch Mod")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_Pedia.xml", "Civilopedia")
        (FixPathSeps @"Assets\XML\Text\CIV4GameText_Colonization_Strategy.xml", "Strategy")
        (FixPathSeps @"Assets\XML\Text\CIV4GameTextInfos_Objects_Original.xml", "Infos Objects Original")
        (FixPathSeps @"Assets\XML\Text\CIV4GameTextInfos_Original.xml", "Infos Original")
    |]

/// <summary>
/// Parses CIV4TechInfos.xml to map quote localization tags (e.g. TXT_KEY_TECH_*_QUOTE)
/// to their 2D sound script IDs (e.g. AS2D_TECH_*).
/// </summary>
let internal parseTechQuoteSoundScriptMap (techInfosXml: XElement) : Map<string, string> =
    techInfosXml.Descendants()
    |> Seq.filter (fun e -> e.Name.LocalName = "TechInfo")
    |> Seq.choose (fun tech ->
        let quoteOpt =
            tech.Elements()
            |> Seq.tryFind (fun e -> e.Name.LocalName = "Quote")
            |> Option.map (fun e -> e.Value.Trim())
            |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
        let soundOpt =
            tech.Elements()
            |> Seq.tryFind (fun e -> e.Name.LocalName = "Sound")
            |> Option.map (fun e -> e.Value.Trim())
            |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
        match quoteOpt, soundOpt with
        | Some q, Some s -> Some (q, s)
        | _ -> None
    )
    |> Map.ofSeq

/// <summary>
/// Parses Audio2DScripts.xml to map ScriptID (e.g. AS2D_TECH_*) to SoundID (e.g. SND_TECH_*).
/// </summary>
let internal parseAudio2DScriptsMap (scriptsXml: XElement) : Map<string, string> =
    scriptsXml.Descendants()
    |> Seq.filter (fun e -> e.Name.LocalName = "Script2DSound")
    |> Seq.choose (fun script ->
        let scriptIdOpt =
            script.Elements()
            |> Seq.tryFind (fun e -> e.Name.LocalName = "ScriptID")
            |> Option.map (fun e -> e.Value.Trim())
            |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
        let soundIdOpt =
            script.Elements()
            |> Seq.tryFind (fun e -> e.Name.LocalName = "SoundID")
            |> Option.map (fun e -> e.Value.Trim())
            |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
        match scriptIdOpt, soundIdOpt with
        | Some sid, Some snd -> Some (sid, snd)
        | _ -> None
    )
    |> Map.ofSeq

/// <summary>
/// Parses AudioDefines.xml to map SoundID (e.g. SND_TECH_*) to relative asset filename (e.g. Sounds/Tech/Tech_Mysticism).
/// </summary>
let internal parseAudioDefinesMap (definesXml: XElement) : Map<string, string> =
    definesXml.Descendants()
    |> Seq.filter (fun e -> e.Name.LocalName = "SoundData")
    |> Seq.choose (fun sd ->
        let soundIdOpt =
            sd.Elements()
            |> Seq.tryFind (fun e -> e.Name.LocalName = "SoundID")
            |> Option.map (fun e -> e.Value.Trim())
            |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
        let filenameOpt =
            sd.Elements()
            |> Seq.tryFind (fun e -> e.Name.LocalName = "Filename")
            |> Option.map (fun e -> e.Value.Trim())
            |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
        match soundIdOpt, filenameOpt with
        | Some sid, Some fn -> Some (sid, fn)
        | _ -> None
    )
    |> Map.ofSeq

let private mergeMaps (baseMap: Map<'K, 'V>) (overrideMap: Map<'K, 'V>) : Map<'K, 'V> =
    overrideMap |> Map.fold (fun acc k v -> acc |> Map.add k v) baseMap

/// <summary>
/// Resolves quote localization tags (TXT_KEY_*) to actual audio file paths across one or more
/// Civ IV asset roots (later roots override earlier ones for expansions).
/// </summary>
let internal resolveCiv4QuoteAudioFiles (assetRoots: string array) : Map<string, string> =
    let loadXmlIfExists (filePath: string) =
        if File.Exists(filePath) then
            use fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Some (XElement.Load(fs))
        else
            None

    let mutable quoteToScript = Map.empty<string, string>
    let mutable scriptToSound = Map.empty<string, string>
    let mutable soundToRelPath = Map.empty<string, string>

    for root in assetRoots do
        match loadXmlIfExists (Path.Combine(root, FixPathSeps @"XML\Technologies\CIV4TechInfos.xml")) with
        | Some xml -> quoteToScript <- mergeMaps quoteToScript (parseTechQuoteSoundScriptMap xml)
        | None -> ()
        match loadXmlIfExists (Path.Combine(root, FixPathSeps @"XML\Audio\Audio2DScripts.xml")) with
        | Some xml -> scriptToSound <- mergeMaps scriptToSound (parseAudio2DScriptsMap xml)
        | None -> ()
        match loadXmlIfExists (Path.Combine(root, FixPathSeps @"XML\Audio\AudioDefines.xml")) with
        | Some xml -> soundToRelPath <- mergeMaps soundToRelPath (parseAudioDefinesMap xml)
        | None -> ()

    let tryFindAudioFile (relPath: string) =
        let normalizedRel =
            relPath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
        let extensions = [| ".mp3"; ".wav"; ".ogg"; "" |]
        // Search from newest expansion root back to base game Assets
        assetRoots
        |> Array.rev
        |> Array.tryPick (fun root ->
            extensions
            |> Array.tryPick (fun ext ->
                let candidate = Path.Combine(root, normalizedRel + ext)
                if File.Exists(candidate) then Some candidate else None
            )
        )

    quoteToScript
    |> Map.toSeq
    |> Seq.choose (fun (quoteTag, scriptId) ->
        scriptToSound
        |> Map.tryFind scriptId
        |> Option.bind (fun soundId -> soundToRelPath |> Map.tryFind soundId)
        |> Option.bind tryFindAudioFile
        |> Option.map (fun audioPath -> (quoteTag, audioPath))
    )
    |> Map.ofSeq

let private parseAudioLanguageArg (args: string array) : string =
    let prefix = "--audio-language="
    args
    |> Array.tryPick (fun a ->
        if a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) then
            let v = a.Substring(prefix.Length).Trim().ToLowerInvariant()
            if String.IsNullOrWhiteSpace(v) then None else Some v
        else
            None
    )
    |> Option.defaultValue "en"

let private extractCiv4FamilyWithMedia
    (path: string)
    (contentSpecs: (string * string) array)
    (assetRoots: string array)
    (args: string array)
    (mediaCtx: MediaExtractionContext)
    : ExtractedContent =
    let contentPathsToLessons =
        contentSpecs
        |> Array.mapi (fun i (k, v) -> (Path.Combine(path, k), createLesson i v))
        |> Array.filter (fun (p, _) -> File.Exists(p))

    let cardsForContent (p: string, l: LessonRecord) =
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)
        |> Array.map (fun c -> (rootName, c))

    let taggedCards =
        contentPathsToLessons
        |> Array.collect cardsForContent

    let finalCards =
        match mediaCtx.ExtractAudio, mediaCtx.MediaStore with
        | true, Some store ->
            let audioLanguage = parseAudioLanguageArg args
            let quoteAudioMap = resolveCiv4QuoteAudioFiles assetRoots
            mediaCtx.Log(sprintf "Civ IV audio extraction (%s): resolved %d tech quote audio files." audioLanguage quoteAudioMap.Count)

            let ingestedByPath = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            let mutable ingestedCount = 0
            let mutable errorCount = 0

            let attachAudio (rootName: string, card: CardRecord) =
                if not (String.Equals(card.LanguageTag, audioLanguage, StringComparison.OrdinalIgnoreCase)) then
                    card
                else
                    let xmlTag =
                        if card.GenderlessKey.StartsWith(rootName, StringComparison.Ordinal) then
                            card.GenderlessKey.Substring(rootName.Length)
                        else
                            card.GenderlessKey

                    match quoteAudioMap |> Map.tryFind xmlTag with
                    | None -> card
                    | Some audioPath ->
                        match ingestedByPath.TryGetValue(audioPath) with
                        | true, hash -> { card with SoundResource = hash }
                        | false, _ ->
                            match ingestExtractedAudioFile store mediaCtx.AudioTools mediaCtx.AudioOptions card.LanguageTag audioPath audioPath None with
                            | Ok hash ->
                                ingestedByPath.[audioPath] <- hash
                                ingestedCount <- ingestedCount + 1
                                { card with SoundResource = hash }
                            | Error err ->
                                errorCount <- errorCount + 1
                                if errorCount <= 5 then
                                    mediaCtx.Log(sprintf "Warning: failed to ingest Civ IV audio '%s': %s" audioPath err)
                                card

            let updated = taggedCards |> Array.map attachAudio
            mediaCtx.Log(sprintf "Civ IV audio extraction complete: ingested %d audio files (%d errors)." ingestedCount errorCount)
            updated
        | _ ->
            taggedCards |> Array.map snd

    let (_, lessons) = contentPathsToLessons |> Array.unzip

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = finalCards
    }

let ExtractCiv4WithMedia (path: string) (args: string array) (mediaCtx: MediaExtractionContext) =
    let assetRoots = [| Path.Combine(path, "Assets") |]
    extractCiv4FamilyWithMedia path civ4Content assetRoots args mediaCtx

let ExtractCiv4WarlordsWithMedia (path: string) (args: string array) (mediaCtx: MediaExtractionContext) =
    let assetRoots =
        [|
            Path.Combine(path, "Assets")
            Path.Combine(path, FixPathSeps @"Warlords\Assets")
        |]
    extractCiv4FamilyWithMedia path (Array.concat [| civ4Content; civ4WarlordsContent |]) assetRoots args mediaCtx

let ExtractCiv4BeyondTheSwordWithMedia (path: string) (args: string array) (mediaCtx: MediaExtractionContext) =
    let assetRoots =
        [|
            Path.Combine(path, "Assets")
            Path.Combine(path, FixPathSeps @"Warlords\Assets")
            Path.Combine(path, FixPathSeps @"Beyond the Sword\Assets")
        |]
    extractCiv4FamilyWithMedia path (Array.concat [| civ4Content; civ4WarlordsContent; civ4BtsContent |]) assetRoots args mediaCtx

let ExtractCiv4(path: string) =
    ExtractCiv4WithMedia path [||] MediaExtractionContext.None

let ExtractCiv4Warlords(path: string) =
    ExtractCiv4WarlordsWithMedia path [||] MediaExtractionContext.None

let ExtractCiv4BeyondTheSword(path: string) =
    ExtractCiv4BeyondTheSwordWithMedia path [||] MediaExtractionContext.None

let ExtractCiv4Colonization(path: string) = 
    // create lessons for each of the localization files
    let contentPathsToLessons = 
        civ4ColonizationContent
        |> Array.mapi(fun i (k, v) -> (Path.Combine(path, k), createLesson(i)(v)))

    let cardsForContent(p: string, l: LessonRecord) = 
        let rootName = Path.GetFileNameWithoutExtension(p)
        use fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        generateCardsForXmlStream(l.ID, rootName)(fs)

    let cards = 
        contentPathsToLessons
        |> Array.collect cardsForContent

    let (_, lessons) = contentPathsToLessons |> Array.unzip

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
