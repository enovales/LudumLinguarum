module Balatro

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

(***************************************************************************)
(********************************** Balatro ********************************)
(***************************************************************************)
// Balatro is a LÖVE2D game. On Steam, the game data is fused into Balatro.exe
// as a ZIP archive appended after the executable stub (a standalone .love
// file is also supported). All localized text lives in localization/<lang>.lua
// entries inside that archive. Each file returns a Lua table of the form:
//
//   return {
//       descriptions={ <Group>={ <key>={ name="...", text={ "...", ... }, ... } } },
//       misc={ <group>={ <key>="..." | { "...", ... } | { <subkey>="..." } } },
//   }
//
// The table is walked generically: every string leaf becomes a card keyed by
// its path (e.g. "descriptions/<Group>/<key>/text"), and arrays of strings
// (multi-line descriptions) are joined into a single card.

/// A parsed Lua value from a Balatro localization file. Tables map keys to
/// values; the key is None for positional (array) entries, so that arrays can
/// be told apart from tables with explicit ["1"], ["2"], ... keys.
type internal LuaValue =
    | LuaString of string
    | LuaTable of (string option * LuaValue) array

type private LuaToken =
    | TString of string
    | TIdent of string
    | TOpenBrace
    | TCloseBrace
    | TOpenBracket
    | TCloseBracket
    | TEquals
    | TComma

let private tokenizeLua(text: string): LuaToken array =
    let n = text.Length
    let tokens = ResizeArray<LuaToken>()
    let mutable i = 0
    while i < n do
        let c = text.[i]
        if Char.IsWhiteSpace(c) then
            i <- i + 1
        elif c = '-' && i + 1 < n && text.[i + 1] = '-' then
            // line comment
            while i < n && text.[i] <> '\n' do
                i <- i + 1
        elif c = '"' then
            let sb = StringBuilder()
            i <- i + 1
            let mutable closed = false
            while i < n && not closed do
                let ch = text.[i]
                if ch = '\\' && i + 1 < n then
                    sb.Append(
                        match text.[i + 1] with
                        | 'n' -> '\n'
                        | 't' -> '\t'
                        | 'r' -> '\r'
                        | '"' -> '"'
                        | '\'' -> '\''
                        | '\\' -> '\\'
                        | other -> other) |> ignore
                    i <- i + 2
                elif ch = '"' then
                    closed <- true
                    i <- i + 1
                else
                    sb.Append(ch) |> ignore
                    i <- i + 1
            if not closed then
                raise(FormatException("Unterminated string literal in localization file"))
            tokens.Add(TString(sb.ToString()))
        elif c = '{' then tokens.Add(TOpenBrace); i <- i + 1
        elif c = '}' then tokens.Add(TCloseBrace); i <- i + 1
        elif c = '[' then tokens.Add(TOpenBracket); i <- i + 1
        elif c = ']' then tokens.Add(TCloseBracket); i <- i + 1
        elif c = '=' then tokens.Add(TEquals); i <- i + 1
        elif c = ',' then tokens.Add(TComma); i <- i + 1
        elif Char.IsLetter(c) || c = '_' then
            let start = i
            while i < n && (Char.IsLetterOrDigit(text.[i]) || text.[i] = '_') do
                i <- i + 1
            tokens.Add(TIdent(text.Substring(start, i - start)))
        else
            raise(FormatException(sprintf "Unexpected character '%c' in localization file" c))
    tokens.ToArray()

