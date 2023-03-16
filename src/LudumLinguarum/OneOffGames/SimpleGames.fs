module SimpleGames

open CsvTools
open FsGettextUtils
open IniParser.Model
open LLDatabase
open LLUtils
open SharpCompress.Archives.Rar

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open IniParser.Parser

(***************************************************************************)
(************************** Skulls of the Shogun ***************************)
(***************************************************************************)
let ExtractSkullsOfTheShogun(path: string) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }
    let cards = OneOffGamesUtils.ExtractStringsFromAssemblies(path, Path.Combine(path, "SkullsOfTheShogun.exe"), "SkullsOfTheShogun.resources.dll", "TBS.Properties.AllResources", "gametext", lessonEntry.ID)

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }

(***************************************************************************)
(******************************** Hell Yeah ********************************)
(***************************************************************************)
let ExtractHellYeah(path: string) = 
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

    let createAndExtract(i: int)(path: string, mainResPath: string, resDllName: string, resourceRoot: string, keyRoot: string, lessonName: string) = 
        let lessonEntry = {
            LessonRecord.ID = i;
            Name = lessonName
        }
        (lessonEntry, OneOffGamesUtils.ExtractStringsFromAssemblies(path, mainResPath, resDllName, resourceRoot, keyRoot, lessonEntry.ID))

    let (lessons, cardGroups) = 
        resTable
        |> Array.mapi createAndExtract
        |> Array.unzip

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cardGroups |> Array.collect id
    }

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

let ExtractMagicalDropV(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"localization\localization_de-DE.xml"
            FixPathSeps @"localization\localization_eng-US.xml"
            FixPathSeps @"localization\localization_es-ES.xml"
            FixPathSeps @"localization\localization_fr-FR.xml"
            FixPathSeps @"localization\localization_it-IT.xml"
            FixPathSeps @"localization\localization_ja-JP.xml"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let filePathsAndLanguages = 
        [| "de"; "en"; "es"; "fr"; "it"; "ja" |] |> Array.zip(filePaths)

    let lessonStoryEntry = {
        LessonRecord.ID = 0
        Name = "Story Text"
    }
    let lessonUIEntry = { 
        LessonRecord.ID = 1
        Name = "UI Text"
    }

    let generateCardsForLocalization(locPath: string, lang: string) = 
        let cleanedXml = File.ReadAllText(locPath) |> sanitizeMagicalDropVXml
        let (storyStrings, nonStoryStrings) = 
            generateMagicalDropVStringMap(cleanedXml)
            |> Map.partition(fun k t -> (k.StartsWith("story", StringComparison.InvariantCultureIgnoreCase) || k.StartsWith("ID_storyintro")))

        [|
            storyStrings |> AssemblyResourceTools.createCardRecordForStrings(lessonStoryEntry.ID, "storytext", lang, "masculine")
            nonStoryStrings |> AssemblyResourceTools.createCardRecordForStrings(lessonUIEntry.ID, "uitext", lang, "masculine")
        |] |> Array.concat

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonStoryEntry; lessonUIEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = filePathsAndLanguages |> Array.collect generateCardsForLocalization
    }

(***************************************************************************)
(******************************** Audiosurf ********************************)
(***************************************************************************)
let ExtractAudiosurf(path: string) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }

    let languageFiles = Directory.GetFiles(Path.Combine(path, FixPathSeps @"engine\LanguagePacks"), "aslang_*.xml", SearchOption.AllDirectories)
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
        |> AssemblyResourceTools.createCardRecordForStrings(lessonEntry.ID, "gametext", lang, "masculine")

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = languageFilesAndLanguages |> Array.collect generateCardsForLanguage
    }

