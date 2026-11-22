module SupergiantGames

open AudioPipeline
open CsvTools
open FmodTools
open LLDatabase
open LLUtils
open LudumLinguarumPlugins

open Json.Path

open System
open System.IO
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open System.Xml.Linq

module Lua =
    open FParsec

    open System.IO

    // Minimal tools to deal with parsing the Lua data declaration files, so that we can extract text from them for
    // English-language cues in Hades which are not present in the subtitle files.

    // Trace operator, for debugging parsers. https://www.quanttec.com/fparsec/users-guide/debugging-a-parser.html#tracing-a-parser
    let private (<!>) (p: Parser<_,_>) label : Parser<_,_> =
        fun stream ->
            printfn "%A: Entering %s" stream.Position label
            let reply = p stream
            match box reply.Result with
            | null ->
                printfn "%A: Leaving %s (%A)" stream.Position label reply.Status
            | _ ->
                printfn "%A: Leaving %s (%A) (%A)" stream.Position label reply.Status reply.Result

            reply

    // Module for the parser which is used to replace multi-line and single-line comments with a single space.
    // Note that this currently doesn't correctly deal with comment strings within quotes.
    module LuaCommentStrippingGrammar =
        type internal CommentPreprocessorNode =
            | NonCommentBlock of string
            | Comment

        let private longBracketOpen: Parser<_, Unit> = between (skipChar '[') (skipChar '[') (many(skipChar '='))
        let private longBracketClose: Parser<_, Unit> = between (skipChar ']') (skipChar ']') (many(skipChar '='))
        let private multiLineComment: Parser<_, Unit> = attempt (skipChar '-' .>> skipChar '-' .>> longBracketOpen .>> (manyCharsTill anyChar longBracketClose))
        let private lineComment: Parser<_, Unit> = attempt (skipChar '-' .>> skipChar '-' .>> skipRestOfLine false)
        let private comment =
            choice [
                multiLineComment
                lineComment
            ] >>% Comment

        let private nonComment =
            // Note that we have to use `lookAhead` for the comment/end-of-file check, because
            // we don't want to actually consume those characters right now. (We want to
            // parse the comment block on its own, so we can accurately replace it with a single
            // space.)
            manyCharsTill anyChar (choice [lookAhead comment >>% (); eof]) |>> NonCommentBlock

        let internal commentPreprocessorParser: Parser<_, Unit> =
            manyTill (choice [comment; nonComment]) eof

    let internal hadesCueRegex = new Regex(@"Cue\s*=\s*""(?<Cue>.*?)""")
    let internal hadesTextRegex = new Regex(@"Text\s=\s*""(?<Text>.*?)""")


    let stripComments(luaSource: string): string =
        let parsedLua = run LuaCommentStrippingGrammar.commentPreprocessorParser luaSource
        match parsedLua with
        | ParserResult.Failure (s, e, us) -> failwith "failed to remove comments from Lua successfully"
        | ParserResult.Success (result, _, _) ->
            result
            |> List.map (fun b ->
                match b with
                | LuaCommentStrippingGrammar.NonCommentBlock s -> s
                | LuaCommentStrippingGrammar.Comment -> " "
            )
            |> String.concat ""


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
        |]
        |> Array.collect id

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonGameTextEntry; lessonSubtitlesEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = finalCards
    }
    
let ExtractBastion(path: string) = extractSupergiantGame(path)
let ExtractTransistor(path: string) = extractSupergiantGame(path)
let ExtractPyre(path: string) = extractSupergiantGame(path)

