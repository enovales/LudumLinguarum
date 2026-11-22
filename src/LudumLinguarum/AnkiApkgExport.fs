module AnkiApkgExport

open MediaDatabase
open SQLite
open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

type ApkgMediaAttachment =
    {
        FileName: string
        Data: byte array
    }

type ApkgNoteInput =
    {
        StableKey: string
        RecognitionText: string
        ProductionText: string
        RecognitionAudio: ApkgMediaAttachment option
        ProductionAudio: ApkgMediaAttachment option
        Image: ApkgMediaAttachment option
        Lesson: string
        Speaker: string
        Reversible: bool
        IncludeListeningCard: bool
        Tags: string list
    }

let private htmlTagRegex = Regex("<[^>]*>", RegexOptions.Compiled)
let private fieldSeparator = "\u001f"

/// <summary>
/// Computes Anki's field checksum (first 8 hex characters of SHA-1 of HTML-stripped sort field, as int64).
/// </summary>
let computeAnkiFieldChecksum (sortField: string) : int64 =
    let stripped = htmlTagRegex.Replace(sortField, "").Trim()
    let hash = SHA1.HashData(Encoding.UTF8.GetBytes(stripped))
    let value =
        (uint32 hash.[0] <<< 24)
        ||| (uint32 hash.[1] <<< 16)
        ||| (uint32 hash.[2] <<< 8)
        ||| uint32 hash.[3]
    int64 value

/// <summary>
/// Computes a deterministic positive 53-bit integer ID from an arbitrary string.
/// </summary>
let computeStableId (seed: string) : int64 =
    let hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed))
    let raw = BitConverter.ToUInt64(hash, 0) &&& 0x001FFFFFFFFFFFFFuL
    if raw < 1000000uL then int64 (raw + 1000000uL) else int64 raw

let private base62Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"

/// <summary>
/// Computes a deterministic Anki-compatible note GUID (10 printable ASCII characters).
/// </summary>
let computeStableGuid (seed: string) : string =
    let hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed))
    let chars = Array.zeroCreate<char> 10
    for i in 0 .. 9 do
        let idx = int hash.[i] % base62Alphabet.Length
        chars.[i] <- base62Alphabet.[idx]
    new string(chars)

let private sanitizeTag (tag: string) : string =
    if String.IsNullOrWhiteSpace(tag) then
        ""
    else
        Regex.Replace(tag.Trim(), @"\s+", "_")

let private escapeHtmlText (s: string) : string =
    if String.IsNullOrEmpty(s) then
        ""
    else
        s.Replace("&", "&amp;")
         .Replace("<", "&lt;")
         .Replace(">", "&gt;")
         .Replace("\r\n", "<br>")
         .Replace("\n", "<br>")
         .Replace("\r", "<br>")