(***************************************************************************)
(****************** Bastion, Transistor, Pyre, and Hades *******************)
(***************************************************************************)
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
    let fields = extractFieldsForLine(Some("\\t"))(l)

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
let ExtractHatofulBoyfriend(path: string) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }
    let filesToExtract = 
        [|
            Path.Combine(path, FixPathSeps @"hatoful_Data\StreamingAssets\LocalisationUnicode.txt")
        |]

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = 
            filesToExtract |> Array.collect (File.ReadAllLines >> Array.filter(String.IsNullOrWhiteSpace >> not) >> hbGenerateCardsForLines(lessonEntry.ID))
    }

(***************************************************************************)
(********************* Hatoful Boyfriend: Holiday Star *********************)
(***************************************************************************)
let ExtractHatofulBoyfriendHolidayStar(path: string) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }
    let filesToExtract = 
        [|
            Path.Combine(path, FixPathSeps @"HB2_Data\StreamingAssets\localisation\localisationunicode_EN.txt")
            Path.Combine(path, FixPathSeps @"HB2_Data\StreamingAssets\localisation\localisationunicode_FR.txt")
            Path.Combine(path, FixPathSeps @"HB2_Data\StreamingAssets\localisation\localisationunicode_GER.txt")
            Path.Combine(path, FixPathSeps @"HB2_Data\StreamingAssets\localisation\localisationunicode_JP.txt")
        |]

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = 
            filesToExtract |> Array.collect (File.ReadAllLines >> Array.filter(String.IsNullOrWhiteSpace >> not) >> hbGenerateCardsForLines(lessonEntry.ID))        
    }

(***************************************************************************)
(*************************** Braid & The Witness ***************************)
(***************************************************************************)
let private ExtractJBGames(path: string, fileNamesToLanguage: Map<string, string>) = 
    let lessonEntry = {
        LessonRecord.ID = 0
        Name = "Game Text"
    }

    let moFiles = Directory.GetFiles(Path.Combine(path, FixPathSeps @"data\strings"), "*.mo", SearchOption.AllDirectories)
    let createCardForStringPair(lesson: LessonRecord, language: string)(data: int * (MoString * MoString)) = 
        let (index, (original, translated)) = data
        let key = 
            if String.IsNullOrWhiteSpace(original.singular) then
                index.ToString()
            else
                original.singular

        {
            CardRecord.ID = 0
            CardRecord.Gender = "masculine"
            CardRecord.GenderlessKey = key
            CardRecord.GenderlessKeyHash = 0
            CardRecord.Key = key
            CardRecord.KeyHash = 0
            CardRecord.LanguageTag = language
            CardRecord.LessonID = lesson.ID
            CardRecord.Reversible = true
            CardRecord.SoundResource = ""
            CardRecord.Text = translated.singular.Trim().Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')
        }

    let createCardsForMoInfo(l: LessonRecord, s: Stream, mi: MoFileInfo, language: string) = 
        if (mi.translatedStringLengthsAndOffsets.Length = 0) then
            [||]
        else
            let possibleMimeHeader = GetTranslatedStrings(s, mi, Encoding.UTF8)([| 0 |]) |> Seq.head
            let (encoding, originalStringsStart, translatedStringsStart) = 
                match GetEncodingFromMIMEHeader(possibleMimeHeader.singular) with
                | Some(e) -> (e, 1, 1)
                | _ -> (Encoding.UTF8, 0, 0)

            GetTranslatedStrings(s, mi, encoding)(seq { translatedStringsStart..mi.translatedStringLengthsAndOffsets.Length - 1})
            |> Seq.zip(GetOriginalStrings(s, mi, encoding)(seq { originalStringsStart..mi.originalStringLengthsAndOffsets.Length - 1}))
            |> Seq.filter(fun (os, _) -> not(os.singular.StartsWith("credits_")))
            |> Seq.mapi(fun idx t -> (idx, t))
            |> Seq.map(createCardForStringPair(l, language))
            |> Array.ofSeq

    let createCardsForMoFile(l: LessonRecord)(mf: string) = 
        match fileNamesToLanguage |> Map.tryFind(Path.GetFileNameWithoutExtension(mf).ToLower()) with
        | Some(language) -> 
            // ***TODO: read the MO file, extract all original and translated strings, and then
            // create cards for them.
            use fs = new FileStream(mf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            let mi = GetMoInfo(fs)
            createCardsForMoInfo(l, fs, mi, language)
        | None -> 
            // ***TODO: output a message indicating that the language wasn't found
            [||]

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = moFiles |> Array.collect(createCardsForMoFile(lessonEntry))
    }
    
