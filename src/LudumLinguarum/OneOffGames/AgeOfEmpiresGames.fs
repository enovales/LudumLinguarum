module AgeOfEmpiresGames

open AudioPipeline
open LLDatabase
open LLUtils
open LudumLinguarumPlugins
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq

let private formattingRegexes = 
    [|
        new Regex(@"\<.*\>")
    |]

let private stripFormattingTags(s: string) = 
    Array.fold (fun (a: string)(r: Regex) -> r.Replace(a, "")) s formattingRegexes

let private trimString(s: string) = s.Trim()
let private replaceEscapeChars(s: string) = s.Replace(@"\r", " ").Replace(@"\n", " ")
let private consolidateWhitespace(s: string) = Regex.Replace(s, @"\s+", " ")

let private sanitizePipeline =
    stripFormattingTags >> replaceEscapeChars >> consolidateWhitespace >> trimString

/////////////////////////////////////////////////////////////////////////////
// Age of Empires II HD

// language directories need to be remapped to actual language codes
let private aoe2HDLanguages = [| "br"; "de"; "en"; "es"; "fr"; "it"; "jp"; "ko"; "nl"; "ru"; "zh" |]
let private aoe2HDLanguagesToIso = 
    [|
        ("br", "br"); ("de", "de"); ("en", "en"); ("es", "es"); ("fr", "fr"); ("it", "it")
        ("jp", "ja"); ("ko", "ko"); ("nl", "nl"); ("ru", "ru"); ("zh", "zh-CN")
    |]
    |> Map.ofArray

let internal createLesson(i: int)(title: string): LessonRecord = 
    {
        LessonRecord.ID = i;
        Name = title
    }

let private aoe2MassageText(v: string) = 
    let removeStrings = [| "<b>"; "<B>"; "<i>"; "<I>"; "<GREY>"; "<BLUE>"; "<PURPLE>"; "<GREEN>"; "<AQUA>"; "<RED>"; "<YELLOW>"; "<ORANGE>" |]
    let trimmed = v.TrimStart([| '"' |]).TrimEnd([| '"' |])
    let removed = removeStrings |> Array.fold(fun (s: string)(toRemove: string) -> s.Replace(toRemove, "")) trimmed
    removed.Replace(@"\n", Environment.NewLine).Trim()


let private extractAOE2HDHistoryFile(fn: string, lid: int, lang: string) = 
    AssemblyResourceTools.createCardRecordForStrings(lid, "", lang, "masculine")([| ("card", aoe2MassageText(File.ReadAllText(fn, Encoding.UTF8))) |] |> Map.ofArray)

let private getHistoryLessonName(fn: string) = 
    Path.GetFileNameWithoutExtension(fn).Replace("-utf8", "")

