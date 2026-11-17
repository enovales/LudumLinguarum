module RggEngine.RggGames

open LLDatabase
open LLUtils
open RggEngine.ParArchive
open RggEngine.RggFormats
open RggEngine.Sllz
open System
open System.Collections.Generic
open System.IO
open System.Text

/// Engine family for an RGG Studio game.
type RggEngineKind =
    | OldEngine
    | DragonEngine

/// Metadata describing a supported RGG Studio game.
type RggGameSpec =
    {
        Name: string
        Engine: RggEngineKind
        /// Optional Dragon Engine archive codename (e.g. "yazawa", "judge", "coyote", "aston", "elvis", "spr").
        Codename: string option
        SupportedLanguages: string array
    }

/// Multi-letter language code mapping (used in Dragon Engine and Old Engine Remasters).
let internal multiLetterLanguageMap =
    [|
        ("de", "de")
        ("en", "en")
        ("es", "es")
        ("ese", "es")
        ("esl", "es-419")
        ("ls", "es-419")
        ("fr", "fr")
        ("it", "it")
        ("ja", "ja")
        ("jp", "ja")
        ("ko", "ko")
        ("kr", "ko")
        ("pl", "pl")
        ("pt", "pt-BR")
        ("br", "pt-BR")
        ("ru", "ru")
        ("zh", "zh-TW")
        ("zht", "zh-TW")
        ("cht", "zh-TW")
        ("zhs", "zh-CN")
        ("chs", "zh-CN")
    |]
    |> Map.ofArray

/// Dragon Engine single-letter language directory codes (used in Yakuza 6: data/db/e, data/db/j, data/db/c, data/db/k).
let internal dragonEngineSingleLetterMap =
    [|
        ("e", "en")
        ("j", "ja")
        ("c", "zh-TW")
        ("k", "ko")
    |]
    |> Map.ofArray

/// Old Engine single-letter language suffix codes (used in Yakuza 0 / Yakuza Kiwami: .bin_c, .bin_j, wdr_par_c, auth_w64_e, etc.).
let internal oldEngineSingleLetterMap =
    [|
        ("c", "en")
        ("e", "en")
        ("j", "ja")
        ("h", "zh-CN")
        ("z", "zh-TW")
        ("k", "ko")
        ("d", "de")
        ("f", "fr")
        ("i", "it")
        ("l", "es-419")
        ("p", "pt-BR")
        ("r", "ru")
        ("s", "es")
    |]
    |> Map.ofArray

/// Maps a Dragon Engine language folder/archive code to an IETF language tag.
let tryMapDragonEngineLanguage (code: string) : string option =
    if String.IsNullOrWhiteSpace(code) then
        None
    else
        let lower = code.Trim().ToLowerInvariant()
        match multiLetterLanguageMap |> Map.tryFind lower with
        | Some tag -> Some tag
        | None -> dragonEngineSingleLetterMap |> Map.tryFind lower

/// Maps an Old Engine language suffix/folder code to an IETF language tag.
let tryMapOldEngineLanguage (code: string) : string option =
    if String.IsNullOrWhiteSpace(code) then
        None
    else
        let lower = code.Trim().ToLowerInvariant()
        match multiLetterLanguageMap |> Map.tryFind lower with
        | Some tag -> Some tag
        | None -> oldEngineSingleLetterMap |> Map.tryFind lower

/// Finds candidate data root directories inside a game installation path.
let internal findDataRoots (gamePath: string) : string array =
    let normalizedRoot = FixPathSeps gamePath
    if not (Directory.Exists(normalizedRoot)) then
        [||]
    else
        let subCandidates =
            [|
                Path.Combine(normalizedRoot, "runtime", "media", "data")
                Path.Combine(normalizedRoot, "media", "data")
                Path.Combine(normalizedRoot, "main", "data")
                Path.Combine(normalizedRoot, "data")
            |]
            |> Array.filter Directory.Exists

        if subCandidates.Length > 0 then
            subCandidates
        else
            [| normalizedRoot |]