let ExtractBraid(path: string) = 
  let fileNamesToLanguage = 
    [|
      ("english", "en"); ("french", "fr"); ("german", "de"); ("italian", "it"); ("japanese", "ja")
      ("korean", "ko"); ("polish", "pl"); ("portuguese", "pt"); ("russian", "ru"); ("spanish", "es")
      ("tchinese", "zh"); ("czech", "cs"); ("georgian", "ka")
    |]
    |> Map.ofArray

  ExtractJBGames(path, fileNamesToLanguage)

let ExtractTheWitness(path: string) = 
  let fileNamesToLanguage = 
    [|
      ("ar", "ar"); ("de", "de"); ("en", "en"); ("es_ES", "es-ES"); ("es_LA", "es-LA")
      ("fr", "fr"); ("hu", "hu"); ("id", "id"); ("it", "it"); ("ja", "ja"); ("ko", "ko")
      ("pl", "pl"); ("pt_BR", "pt-BR"); ("pt_PT", "pt-PT"); ("ru", "ru")
      ("zh_CN", "zh-CN"); ("zh_TW", "zh-TW")
    |]
    |> Map.ofArray

  ExtractJBGames(path, fileNamesToLanguage)

(***************************************************************************)
(************************** IHF Handball Challenge *************************)
(***************************************************************************)
let private extractIHFHandballChallenge(path: string) = 
    let subtrees = [| "application"; "hud"; "gui" |]
    let subtreesAndLessons = 
        (subtrees |> Array.mapi (fun i n -> { LessonRecord.ID = i; Name = n }))
        |> Array.zip(subtrees)

    let countryToLanguageMap = 
        [|
            ("de", "de")
            ("dk", "da")
            ("en", "en")
            ("es", "es")
            ("fr", "fr")
            ("hu", "hu")
            ("it", "it")
            ("pl", "pl")
            ("pt", "pt")
            ("se", "sv")
        |]
        |> Map.ofArray

    let getCardsForXml(lang: string, fn: string) = 
        let sanitizeXml(s: string) = 
            s.Replace(new String([| char 0x1b |]), "")

        let sanitized = File.ReadAllText(fn) |> sanitizeXml
        let xel = XElement.Load(new StringReader(sanitized))

        let getCardsForSubtree(subtreeName: string, lesson: LessonRecord) = 
            let getKeyAndValueForElement(entry: XElement) = 
                let key = (entry.Descendants(XName.Get("key")) |> Array.ofSeq |> Seq.head).Value
                let regexesToRemove = 
                    [| 
                        (new Regex(@"\[.*?\]"), "")
                        (new Regex(@"\s\s+"), " ")
                    |]

                let rawValue =
                    (entry.Descendants(XName.Get("value")) |> Array.ofSeq |> Seq.head).Value

                let sanitizedValue = 
                    regexesToRemove |> Array.fold (fun (u: string)(t: Regex, replacement: string) -> t.Replace(u, replacement)) rawValue

                (key, sanitizedValue)

            xel.Descendants(XName.Get(subtreeName)) 
            |> Seq.collect(fun r -> r.Descendants(XName.Get("entry")))
            |> Array.ofSeq 
            |> Array.map getKeyAndValueForElement
            |> Map.ofArray
            |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, subtreeName, lang, "masculine")

        subtreesAndLessons
        |> Array.collect getCardsForSubtree

    let allCards = 
        Directory.GetDirectories(Path.Combine(path, @"language"))
        |> Array.map (fun d -> (countryToLanguageMap.Item(Path.GetFileName(d)), Path.Combine(d, FixPathSeps @"XMLData\i18n.xml")))
        |> Array.collect getCardsForXml

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = subtreesAndLessons |> Array.map snd
        LudumLinguarumPlugins.ExtractedContent.cards = allCards
    }