let private extractAOE2HDHistoryFiles(path: string) = 
    let languagesAndPathsByLessonNames = 
        aoe2HDLanguages
        |> Array.collect (fun l -> Directory.GetFiles(Path.Combine(path, FixPathSeps(@"resources\" + l + @"\strings\history"))) |> Array.map (fun p -> (aoe2HDLanguagesToIso.[l], p)))
        |> Array.groupBy (fun (_, p) -> getHistoryLessonName(p))

    let (lessonNames, languagesAndPaths) = languagesAndPathsByLessonNames |> Array.unzip
    let lessons = 
        lessonNames
        |> Array.mapi(fun i n -> { LessonRecord.ID = i; Name = n })
        
    let languagesAndPathsByLessons = 
        languagesAndPaths
        |> Array.zip lessons

    languagesAndPathsByLessons
    |> Array.map (fun (lesson, lps) -> (lesson, lps |> Array.map(fun (lang, path) -> extractAOE2HDHistoryFile(path, lesson.ID, lang)) |> Array.collect id))

let private aoe2HDCampaignStringRegex = new Regex(@"^(\S+)\s+(.+)$")
let private extractAOE2HDCampaignStrings(path: string) = 
    let lesson = { LessonRecord.ID = 0; Name = "Game Text" }
    let trimLineComments(s: string) = 
        match s.IndexOf("//") with
        | i when i >= 0 -> s.Substring(0, i).Trim()
        | _ -> s

    let cardsForLanguage(lang: string) = 
        let p = Path.Combine(path, FixPathSeps(@"resources\" + lang + @"\strings\key-value\key-value-strings-utf8.txt"))
        let lines = 
            File.ReadAllLines(p, Encoding.UTF8)
            |> Array.map(fun l -> l.Trim())
            |> Array.filter(fun l -> not(l.StartsWith("//")) && not(String.IsNullOrWhiteSpace(l)))
            |> Array.map trimLineComments

        let kvPairForLine(l: string) = 
            let m = aoe2HDCampaignStringRegex.Match(l)
            if m.Success then
                [| (m.Groups.[1].Value, aoe2MassageText(m.Groups.[2].Value)) |]
            else
                [||]

        lines
        |> Array.collect kvPairForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, "", aoe2HDLanguagesToIso.[lang], "masculine")


    [| (lesson, aoe2HDLanguages |> Array.collect cardsForLanguage) |]

let private aoe2HDLauncherStringRegex = new Regex(@"^([^=]+)=(.+)$")
let private extractAOE2HDLauncherStrings(path: string) = 
    let lesson = { LessonRecord.ID = 0; Name = "Launcher Text" }
    let cardsForLocaleIni(lang: string) = 
        let p = Path.Combine(path, FixPathSeps(@"resources\_launcher\" + lang + @"\locale.ini"))
        let lines =
            File.ReadAllLines(p)
            |> Array.map (fun l -> l.Trim())
            |> Array.filter(fun l -> not(l.StartsWith("[")) && not (String.IsNullOrWhiteSpace(l)))
        let kvPairForLine(l: string) = 
            let m = aoe2HDLauncherStringRegex.Match(l)
            if m.Success then
                [| (m.Groups.[1].Value, m.Groups.[2].Value.TrimStart([| '"' |]).TrimEnd([| '"' |])) |]
            else
                [||]

        lines
        |> Array.collect kvPairForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, "", lang, "masculine")

    [| (lesson, aoe2HDLanguages |> Array.collect cardsForLocaleIni) |]

let ExtractAOE2HD(path: string) = 
    // Extract strings from the "history" files, each of which is just a single long passage.
    let allLessonGroupsAndCards = 
        [|
            extractAOE2HDHistoryFiles(path)
            extractAOE2HDCampaignStrings(path)
            extractAOE2HDLauncherStrings(path)
        |]

    // Get all lessons, and renumber them. Then apply that remapping to all of the cards.
    let originalLessonsToRemappedLessons = 
        allLessonGroupsAndCards
        |> Array.collect(fun lgc -> lgc |> Array.map(fst))
        |> Array.mapi(fun i l -> (l, { l with ID = i }))

    let originalLessonIDsToRemappedLessonIDs = 
        originalLessonsToRemappedLessons
        |> Array.map(fun (original, remapped) -> (original.ID, remapped.ID))
        |> Map.ofArray

    let remapLessonForCardGroup(mapping: Map<int, int>)(lcg: LessonRecord * CardRecord array) = 
        let (lesson, cards) = lcg
        let newLessonID = mapping |> Map.find(lesson.ID)
        cards |> Array.map(fun c -> { c with LessonID = newLessonID })

    let allCards = 
        allLessonGroupsAndCards
        |> Array.collect(fun lgc -> lgc |> Array.map(remapLessonForCardGroup(originalLessonIDsToRemappedLessonIDs)))
        |> Array.collect id

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = originalLessonsToRemappedLessons |> Array.map snd
        LudumLinguarumPlugins.ExtractedContent.cards = allCards
    }

/////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////
// Age of Empires III

type internal Aoe3XmlEntry =
    {
        LocId: string
        Text: string
        Speaker: string
        SoundFileName: string option
    }

let private mapAoe3LanguageName (languageName: string) : string =
    match languageName.Trim() with
    | "English" -> "en"
    | "French" -> "fr"
    | "German" -> "de"
    | "Spanish" -> "es"
    | "Italian" -> "it"
    | other -> failwith ("unrecognized language name in Language name attribute: " + other)

/// <summary>
/// Parses an Age of Empires III stringtable XML element, returning the detected language tag
/// and all string entries (including optional gamecharacter speaker and soundfilename attributes).
/// </summary>
let internal parseAoe3StringTableXml (xel: XElement) : string * Aoe3XmlEntry array =
    let languageNode =
        xel.Descendants()
        |> Seq.find (fun t -> t.Name.LocalName = "Language")
    let languageTag =
        languageNode.Attribute(XName.Get("name")).Value
        |> mapAoe3LanguageName

    let tryAttr (el: XElement) (name: string) =
        let attr = el.Attribute(XName.Get(name))
        if isNull attr || String.IsNullOrWhiteSpace(attr.Value) then None
        else Some (attr.Value.Trim())

    let entries =
        xel.Descendants()
        |> Seq.filter (fun t -> t.Name.LocalName = "String")
        |> Seq.choose (fun el ->
            match tryAttr el "_locID" with
            | None -> None
            | Some locId ->
                let text = sanitizePipeline (el.Value.Trim())
                if String.IsNullOrWhiteSpace(text) then
                    None
                else
                    Some
                        {
                            LocId = locId
                            Text = text
                            Speaker = tryAttr el "gamecharacter" |> Option.defaultValue ""
                            SoundFileName = tryAttr el "soundfilename"
                        }
        )
        |> Seq.groupBy (fun e -> e.LocId)
        |> Seq.map (fun (_, group) ->
            // Prefer an entry with a soundfilename if the same _locID appears twice
            group
            |> Seq.sortBy (fun e -> if e.SoundFileName.IsSome then 0 else 1)
            |> Seq.head
        )
        |> Array.ofSeq

    (languageTag, entries)

/// <summary>
/// Indexes loose audio files (.mp3 / .wav) under Age of Empires III's `bin\Sound` directory
/// by lowercase filename stem (prioritizing `dialog`, `Chats`, `cinematics`, and `taunts`).
/// </summary>
let internal indexAoe3SoundFiles (rootPath: string) : Map<string, string> =
    let soundRoot = Path.Combine(rootPath, FixPathSeps @"bin\Sound")
    if not (Directory.Exists(soundRoot)) then
        Map.empty
    else
        let priorityForPath (fullPath: string) =
            let rel = fullPath.Substring(soundRoot.Length).ToLowerInvariant()
            if rel.Contains("dialog") then 0
            elif rel.Contains("chats") then 1
            elif rel.Contains("cinematics") then 2
            elif rel.Contains("taunts") then 3
            else 4

        Directory.GetFiles(soundRoot, "*.*", SearchOption.AllDirectories)
        |> Array.filter (fun p ->
            let ext = Path.GetExtension(p).ToLowerInvariant()
            ext = ".mp3" || ext = ".wav"
        )
        |> Array.sortBy (fun p -> (priorityForPath p, p.ToLowerInvariant()))
        |> Array.fold (fun acc p ->
            let stem = Path.GetFileNameWithoutExtension(p).ToLowerInvariant()
            if acc |> Map.containsKey stem then acc
            else acc |> Map.add stem p
        ) Map.empty

let ExtractAOE3WithMedia (path: string) (_args: string array) (mediaCtx: MediaExtractionContext) =
    let stringSources =
        [|
            (FixPathSeps @"bin\data\stringtable.xml", "Base Game")
            (FixPathSeps @"bin\data\stringtablex.xml", "Warchiefs Expansion")
            (FixPathSeps @"bin\data\stringtabley.xml", "Asian Dynasties Expansion")
            (FixPathSeps @"bin\data\unithelpstrings.xml", "Base Game Unit Help")
            (FixPathSeps @"bin\data\unithelpstringsx.xml", "Warchiefs Expansion Unit Help")
            (FixPathSeps @"bin\data\unithelpstringsy.xml", "Asian Dynasties Expansion Unit Help")
        |]

    let pathsToLessons =
        stringSources
        |> Array.mapi (fun i (p, lessonName) -> (Path.Combine(path, p), { LessonRecord.ID = i; Name = lessonName }))

    let parsedPerLesson =
        pathsToLessons
        |> Array.map (fun (xmlPath, lesson) ->
            if File.Exists(xmlPath) then
                use fs = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                let xel = XElement.Load(fs)
                let (lang, entries) = parseAoe3StringTableXml xel
                (lesson, Some (lang, entries))
            else
                (lesson, None)
        )

    let rawCardsWithSound =
        parsedPerLesson
        |> Array.collect (fun (lesson, parsedOpt) ->
            match parsedOpt with
            | None -> [||]
            | Some (lang, entries) ->
                entries
                |> Array.map (fun entry ->
                    let card =
                        {
                            CardRecord.ID = 0
                            LessonID = lesson.ID
                            Text = entry.Text
                            Gender = "masculine"
                            Key = entry.LocId + "masculine"
                            GenderlessKey = entry.LocId
                            KeyHash = 0
                            GenderlessKeyHash = 0
                            SoundResource = ""
                            ImageResource = ""
                            Speaker = entry.Speaker
                            LanguageTag = lang
                            Reversible = true
                        }
                    (card, entry.SoundFileName)
                )
        )

    let finalCards =
        match mediaCtx.ExtractAudio, mediaCtx.MediaStore with
        | true, Some store ->
            let soundFiles = indexAoe3SoundFiles path
            let workItems =
                rawCardsWithSound
                |> Array.choose (fun (card, soundNameOpt) ->
                    soundNameOpt
                    |> Option.bind (fun sn ->
                        let key = Path.GetFileNameWithoutExtension(sn).ToLowerInvariant()
                        soundFiles
                        |> Map.tryFind key
                        |> Option.map (fun audioPath ->
                            {
                                AudioIngestWorkItem.Key = key
                                LanguageTag = card.LanguageTag
                                SourceDescription = audioPath
                                InputFilePath = audioPath
                                SubsongIndex = None
                            }
                        )
                    )
                )
                |> Array.distinctBy (fun w -> w.Key)

            mediaCtx.Log(sprintf "Age of Empires III audio extraction: matched %d unique voice clips from %d indexed sound files." workItems.Length soundFiles.Count)
            let (hashBySoundKey, succeeded, errors) =
                ingestExtractedAudioFileBatch store mediaCtx.AudioTools mediaCtx.AudioOptions mediaCtx.Log workItems
            mediaCtx.Log(sprintf "Age of Empires III audio extraction complete: ingested %d clips (%d errors)." succeeded errors)

            rawCardsWithSound
            |> Array.map (fun (card, soundNameOpt) ->
                match soundNameOpt with
                | Some sn ->
                    let key = Path.GetFileNameWithoutExtension(sn).ToLowerInvariant()
                    match hashBySoundKey |> Map.tryFind key with
                    | Some hash -> { card with SoundResource = hash }
                    | None -> card
                | None -> card
            )
        | _ ->
            rawCardsWithSound |> Array.map fst

    let lessons = parsedPerLesson |> Array.map fst

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = finalCards
    }

let ExtractAOE3(path: string) =
    ExtractAOE3WithMedia path [||] MediaExtractionContext.None