let private createCard (lessonId: int) (languageTag: string) (key: string) (text: string) : CardRecord =
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
    }

/// Parses an Old Engine file's bytes based on its filename and returns (entryKey, text) pairs.
let internal parseOldEngineFileBytes (fileName: string, rawBytes: byte array, languageTag: string) : (string * string) array =
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
    let encoding = Encoding.UTF8
    let lowerName = fileName.ToLowerInvariant()
    try
        if lowerName.StartsWith("string_tbl.bin", StringComparison.Ordinal) then
            StringTbl.parse (rawBytes, encoding)
        elif lowerName.StartsWith("encounter_pupup_message.bin", StringComparison.Ordinal)
             || lowerName.StartsWith("encounter_popup_message.bin", StringComparison.Ordinal) then
            Epmb.parse (rawBytes, encoding)
        elif lowerName.StartsWith("dispose_string.bin", StringComparison.Ordinal) then
            OffsetTableBin.parse (rawBytes, 8L, encoding)
        elif lowerName.EndsWith(".msg", StringComparison.Ordinal) then
            Msg.parse (rawBytes, encoding)
        elif lowerName = "cmn.bin" then
            CmnBin.parse (rawBytes, encoding, languageTag <> "ja")
        elif Armp.isArmp rawBytes then
            Armp.parse rawBytes
        elif Bin20070319.isBin20070319 rawBytes then
            Bin20070319.parse (rawBytes, encoding)
        else
            [||]
    with _ ->
        [||]

/// Parses a Dragon Engine file's bytes based on its filename and returns (entryKey, text) pairs.
let internal parseDragonEngineFileBytes (fileName: string, rawBytes: byte array, languageTag: string) : (string * string) array =
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
    let lowerName = fileName.ToLowerInvariant()
    try
        if Armp.isArmp rawBytes then
            Armp.parse rawBytes
        elif lowerName = "cmn.bin" then
            CmnBin.parse (rawBytes, Encoding.UTF8, languageTag <> "ja")
        elif Bin20070319.isBin20070319 rawBytes then
            Bin20070319.parse (rawBytes, Encoding.UTF8)
        else
            [||]
    with _ ->
        [||]

/// Checks whether a filename has an Old Engine language suffix (such as "ability.bin_c", "ability.bin_j", "name_c.bin")
/// or a leading language directory segment (such as "en/sound_auth_text.bin" inside db.nex)
/// and returns (normalizedBaseNameWithoutExtension, languageTag option).
let internal splitOldEngineFileLanguage (filePath: string) : string * string option =
    let normalized = filePath.Replace('\\', '/').Trim('/')
    let dirPart =
        let lastSlash = normalized.LastIndexOf('/')
        if lastSlash >= 0 then normalized.Substring(0, lastSlash + 1) else ""
    let fileName = Path.GetFileName(normalized)
    let ext = Path.GetExtension(fileName)
    let stem = Path.GetFileNameWithoutExtension(fileName)

    let tryLeadingDirLanguage () =
        let firstSlash = dirPart.IndexOf('/')
        if firstSlash > 0 then
            let firstSeg = dirPart.Substring(0, firstSlash).ToLowerInvariant()
            match multiLetterLanguageMap |> Map.tryFind firstSeg with
            | Some tag ->
                let restDir = dirPart.Substring(firstSlash + 1)
                (restDir + stem, Some tag)
            | None ->
                (dirPart + stem, None)
        else
            (dirPart + stem, None)

    // Case 1: ".bin_c", ".bin_j", ".bin_en", etc.
    if ext.StartsWith(".bin_", StringComparison.OrdinalIgnoreCase) && ext.Length > 5 then
        let langCode = ext.Substring(5)
        match tryMapOldEngineLanguage langCode with
        | Some tag -> (dirPart + stem, Some tag)
        | None -> (dirPart + stem, None)
    else
        // Case 2: "name_c.bin", "name_j.bin", "boot_en.bin", etc.
        let lastUnderscore = stem.LastIndexOf('_')
        if lastUnderscore > 0 && lastUnderscore < stem.Length - 1 then
            let suffix = stem.Substring(lastUnderscore + 1)
            match tryMapOldEngineLanguage suffix with
            | Some tag ->
                let cleanStem = stem.Substring(0, lastUnderscore)
                (dirPart + cleanStem, Some tag)
            | None ->
                tryLeadingDirLanguage ()
        else
            tryLeadingDirLanguage ()

