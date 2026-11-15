module RGGStudio.DragonEngineGames

open LLDatabase
open LudumLinguarumPlugins
open RGGStudio.ArmpBinFile
open RGGStudio.ParArchive
open System
open System.IO
open System.Text.RegularExpressions

// Dragon Engine RGG Studio games store their localized text as armp .bin
// tables. Depending on the game, those tables are either loose files in a
// language-coded directory (Yakuza 6 / Kiwami 2: data/db/e) or packed inside
// language-suffixed PAR archives (Judgment, Yakuza: Like a Dragon and later:
// db.yazawa.en.par). The game data lives either in <root>/data or in
// <root>/runtime/media/data depending on the release.

let private languageCodeMap =
    [|
        ("en", "en"); ("e", "en"); ("eng", "en")
        ("ja", "ja"); ("j", "ja"); ("jp", "ja"); ("jpn", "ja")
        ("de", "de"); ("ger", "de")
        ("es", "es"); ("spa", "es")
        ("fr", "fr"); ("fre", "fr"); ("fra", "fr")
        ("it", "it"); ("ita", "it")
        ("ko", "ko"); ("k", "ko"); ("kor", "ko")
        ("pt", "pt-BR"); ("pt-br", "pt-BR"); ("bra", "pt-BR")
        ("ru", "ru"); ("rus", "ru")
        ("zh", "zh-TW"); ("zht", "zh-TW"); ("tw", "zh-TW"); ("c", "zh-TW"); ("cht", "zh-TW")
        ("zhs", "zh-CN"); ("cn", "zh-CN"); ("zh-cn", "zh-CN"); ("chs", "zh-CN")
    |] |> Map.ofArray

/// Maps a game language code (from a par file name or db directory name) to
/// an IETF language tag, or None if the code is not a language code.
let internal languageTagFromCode(code: string): string option =
    let normalized = code.ToLowerInvariant()
    languageCodeMap |> Map.tryFind normalized

