module SimpleGames

open AssemblyResourceTools
open CsvTools
open LLDatabase
open OneOffGamesUtils
open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

(***************************************************************************)
(************************** Skulls of the Shogun ***************************)
(***************************************************************************)
let ExtractSkullsOfTheShogun(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
    OneOffGamesUtils.ExtractStringsFromAssemblies(path, Path.Combine(path, "SkullsOfTheShogun.exe"), "SkullsOfTheShogun.resources.dll", "TBS.Properties.AllResources", "gametext", lessonEntryWithId.ID)
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

(***************************************************************************)
(******************************** Hell Yeah ********************************)
(***************************************************************************)
let ExtractHellYeah(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    // table format: arguments to OneOffGamesUtils.ExtractStringsFromAssemblies, but with 
    // a lesson name instead of an id at the end.
    let mainExePath = Path.Combine(path, "HELLYEAH.exe")
    let mainResourceDllName = "HELLYEAH.resources.dll"
    let gameLibraryPath = Path.Combine(path, "HY_GameLibrary.dll")
    let gameLibraryResourceDllName = "HY_GameLibrary.resources.dll"
    let easyStoragePath = Path.Combine(path, "EasyStorage.dll")
    let easyStorageResourceDllName = "EasyStorage.resources.dll"
    let resTable = 
        [|
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.ACHIEVEMENTS", "achievements", "achievements")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.CUSTO", "customization", "customization")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.GENERAL", "general", "general")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.ISLAND", "island", "island")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.MISSIONS", "missions", "missions")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.OBJECTIVES", "objectives", "objectives")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.PCONLY", "pc-only", "pc-only")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.POKEDEX", "compendium", "compendium")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.SCRIPTS", "dialogue", "dialogue")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.SHOP", "shop", "shop")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.TELEPORT", "teleport", "teleport")
            (path, mainExePath, mainResourceDllName, "HELLYEAH_4_0.TEXTS.TRIGGERS", "triggers", "triggers")
            (path, gameLibraryPath, gameLibraryResourceDllName, "HY_GameLibrary.TEXTS.LOADING", "loading-text", "loading-text")
            (path, gameLibraryPath, gameLibraryResourceDllName, "HY_GameLibrary.TEXTS.SUBTITLES", "subtitles", "subtitles")
            (path, gameLibraryPath, gameLibraryResourceDllName, "HY_GameLibrary.TEXTS.TRC", "certification", "certification")
            (path, gameLibraryPath, gameLibraryResourceDllName, "HY_GameLibrary.TEXTS.UI", "ui", "ui")
            (path, easyStoragePath, easyStorageResourceDllName, "EasyStorage.Strings", "easystorage", "easystorage")
        |]

    let createAndExtract(path: string, mainResPath: string, resDllName: string, resourceRoot: string, keyRoot: string, lessonName: string) = 
        let lessonEntry = {
            LessonRecord.GameID = g.ID;
            ID = 0;
            Name = lessonName
        }
        let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
        OneOffGamesUtils.ExtractStringsFromAssemblies(path, mainResPath, resDllName, resourceRoot, keyRoot, lessonEntryWithId.ID)

    resTable
    |> Array.collect createAndExtract
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

(***************************************************************************)
(***************************** Magical Drop V ******************************)
(***************************************************************************)
let internal sanitizeMagicalDropVXml(x: string) = 
    x.Replace("&", "&amp;").Replace("<string placeholder>", "")

let internal sanitizeMagicalDropVFormatStrings(x: string) = 
    x.Replace("\\n", " ")

let internal generateMagicalDropVStringMap(cleanedXml: string): Map<string, string> = 
    use stringReader = new StringReader(cleanedXml)
    let xel = XElement.Load(stringReader)
    xel.Descendants(XName.Get("content")) 
    |> Array.ofSeq 
    |> Array.collect(fun t -> t.Descendants() |> Array.ofSeq) 
    |> Array.map (fun t -> (t.Name.LocalName, t.Value |> sanitizeMagicalDropVFormatStrings)) |> Map.ofArray