/// Checks whether a container name (folder or .par stem, e.g. "boot_en", "wdr_par_c", "pausepar_e", "auth_w64_j")
/// has an Old Engine language suffix, returning (normalizedCategoryName, languageTag option).
let internal splitOldEngineContainerLanguage (containerName: string) : string * string option =
    let stem =
        if containerName.EndsWith(".par", StringComparison.OrdinalIgnoreCase) then
            containerName.Substring(0, containerName.Length - 4)
        else
            containerName

    let normalizeCategory (rawCat: string) =
        let lower = rawCat.ToLowerInvariant()
        if lower.StartsWith("wdr_par", StringComparison.Ordinal) || lower.StartsWith("wdr", StringComparison.Ordinal) then "wdr"
        elif lower.StartsWith("boot", StringComparison.Ordinal) then "boot"
        elif lower.StartsWith("stay", StringComparison.Ordinal) then "stay"
        elif lower.StartsWith("pause", StringComparison.Ordinal) then "pause"
        elif lower.StartsWith("auth", StringComparison.Ordinal) then "auth"
        elif lower.StartsWith("hact", StringComparison.Ordinal) then "hact"
        elif lower.StartsWith("map", StringComparison.Ordinal) then "map"
        elif lower = "common" then "wdr/common"
        else lower

    let lastUnderscore = stem.LastIndexOf('_')
    if lastUnderscore > 0 && lastUnderscore < stem.Length - 1 then
        let suffix = stem.Substring(lastUnderscore + 1)
        match tryMapOldEngineLanguage suffix with
        | Some tag ->
            let baseCat = stem.Substring(0, lastUnderscore)
            (normalizeCategory baseCat, Some tag)
        | None ->
            (normalizeCategory stem, None)
    else
        (normalizeCategory stem, None)

/// Extracts cards from a collection of virtual files belonging to an Old Engine container.
let private extractCardsFromOldEngineVirtualFiles
    (lessonId: int)
    (containerCategory: string)
    (containerLanguageOpt: string option)
    (virtualFiles: (string * byte array) array)
    : CardRecord array =

    // Identify all base stems that have an explicit localized ".bin_c" or ".bin_e" file so that
    // an unsuffixed sibling ".bin" in the same container can be recognized as the original Japanese ("ja") file.
    let stemsWithExplicitLocalization = HashSet<string>(StringComparer.OrdinalIgnoreCase)
    for (vPath, _) in virtualFiles do
        let (baseStem, langOpt) = splitOldEngineFileLanguage vPath
        if langOpt.IsSome then
            stemsWithExplicitLocalization.Add(baseStem) |> ignore

    let cards = ResizeArray<CardRecord>()
    for (vPath, bytes) in virtualFiles do
        let fileName = Path.GetFileName(vPath)
        let (baseStem, fileLangOpt) = splitOldEngineFileLanguage vPath
        let effectiveLangOpt =
            match fileLangOpt, containerLanguageOpt with
            | Some lang, _ -> Some lang
            | None, Some lang -> Some lang
            | None, None ->
                if stemsWithExplicitLocalization.Contains(baseStem) then
                    Some "ja"
                else
                    None

        match effectiveLangOpt with
        | Some langTag ->
            let entries = parseOldEngineFileBytes (fileName, bytes, langTag)
            let keyPrefix =
                if String.IsNullOrEmpty(containerCategory) then baseStem
                else $"{containerCategory}/{baseStem}"
            for (entryKey, text) in entries do
                cards.Add(createCard lessonId langTag $"{keyPrefix}:{entryKey}" text)
        | None -> ()

    cards.ToArray()