let ExtractIHFHandballChallenge12(path: string) = 
    extractIHFHandballChallenge(path)
let ExtractIHFHandballChallenge14(path: string) = 
    extractIHFHandballChallenge(path)

let internal extractIntroversionGame(path: string, archiveRelativePath: string, languageMappings: Map<string array, string>) =
    // create lessons for every distinct file in the language mappings
    let languageFileRoots = 
      languageMappings
      |> Map.toArray
      |> Array.map fst
      |> Array.collect (fun fileNames -> fileNames |> Array.map Path.GetFileNameWithoutExtension)

    let lessonsMap = 
      languageFileRoots 
      |> Array.mapi(fun i name -> { LessonRecord.ID = i; Name = name })
      |> Array.zip(languageFileRoots)
      |> Map.ofArray

    use archive = RarArchive.Open(Path.Combine(path, archiveRelativePath))
    let r = archive.ExtractAllEntries()

    let mutable entriesRemain = r.MoveToNextEntry()
    let mutable cards = [||]
    let cardsForEntry(l: string, fs: StreamReader, lessonRecord: LessonRecord) = 
        let r = 
            new Regex(@"(?<key>\S+)(\s+)(?<value>.+)")

        let nonCommentLines = 
            fs.ReadToEnd().Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter(fun l -> not(l.Trim().StartsWith("#")) && r.IsMatch(l))

        let kvForLine l = 
            let m = r.Match(l)
            (m.Groups.["key"].Value, m.Groups.["value"].Value)

        nonCommentLines
        |> Array.map kvForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lessonRecord.ID, "", l, "masculine")

    while (entriesRemain) do
        match languageMappings |> Map.tryFindKey(fun(ks)(_) -> ks |> Array.contains(r.Entry.Key)) with
        | Some(ks) ->
          let language = languageMappings.Item(ks)
          use fs = new StreamReader(r.OpenEntryStream(), Encoding.UTF8)
          let rootName = Path.GetFileNameWithoutExtension(r.Entry.Key)
          cards <- Array.concat([cards; cardsForEntry(language, fs, lessonsMap |> Map.find rootName)])
        | _ -> ()

        entriesRemain <- r.MoveToNextEntry()

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessonsMap |> Map.toArray |> Array.map snd
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }

let ExtractDefcon(path: string) = 
    let languageMappings = 
        [|
            ([| FixPathSeps @"data\language\english.txt" |], "en")
            ([| FixPathSeps @"data\language\french.txt" |], "fr")
            ([| FixPathSeps @"data\language\german.txt" |], "de")
            ([| FixPathSeps @"data\language\italian.txt" |], "it")
            ([| FixPathSeps @"data\language\spanish.txt" |], "es")
        |]
        |> Map.ofArray

    extractIntroversionGame(path, "main.dat", languageMappings)

let ExtractDarwinia(path: string) = 
    let languageMappings = 
        [|
            ([| FixPathSeps @"data\language\english.txt" |], "en")
            ([| FixPathSeps @"data\language\french.txt" |], "fr")
            ([| FixPathSeps @"data\language\german.txt" |], "de")
            ([| FixPathSeps @"data\language\italian.txt" |], "it")
            ([| FixPathSeps @"data\language\spanish.txt" |], "es")
        |]
        |> Map.ofArray

    extractIntroversionGame(path, "language.dat", languageMappings)

let ExtractMultiwinia(path: string) = 
    let languageMappings = 
        [|
            ([| FixPathSeps @"data\language\english.txt" |], "en")
            ([| FixPathSeps @"data\language\french.txt" |], "fr")
            ([| FixPathSeps @"data\language\german.txt" |], "de")
            ([| FixPathSeps @"data\language\italian.txt" |], "it")
            ([| FixPathSeps @"data\language\spanish.txt" |], "es")
        |]
        |> Map.ofArray

    extractIntroversionGame(path, "language.dat", languageMappings)