let private buildModelsJson (modelId: int64) (deckId: int64) (nowSeconds: int64) : string =
    let fields =
        [|
            "RecognitionText"
            "ProductionText"
            "RecognitionAudio"
            "ProductionAudio"
            "Image"
            "Lesson"
            "Speaker"
            "AddReverse"
            "AddListening"
        |]
        |> Array.mapi (fun ord name ->
            dict [
                "name", box name
                "ord", box ord
                "sticky", box false
                "rtl", box false
                "font", box "Arial"
                "size", box 20
                "media", box [||]
            ]
        )

    let templates =
        [|
            dict [
                "name", box "Recognition"
                "ord", box 0
                "qfmt", box "{{RecognitionText}}{{#RecognitionAudio}}<div class=\"audio\">{{RecognitionAudio}}</div>{{/RecognitionAudio}}{{#Image}}<div class=\"media\">{{Image}}</div>{{/Image}}"
                "afmt", box "{{FrontSide}}<hr id=\"answer\"><div class=\"answer\">{{ProductionText}}</div>{{#ProductionAudio}}<div class=\"audio\">{{ProductionAudio}}</div>{{/ProductionAudio}}{{#Speaker}}<div class=\"meta\">{{Speaker}}</div>{{/Speaker}}{{#Lesson}}<div class=\"meta\">{{Lesson}}</div>{{/Lesson}}"
                "bqfmt", box ""
                "bafmt", box ""
                "did", box null
                "bfont", box ""
                "bsize", box 0
            ]
            dict [
                "name", box "Production"
                "ord", box 1
                "qfmt", box "{{#AddReverse}}{{ProductionText}}{{#ProductionAudio}}<div class=\"audio\">{{ProductionAudio}}</div>{{/ProductionAudio}}{{#Image}}<div class=\"media\">{{Image}}</div>{{/Image}}{{/AddReverse}}"
                "afmt", box "{{FrontSide}}<hr id=\"answer\"><div class=\"answer\">{{RecognitionText}}</div>{{#RecognitionAudio}}<div class=\"audio\">{{RecognitionAudio}}</div>{{/RecognitionAudio}}{{#Speaker}}<div class=\"meta\">{{Speaker}}</div>{{/Speaker}}{{#Lesson}}<div class=\"meta\">{{Lesson}}</div>{{/Lesson}}"
                "bqfmt", box ""
                "bafmt", box ""
                "did", box null
                "bfont", box ""
                "bsize", box 0
            ]
            dict [
                "name", box "Listening"
                "ord", box 2
                "qfmt", box "{{#AddListening}}<div class=\"listening-label\">Listen</div><div class=\"audio\">{{ProductionAudio}}</div>{{/AddListening}}"
                "afmt", box "{{FrontSide}}<hr id=\"answer\"><div class=\"answer\">{{ProductionText}}</div><div class=\"translation\">{{RecognitionText}}</div>{{#Image}}<div class=\"media\">{{Image}}</div>{{/Image}}{{#Speaker}}<div class=\"meta\">{{Speaker}}</div>{{/Speaker}}{{#Lesson}}<div class=\"meta\">{{Lesson}}</div>{{/Lesson}}"
                "bqfmt", box ""
                "bafmt", box ""
                "did", box null
                "bfont", box ""
                "bsize", box 0
            ]
        |]

    let css =
        ".card { font-family: Arial, sans-serif; font-size: 20px; text-align: center; color: #222; background-color: #fff; }\n"
        + ".audio { margin-top: 10px; }\n"
        + ".media img { max-width: 90%; max-height: 300px; margin-top: 10px; border-radius: 6px; }\n"
        + ".translation { margin-top: 8px; color: #555; font-size: 18px; }\n"
        + ".meta { margin-top: 8px; color: #777; font-size: 13px; }\n"
        + ".listening-label { font-size: 14px; text-transform: uppercase; letter-spacing: 0.08em; color: #666; }"

    let model =
        dict [
            "id", box modelId
            "name", box "LudumLinguarum"
            "type", box 0
            "mod", box nowSeconds
            "usn", box -1
            "sortf", box 0
            "did", box deckId
            "tmpls", box templates
            "flds", box fields
            "css", box css
            "latexPre", box "\\documentclass[12pt]{article}\n\\special{papersize=3in,5in}\n\\usepackage[utf8]{inputenc}\n\\usepackage{amssymb,amsmath}\n\\pagestyle{empty}\n\\setlength{\\parindent}{0in}\n\\begin{document}\n"
            "latexPost", box "\\end{document}"
            "latexsvg", box false
            "req", box [| [| box 0; box "any"; box [| 0 |] |]; [| box 1; box "all"; box [| 1; 7 |] |]; [| box 2; box "all"; box [| 3; 8 |] |] |]
            "tags", box [||]
            "vers", box [||]
        ]

    let root = dict [ modelId.ToString(), box model ]
    JsonSerializer.Serialize(root)

let private buildDecksJson (deckId: int64) (deckName: string) (nowSeconds: int64) : string =
    let mkDeck (id: int64) (name: string) =
        dict [
            "id", box id
            "name", box name
            "mod", box nowSeconds
            "usn", box -1
            "lrnToday", box [| 0; 0 |]
            "revToday", box [| 0; 0 |]
            "newToday", box [| 0; 0 |]
            "timeToday", box [| 0; 0 |]
            "collapsed", box false
            "browserCollapsed", box false
            "desc", box ""
            "dyn", box 0
            "conf", box 1
            "extendNew", box 0
            "extendRev", box 0
        ]

    let root = Dictionary<string, obj>()
    root.["1"] <- box (mkDeck 1L "Default")
    root.[deckId.ToString()] <- box (mkDeck deckId deckName)
    JsonSerializer.Serialize(root)

let private buildConfJson (deckId: int64) (modelId: int64) : string =
    let conf =
        dict [
            "nextPos", box 1
            "estTimes", box true
            "activeDecks", box [| deckId |]
            "sortType", box "noteFld"
            "timeLim", box 0
            "sortBackwards", box false
            "addToCur", box true
            "curDeck", box deckId
            "newBury", box true
            "newSpread", box 0
            "dueCounts", box true
            "curModel", box (modelId.ToString())
            "collapseTime", box 1200
        ]
    JsonSerializer.Serialize(conf)