/// Parses a Balatro localization file (a `return { ... }` Lua chunk) into a
/// LuaValue. Only the constructs used by the game's localization files are
/// supported: double-quoted strings, identifier and ["string"] keys, nested
/// tables, and positional arrays.
let internal parseLocalizationLua(text: string): LuaValue =
    let tokens = tokenizeLua text
    let mutable pos = 0
    let peek() = if pos < tokens.Length then Some(tokens.[pos]) else None
    let advance() =
        let t = tokens.[pos]
        pos <- pos + 1
        t

    let rec parseValue(): LuaValue =
        match peek() with
        | Some(TOpenBrace) -> parseTable()
        | Some(TString(s)) -> advance() |> ignore; LuaString(s)
        | other -> raise(FormatException("Unexpected token in localization file: " + (match other with Some(t) -> t.ToString() | None -> "end of file")))

    and parseTable(): LuaValue =
        match advance() with
        | TOpenBrace -> ()
        | t -> raise(FormatException("Expected '{' in localization file, got " + t.ToString()))
        let entries = ResizeArray<string option * LuaValue>()
        let mutable positionalIndex = 0
        let mutable finished = false
        while not finished do
            match peek() with
            | Some(TCloseBrace) -> advance() |> ignore; finished <- true
            | Some(TComma) -> advance() |> ignore
            | None -> raise(FormatException("Unterminated table in localization file"))
            | Some(TIdent(k)) when pos + 1 < tokens.Length && tokens.[pos + 1] = TEquals ->
                advance() |> ignore
                advance() |> ignore
                entries.Add((Some(k), parseValue()))
            | Some(TOpenBracket) ->
                advance() |> ignore
                let key =
                    match advance() with
                    | TString(s) -> s
                    | TIdent(s) -> s
                    | t -> raise(FormatException("Expected key in localization file, got " + t.ToString()))
                match advance() with
                | TCloseBracket -> ()
                | t -> raise(FormatException("Expected ']' in localization file, got " + t.ToString()))
                match advance() with
                | TEquals -> ()
                | t -> raise(FormatException("Expected '=' in localization file, got " + t.ToString()))
                entries.Add((Some(key), parseValue()))
            | Some(_) ->
                positionalIndex <- positionalIndex + 1
                entries.Add((None, parseValue()))
        LuaTable(entries.ToArray())

    match peek() with
    | Some(TIdent("return")) -> advance() |> ignore
    | _ -> ()
    let result = parseValue()
    if pos <> tokens.Length then
        raise(FormatException("Unexpected trailing content in localization file"))
    result

// Balatro marks up its strings with inline styling tags such as {C:attention},
// {s:0.85}, {T:tag_double} or the empty reset tag {}. These render as nothing,
// so they are removed. Substitution variables (#1#, $#2#, ...) are kept. Some
// translations contain literal braced words (e.g. Spanish "{valores
// consecutivos}"); those do not match the tag shape (a single letter followed
// by a colon) and are kept as-is.
let private markupTagRegex = Regex(@"\{[A-Za-z]:[^{}]*\}|\{\}", RegexOptions.Compiled)
let private whitespaceRegex = Regex(@"\s+", RegexOptions.Compiled)

let internal sanitizeText(s: string): string =
    let withoutTags = markupTagRegex.Replace(s, "")
    whitespaceRegex.Replace(withoutTags, " ").Trim()

/// Maps a localization file's language code (the file name without extension)
/// to an IETF language tag.
let internal languageCodeToTag(code: string): string option =
    let explicitMapping =
        [|
            ("en-us", "en-US")
            ("es_419", "es-419")
            ("es_ES", "es-ES")
            ("pt_BR", "pt-BR")
            ("zh_CN", "zh-CN")
            ("zh_TW", "zh-TW")
        |] |> Map.ofArray
    match explicitMapping |> Map.tryFind code with
    | Some(tag) -> Some(tag)
    | None ->
        // Generic fallback for any future language files: normalize to an
        // IETF-style tag (e.g. "pt_br" -> "pt-BR", "de" -> "de").
        let parts = code.Replace('_', '-').Split('-')
        if parts.Length > 1 then
            parts.[parts.Length - 1] <- parts.[parts.Length - 1].ToUpperInvariant()
        let tag = String.Join("-", parts)
        if String.IsNullOrWhiteSpace(tag) then None else Some(tag)

/// Returns the language code for a localization archive entry such as
/// "localization/en-us.lua", or None for any other entry.
let private localizationEntryLanguageCode(entryKey: string): string option =
    let normalized = entryKey.Replace('\\', '/')
    if normalized.StartsWith("localization/") && normalized.EndsWith(".lua") then
        let fileName = normalized.Substring("localization/".Length)
        Some(fileName.Substring(0, fileName.Length - ".lua".Length))
    else
        None

/// Scans the tail of a stream for the ZIP end-of-central-directory record and
/// returns the offset at which the ZIP data starts. This is 0 for a plain
/// .love/.zip file, and skips the executable stub of a fused LÖVE2D .exe.
let internal findEmbeddedZipStartOffset(s: Stream): int64 =
    let scanLength = int64 (min s.Length 1048576L)
    let buffer = Array.zeroCreate<byte> (int scanLength)
    s.Seek(s.Length - scanLength, SeekOrigin.Begin) |> ignore
    s.Read(buffer, 0, buffer.Length) |> ignore
    let mutable eocdIndex = -1
    let mutable i = buffer.Length - 22
    while i >= 0 && eocdIndex < 0 do
        if buffer.[i] = 0x50uy && buffer.[i + 1] = 0x4Buy && buffer.[i + 2] = 0x05uy && buffer.[i + 3] = 0x06uy then
            eocdIndex <- i
        else
            i <- i - 1
    if eocdIndex < 0 then
        raise(InvalidDataException("No ZIP end-of-central-directory record found; this does not look like a Balatro game archive."))
    let centralDirectorySize = int64 (BitConverter.ToInt32(buffer, eocdIndex + 12))
    let centralDirectoryOffset = int64 (BitConverter.ToInt32(buffer, eocdIndex + 16))
    let eocdAbsolute = s.Length - scanLength + int64 eocdIndex
    let actualCentralDirectoryOffset = eocdAbsolute - centralDirectorySize
    actualCentralDirectoryOffset - centralDirectoryOffset