let ExtractPrisonArchitect(path: string) = 
  let languageMappings = 
    [|
      ([| FixPathSeps @"data\language\bulgarian\base-language.txt"; FixPathSeps @"data\language\bulgarian\fullgame.txt" |], "bg")
      ([| FixPathSeps @"data\language\czech\base-language.txt"; FixPathSeps @"data\language\czech\fullgame.txt" |], "cs")
      ([| FixPathSeps @"data\language\danish\base-language.txt"; FixPathSeps @"data\language\danish\fullgame.txt" |], "da")
      ([| FixPathSeps @"data\language\dutch\base-language.txt"; FixPathSeps @"data\language\dutch\fullgame.txt" |], "nl")
      ([| FixPathSeps @"data\language\base-language.txt"; FixPathSeps @"data\language\fullgame.txt" |], "en")
      ([| FixPathSeps @"data\language\finnish\base-language.txt"; FixPathSeps @"data\language\finnish\fullgame.txt" |], "fi")
      ([| FixPathSeps @"data\language\french\base-language.txt"; FixPathSeps @"data\language\french\fullgame.txt" |], "fr")
      ([| FixPathSeps @"data\language\german\base-language.txt"; FixPathSeps @"data\language\german\fullgame.txt" |], "de")
      ([| FixPathSeps @"data\language\greek\base-language.txt"; FixPathSeps @"data\language\greek\fullgame.txt" |], "el")
      ([| FixPathSeps @"data\language\hungarian\base-language.txt"; FixPathSeps @"data\language\hungarian\fullgame.txt" |], "hu")
      ([| FixPathSeps @"data\language\italian\base-language.txt"; FixPathSeps @"data\language\italian\fullgame.txt" |], "it")
      ([| FixPathSeps @"data\language\japanese\base-language.txt"; FixPathSeps @"data\language\japanese\fullgame.txt" |], "ja")
      ([| FixPathSeps @"data\language\korean\base-language.txt"; FixPathSeps @"data\language\korean\fullgame.txt" |], "ko")
      ([| FixPathSeps @"data\language\norwegian\base-language.txt"; FixPathSeps @"data\language\norwegian\fullgame.txt" |], "no")
      ([| FixPathSeps @"data\language\polish\base-language.txt"; FixPathSeps @"data\language\polish\fullgame.txt" |], "pl")
      ([| FixPathSeps @"data\language\portuguese\base-language.txt"; FixPathSeps @"data\language\portuguese\fullgame.txt" |], "pt-PT")
      ([| FixPathSeps @"data\language\portuguese-brazil\base-language.txt"; FixPathSeps @"data\language\portuguese-brazil\fullgame.txt" |], "pt-BR")
      ([| FixPathSeps @"data\language\romanian\base-language.txt"; FixPathSeps @"data\language\romanian\fullgame.txt" |], "ro")
      ([| FixPathSeps @"data\language\russian\base-language.txt"; FixPathSeps @"data\language\russian\fullgame.txt" |], "ru")
      ([| FixPathSeps @"data\language\simplifiedchinese\base-language.txt"; FixPathSeps @"data\language\simplifiedchinese\fullgame.txt" |], "zh-CN")
      ([| FixPathSeps @"data\language\spanish\base-language.txt"; FixPathSeps @"data\language\spanish\fullgame.txt" |], "es")
      ([| FixPathSeps @"data\language\swedish\base-language.txt"; FixPathSeps @"data\language\swedish\fullgame.txt" |], "sv")
      ([| FixPathSeps @"data\language\thai\base-language.txt"; FixPathSeps @"data\language\thai\fullgame.txt" |], "th")
      ([| FixPathSeps @"data\language\traditionalchinese\base-language.txt"; FixPathSeps @"data\language\traditionalchinese\fullgame.txt" |], "zh-TW")
      ([| FixPathSeps @"data\language\turkish\base-language.txt"; FixPathSeps @"data\language\turkish\fullgame.txt" |], "tr")
      ([| FixPathSeps @"data\language\ukrainian\base-language.txt"; FixPathSeps @"data\language\ukrainian\fullgame.txt" |], "uk")
    |]
    |> Map.ofArray

  extractIntroversionGame(path, "main.dat", languageMappings)