let private isOldEngineTextEntryName (fileName: string) : bool =
    let ext = Path.GetExtension(fileName).ToLowerInvariant()
    ext = ".bin" || ext.StartsWith(".bin_", StringComparison.Ordinal) || ext = ".msg"

let private isOldEngineNestedParCandidate (fileName: string) : bool =
    let stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant()
    not (String.IsNullOrEmpty(stem))
    && not (stem |> Seq.forall Char.IsDigit)
    && stem <> "ptc"
    && stem <> "res"
    && stem <> "tex"

let private shouldScanOldEngineParPath (root: string) (parPath: string) : bool =
    let rel = Path.GetRelativePath(root, parPath).Replace('\\', '/')
    let segments = rel.Split('/', StringSplitOptions.RemoveEmptyEntries)
    if segments.Length = 1 then
        let lowerFile = segments.[0].ToLowerInvariant()
        lowerFile <> "chara.par" && lowerFile <> "flood.par" && lowerFile <> "hact.par"
    elif segments.Length >= 2 then
        let topDir = segments.[0].ToLowerInvariant()
        topDir.StartsWith("boot", StringComparison.Ordinal)
        || topDir.StartsWith("stay", StringComparison.Ordinal)
        || topDir.StartsWith("pause", StringComparison.Ordinal)
        || topDir.StartsWith("wdr", StringComparison.Ordinal)
        || topDir.StartsWith("auth", StringComparison.Ordinal)
        || topDir.StartsWith("hact", StringComparison.Ordinal)
        || topDir.StartsWith("common", StringComparison.Ordinal)
    else
        false

let private shouldSkipOldEngineLooseFolder (topFolder: string) : bool =
    let lower = topFolder.ToLowerInvariant()
    lower = "stage"
    || lower = "chara"
    || lower = "chara_arc"
    || lower = "chara_common"
    || lower.StartsWith("motion", StringComparison.Ordinal)
    || lower.StartsWith("movie", StringComparison.Ordinal)
    || lower.StartsWith("sound", StringComparison.Ordinal)
    || lower.StartsWith("stream", StringComparison.Ordinal)
    || lower = "shader"
    || lower = "effect"
    || lower = "2dpar"
    || lower = "fontpar"
    || lower = "light_anim"
    || lower = "cloth"
    || lower = "loading"
    || lower = "drama_scanner"
    || lower = "fighter"
    || lower = "df"
    || lower = "crm"
    || lower = "module"
    || lower.StartsWith("puid", StringComparison.Ordinal)
    || lower = "scenario"
    || lower.StartsWith("map", StringComparison.Ordinal)