/// Reads and parses every localization/<lang>.lua entry in the game archive
/// (Balatro.exe or a .love file), returning (language tag, parsed table) pairs.
let internal readLocalizationFilesFromArchive(archiveStream: Stream): (string * LuaValue) array =
    let zipStart = findEmbeddedZipStartOffset archiveStream
    use offsetStream = new StreamTools.OffsetStream(archiveStream, zipStart)
    use archive = SharpCompress.Archives.Zip.ZipArchive.Open(offsetStream)
    archive.Entries
    |> Seq.choose (fun entry ->
        if entry.IsDirectory then
            None
        else
            match localizationEntryLanguageCode entry.Key with
            | None -> None
            | Some(code) ->
                match languageCodeToTag code with
                | None -> None
                | Some(tag) ->
                    use entryStream = entry.OpenEntryStream()
                    use reader = new StreamReader(entryStream, Encoding.UTF8)
                    Some(tag, parseLocalizationLua(reader.ReadToEnd())))
    |> Array.ofSeq

/// Walks a parsed localization table, producing (card key, text) pairs for
/// every string. Arrays of strings (multi-line text) are joined into a single
/// entry at the array's own path; tables with explicit keys are recursed into.
let rec internal collectStrings(path: string)(value: LuaValue): (string * string) list =
    let childPath(k: string) = if path = "" then k else path + "/" + k
    match value with
    | LuaString(s) ->
        let sanitized = sanitizeText s
        if String.IsNullOrWhiteSpace(sanitized) then [] else [ (path, sanitized) ]
    | LuaTable(entries) when (entries |> Array.forall (fun (k, _) -> k.IsNone)) && (entries |> Array.forall (fun (_, v) -> match v with LuaString _ -> true | _ -> false)) ->
        let joined =
            entries
            |> Array.choose (fun (_, v) -> match v with LuaString(s) -> Some(sanitizeText s) | _ -> None)
            |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
            |> String.concat " "
        if String.IsNullOrWhiteSpace(joined) then [] else [ (path, joined) ]
    | LuaTable(entries) ->
        entries
        |> Array.indexed
        |> List.ofArray
        |> List.collect (fun (i, (k, v)) ->
            let segment = match k with Some(s) -> s | None -> string (i + 1)
            collectStrings (childPath segment) v)

let private createCard(lessonId: int)(languageTag: string)(key: string)(text: string): CardRecord =
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

let internal cardsForLanguage(lessonId: int)(languageTag: string)(root: LuaValue): CardRecord array =
    collectStrings "" root
    |> List.map (fun (key, text) -> createCard lessonId languageTag key text)
    |> Array.ofList

let ExtractBalatro(path: string): LudumLinguarumPlugins.ExtractedContent =
    let lessonEntry = {
        LessonRecord.ID = 0
        Name = "Game Text"
    }

    let archivePath =
        let exePath = Path.Combine(path, "Balatro.exe")
        if File.Exists(exePath) then
            Some(exePath)
        else
            match Directory.GetFiles(path, "*.love") with
            | [||] -> None
            | loveFiles -> Some(loveFiles.[0])

    match archivePath with
    | None ->
        raise(ArgumentException("Could not find Balatro.exe or a .love archive in '" + path + "'. Is this the root directory of Balatro?"))
    | Some(ap) ->
        use stream = new FileStream(ap, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        let localizationFiles = readLocalizationFilesFromArchive(stream)
        if localizationFiles.Length = 0 then
            raise(ArgumentException("No localization/*.lua entries found in '" + ap + "'. Is this a valid Balatro installation?"))

        let cards =
            localizationFiles
            |> Array.collect (fun (tag, root) -> cardsForLanguage lessonEntry.ID tag root)

        {
            LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
            LudumLinguarumPlugins.ExtractedContent.cards = cards
        }