(***************************************************************************)
(****************************** The Escapists ******************************)
(***************************************************************************)
let ExtractTheEscapists(path: string) = 
    let languageMap = 
        [| ("eng", "en"); ("fre", "fr"); ("ger", "de"); ("ita", "it"); ("pol", "pl"); ("rus", "ru"); ("spa", "es") |]

    let resourcesToLessons = 
        [|
            (FixPathSeps @"Data\data_{0}.dat", { LessonRecord.ID = 0; Name = "Game Data" })
            (FixPathSeps @"Data\items_{0}.dat", { LessonRecord.ID = 1; Name = "Items" })
            (FixPathSeps @"Data\speech_{0}.dat", { LessonRecord.ID = 2; Name = "Speech" })
            (FixPathSeps @"Editor\data\{0}.dat", { LessonRecord.ID = 3; Name = "Editor" })
        |]

    let cardsForResource(languagePath: string, language: string)(resourcePathAndLesson: string * LessonRecord) = 
        let (resourcePath, lesson) = resourcePathAndLesson
        let filePath = Path.Combine(path, String.Format(resourcePath, languagePath))

        // Filter out the numeric value prefix for 'Craft' keys in the items file.
        let itemCraftMap(key: KeyData) = 
            if (lesson.Name = "Items") && (key.KeyName = "Craft") && (key.Value.Contains("_")) then
                key.Value <- new String(key.Value.ToCharArray() |> Array.skipWhile(fun c -> c <> '_') |> Array.skip(1))
                key
            else
                key

        let cardsForSection(sectionName: string, keys: KeyDataCollection) = 
            keys
            |> Seq.map itemCraftMap
            |> Seq.map (fun key -> (key.KeyName, key.Value))
            |> Array.ofSeq
            |> Map.ofArray
            |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, sectionName, language, "masculine")

        // We only want the 'Name' and 'Craft' entries out of the items file.
        let filterItemLines(line: string) = 
            (lesson.Name <> "Items") || line.StartsWith("[") || line.StartsWith("Name") || line.StartsWith("Craft")

        try
            let fileLines = 
                File.ReadAllLines(filePath, Encoding.GetEncoding(1252))
                |> Array.map(fun line -> line.Trim())
                |> Array.filter(fun line -> not(String.IsNullOrWhiteSpace(line)))
                |> Array.filter(fun line -> not(line.StartsWith("count=")))
                |> Array.filter(fun line -> not(line.StartsWith("---") && line.Contains("DLC")))
                |> Array.skipWhile(fun line -> not(line.StartsWith("[")))
                |> Array.filter filterItemLines
            let fileContents = String.Join(Environment.NewLine, fileLines)
                
            let parser = new IniDataParser()
            parser.Configuration.AllowDuplicateKeys <- true
            parser.Configuration.SkipInvalidLines <- true

            let iniData = parser.Parse(fileContents)

            iniData.Sections
            |> Seq.collect (fun section -> cardsForSection(section.SectionName, section.Keys))
            |> Array.ofSeq
        with
            | ex -> [||]

    let cardsForLanguage languagePathAndLanguage = 
        let (languagePath, language) = languagePathAndLanguage

        resourcesToLessons
        |> Array.collect(cardsForResource(languagePath, language))

    let cards = 
        languageMap
        |> Array.collect cardsForLanguage

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = resourcesToLessons |> Array.map snd
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }

