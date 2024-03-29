module ParadoxStrategyGames

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open YamlDotNet.RepresentationModel
open YamlTools

/////////////////////////////////////////////////////////////////////////////
// General functions for dealing with Paradox strategy games

let internal localizationsAndEncodings = 
    [|
        ("en", Encoding.GetEncoding("Windows-1252"))
        ("fr", Encoding.GetEncoding("Windows-1252"))
        ("de", Encoding.GetEncoding("Windows-1252"))
        ("pl", Encoding.GetEncoding("windows-1250"))
        ("es", Encoding.GetEncoding("Windows-1252"))
        ("it", Encoding.GetEncoding("Windows-1252"))
        //("sv", Encoding.GetEncoding("Windows-1252"))
        ("cz", Encoding.GetEncoding("Windows-1250"))
        ("hu", Encoding.GetEncoding("Windows-1250"))
        ("nl", Encoding.GetEncoding("Windows-1252"))
        ("pt", Encoding.GetEncoding("Windows-1252"))
        ("ru", Encoding.GetEncoding("koi8-r"))
        ("fi", Encoding.GetEncoding("Windows-1252"))
    |]

/// <summary>
/// Generates cards for semicolon-separated files.
/// </summary>
/// <param name="lessonID">the lesson ID to use</param>
/// <param name="keyRoot">root for the Key field of generated cards</param>
/// <param name="ssvBytes">raw bytes of the SSV file</param>
let internal generateCardsForSSVContent(lessonID: int, keyRoot: string)(ssvBytes: byte array) = 
    let extractor = CsvTools.extractFieldsForLine(Some(";"))
    let cardsForLine(i: int, lang: string)(fields: string array) = 
        let tag = fields |> Array.head
        if (i >= fields.Length) then
            [||]
        else
            let langValue = fields.[i].Trim()
            match langValue with
            | v when not(String.IsNullOrWhiteSpace(v)) && v <> "x" && v <> "*" -> 
                [|
                    {
                        CardRecord.ID = 0
                        LessonID = lessonID
                        Text = v
                        Gender = "masculine"
                        Key = keyRoot + tag + "masculine"
                        GenderlessKey = keyRoot + tag
                        KeyHash = 0
                        GenderlessKeyHash = 0
                        SoundResource = ""
                        LanguageTag = lang
                        Reversible = true
                    }
                |]
            | _ -> [||]

    // for each available language, extract the whole file again with that language's encoding.
    let createExtractionTuple(i: int)(lang: string, enc: Encoding) = 
        (i + 1, lang, enc.GetString(ssvBytes).Split([| Environment.NewLine |], StringSplitOptions.None))
    let cardsForLocalizationAndEncoding(i: int, lang: string, lines: string array) = 
        let nonCommentLine(l: string) = not(l.StartsWith("#")) && not(String.IsNullOrWhiteSpace(l))
        lines 
        |> Array.filter nonCommentLine 
        |> Array.map extractor
        |> Array.collect(cardsForLine(i, lang))

    localizationsAndEncodings
    |> Array.mapi createExtractionTuple
    |> Array.collect cardsForLocalizationAndEncoding

let internal generateCardsForFile(lid: int)(p: string) = 
    File.ReadAllBytes(p) 
    |> generateCardsForSSVContent(lid, Path.GetFileNameWithoutExtension(p))

let internal generateCardsForSSVs(lid: int, ssvDir: string) = 
    let files = Directory.GetFiles(ssvDir, "*.csv")

    files
    |> Seq.collect(generateCardsForFile(lid))
    |> Array.ofSeq

let internal createLesson(i: int)(n: string) = 
    {
        LessonRecord.ID = i
        Name = n
    }

/////////////////////////////////////////////////////////////////////////////
// Europa Universalis III
let ExtractEU3(path: string) = 
    let lesson = 
        {
            LessonRecord.ID = 0
            Name = "Game Text"
        }

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lesson |]
        LudumLinguarumPlugins.ExtractedContent.cards = generateCardsForSSVs(lesson.ID, Path.Combine(path, "localisation"))
    }

/////////////////////////////////////////////////////////////////////////////
// Hearts of Iron III
let ExtractHOI3(path: string) = 
    // Hearts of Iron 3 has better grouping in its localization files, so we'll
    // go ahead and create a lesson for each one.
    let makeLessonAndCardsForFile(i: int)(p: string) = 
        let lesson = 
            {
                LessonRecord.ID = i
                Name = Path.GetFileNameWithoutExtension(p)
            }

        let cards = generateCardsForFile(lesson.ID)(p)
        (lesson, cards)

    let (lessons, cards) = 
        Directory.GetFiles(Path.Combine(path, "localisation"), "*.csv")
        |> Array.mapi makeLessonAndCardsForFile
        |> Array.unzip

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards |> Array.collect id
    }

/////////////////////////////////////////////////////////////////////////////
// Victoria II
let ExtractVictoria2(path: string) = 
    let lesson = 
        {
            LessonRecord.ID = 0
            Name = "Game Text"
        }

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lesson |]
        LudumLinguarumPlugins.ExtractedContent.cards = generateCardsForSSVs(lesson.ID, Path.Combine(path, "localisation"))
    }