let private parLanguageRegex = Regex(@"\.([a-z]+)\.par$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

let private parFileLanguageTag(parPath: string): string option =
    let fileName = Path.GetFileName(parPath)
    let matchResult = parLanguageRegex.Match(fileName)
    if not matchResult.Success then
        None
    else
        languageTagFromCode(matchResult.Groups.[1].Value)

let private sanitizeText(s: string): string =
    if String.IsNullOrWhiteSpace(s) then
        ""
    else
        let normalized = s.Replace('\r', ' ').Replace('\n', ' ').Trim()
        let collapsed = Regex.Replace(normalized, @"\s+", " ")
        collapsed.Trim()

type internal TextSource =
    | ParArchiveSource of string * string
    | LooseDirectorySource of string * string

let private textSourcesForDataDir(dataDir: string): TextSource array =
    let parSources =
        try
            Directory.EnumerateFiles(dataDir, "*.par", SearchOption.AllDirectories)
            |> Seq.choose (fun parPath ->
                match parFileLanguageTag(parPath) with
                | Some(languageTag) -> Some(ParArchiveSource(parPath, languageTag))
                | None -> None)
            |> List.ofSeq
        with
        | :? UnauthorizedAccessException -> []
        | :? PathTooLongException -> []

    let dbDir = Path.Combine(dataDir, "db")
    let looseSources =
        if Directory.Exists(dbDir) then
            try
                Directory.EnumerateDirectories(dbDir)
                |> Seq.choose (fun dir ->
                    match languageTagFromCode(Path.GetFileName(dir)) with
                    | Some(languageTag) -> Some(LooseDirectorySource(dir, languageTag))
                    | None -> None)
                |> List.ofSeq
            with
            | :? UnauthorizedAccessException -> []
        else
            []

    Array.ofList (parSources @ looseSources)

let private cardsForBinFile(lessonId: int)(languageTag: string)(binName: string)(armpFile: ArmpFile): CardRecord array =
    armpFile.columns
    |> Array.collect (fun column ->
        column.values
        |> Array.mapi (fun rowIndex text -> (rowIndex, sanitizeText text))
        |> Array.filter (fun (_, text) -> text <> "")
        |> Array.map (fun (rowIndex, text) ->
            let key = column.name + "/" + string rowIndex
            {
                CardRecord.ID = 0
                LessonID = lessonId
                Text = text
                Gender = "masculine"
                Key = key
                GenderlessKey = key
                KeyHash = 0
                GenderlessKeyHash = 0
                SoundResource = ""
                LanguageTag = languageTag
                Reversible = true
            }))

let private lessonNameForBinFile(binPath: string): string =
    let fileName = Path.GetFileName(binPath.Replace('\\', '/'))
    if fileName.EndsWith(".bin", StringComparison.InvariantCultureIgnoreCase) then
        fileName.Substring(0, fileName.Length - 4)
    else
        fileName

let internal extractCardsFromTextSources(sources: TextSource array): ExtractedContent =
    let tableSources =
        sources
        |> Array.collect (fun source ->
            match source with
            | ParArchiveSource(parPath, languageTag) ->
                readParArchiveFromFile(parPath)
                |> Array.filter (fun entry -> isArmpBinFile(entry.data))
                |> Array.map (fun entry -> (lessonNameForBinFile(entry.path), languageTag, parseArmpBinFile(entry.data)))
            | LooseDirectorySource(dir, languageTag) ->
                try
                    Directory.EnumerateFiles(dir, "*.bin", SearchOption.AllDirectories)
                    |> Seq.filter (fun binPath -> isArmpBinFile(File.ReadAllBytes(binPath)))
                    |> Seq.map (fun binPath ->
                        (lessonNameForBinFile(binPath), languageTag, parseArmpBinFile(File.ReadAllBytes(binPath))))
                    |> List.ofSeq
                with
                | :? UnauthorizedAccessException -> []
                |> Array.ofList)

    if tableSources.Length = 0 then
        raise(ArgumentException("No localized armp .bin text files found. Is this a supported RGG Studio game installation?"))

    let lessons =
        tableSources
        |> Array.map (fun (lessonName, _, _) -> lessonName)
        |> Array.distinct
        |> Array.sort
        |> Array.mapi (fun i name -> { LessonRecord.ID = i; Name = name })

    let lessonIdByName = lessons |> Array.map (fun l -> (l.Name, l.ID)) |> Map.ofArray

    let cards =
        tableSources
        |> Array.collect (fun (lessonName, languageTag, armpFile) ->
            cardsForBinFile(lessonIdByName.[lessonName])(languageTag)(lessonName)(armpFile))

    {
        ExtractedContent.lessons = lessons
        ExtractedContent.cards = cards
    }

let internal extractDragonEngineGame(path: string): ExtractedContent =
    let dataDirs =
        [|
            Path.Combine(path, "data")
            Path.Combine(path, "runtime", "media", "data")
        |]
        |> Array.filter Directory.Exists

    if dataDirs.Length = 0 then
        raise(ArgumentException("Could not find a 'data' or 'runtime/media/data' directory under '" + path + "'. Is this the root directory of the game?"))

    let sources = dataDirs |> Array.collect textSourcesForDataDir

    if sources.Length = 0 then
        raise(ArgumentException("No language-specific text archives found under '" + path + "'. Is this a supported RGG Studio game installation?"))

    extractCardsFromTextSources(sources)

let ExtractYakuza6(path: string): ExtractedContent =
    extractDragonEngineGame(path)

let ExtractYakuzaKiwami2(path: string): ExtractedContent =
    extractDragonEngineGame(path)

let ExtractJudgment(path: string): ExtractedContent =
    extractDragonEngineGame(path)

let ExtractLostJudgment(path: string): ExtractedContent =
    extractDragonEngineGame(path)

let ExtractYakuzaLikeADragon(path: string): ExtractedContent =
    extractDragonEngineGame(path)