/// Extracts localized content from an Old Engine / Kiwami Engine game directory.
let extractOldEngineGame (gameName: string) (path: string) : LudumLinguarumPlugins.ExtractedContent =
    let lesson = { LessonRecord.ID = 0; Name = "Game Text" }
    let dataRoots = findDataRoots path
    if dataRoots.Length = 0 then
        raise (ArgumentException($"Directory '{path}' does not exist for {gameName}."))

    let allCards = ResizeArray<CardRecord>()

    for root in dataRoots do
        // 1. Enumerate localization .par files under root
        let parFiles =
            if Directory.Exists(root) then
                Directory.GetFiles(root, "*.par", SearchOption.AllDirectories)
                |> Array.filter (shouldScanOldEngineParPath root)
                |> Array.sort
            else
                [||]

        for parPath in parFiles do
            if isParFile parPath then
                let relParPath = Path.GetRelativePath(root, parPath).Replace('\\', '/')
                let segments = relParPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                let parFileName = Path.GetFileName(parPath)
                let parentDirName = if segments.Length >= 2 then segments.[segments.Length - 2] else ""

                let (parCategory, parLangOpt) = splitOldEngineContainerLanguage parFileName
                let (parentCategory, parentLangOpt) =
                    if String.IsNullOrEmpty(parentDirName) then ("", None)
                    else splitOldEngineContainerLanguage parentDirName

                let effectiveLangOpt =
                    match parLangOpt, parentLangOpt with
                    | Some l, _ -> Some l
                    | None, Some l -> Some l
                    | None, None -> None

                let effectiveCategory =
                    if not (String.IsNullOrEmpty(parentCategory)) && parentCategory <> parCategory then
                        if parCategory = "wdr" && parentCategory = "wdr" then "wdr"
                        elif parentCategory = "wdr" && parCategory = "wdr/common" then "wdr/common"
                        elif parentCategory = "auth" || parentCategory = "hact" then $"{parentCategory}/{Path.GetFileNameWithoutExtension(parFileName)}"
                        else parCategory
                    else
                        parCategory

                let virtualFiles = extractFilteredFromFile (parPath, true, isOldEngineTextEntryName, isOldEngineNestedParCandidate)
                let extracted = extractCardsFromOldEngineVirtualFiles lesson.ID effectiveCategory effectiveLangOpt virtualFiles
                allCards.AddRange(extracted)

        // 2. Also support unpacked/loose files (.bin, .bin_*, .msg) in the data root
        let looseFiles =
            if Directory.Exists(root) then
                Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                |> Seq.filter (fun p ->
                    let ext = Path.GetExtension(p).ToLowerInvariant()
                    ext = ".bin" || ext.StartsWith(".bin_", StringComparison.Ordinal) || ext = ".msg")
                |> Seq.sort
                |> Seq.toArray
            else
                [||]

        if looseFiles.Length > 0 then
            // Group loose files by their top-level container folder
            let groupedByFolder =
                looseFiles
                |> Array.groupBy (fun fullPath ->
                    let rel = Path.GetRelativePath(root, fullPath).Replace('\\', '/')
                    let firstSlash = rel.IndexOf('/')
                    if firstSlash > 0 then rel.Substring(0, firstSlash) else "")

            for (topFolder, filesInFolder) in groupedByFolder do
                if not (shouldSkipOldEngineLooseFolder topFolder) then
                    let (folderCat, folderLangOpt) =
                        if String.IsNullOrEmpty(topFolder) then ("", None)
                        else splitOldEngineContainerLanguage topFolder

                    let virtualFiles =
                        filesInFolder
                        |> Array.map (fun fullPath ->
                            let rel = Path.GetRelativePath(root, fullPath).Replace('\\', '/')
                            let relInsideFolder =
                                if not (String.IsNullOrEmpty(topFolder)) && rel.StartsWith(topFolder + "/", StringComparison.Ordinal) then
                                    rel.Substring(topFolder.Length + 1)
                                else
                                    rel
                            (relInsideFolder, File.ReadAllBytes(fullPath)))

                    let extracted = extractCardsFromOldEngineVirtualFiles lesson.ID folderCat folderLangOpt virtualFiles
                    allCards.AddRange(extracted)

    // Deduplicate by (LanguageTag, Key) while preserving order
    let seen = HashSet<string * string>()
    let uniqueCards =
        allCards
        |> Seq.filter (fun c -> seen.Add((c.LanguageTag, c.Key)))
        |> Seq.toArray

    if uniqueCards.Length = 0 then
        raise (ArgumentException($"No RGG Studio Old Engine localization files were found in '{path}' for {gameName}."))

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lesson |]
        LudumLinguarumPlugins.ExtractedContent.cards = uniqueCards
    }