/////////////////////////////////////////////////////////////////////////////
// Europa Universalis IV
let internal mkEu4NumericKeyAnnotationRegex() = new Regex(@"^(?:\s*\S+:)([0-9]+)(?:.*)", RegexOptions.None)
let rec internal eu4StripNumericKeyAnnotations(s: string) = 
    let m = mkEu4NumericKeyAnnotationRegex().Match(s)
    if (m.Success) then
        eu4StripNumericKeyAnnotations(s.Remove(m.Groups.[1].Index, m.Groups.[1].Length))
    else
        s

let internal eu4KeyRegex = new Regex(@"^(?:\s*)(\S+:)", RegexOptions.None)

let internal eu4EscapeQuotedValues(s: string) = 
    match (s.IndexOf('"'), s.LastIndexOf('"')) with
    | (a, _) when a < 0 -> s
    | (a, b) when a = b -> s
    | (a, b) when b > (a + 1) ->
        let len = b - (a + 1)
        let quoted = s.Substring(a + 1, len)
        s.Remove(a + 1, len).Insert(a + 1, quoted.Replace('"', '\'').Trim())
    | _ -> s

let ExtractEU4(path: string) = 
    // The localization .yml files are named xyz_l_language_optional_suffix.yml. Extract the
    // lesson names, and then group the files by lesson for extraction.
    let supportedLanguages = [| "english"; "french"; "german"; "spanish" |]
    let extractLessonName(p: string) = 
        supportedLanguages |> Array.fold (fun (s: string)(l: string) -> s.Replace("_l_" + l, "")) p
        
    let ymls = Directory.GetFiles(Path.Combine(path, "localisation"), "*.yml")
    let ymlsByLessonName = ymls |> Array.groupBy extractLessonName

    let cardsForYml(lid: int)(yd: YamlDocument) = 
        let languages = 
            [|
                ("l_english", "en")
                ("l_french", "fr")
                ("l_german", "de")
                ("l_spanish", "es")
            |]

        let cardsForLanguage nodeNameAndLang = 
            let (nodeName, lang) = nodeNameAndLang
            yd.RootNode
            |> YamlTools.findMappingNodesWithName(nodeName)
            |> Seq.collect YamlTools.convertMappingNodeToStringPairs
            |> Map.ofSeq
            |> AssemblyResourceTools.createCardRecordForStrings(lid, "", lang, "masculine")

        languages
        |> Seq.collect cardsForLanguage
        |> Array.ofSeq

    let cardsForLesson(lesson: LessonRecord, files: string array) = 
        let cardsForFile(fn: string) = 
            // there are files that have duplicate keys, so we need to filter them out.
            let keyForLine l = 
                let m = eu4KeyRegex.Match(l)
                if m.Success then
                    Some(m.Groups.[1].Value)
                else
                    None

            let contentsLines = 
                File.ReadAllLines(fn) 
                |> Seq.map (eu4StripNumericKeyAnnotations >> eu4EscapeQuotedValues)
                |> Seq.distinctBy keyForLine

            let rebuiltContents = String.Join(Environment.NewLine, contentsLines)
            use sr = new StringReader(rebuiltContents)
            let ys = new YamlStream()
            ys.Load(sr)

            ys.Documents
            |> Seq.collect(cardsForYml(lesson.ID))

        files
        |> Seq.collect cardsForFile

    let ymlsByLesson = 
        ymlsByLessonName
        |> Array.mapi(fun i (name: string, files: string array) -> (createLesson(i)(name), files))

    let cards = 
        ymlsByLesson
        |> Seq.map cardsForLesson
        |> Seq.collect id
        |> Array.ofSeq

    let (lessons, _) = ymlsByLesson |> Array.unzip
    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }

/////////////////////////////////////////////////////////////////////////////
// Crusader Kings II

let ExtractCrusaderKings2(path: string) = 
    // Crusader Kings II has better grouping in its localization files, so we'll
    // go ahead and create a lesson for each one that is not blacklisted.
    let fileBlacklist = 
        [|
            "WikipediaLinks"
            "z_PLACEHOLDER_DO_NOT_FORGET_ME"
            "z_proofreading_temp"
            "z_notranslate"
        |]
    let isFileBlacklisted(s: string) = fileBlacklist |> Array.contains(Path.GetFileNameWithoutExtension(s))

    let filesToProcess = 
        Directory.GetFiles(Path.Combine(path, "localisation"), "*.csv")
        |> Array.filter(isFileBlacklisted >> not)

    let lessons = 
        filesToProcess
        |> Array.map Path.GetFileNameWithoutExtension
        |> Array.mapi createLesson

    let cards = 
        lessons
        |> Array.zip filesToProcess
        |> Array.map(fun (p: string, l: LessonRecord) -> generateCardsForFile(l.ID)(p))
        |> Array.collect id

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
