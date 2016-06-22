module ParadoxStrategyGames

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open YamlDotNet.RepresentationModel
open YamlTools

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

let internal generateCardsForSSVContent(lessonID: int, keyRoot: string)(ssvBytes: byte array) = 
    let extractor = CsvTools.extractFieldsForLine(";")
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

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let ExtractEU3(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lesson = createLesson(g.ID, db)("Game Text")

    generateCardsForSSVs(lesson.ID, Path.Combine(path, "localisation"))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractHOI3(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    // Hearts of Iron 3 has better grouping in its localization files, so we'll
    // go ahead and create a lesson for each one.
    let lessonGenerator = createLesson(g.ID, db)
    Directory.GetFiles(Path.Combine(path, "localisation"), "*.csv")
    |> Array.collect(fun p -> generateCardsForFile(lessonGenerator(Path.GetFileNameWithoutExtension(p)).ID)(p))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractVictoria2(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lesson = createLesson(g.ID, db)("Game Text")

    generateCardsForSSVs(lesson.ID, Path.Combine(path, "localisation"))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let internal eu4NumericKeyAnnotationRegex = new Regex(@"^(?:\s*\S+:)([0-9]+)(?:.*)", RegexOptions.None)
let rec internal eu4StripNumericKeyAnnotations(s: string) = 
    let m = eu4NumericKeyAnnotationRegex.Match(s)
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

let ExtractEU4(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    // The localization .yml files are named xyz_l_language_optional_suffix.yml. Extract the
    // lesson names, and then group the files by lesson for extraction.
    let lessonGenerator = createLesson(g.ID, db)
    let supportedLanguages = [| "english"; "french"; "german"; "spanish" |]
    let extractLessonName(p: string) = 
        supportedLanguages |> Array.fold (fun (s: string)(l: string) -> s.Replace("_l_" + l, "")) p
        
    let ymls = Directory.GetFiles(Path.Combine(path, "localisation"), "*.yml")
    let ymlsByLesson = ymls |> Array.groupBy extractLessonName

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

    let cardsForLesson(lessonName: string, files: string array) = 
        let lesson = lessonGenerator(lessonName)
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

    ymlsByLesson
    |> Seq.collect cardsForLesson
    |> Array.ofSeq
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()