/// Attempts to parse a Dragon Engine .par archive filename (e.g. "db.par", "db.yazawa.en.par", "db.elvis.ja.par", "db_en.par")
/// into (categoryPrefix, languageTag option).
let internal tryParseDragonEngineParName (parFileName: string) : (string * string option) option =
    if not (parFileName.EndsWith(".par", StringComparison.OrdinalIgnoreCase)) then
        None
    else
        let stem = parFileName.Substring(0, parFileName.Length - 4)
        let lowerStem = stem.ToLowerInvariant()
        if lowerStem = "db" || lowerStem = "talk" || lowerStem = "auth" then
            Some(lowerStem, None)
        else
            let dotParts = lowerStem.Split('.', StringSplitOptions.RemoveEmptyEntries)
            if dotParts.Length >= 2 && (dotParts.[0] = "db" || dotParts.[0] = "talk" || dotParts.[0] = "auth" || dotParts.[0] = "ui") then
                let lastPart = dotParts.[dotParts.Length - 1]
                match tryMapDragonEngineLanguage lastPart with
                | Some tag -> Some(dotParts.[0], Some tag)
                | None -> Some(dotParts.[0], None)
            else
                let underParts = lowerStem.Split('_', StringSplitOptions.RemoveEmptyEntries)
                if underParts.Length >= 2 && (underParts.[0] = "db" || underParts.[0] = "talk" || underParts.[0] = "auth" || underParts.[0] = "ui") then
                    let lastPart = underParts.[underParts.Length - 1]
                    match tryMapDragonEngineLanguage lastPart with
                    | Some tag -> Some(underParts.[0], Some tag)
                    | None -> Some(underParts.[0], None)
                else
                    None

/// Resolves (languageTag, normalizedRelativeFileStem) for a virtual path inside a Dragon Engine archive or folder.
let internal resolveDragonEngineVirtualPath (categoryPrefix: string, archiveLangOpt: string option, virtualPath: string) : (string * string) option =
    let normalized = virtualPath.Replace('\\', '/').Trim('/')
    let segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
    if segments.Length = 0 then
        None
    else
        // Strip leading container segment ("db", "db.<codename>", "ui", "ui.<codename>", "talk", "talk_<codename>", etc.) if present
        let isContainerPrefix (seg: string) =
            let lower = seg.ToLowerInvariant()
            lower = "db" || lower = "ui" || lower = "talk" || lower = "auth"
            || lower.StartsWith("db.", StringComparison.Ordinal)
            || lower.StartsWith("ui.", StringComparison.Ordinal)
            || lower.StartsWith("talk.", StringComparison.Ordinal)
            || lower.StartsWith("auth.", StringComparison.Ordinal)
            || lower.StartsWith("db_", StringComparison.Ordinal)
            || lower.StartsWith("ui_", StringComparison.Ordinal)
            || lower.StartsWith("talk_", StringComparison.Ordinal)
            || lower.StartsWith("auth_", StringComparison.Ordinal)

        let segmentsAfterDb =
            if segments.Length >= 2 && isContainerPrefix segments.[0] then
                segments.[1..]
            else
                segments

        let firstSegLang =
            if segmentsAfterDb.Length >= 2 then
                tryMapDragonEngineLanguage segmentsAfterDb.[0]
            else
                None

        match firstSegLang, archiveLangOpt with
        | Some segLang, _ ->
            let remainingPath = String.Join("/", segmentsAfterDb.[1..])
            let stem =
                let ext = Path.GetExtension(remainingPath)
                if not (String.IsNullOrEmpty(ext)) then
                    remainingPath.Substring(0, remainingPath.Length - ext.Length)
                else
                    remainingPath
            Some(segLang, $"{categoryPrefix}/{stem}")
        | None, Some archLang ->
            let remainingPath = String.Join("/", segmentsAfterDb)
            let stem =
                let ext = Path.GetExtension(remainingPath)
                if not (String.IsNullOrEmpty(ext)) then
                    remainingPath.Substring(0, remainingPath.Length - ext.Length)
                else
                    remainingPath
            Some(archLang, $"{categoryPrefix}/{stem}")
        | None, None ->
            None

let private isNonLocalizedDragonEngineBin (fileName: string) : bool =
    let lower = fileName.ToLowerInvariant()
    lower.StartsWith("sound_se_name_table", StringComparison.Ordinal)
    || lower.StartsWith("character_tex_change_table", StringComparison.Ordinal)
    || lower.StartsWith("character_model_model_data", StringComparison.Ordinal)
    || lower.StartsWith("character_character_data", StringComparison.Ordinal)
    || lower.StartsWith("sound_cuesheet_info", StringComparison.Ordinal)
    || lower.StartsWith("sound_speech_info", StringComparison.Ordinal)
    || lower.StartsWith("sound_movie_info", StringComparison.Ordinal)
    || lower.StartsWith("talk_param", StringComparison.Ordinal)
    || lower.StartsWith("rumble", StringComparison.Ordinal)
    || lower.StartsWith("scene_preload_", StringComparison.Ordinal)
    || lower.StartsWith("db_unittest_", StringComparison.Ordinal)
    || lower.StartsWith("db2_test_", StringComparison.Ordinal)
    || lower.StartsWith("db_example_", StringComparison.Ordinal)