let ExtractMagicalDropV(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let filePaths = 
        [|
            @"localization\localization_de-DE.xml"
            @"localization\localization_eng-US.xml"
            @"localization\localization_es-ES.xml"
            @"localization\localization_fr-FR.xml"
            @"localization\localization_it-IT.xml"
            @"localization\localization_ja-JP.xml"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let filePathsAndLanguages = 
        [| "de"; "en"; "es"; "fr"; "it"; "ja" |] |> Array.zip(filePaths)

    let lessonStoryEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Story Text"
    }
    let lessonUIEntry = { lessonStoryEntry with Name = "UI Text" }
    let lessonStoryEntryWithId = { lessonStoryEntry with ID = db.CreateOrUpdateLesson(lessonStoryEntry) }
    let lessonUIEntryWithId = { lessonUIEntry with ID = db.CreateOrUpdateLesson(lessonUIEntry) }

    let generateCardsForLocalization(locPath: string, lang: string) = 
        let cleanedXml = File.ReadAllText(locPath) |> sanitizeMagicalDropVXml
        let (storyStrings, nonStoryStrings) = 
            generateMagicalDropVStringMap(cleanedXml)
            |> Map.partition(fun k t -> (k.StartsWith("story", StringComparison.InvariantCultureIgnoreCase) || k.StartsWith("ID_storyintro")))

        [|
            storyStrings |> AssemblyResourceTools.createCardRecordForStrings(lessonStoryEntryWithId.ID, "storytext", lang, "masculine")
            nonStoryStrings |> AssemblyResourceTools.createCardRecordForStrings(lessonUIEntryWithId.ID, "uitext", lang, "masculine")
        |] |> Array.concat

    filePathsAndLanguages 
    |> Array.collect generateCardsForLocalization
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

(***************************************************************************)
(******************************** Audiosurf ********************************)
(***************************************************************************)
let ExtractAudiosurf(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

    let languageFiles = Directory.GetFiles(Path.Combine(path, @"engine\LanguagePacks"), "aslang_*.xml", SearchOption.AllDirectories)
    let languageRegex = new Regex("(aslang_)(..).+")

    let languageForFile(p: string) =
        languageRegex.Match(p).Groups.[2].Value.ToLowerInvariant()

    let languageFilesAndLanguages = (languageFiles |> Array.map languageForFile) |> Array.zip languageFiles
    let generateCardsForLanguage(filePath: string, lang: string) = 
        use streamReader = new StreamReader(filePath)
        let xel = XElement.Load(streamReader)
        xel.Descendants(XName.Get("S")) 
        |> Seq.map (fun t -> (t.Attribute(XName.Get("EN")).Value, t.Value.Trim()))
        |> Map.ofSeq
        |> AssemblyResourceTools.createCardRecordForStrings(lessonEntryWithId.ID, "gametext", lang, "masculine")

    languageFilesAndLanguages
    |> Array.collect generateCardsForLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()

(***************************************************************************)
(********************************* Bastion *********************************)
(***************************************************************************)
let ExtractBastion(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonGameTextEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonGameTextEntryWithId = { lessonGameTextEntry with ID = db.CreateOrUpdateLesson(lessonGameTextEntry) }
    let lessonSubtitlesEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Subtitles"
    }
    let lessonSubtitlesEntryWithId = { lessonSubtitlesEntry with ID = db.CreateOrUpdateLesson(lessonSubtitlesEntry) }

    // Subtitle handling
    let subtitleDirectories = Directory.GetDirectories(Path.Combine(path, @"Content\Subtitles"))
    let subtitleLineRegex = new Regex("^(.+?),(.+)")
    let generateCardsForSubtitles(subtitleDir: string) = 
        let files = Directory.GetFiles(subtitleDir, "*.csv")
        let language = Path.GetFileName(subtitleDir).ToLowerInvariant()
        let mungeText(s: string) = 
            s.Trim().TrimStart('"').TrimEnd('"').Replace(@"\n", " ")
        let extractStringKeyAndStringForLine(i: int)(s: string) = 
            let (stringKey, stringValue) = (s.Substring(0, s.IndexOf(',')), s.Substring(s.IndexOf(',') + 1))
            let keyToUse = 
                if (String.IsNullOrWhiteSpace(stringKey)) then
                    i.ToString()
                else
                    stringKey

            (keyToUse, stringValue)

        let subtitlesForFile(subtitlePath: string) = 
            File.ReadAllLines(subtitlePath) 
            |> Array.skip(1)
            |> Array.filter (fun s -> s.Contains(","))
            |> Array.mapi extractStringKeyAndStringForLine
            |> Map.ofArray
            |> Map.filter(fun k _ -> not(String.IsNullOrWhiteSpace(k)))
            |> AssemblyResourceTools.createCardRecordForStrings(
                lessonSubtitlesEntryWithId.ID, 
                "subtitle_" + Path.GetFileNameWithoutExtension(subtitlePath) + "_", language, "masculine")

        files
        |> Array.collect subtitlesForFile

    // HelpText.*.xml handling
    let gameTextFiles = Directory.GetFiles(Path.Combine(path, @"Content\Game\Text"), "HelpText*.xml")
    let gameTextLanguageRegex = new Regex(@"(HelpText\.)(..).+")
    let generateCardsForGameText(filePath: string) = 
        let rec mungeText(s: string) = 
            let regexesToRemove = 
                [| 
                    (new Regex(@"\\n"), " ")
                    (new Regex(@"\\Color\s[^\s]*\s"), "")
                    (new Regex(@"\\UpgradeStatInteger\s[^\s]*\s"), "")
                    (new Regex(@"\\UpgradeStatPercent\s[^\s]*\s"), "")
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
        |> AssemblyResourceTools.createCardRecordForStrings(lessonGameTextEntryWithId.ID, "gametext", language, "masculine")

    // LaunchText.xml handling
    let generateCardsForLaunchText = 
        let generateCardsForHelpTextElement(el: XElement) = 
            let language = el.Attribute(XName.Get("lang")).Value
            let generateStringsForTextElement(e: XElement) = 
                (e.Attribute(XName.Get("Id")).Value, e.Attribute(XName.Get("DisplayName")).Value)

            el.Descendants(XName.Get("Text"))
            |> Seq.map generateStringsForTextElement
            |> Map.ofSeq
            |> AssemblyResourceTools.createCardRecordForStrings(lessonGameTextEntryWithId.ID, "launchtext", language, "masculine")

        let helpTextPath = Path.Combine(path, @"Content\Game\Text\LaunchText.xml")
        use reader = new StreamReader(helpTextPath)
        let xel = XElement.Load(reader)
        xel.Descendants(XName.Get("HelpText"))
        |> Seq.collect generateCardsForHelpTextElement
        |> Array.ofSeq

    let subtitleCards = 
        subtitleDirectories
        |> Array.collect generateCardsForSubtitles

    let helpTextCards = 
        gameTextFiles
        |> Array.collect generateCardsForGameText

    [|
        subtitleCards
        helpTextCards
        generateCardsForLaunchText
    |]
    |> Array.collect id
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()

let internal hbFormatTokenRegexes = 
    [|
        (new Regex(@"\[.*?\]"), "")
        (new Regex(@"\\n"), " ")
    |]

let internal stripHbRegex(s: string)(r: Regex, replacement: string): string = 
    r.Replace(s, replacement)

let internal stripHbFormattingTokens(s: string) = 
    hbFormatTokenRegexes
    |> Seq.fold stripHbRegex s

let hbGenerateCardsForLine(languages: string seq, lid: int)(l: string): CardRecord array = 
    let fields = extractFieldsForLine(l)

    let cardId = fields |> Array.head
    let generateCardForIdAndText(language: string, text: string) = 
        {
            CardRecord.ID = 0
            LessonID = lid
            Text = text
            Gender = "masculine"
            Key = cardId + "masculine"
            GenderlessKey = cardId
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = language
            Reversible = true
        }

    fields 
    |> Seq.tail
    |> Seq.map stripHbFormattingTokens
    |> Seq.zip(languages)
    |> Seq.map generateCardForIdAndText
    |> Array.ofSeq

let hbGenerateCardsForLines(lid: int)(lines: string array): CardRecord array = 
    let languageMap = 
        [|
            ("EN", "en")
            ("JP", "ja")
            ("IT", "it")
            ("GER", "de")
            ("FR", "fr")
            ("SP", "es")
            ("Russian", "ru")
        |]
        |> Map.ofArray
    let columnHeaders = (lines |> Array.head).Split('\t')
    let languages = columnHeaders |> Array.skip(1) |> Array.map (fun c -> languageMap |> Map.find(c))
    let dataLines = lines |> Array.skip(1)

    dataLines
    |> Array.collect(hbGenerateCardsForLine(languages, lid))

(***************************************************************************)
(**************************** Hatoful Boyfriend ****************************)
(***************************************************************************)
let ExtractHatofulBoyfriend(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
    let filesToExtract = 
        [|
            Path.Combine(path, @"hatoful_Data\StreamingAssets\LocalisationUnicode.txt")
        |]

    filesToExtract
    |> Array.collect (File.ReadAllLines >> Array.filter(String.IsNullOrWhiteSpace >> not) >> hbGenerateCardsForLines(lessonEntryWithId.ID))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

(***************************************************************************)
(********************* Hatoful Boyfriend: Holiday Star *********************)
(***************************************************************************)
let ExtractHatofulBoyfriendHolidayStar(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
    let filesToExtract = 
        [|
            Path.Combine(path, @"HB2_Data\StreamingAssets\localisation\localisationunicode_EN.txt")
            Path.Combine(path, @"HB2_Data\StreamingAssets\localisation\localisationunicode_FR.txt")
            Path.Combine(path, @"HB2_Data\StreamingAssets\localisation\localisationunicode_GER.txt")
            Path.Combine(path, @"HB2_Data\StreamingAssets\localisation\localisationunicode_JP.txt")
        |]

    filesToExtract
    |> Array.collect (File.ReadAllLines >> Array.filter(String.IsNullOrWhiteSpace >> not) >> hbGenerateCardsForLines(lessonEntryWithId.ID))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
