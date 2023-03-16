module SupergiantGames

open LLDatabase
open LLUtils

open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

let private stripLanguageRegion(l: string) = 
    if l.Contains("-") then
        l.Substring(0, l.IndexOf("-"))
    else
        l

let private generateCardsForAllSubtitleDirectories(path: string, lessonId: int) =
    // Subtitle handling
    let subtitleDirectories = Directory.GetDirectories(Path.Combine(path, FixPathSeps @"Content\Subtitles"))
    let generateCardsForSubtitles(subtitleDir: string) = 
        let files = Directory.GetFiles(subtitleDir, "*.csv")
        let language = stripLanguageRegion(Path.GetFileName(subtitleDir).ToLowerInvariant())
        let mungeText(s: string) = 
            s.Trim().TrimStart('"').TrimEnd('"').Replace(@"\n", " ")
        let extractStringKeyAndStringForLine(i: int)(s: string) = 
            let (stringKey, stringValue) = (s.Substring(0, s.IndexOf(',')), s.Substring(s.IndexOf(',') + 1))
            let keyToUse = 
                if (String.IsNullOrWhiteSpace(stringKey)) then
                    i.ToString()
                else
                    stringKey

            (keyToUse, stringValue |> mungeText)

        let subtitlesForFile(subtitlePath: string) = 
            File.ReadAllLines(subtitlePath) 
            |> Array.skip(1)
            |> Array.filter (fun s -> s.Contains(","))
            |> Array.mapi extractStringKeyAndStringForLine
            |> Map.ofArray
            |> Map.filter(fun k _ -> not(String.IsNullOrWhiteSpace(k)))
            |> AssemblyResourceTools.createCardRecordForStrings(
                lessonId, 
                "subtitle_" + Path.GetFileNameWithoutExtension(subtitlePath) + "_", language, "masculine")

        files
        |> Array.collect subtitlesForFile

    subtitleDirectories
    |> Array.collect generateCardsForSubtitles

let private generateCardsForFormattedXml(path: string, fileRoot: string, fileWildcard: string, lessonId: int) =
    let gameTextFiles = Directory.GetFiles(Path.Combine(path, FixPathSeps @"Content\Game\Text"), fileWildcard)
    let gameTextLanguageRegex = new Regex(@"(" + fileRoot + @"\.)(..).+")
    let generateCardsForGameText(filePath: string) = 
        let rec mungeText(s: string) = 
            let regexesToRemove = 
                [| 
                    (new Regex(@"\\n"), " ")
                    (new Regex(@"\\Color\s[^\s]*\s"), "")
                    (new Regex(@"\\UpgradeStatInteger\s[^\s]*\s"), "")
                    (new Regex(@"\\UpgradeStatPercent\s[^\s]*\s"), "")
                    (new Regex(@"\\Format Text[^\s]*"), "")
                |]
            regexesToRemove |> Array.fold (fun (u: string)(t: Regex, replacement: string) -> t.Replace(u, replacement)) s

        let generateStringsForElement(xel: XElement) = 
            let id = xel.Attribute(XName.Get("Id")).Value
            let displayName = xel.Attributes(XName.Get("DisplayName")) |> Seq.tryHead |> Option.map (fun t -> t.Value)
            let description = xel.Attributes(XName.Get("Description")) |> Seq.tryHead |> Option.map (fun t -> t.Value |> mungeText)

            let idToDisplayName = displayName |> Option.map (fun dn -> (id, dn)) |> Option.toArray
            let idToDescription = description |> Option.filter (fun d -> d <> "N/A") |> Option.map (fun d -> (id, d)) |> Option.toArray

            [| idToDisplayName; idToDescription |] |> Array.concat

        use reader = new StreamReader(filePath)
        let xel = XElement.Load(reader)
        let language = gameTextLanguageRegex.Match(Path.GetFileName(filePath)).Groups.[2].Value.ToLowerInvariant()
        xel.Descendants(XName.Get("Text")) 
        |> Array.ofSeq 
        |> Array.collect generateStringsForElement
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lessonId, "gametext", language, "masculine")

    gameTextFiles
    |> Array.collect generateCardsForGameText

let private generateCardsForLaunchTextXml(path: string, lessonId: int) =
    // LaunchText.xml handling
    let generateCardsForHelpTextElement(el: XElement) = 
        let language = stripLanguageRegion(el.Attribute(XName.Get("lang")).Value)
        let generateStringsForTextElement(e: XElement) = 
            (e.Attribute(XName.Get("Id")).Value, e.Attribute(XName.Get("DisplayName")).Value)

        el.Descendants(XName.Get("Text"))
        |> Seq.map generateStringsForTextElement
        |> Map.ofSeq
        |> AssemblyResourceTools.createCardRecordForStrings(lessonId, "launchtext", language, "masculine")

    let helpTextPath = Path.Combine(path, FixPathSeps @"Content\Game\Text\LaunchText.xml")
    if File.Exists(helpTextPath) then
        use reader = new StreamReader(helpTextPath)
        let xel = XElement.Load(reader)
        xel.Descendants(XName.Get("HelpText"))
        |> Seq.collect generateCardsForHelpTextElement
        |> Array.ofSeq
    else
        [||]

// Handling for the simplified JSON files used by Hades for localization of text included in the game's
// Lua scripts.
let private generateCardsForLanguageSjson(path: string, lessonId: int)(language: string) =
    let gameTextFiles = Directory.GetFiles(Path.Combine(path, FixPathSeps(@"Content\Game\Text\" + language)), "*.sjson")
    let generateCardsForGameText(filePath: string) =
        // Transform the simplified JSON into proper JSON, so we can query it via JSONPath queries.
        // If this SJSON file is an override (file name starts with an underscore), then try and load the
        // original file as well, to generate the English language cards.

        let fileName = Path.GetFileName(filePath)
        let cardKeyToCardValues =
            if (fileName.StartsWith('_')) then
                Map.empty
            else
                Map.empty

        cardKeyToCardValues
        |> AssemblyResourceTools.createCardRecordForStrings(lessonId, "gametext", language, "masculine")

    gameTextFiles
    |> Array.collect generateCardsForGameText

let private generateCardsForSjson(path: string, lessonId: int) =
    let languageDirectories = Directory.GetDirectories(Path.Combine(path, FixPathSeps @"Content\Game\Text"))
    languageDirectories
    |> Array.collect(generateCardsForLanguageSjson(path, lessonId))

    
let private extractSupergiantGame(path: string) = 
    let lessonGameTextEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }
    let lessonSubtitlesEntry = {
        LessonRecord.ID = 1;
        Name = "Subtitles"
    }

    let finalCards = 
        [|
            generateCardsForAllSubtitleDirectories(path, lessonSubtitlesEntry.ID)
            generateCardsForFormattedXml(path, "HelpText", "HelpText*.xml", lessonGameTextEntry.ID)
            generateCardsForFormattedXml(path, "1_Keywords", "1_Keywords*.xml", lessonGameTextEntry.ID)
            generateCardsForFormattedXml(path, "Events", "Events*.xml", lessonGameTextEntry.ID)
            generateCardsForLaunchTextXml(path, lessonGameTextEntry.ID)
            generateCardsForSjson(path, lessonGameTextEntry.ID)
        |]
        |> Array.collect id

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonGameTextEntry; lessonSubtitlesEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = finalCards
    }
    
let ExtractBastion(path: string) = extractSupergiantGame(path)
let ExtractTransistor(path: string) = extractSupergiantGame(path)
let ExtractPyre(path: string) = extractSupergiantGame(path)
let ExtractHades(path: string) = extractSupergiantGame(path)