let private buildDconfJson () : string =
    let defaultConf =
        dict [
            "id", box 1
            "mod", box 0
            "name", box "Default"
            "usn", box 0
            "maxTaken", box 60
            "autoplay", box true
            "timer", box 0
            "replayq", box true
            "new", box (dict [
                "bury", box false
                "delays", box [| 1; 10 |]
                "initialFactor", box 2500
                "ints", box [| 1; 4; 0 |]
                "order", box 1
                "perDay", box 20
            ])
            "rev", box (dict [
                "bury", box false
                "ease4", box 1.3
                "ivlFct", box 1.0
                "maxIvl", box 36500
                "perDay", box 200
                "hardFactor", box 1.2
            ])
            "lapse", box (dict [
                "delays", box [| 10 |]
                "leechAction", box 1
                "leechFails", box 8
                "minInt", box 1
                "mult", box 0.0
            ])
        ]
    let root = dict [ "1", box defaultConf ]
    JsonSerializer.Serialize(root)

let private createAnkiSchema (conn: SQLiteConnection) =
    let statements =
        [|
            "CREATE TABLE col (id integer primary key, crt integer not null, mod integer not null, scm integer not null, ver integer not null, dty integer not null, usn integer not null, ls integer not null, conf text not null, models text not null, decks text not null, dconf text not null, tags text not null);"
            "CREATE TABLE notes (id integer primary key, guid text not null, mid integer not null, mod integer not null, usn integer not null, tags text not null, flds text not null, sfld text not null, csum integer not null, flags integer not null, data text not null);"
            "CREATE TABLE cards (id integer primary key, nid integer not null, did integer not null, ord integer not null, mod integer not null, usn integer not null, type integer not null, queue integer not null, due integer not null, ivl integer not null, factor integer not null, reps integer not null, lapses integer not null, left integer not null, odue integer not null, odid integer not null, flags integer not null, data text not null);"
            "CREATE TABLE revlog (id integer primary key, cid integer not null, usn integer not null, ease integer not null, ivl integer not null, lastIvl integer not null, factor integer not null, time integer not null, type integer not null);"
            "CREATE TABLE graves (usn integer not null, oid integer not null, type integer not null);"
            "CREATE INDEX ix_notes_usn on notes (usn);"
            "CREATE INDEX ix_cards_usn on cards (usn);"
            "CREATE INDEX ix_revlog_usn on revlog (usn);"
            "CREATE INDEX ix_cards_nid on cards (nid);"
            "CREATE INDEX ix_cards_sched on cards (did, queue, due);"
            "CREATE INDEX ix_revlog_cid on revlog (cid);"
            "CREATE INDEX ix_notes_csum on notes (csum);"
        |]
    for sql in statements do
        conn.Execute(sql) |> ignore

