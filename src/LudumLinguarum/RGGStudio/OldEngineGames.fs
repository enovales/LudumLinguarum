module RGGStudio.OldEngineGames

open LLDatabase
open LudumLinguarumPlugins
open RGGStudio.LocalizedBinFile
open RGGStudio.ParArchive
open System
open System.IO
open System.Text.RegularExpressions

// The pre-Dragon Engine RGG Studio games (Yakuza 0, Yakuza Kiwami) keep their
// localized text in 20070319 .bin files inside media/data/bootpar/boot.par.
// Each text file has a language-suffixed variant per language:
//   .bin_j - Japanese
//   .bin_c - Chinese slot, which holds the English localization on the PC
//            releases
//   .bin_k - Korean

let private languageSuffixes =
    [|
        ("j", "ja")
        ("c", "en")
        ("k", "ko")
    |] |> Map.ofArray

let private languageSuffixRegex = Regex(@"\.bin_([a-z])$", RegexOptions.Compiled)

let private fileNameWithoutLanguageSuffix(fileName: string): string option =
    let matchResult = languageSuffixRegex.Match(fileName)
    if matchResult.Success then
        Some(fileName.Substring(0, matchResult.Index))
    else
        None

let private entryLanguageTag(entryPath: string): (string * string) option =
    let normalizedPath = entryPath.Replace('\\', '/')
    let fileName = Path.GetFileName(normalizedPath)
    let matchResult = languageSuffixRegex.Match(fileName)
    if not matchResult.Success then
        None
    else
        let suffix = matchResult.Groups.[1].Value
        match languageSuffixes |> Map.tryFind suffix with
        | Some(languageTag) -> Some((fileNameWithoutLanguageSuffix(fileName).Value, languageTag))
        | None -> None

let private isLocalizedBinEntry(entry: ParEntry): bool =
    (entryLanguageTag entry.path).IsSome
    && entry.data.Length >= 4
    && entry.data.[0..3] = binMagic

let private sanitizeText(s: string): string =
    if String.IsNullOrWhiteSpace(s) then
        ""
    else
        let normalized = s.Replace('\r', ' ').Replace('\n', ' ').Trim()
        let collapsed = Regex.Replace(normalized, @"\s+", " ")
        collapsed.Trim()

let internal extractCardsFromParEntries(entries: ParEntry array): ExtractedContent =
    let textEntries =
        entries
        |> Array.filter isLocalizedBinEntry
        |> Array.map (fun entry ->
            let (lessonName, languageTag) = entryLanguageTag entry.path |> Option.get
            (lessonName, languageTag, parseLocalizedBinFile entry.data))

    if textEntries.Length = 0 then
        raise(ArgumentException("No localized .bin text files found in the game archive. Is this a supported RGG Studio game installation?"))

    let lessons =
        textEntries
        |> Array.map (fun (lessonName, _, _) -> lessonName)
        |> Array.distinct
        |> Array.sort
        |> Array.mapi (fun i name -> { LessonRecord.ID = i; Name = name })

    let lessonIdByName = lessons |> Array.map (fun l -> (l.Name, l.ID)) |> Map.ofArray

    let cards =
        textEntries
        |> Array.collect (fun (lessonName, languageTag, binFile) ->
            let lessonId = lessonIdByName.[lessonName]
            binFile.parameters
            |> Array.collect (fun parameter ->
                parameter.values
                |> Array.mapi (fun entryIndex text ->
                    (entryIndex, sanitizeText text))
                |> Array.filter (fun (_, text) -> text <> "")
                |> Array.map (fun (entryIndex, text) ->
                    let key = parameter.name + "/" + string entryIndex
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
                    })))

    {
        ExtractedContent.lessons = lessons
        ExtractedContent.cards = cards
    }

let private extractOldEngineGame(path: string): ExtractedContent =
    let bootParPath = Path.Combine(path, "media", "data", "bootpar", "boot.par")
    if not(File.Exists(bootParPath)) then
        raise(ArgumentException("Could not find '" + bootParPath + "'. Is this the root directory of the game?"))

    extractCardsFromParEntries(readParArchiveFromFile(bootParPath))

let ExtractYakuza0(path: string): ExtractedContent =
    extractOldEngineGame(path)

let ExtractYakuzaKiwami(path: string): ExtractedContent =
    extractOldEngineGame(path)