let private isDragonEngineCandidateBin (fileName: string) : bool =
    fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
    && not (isNonLocalizedDragonEngineBin fileName)

/// Extracts localized content from a Dragon Engine game directory.
let extractDragonEngineGame (gameName: string) (path: string) : LudumLinguarumPlugins.ExtractedContent =
    let lesson = { LessonRecord.ID = 0; Name = "Game Text" }
    let dataRoots = findDataRoots path
    if dataRoots.Length = 0 then
        raise (ArgumentException($"Directory '{path}' does not exist for {gameName}."))

    let allCards = ResizeArray<CardRecord>()

    for root in dataRoots do
        if Directory.Exists(root) then
            // 1. Scan .par archives in root (such as db.par, db.<codename>.<lang>.par, ui.<codename>.<lang>.par)
            let parFiles =
                Directory.GetFiles(root, "*.par", SearchOption.AllDirectories)
                |> Array.sort

            for parPath in parFiles do
                let parName = Path.GetFileName(parPath)
                match tryParseDragonEngineParName parName with
                | Some(categoryPrefix, archiveLangOpt) ->
                    // Unlocalized talk_*.par / auth_*.par containers hold thousands of nested animation/voice .par files without language folders
                    if not ((categoryPrefix = "talk" || categoryPrefix = "auth") && archiveLangOpt.IsNone) && isParFile parPath then
                        let virtualFiles = extractFilteredFromFile (parPath, true, isDragonEngineCandidateBin, (fun _ -> true))
                        for (vPath, fileBytes) in virtualFiles do
                            let fileName = Path.GetFileName(vPath)
                            match resolveDragonEngineVirtualPath (categoryPrefix, archiveLangOpt, vPath) with
                            | Some(langTag, normStem) ->
                                let entries = parseDragonEngineFileBytes (fileName, fileBytes, langTag)
                                for (entryKey, text) in entries do
                                    allCards.Add(createCard lesson.ID langTag $"{normStem}:{entryKey}" text)
                            | None -> ()
                | None -> ()

            // 2. Scan loose .bin files (such as data/db/e/*.bin, data/db/j/*.bin, data/db/en/*.bin, data/db.yazawa/en/*.bin)
            let looseBins =
                Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories)
                |> Array.sort

            for binPath in looseBins do
                let fileName = Path.GetFileName(binPath)
                if isDragonEngineCandidateBin fileName then
                    let relPath = Path.GetRelativePath(root, binPath).Replace('\\', '/')
                    match resolveDragonEngineVirtualPath ("db", None, relPath) with
                    | Some(langTag, normStem) ->
                        let fileBytes = File.ReadAllBytes(binPath)
                        let entries = parseDragonEngineFileBytes (fileName, fileBytes, langTag)
                        for (entryKey, text) in entries do
                            allCards.Add(createCard lesson.ID langTag $"{normStem}:{entryKey}" text)
                    | None -> ()

    // Deduplicate by (LanguageTag, Key) while preserving order
    let seen = HashSet<string * string>()
    let uniqueCards =
        allCards
        |> Seq.filter (fun c -> seen.Add((c.LanguageTag, c.Key)))
        |> Seq.toArray

    if uniqueCards.Length = 0 then
        raise (ArgumentException($"No RGG Studio Dragon Engine localization files were found in '{path}' for {gameName}."))

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lesson |]
        LudumLinguarumPlugins.ExtractedContent.cards = uniqueCards
    }