module Hades =
    let private getLuaScriptCueAndText(commentStrippedText: string) =
        // Rather than write a parser for the Lua grammar, we're going to consume the text by line, and
        // filter for cue and text lines that appear in pairs.
        let lines =
            commentStrippedText.ReplaceLineEndings().Split([| Environment.NewLine|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter(fun l -> l.Contains("Cue = ") || l.Contains("Text = "))

        lines
        |> Array.pairwise
        |> Array.filter(fun (p1, p2) -> p1.Contains("Cue = ") && p2.Contains("Text = "))
        |> Array.map(fun (p1, p2) -> (Lua.hadesCueRegex.Match(p1).Groups.Item("Cue").Value, Lua.hadesTextRegex.Match(p2).Groups.Item("Text").Value))
        |> Map.ofArray
        
    let internal generateCardsForLuaScriptWithCue(lessonId: int)(path: string) : (string option * CardRecord) array =
        let fileText = File.ReadAllText(path)
        let commentStrippedText = Lua.stripComments(fileText)
        let fileRootName = Path.GetFileNameWithoutExtension(path)

        getLuaScriptCueAndText(commentStrippedText)
        |> Map.toArray
        |> Array.map (fun (cue, text) ->
            let card =
                {
                    CardRecord.ID = 0
                    LessonID = lessonId
                    Text = text
                    Gender = "masculine"
                    Key = fileRootName + cue + "masculine"
                    GenderlessKey = fileRootName + cue
                    KeyHash = 0
                    GenderlessKeyHash = 0
                    SoundResource = ""
                    ImageResource = ""
                    Speaker = ""
                    LanguageTag = "en"
                    Reversible = true
                }
            (Some cue, card)
        )

    // Hades isn't structured the same way as the other Supergiant games, and requires some bespoke 
    // mapping to allow us to match up English text that is stored in some of the Lua scripts, 
    // with the localized versions, which are in the subtitle or game text files.
    let internal generateCardsForLuaScript(lessonId: int)(path: string) =
        generateCardsForLuaScriptWithCue lessonId path |> Array.map snd

    let internal normalizeQuotesToDoubleQuotes(sjson: string) =
        let pattern = "=[\s]+(?<!\")'(?:(?>[^'\"]+)|\"\"|''|'(?!\"'))*'$"
        let replacement = "\"${0:1,-2}\""

        let replaceMatch (m: Match) =
            let matchedString = m.Value.Substring(1, m.Value.Length - 2)
            let replaced = matchedString.Replace("\"", "'")
            "\"" + replaced + "\""

        Regex.Replace(sjson, pattern, MatchEvaluator(replaceMatch), RegexOptions.Multiline)

    // Handling for the simplified JSON files used by Hades for localization of text included in the game's
    // Lua scripts.
    let private sjsonFileNameRegex = new Regex(@"(?<name>[^\.]+)[\.](?<lang>..(-..)?)[\.]sjson")
    let private sjsonIdAndDisplayNameJSONPath = JsonPath.Parse("$..[?(@.Id && @.DisplayName)]")
    let private sjsonNonStandardCharacterEscapingRegex1 = new Regex(@"\\Column [0-9]+")

    let private generateCardsForLanguageSjson(path: string, lessonId: int)(language: string) =
        let gameTextFiles = Directory.GetFiles(Path.Combine(path, FixPathSeps(@"Content\Game\Text\" + language)), "*.sjson")
        let generateCardsForGameText(filePath: string) =
            // Transform the simplified JSON into proper JSON, so we can query it via JSONPath queries.
            // If this SJSON file is an override (file name starts with an underscore), then try and load the
            // original file as well, to generate the English language cards.

            let fileName = Path.GetFileName(filePath)
            let filePartsResults = sjsonFileNameRegex.Match(fileName)
            let fileRootName = filePartsResults.Groups.Item("name").Value

            // There are some issues with the files that come with the game, where it doesn't seem to conform to the
            // published SJSON spec. We'll perform some ad-hoc fixups to try and get things working, without
            // going too deep into trying to understand if there have been further changes to the SJSON grammar.
            // 1) remove leading/trailing whitespace
            // 2) remove braces wrapping the files
            // 3) sanitize quadruple quotes
            // 4) remove unnecessary quadruple/triple quotes
            // 5) fix text errors with quoting
            // 6) formatting metadata of the form \Column xxx, which violates the character escaping parts of the SJSON grammar
            // 7) remove non-breaking spaces
            let sjsonText =
                File
                    .ReadAllText(filePath)
                    .Trim()
                    .Trim([|'{'; '}'|])
                    .Replace("\\[", "[")
                    .Replace("\\]", "]")
                    .Replace("\t", "    ")
                    .Replace(char(0xA0), ' ')

            let sjsonText2 = sjsonNonStandardCharacterEscapingRegex1.Replace(sjsonText, "")
            let jsonText = SjsonTools.sjsonToJSON(sjsonText2)

            let keyRoot =
                if (fileRootName.StartsWith('_')) then
                    fileRootName.Substring(1)
                else
                    fileRootName

            let jsonPathResult = sjsonIdAndDisplayNameJSONPath.Evaluate(JsonNode.Parse(jsonText))
            jsonPathResult.Matches
            |> Seq.map (fun m -> (m.Value.Item("Id").ToString(), m.Value.Item("DisplayName").ToString()))
            |> Map.ofSeq
            |> Map.toArray
            |> Array.map (fun (idKey, displayName) ->
                let card =
                    {
                        CardRecord.ID = 0
                        LessonID = lessonId
                        Text = displayName
                        Gender = "masculine"
                        Key = keyRoot + idKey + "masculine"
                        GenderlessKey = keyRoot + idKey
                        KeyHash = 0
                        GenderlessKeyHash = 0
                        SoundResource = ""
                        ImageResource = ""
                        Speaker = ""
                        LanguageTag = language
                        Reversible = true
                    }
                (Some idKey, card)
            )

        gameTextFiles
        |> Array.collect generateCardsForGameText

    let internal generateCardsForSjsonWithCue(path: string, lessonId: int) =
        let languageDirectories = Directory.GetDirectories(Path.Combine(path, FixPathSeps @"Content\Game\Text"))
        languageDirectories
        |> Array.map (fun d -> Path.GetFileName(d))
        |> Array.collect(generateCardsForLanguageSjson(path, lessonId))

    let internal generateCardsForSjson(path: string, lessonId: int) =
        generateCardsForSjsonWithCue(path, lessonId) |> Array.map snd

    let internal generateCardsForAllSubtitleDirectoriesWithCue(path: string, lessonId: int) =
        // Subtitle handling
        let subtitleDirectories = Directory.GetDirectories(Path.Combine(path, FixPathSeps @"Content\Subtitles"))
        let generateCardsForSubtitles(subtitleDir: string) = 
            let files = Directory.GetFiles(subtitleDir, "*.csv")
            let language = stripLanguageRegion(Path.GetFileName(subtitleDir).ToLowerInvariant())
            let subtitlesForFile(subtitlePath: string) =
                let lines = File.ReadAllLines(subtitlePath)
                let headerFields = lines |> Array.head |> extractFieldsForLine(Some(","))
                let lastIDIndex = headerFields |> Array.findIndexBack(fun f -> f = "ID")
                let lineIndex = headerFields |> Array.findIndexBack(fun f -> f = "Line")
                let greatestFieldIndex = Math.Max(lastIDIndex, lineIndex)
                let keyRoot = "subtitle_" + Path.GetFileNameWithoutExtension(subtitlePath) + "_"

                lines
                |> Array.skip 1
                |> Array.map (extractFieldsForLine(Some(",")))
                |> Array.filter (fun lineFields -> lineFields.Length > greatestFieldIndex)
                |> Array.map (fun lineFields -> (lineFields.[lastIDIndex], lineFields.[lineIndex]))
                |> Map.ofArray
                |> Map.toArray
                |> Array.map (fun (idKey, lineText) ->
                    let card =
                        {
                            CardRecord.ID = 0
                            LessonID = lessonId
                            Text = lineText
                            Gender = "masculine"
                            Key = keyRoot + idKey + "masculine"
                            GenderlessKey = keyRoot + idKey
                            KeyHash = 0
                            GenderlessKeyHash = 0
                            SoundResource = ""
                            ImageResource = ""
                            Speaker = ""
                            LanguageTag = language
                            Reversible = true
                        }
                    (Some idKey, card)
                )

            files
            |> Array.collect subtitlesForFile

        subtitleDirectories
        |> Array.collect generateCardsForSubtitles

    let internal generateCardsForAllSubtitleDirectories(path: string, lessonId: int) =
        generateCardsForAllSubtitleDirectoriesWithCue(path, lessonId) |> Array.map snd

    // Used to remove formatting directives from the final text.
    let internal formattingBraceRemovalRegex = new Regex(@"\{[^\}]*\}")
    let internal formattingEscapedWhitespaceRemovalRegex = new Regex(@"\\n|\\r|\\t")
    let internal formattingAtDirectiveRemovalRegex = new Regex(@"\@[^\s]*\s*")


let private attachHadesFamilyAudioAndSpeakers
    (gameName: string)
    (cueBankMap: Map<string, FmodBankEntry>)
    (mediaCtx: MediaExtractionContext)
    (cardsWithCue: (string option * CardRecord) array)
    : CardRecord array =
    // Clean formatting directives first and enrich with speaker metadata when a cue matches
    let cleanedWithCue =
        cardsWithCue
        |> Array.map (fun (cueOpt, c) ->
            let cleanedText =
                Hades.formattingAtDirectiveRemovalRegex.Replace(
                    Hades.formattingEscapedWhitespaceRemovalRegex.Replace(
                        Hades.formattingBraceRemovalRegex.Replace(c.Text, ""),
                        " "
                    ),
                    ""
                )
            let normalizedCueOpt =
                cueOpt
                |> Option.map normalizeCueId
                |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
            let speaker =
                match normalizedCueOpt |> Option.bind (fun k -> cueBankMap |> Map.tryFind k) with
                | Some entry -> entry.Speaker
                | None -> c.Speaker
            (normalizedCueOpt, { c with Text = cleanedText; Speaker = speaker })
        )

    let withAudio =
        match mediaCtx.ExtractAudio, mediaCtx.MediaStore with
        | true, Some store when not cueBankMap.IsEmpty ->
            let workItems =
                cleanedWithCue
                |> Array.choose (fun (cueOpt, c) ->
                    if String.Equals(c.LanguageTag, "en", StringComparison.OrdinalIgnoreCase) then
                        cueOpt
                        |> Option.bind (fun cueKey ->
                            cueBankMap
                            |> Map.tryFind cueKey
                            |> Option.map (fun entry ->
                                {
                                    AudioIngestWorkItem.Key = cueKey
                                    LanguageTag = "en"
                                    SourceDescription = sprintf "%s#%d (%s)" (Path.GetFileName(entry.BankPath)) entry.SubsongIndex cueKey
                                    InputFilePath = entry.BankPath
                                    SubsongIndex = Some entry.SubsongIndex
                                }
                            )
                        )
                    else
                        None
                )
                |> Array.distinctBy (fun w -> w.Key)

            mediaCtx.Log(sprintf "%s audio extraction (en): matched %d unique voice cues across FMOD banks." gameName workItems.Length)
            let (hashByCue, succeeded, errors) =
                ingestExtractedAudioFileBatch store mediaCtx.AudioTools mediaCtx.AudioOptions mediaCtx.Log workItems
            mediaCtx.Log(sprintf "%s audio extraction complete: ingested %d clips (%d errors)." gameName succeeded errors)

            cleanedWithCue
            |> Array.map (fun (cueOpt, c) ->
                if String.Equals(c.LanguageTag, "en", StringComparison.OrdinalIgnoreCase) then
                    match cueOpt |> Option.bind (fun k -> hashByCue |> Map.tryFind k) with
                    | Some hash -> { c with SoundResource = hash }
                    | None -> c
                else
                    c
            )
        | _ ->
            cleanedWithCue |> Array.map snd

    // Prefer keeping cards that have audio/speaker populated when deduplicating identical text
    withAudio
    |> Array.sortBy (fun c -> (if String.IsNullOrEmpty(c.SoundResource) then 1 else 0), (if String.IsNullOrEmpty(c.Speaker) then 1 else 0))
    |> Array.distinctBy (fun c -> c.Text)

let ExtractHadesWithMedia (path: string) (_args: string array) (mediaCtx: MediaExtractionContext) =
    let lessonGameTextEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }
    let lessonSubtitlesEntry = {
        LessonRecord.ID = 1;
        Name = "Subtitles"
    }

    let luaScriptsToProcess =
        [
            @"Content\Scripts\DeathLoopData.lua"
            @"Content\Scripts\EncounterData.lua"
            @"Content\Scripts\EnemyData.lua"
            @"Content\Scripts\LootData.lua"
            @"Content\Scripts\NPCData.lua"
            @"Content\Scripts\RoomData.lua"
            @"Content\Scripts\RoomDataAsphodel.lua"
            @"Content\Scripts\RoomDataElysium.lua"
            @"Content\Scripts\RoomDataSecrets.lua"
            @"Content\Scripts\RoomDataStyx.lua"
            @"Content\Scripts\RoomDataSurface.lua"
            @"Content\Scripts\RoomDataTartarus.lua"
        ]
        |> List.map FixPathSeps
        |> List.map (fun relativePath -> Path.Combine(path, relativePath))

    let noCue (cards: CardRecord array) = cards |> Array.map (fun c -> (None, c))

    let cardsWithCue =
        [|
            [|
                Hades.generateCardsForAllSubtitleDirectoriesWithCue(path, lessonSubtitlesEntry.ID)
                generateCardsForFormattedXml(path, "HelpText", "HelpText*.xml", lessonGameTextEntry.ID) |> noCue
                generateCardsForFormattedXml(path, "1_Keywords", "1_Keywords*.xml", lessonGameTextEntry.ID) |> noCue
                generateCardsForFormattedXml(path, "Events", "Events*.xml", lessonGameTextEntry.ID) |> noCue
                generateCardsForLaunchTextXml(path, lessonGameTextEntry.ID) |> noCue
                Hades.generateCardsForSjsonWithCue(path, lessonGameTextEntry.ID)
            |]
            luaScriptsToProcess |> List.map (Hades.generateCardsForLuaScriptWithCue(lessonGameTextEntry.ID)) |> Array.ofList
        |]
        |> Array.collect id
        |> Array.collect id

    let voFsb = Path.Combine(path, FixPathSeps @"Content\Audio\FMOD\Build\Desktop\VO.fsb")
    let voHeader = Path.Combine(path, FixPathSeps @"Content\Audio\FMOD\Build\Desktop\VO.h")
    let cueBankMap = loadSingleBankCueMap voFsb voHeader None

    let finalCards = attachHadesFamilyAudioAndSpeakers "Hades" cueBankMap mediaCtx cardsWithCue

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonGameTextEntry; lessonSubtitlesEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = finalCards
    }

let ExtractHades(path: string) =
    ExtractHadesWithMedia path [||] MediaExtractionContext.None

// Unlike the original Hades, Hades II does not ship with subtitle CSV files or the HelpText/Events/LaunchText
// XML files: all localized text (including dialogue and subtitles) lives in the per-language SJSON files
// underneath the game text directory, which is handled by the SJSON logic shared with the original Hades.
let ExtractHades2WithMedia (path: string) (_args: string array) (mediaCtx: MediaExtractionContext) =
    let lessonGameTextEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }

    // Hades II has a much larger set of Lua scripts containing English-language cues and text than the
    // original Hades did, so rather than maintaining a fixed list of scripts, process all of them.
    let luaScriptsToProcess =
        Directory.GetFiles(Path.Combine(path, FixPathSeps @"Content\Scripts"), "*.lua")

    let cardsWithCue =
        [|
            [|
                Hades.generateCardsForSjsonWithCue(path, lessonGameTextEntry.ID)
            |]
            luaScriptsToProcess |> Array.map (Hades.generateCardsForLuaScriptWithCue(lessonGameTextEntry.ID))
        |]
        |> Array.collect id
        |> Array.collect id

    let voDir = Path.Combine(path, FixPathSeps @"Content\Audio\Desktop\VO")
    let cueBankMap = loadDirectoryBankCueMap voDir

    let finalCards = attachHadesFamilyAudioAndSpeakers "Hades II" cueBankMap mediaCtx cardsWithCue

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonGameTextEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = finalCards
    }

let ExtractHades2(path: string) =
    ExtractHades2WithMedia path [||] MediaExtractionContext.None