(***************************************************************************)
(***************************** Super Meat Boy ******************************)
(***************************************************************************)
type MeatBoyStrings() = 
    member val ``ref`` = "" with get, set
    member val English = "" with get, set
    member val Japanese = "" with get, set
    member val German = "" with get, set
    member val French = "" with get, set
    member val Spanish = "" with get, set
    member val Italian = "" with get, set
    member val Korean = "" with get, set
    member val Tchinese = "" with get, set
    member val Portuguese = "" with get, set
    member val Schinese = "" with get, set
    member val Polish = "" with get, set
    member val Russian = "" with get, set

let ExtractSuperMeatBoy(path: string) = 
    let cardForLanguage(key: string, text: string, language: string) = 
        {
            CardRecord.ID = 0
            LessonID = 0
            Text = text
            Gender = "masculine"
            Key = key
            GenderlessKey = key
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = language
            Reversible = true
        }

    let cardsForMBS(mbs: MeatBoyStrings) = 
        [|
            cardForLanguage(mbs.``ref``, mbs.English, "en")
            cardForLanguage(mbs.``ref``, mbs.Japanese, "ja")
            cardForLanguage(mbs.``ref``, mbs.German, "de")
            cardForLanguage(mbs.``ref``, mbs.French, "fr")
            cardForLanguage(mbs.``ref``, mbs.Spanish, "es")
            cardForLanguage(mbs.``ref``, mbs.Italian, "it")
            cardForLanguage(mbs.``ref``, mbs.Korean, "kr")
            cardForLanguage(mbs.``ref``, mbs.Tchinese, "zh-TW")
            cardForLanguage(mbs.``ref``, mbs.Portuguese, "pt")
            cardForLanguage(mbs.``ref``, mbs.Schinese, "zh-CN")
            cardForLanguage(mbs.``ref``, mbs.Polish, "pl")
            cardForLanguage(mbs.``ref``, mbs.Russian, "ru")
        |]

    let cards = 
        File.ReadAllText(Path.Combine(path, @"locdb.txt"))
        |> CsvTools.extractCsv<MeatBoyStrings>(Some("\t"))
        |> Array.collect cardsForMBS

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| { LessonRecord.ID = 0; Name = "Game Text" } |]
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }

(***************************************************************************)
(*********************** Mega Man Legacy Collection ************************)
(***************************************************************************)
let ExtractMegaManLegacyCollection(path: string) = 
    let languages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "pt-BR"; "ru" |]

    use archive = new Ionic.Zip.ZipFile(Path.Combine(path, "data.pie"))
    archive.set_Password("P091uWEdwe4lI6StDNMNlkodPGvJ38bL3HW6t3BCMYdFi83FXKu7k0NsHP8caDKS")
    archive.Encryption <- Ionic.Zip.EncryptionAlgorithm.WinZipAes256

    // Extract game files to a temporary path, but try cleaning it first.
    let tempPath = Path.Combine(Path.GetTempPath(), @"LudumLinguarumMMLC")
    try Directory.Delete(tempPath) with | _ -> ()

    let cardsForLanguage(language: string) = 
        let stringsFilePath = Path.Combine(tempPath, FixPathSeps(@"locale\Strings-" + language.ToLowerInvariant() + ".bin"))
        let stringsFileBytes = File.ReadAllBytes(stringsFilePath)

        // Read null-terminated strings until the end of the file
        Seq.unfold (readNextStringUnfold(stringsFileBytes, Encoding.UTF8)) 0x1FF4
        |> Array.ofSeq
        |> Array.mapi(fun i s -> (i.ToString(), s))
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(0, "", language, "masculine")

    let cards = 
        try
            try
                archive.ExtractSelectedEntries("*.*", "locale", tempPath)
                languages |> Array.collect cardsForLanguage
            with
            | _ -> [||]
         finally
            try Directory.Delete(tempPath, true) with | _ -> ()

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| { LessonRecord.ID = 0; Name = "Game Text" } |]
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