/// List of all supported RGG Studio games and their engine specifications.
let supportedGames: RggGameSpec array =
    [|
        // Old Engine / Kiwami Engine games
        { Name = "Yakuza 0"; Engine = OldEngine; Codename = None; SupportedLanguages = [| "en"; "ja"; "zh-CN" |] }
        { Name = "Yakuza Kiwami"; Engine = OldEngine; Codename = None; SupportedLanguages = [| "de"; "en"; "es"; "es-419"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }
        { Name = "Yakuza 3 Remastered"; Engine = OldEngine; Codename = None; SupportedLanguages = [| "en"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }
        { Name = "Yakuza 4 Remastered"; Engine = OldEngine; Codename = None; SupportedLanguages = [| "en"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }
        { Name = "Yakuza 5 Remastered"; Engine = OldEngine; Codename = None; SupportedLanguages = [| "en"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }

        // Dragon Engine games
        { Name = "Yakuza 6: The Song of Life"; Engine = DragonEngine; Codename = Some "lexus"; SupportedLanguages = [| "en"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }
        { Name = "Yakuza Kiwami 2"; Engine = DragonEngine; Codename = Some "lexus2"; SupportedLanguages = [| "en"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }
        { Name = "Yakuza Kiwami 3 & Dark Ties"; Engine = DragonEngine; Codename = Some "bis"; SupportedLanguages = [| "de"; "en"; "es"; "es-419"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }
        { Name = "Yakuza: Like a Dragon"; Engine = DragonEngine; Codename = Some "yazawa"; SupportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }
        { Name = "Judgment"; Engine = DragonEngine; Codename = Some "judge"; SupportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }
        { Name = "Lost Judgment"; Engine = DragonEngine; Codename = Some "coyote"; SupportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "zh-CN"; "zh-TW" |] }
        { Name = "Like a Dragon Gaiden: The Man Who Erased His Name"; Engine = DragonEngine; Codename = Some "aston"; SupportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }
        { Name = "Like a Dragon: Infinite Wealth"; Engine = DragonEngine; Codename = Some "elvis"; SupportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }
        { Name = "Like a Dragon: Pirate Yakuza in Hawaii"; Engine = DragonEngine; Codename = Some "spr"; SupportedLanguages = [| "de"; "en"; "es"; "fr"; "it"; "ja"; "ko"; "pt-BR"; "ru"; "zh-CN"; "zh-TW" |] }
    |]

/// Filters out internal identifier/parameter keys that appear in 2 or more languages with the exact same untranslated text in every language.
let private filterUnlocalizedMultiLanguageCards (cards: CardRecord array) : CardRecord array =
    let keyStats = Dictionary<string, int * string * bool>(StringComparer.Ordinal)
    for c in cards do
        match keyStats.TryGetValue(c.Key) with
        | false, _ ->
            keyStats.[c.Key] <- (1, c.Text, false)
        | true, (count, firstText, hasDiff) ->
            let nowDiff = hasDiff || not (String.Equals(firstText, c.Text, StringComparison.Ordinal))
            keyStats.[c.Key] <- (count + 1, firstText, nowDiff)

    cards
    |> Array.filter (fun c ->
        match keyStats.TryGetValue(c.Key) with
        | true, (count, _, hasDiff) -> count < 2 || hasDiff
        | false, _ -> true)

/// Extracts localized text for a supported RGG Studio game given its spec and installation directory.
let extractGame (spec: RggGameSpec) (path: string) : LudumLinguarumPlugins.ExtractedContent =
    let extracted =
        match spec.Engine with
        | OldEngine -> extractOldEngineGame spec.Name path
        | DragonEngine -> extractDragonEngineGame spec.Name path
    let supportedSet = HashSet<string>(spec.SupportedLanguages, StringComparer.OrdinalIgnoreCase)
    let supportedCards =
        extracted.cards
        |> Array.filter (fun c -> supportedSet.Contains(c.LanguageTag))
    let baseCards =
        if supportedCards.Length > 0 then supportedCards
        else extracted.cards
    let filteredCards = filterUnlocalizedMultiLanguageCards baseCards
    if filteredCards.Length > 0 then
        { extracted with cards = filteredCards }
    else
        { extracted with cards = baseCards }