/// <summary>
/// Writes a self-contained Anki .apkg archive (collection.anki2 + media JSON + deduplicated media files).
/// </summary>
let writeApkg (outputPath: string) (deckName: string) (notes: ApkgNoteInput seq) : int * int =
    let outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath))
    if not (String.IsNullOrEmpty(outputDir)) && not (Directory.Exists(outputDir)) then
        Directory.CreateDirectory(outputDir) |> ignore

    let tempDir = Path.Combine(Path.GetTempPath(), "ll-apkg-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(tempDir) |> ignore
    let tempDbPath = Path.Combine(tempDir, "collection.anki2")

    let nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    let nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    let modelId = computeStableId "LudumLinguarum-NoteType-v1"
    let deckId = computeStableId ("LudumLinguarum-Deck-" + deckName)

    let mediaByFilename = Dictionary<string, byte array>(StringComparer.OrdinalIgnoreCase)
    let registerMedia (attOpt: ApkgMediaAttachment option) =
        match attOpt with
        | None -> ()
        | Some att when not (String.IsNullOrWhiteSpace(att.FileName)) && att.Data.Length > 0 ->
            if not (mediaByFilename.ContainsKey(att.FileName)) then
                mediaByFilename.[att.FileName] <- att.Data
        | _ -> ()

    let mutable noteCount = 0
    let mutable cardCount = 0

    try
        do
            use conn = new SQLiteConnection(tempDbPath)
            createAnkiSchema conn

            let confJson = buildConfJson deckId modelId
            let modelsJson = buildModelsJson modelId deckId nowSeconds
            let decksJson = buildDecksJson deckId deckName nowSeconds
            let dconfJson = buildDconfJson ()

            conn.Execute(
                "INSERT INTO col (id, crt, mod, scm, ver, dty, usn, ls, conf, models, decks, dconf, tags) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                1,
                nowSeconds,
                nowMs,
                nowMs,
                11,
                0,
                0,
                0,
                confJson,
                modelsJson,
                decksJson,
                dconfJson,
                "{}"
            )
            |> ignore

            let mutable nextNoteId = nowMs
            let mutable nextCardId = nowMs
            let mutable nextDue = 1L

            conn.RunInTransaction(fun () ->
                for note in notes do
                    registerMedia note.RecognitionAudio
                    registerMedia note.ProductionAudio
                    registerMedia note.Image

                    let recogAudioField =
                        match note.RecognitionAudio with
                        | Some a -> sprintf "[sound:%s]" a.FileName
                        | None -> ""
                    let prodAudioField =
                        match note.ProductionAudio with
                        | Some a -> sprintf "[sound:%s]" a.FileName
                        | None -> ""
                    let imageField =
                        match note.Image with
                        | Some img -> sprintf "<img src=\"%s\">" img.FileName
                        | None -> ""

                    let addReverse = if note.Reversible then "y" else ""
                    let addListening =
                        if note.IncludeListeningCard && note.ProductionAudio.IsSome then "y" else ""

                    let recogHtml = escapeHtmlText note.RecognitionText
                    let prodHtml = escapeHtmlText note.ProductionText
                    let lessonHtml = escapeHtmlText note.Lesson
                    let speakerHtml = escapeHtmlText note.Speaker

                    let fields =
                        [|
                            recogHtml
                            prodHtml
                            recogAudioField
                            prodAudioField
                            imageField
                            lessonHtml
                            speakerHtml
                            addReverse
                            addListening
                        |]
                    let fldsJoined = String.Join(fieldSeparator, fields)
                    let sfld = note.RecognitionText.Trim()
                    let csum = computeAnkiFieldChecksum sfld
                    let guid = computeStableGuid (deckName + "\u0000" + note.StableKey)

                    let cleanedTags =
                        note.Tags
                        |> List.map sanitizeTag
                        |> List.filter (fun t -> not (String.IsNullOrWhiteSpace(t)))
                        |> List.distinct
                    let tagsField =
                        if cleanedTags.IsEmpty then ""
                        else " " + String.Join(" ", cleanedTags) + " "

                    let nid = nextNoteId
                    nextNoteId <- nextNoteId + 1L

                    conn.Execute(
                        "INSERT INTO notes (id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                        nid,
                        guid,
                        modelId,
                        nowSeconds,
                        -1,
                        tagsField,
                        fldsJoined,
                        sfld,
                        csum,
                        0,
                        ""
                    )
                    |> ignore
                    noteCount <- noteCount + 1

                    let insertCard (ord: int) =
                        let cid = nextCardId
                        nextCardId <- nextCardId + 1L
                        let due = nextDue
                        nextDue <- nextDue + 1L
                        conn.Execute(
                            "INSERT INTO cards (id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                            cid,
                            nid,
                            deckId,
                            ord,
                            nowSeconds,
                            -1,
                            0,
                            0,
                            due,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            ""
                        )
                        |> ignore
                        cardCount <- cardCount + 1

                    insertCard 0
                    if note.Reversible then
                        insertCard 1
                    if note.IncludeListeningCard && note.ProductionAudio.IsSome then
                        insertCard 2
            )

        if File.Exists(outputPath) then
            File.Delete(outputPath)

        use zipStream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)
        use archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen = false)

        // Add collection.anki2
        do
            let dbEntry = archive.CreateEntry("collection.anki2", CompressionLevel.Optimal)
            use dbEntryStream = dbEntry.Open()
            use srcDbStream = File.OpenRead(tempDbPath)
            srcDbStream.CopyTo(dbEntryStream)

        // Add numbered media files + media JSON index
        let mediaIndex = Dictionary<string, string>()
        let orderedMedia =
            mediaByFilename
            |> Seq.sortBy (fun kv -> kv.Key)
            |> Seq.indexed

        for (idx, kv) in orderedMedia do
            let key = idx.ToString()
            mediaIndex.[key] <- kv.Key
            let entry = archive.CreateEntry(key, CompressionLevel.NoCompression)
            use s = entry.Open()
            s.Write(kv.Value, 0, kv.Value.Length)

        do
            let mediaJsonEntry = archive.CreateEntry("media", CompressionLevel.Optimal)
            use mediaJsonStream = mediaJsonEntry.Open()
            let mediaJsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mediaIndex))
            mediaJsonStream.Write(mediaJsonBytes, 0, mediaJsonBytes.Length)

        (noteCount, cardCount)
    finally
        try Directory.Delete(tempDir, true) with _ -> ()
