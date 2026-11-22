module MediaDatabase

open SQLite
open System
open System.Security.Cryptography
open System.Text

// Ensure the SQLite native provider is initialized even if MediaDatabase is used standalone.
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3())

type MediaKind =
    | Audio = 0
    | Image = 1

type MediaSource =
    | Extracted = 0
    | Tts = 1

/// <summary>
/// Provenance metadata for TTS-synthesized audio clips so users can inspect how
/// an audio asset was generated and decide when to regenerate it.
/// </summary>
type TtsProvenance =
    {
        Provider: string
        Voice: string
        Model: string
        LanguageTag: string
        GeneratedAtUtc: string
    }

type MediaRecord =
    {
        Hash: string
        Kind: MediaKind
        MimeType: string
        FileExtension: string
        LanguageTag: string
        Source: MediaSource
        SourceDescription: string
        TtsProvider: string
        TtsVoice: string
        TtsModel: string
        GeneratedAtUtc: string
        DurationMs: int
        Width: int
        Height: int
        ByteLength: int
        Data: byte array
    }

/// <summary>
/// Lightweight metadata projection (without the binary BLOB payload).
/// </summary>
type MediaMetadata =
    {
        Hash: string
        Kind: MediaKind
        MimeType: string
        FileExtension: string
        LanguageTag: string
        Source: MediaSource
        SourceDescription: string
        TtsProvider: string
        TtsVoice: string
        TtsModel: string
        GeneratedAtUtc: string
        DurationMs: int
        Width: int
        Height: int
        ByteLength: int
    }

let private safeStr (s: string) =
    if isNull s then "" else s

type MediaEntry() =
    [<PrimaryKey>]
    member val Hash = "" with get, set
    member val Kind = int MediaKind.Audio with get, set
    member val MimeType = "" with get, set
    member val FileExtension = "" with get, set
    [<Indexed>]
    member val LanguageTag = "" with get, set
    [<Indexed>]
    member val Source = int MediaSource.Extracted with get, set
    member val SourceDescription = "" with get, set
    member val TtsProvider = "" with get, set
    member val TtsVoice = "" with get, set
    member val TtsModel = "" with get, set
    member val GeneratedAtUtc = "" with get, set
    member val DurationMs = 0 with get, set
    member val Width = 0 with get, set
    member val Height = 0 with get, set
    member val ByteLength = 0 with get, set
    member val Data: byte array = Array.empty with get, set

    member this.ToMediaRecord() : MediaRecord =
        {
            Hash = safeStr this.Hash
            Kind = enum<MediaKind> this.Kind
            MimeType = safeStr this.MimeType
            FileExtension = safeStr this.FileExtension
            LanguageTag = safeStr this.LanguageTag
            Source = enum<MediaSource> this.Source
            SourceDescription = safeStr this.SourceDescription
            TtsProvider = safeStr this.TtsProvider
            TtsVoice = safeStr this.TtsVoice
            TtsModel = safeStr this.TtsModel
            GeneratedAtUtc = safeStr this.GeneratedAtUtc
            DurationMs = this.DurationMs
            Width = this.Width
            Height = this.Height
            ByteLength = this.ByteLength
            Data = if isNull this.Data then Array.empty else this.Data
        }

    member this.ToMediaMetadata() : MediaMetadata =
        {
            Hash = safeStr this.Hash
            Kind = enum<MediaKind> this.Kind
            MimeType = safeStr this.MimeType
            FileExtension = safeStr this.FileExtension
            LanguageTag = safeStr this.LanguageTag
            Source = enum<MediaSource> this.Source
            SourceDescription = safeStr this.SourceDescription
            TtsProvider = safeStr this.TtsProvider
            TtsVoice = safeStr this.TtsVoice
            TtsModel = safeStr this.TtsModel
            GeneratedAtUtc = safeStr this.GeneratedAtUtc
            DurationMs = this.DurationMs
            Width = this.Width
            Height = this.Height
            ByteLength = this.ByteLength
        }

    static member FromMediaRecord(r: MediaRecord) =
        let e = new MediaEntry()
        e.Hash <- r.Hash
        e.Kind <- int r.Kind
        e.MimeType <- r.MimeType
        e.FileExtension <- r.FileExtension
        e.LanguageTag <- r.LanguageTag
        e.Source <- int r.Source
        e.SourceDescription <- r.SourceDescription
        e.TtsProvider <- r.TtsProvider
        e.TtsVoice <- r.TtsVoice
        e.TtsModel <- r.TtsModel
        e.GeneratedAtUtc <- r.GeneratedAtUtc
        e.DurationMs <- r.DurationMs
        e.Width <- r.Width
        e.Height <- r.Height
        e.ByteLength <- r.Data.Length
        e.Data <- r.Data
        e

/// <summary>
/// Maps a normalized (LanguageTag, Text) pair to its cached TTS media asset and
/// records full provenance so cached TTS can be inspected or selectively regenerated.
/// </summary>
type TtsCacheEntry() =
    [<PrimaryKey>]
    member val CacheKey = "" with get, set
    [<Indexed>]
    member val LanguageTag = "" with get, set
    member val Text = "" with get, set
    [<Indexed>]
    member val MediaHash = "" with get, set
    member val TtsProvider = "" with get, set
    member val TtsVoice = "" with get, set
    member val TtsModel = "" with get, set
    member val GeneratedAtUtc = "" with get, set

type TtsCacheRecord =
    {
        CacheKey: string
        LanguageTag: string
        Text: string
        MediaHash: string
        Provenance: TtsProvenance
    }

let computeSha256Hex (bytes: byte array) : string =
    let hashBytes = SHA256.HashData(bytes)
    Convert.ToHexString(hashBytes).ToLowerInvariant()

let computeTtsCacheKey (languageTag: string) (text: string) : string =
    let normalizedLang = languageTag.Trim().ToLowerInvariant()
    let normalizedText = text.Trim()
    let payload = Encoding.UTF8.GetBytes(normalizedLang + "\u0000" + normalizedText)
    computeSha256Hex payload

let mediaDatabaseSuffix = ".media.db3"

let isMediaDatabaseFilename (pathOrName: string) : bool =
    pathOrName.EndsWith(mediaDatabaseSuffix, StringComparison.OrdinalIgnoreCase)

let makeMediaDatabasePathForTextDb (textDbPath: string) : string =
    if textDbPath = ":memory:" then
        ":memory:"
    elif textDbPath.EndsWith(".db3", StringComparison.OrdinalIgnoreCase) then
        textDbPath.Substring(0, textDbPath.Length - 4) + mediaDatabaseSuffix
    else
        textDbPath + mediaDatabaseSuffix

[<Interface>]
type IMediaStore =
    inherit IDisposable
    abstract member PutMedia: MediaRecord -> string
    abstract member TryGetMedia: string -> MediaRecord option
    abstract member TryGetMetadata: string -> MediaMetadata option
    abstract member HasMedia: string -> bool
    abstract member ListMetadata: unit -> MediaMetadata array
    abstract member DeleteMedia: string -> unit
    abstract member PruneUnreferencedExtracted: Set<string> -> int
    abstract member TryGetTts: languageTag: string * text: string -> (MediaRecord * TtsProvenance) option
    abstract member PutTts: languageTag: string * text: string * record: MediaRecord -> string
    abstract member DeleteTts: languageTag: string * text: string -> unit
    abstract member ClearTtsCache: languageTagOpt: string option -> int
    abstract member Count: int

type MediaDatabase(dbPath: string) =
    let db = new SQLiteConnection(dbPath)
    let dbLock = obj()
    do
        db.CreateTable(typeof<MediaEntry>) |> ignore
        db.CreateTable(typeof<TtsCacheEntry>) |> ignore

    let normalizeRecord (r: MediaRecord) =
        let data = if isNull r.Data then Array.empty else r.Data
        let hash =
            if String.IsNullOrWhiteSpace(r.Hash) then
                computeSha256Hex data
            else
                r.Hash.ToLowerInvariant()
        { r with Hash = hash; ByteLength = data.Length; Data = data }

    let tryDeleteUnreferencedTtsBlob (mediaHash: string) =
        if not (String.IsNullOrWhiteSpace(mediaHash)) then
            let remainingRefs =
                db.ExecuteScalar<int>("select count(*) from TtsCacheEntry where MediaHash = ?", mediaHash)
            if remainingRefs = 0 then
                db.Execute(
                    "delete from MediaEntry where Hash = ? and Source = ?",
                    mediaHash,
                    int MediaSource.Tts
                )
                |> ignore

    member private _.Compact() =
        db.Execute("VACUUM;") |> ignore

    member _.PutMedia(record: MediaRecord) : string =
        let normalized = normalizeRecord record
        lock dbLock (fun () ->
            let existing =
                db.Query<MediaEntry>(
                    "select Hash, Source from MediaEntry where Hash = ?",
                    normalized.Hash
                )
                |> Seq.tryHead

            match existing with
            | Some e when e.Source = int MediaSource.Extracted && normalized.Source = MediaSource.Tts ->
                // Never downgrade native extracted media to TTS provenance if bytes happen to match.
                normalized.Hash
            | Some _ ->
                db.Update(MediaEntry.FromMediaRecord(normalized)) |> ignore
                normalized.Hash
            | None ->
                db.Insert(MediaEntry.FromMediaRecord(normalized)) |> ignore
                normalized.Hash
        )

    member _.TryGetMedia(hash: string) : MediaRecord option =
        if String.IsNullOrWhiteSpace(hash) then
            None
        else
            db.Query<MediaEntry>("select * from MediaEntry where Hash = ?", hash.ToLowerInvariant())
            |> Seq.tryHead
            |> Option.map (fun e -> e.ToMediaRecord())

    member _.TryGetMetadata(hash: string) : MediaMetadata option =
        if String.IsNullOrWhiteSpace(hash) then
            None
        else
            db.Query<MediaEntry>(
                "select Hash, Kind, MimeType, FileExtension, LanguageTag, Source, SourceDescription, TtsProvider, TtsVoice, TtsModel, GeneratedAtUtc, DurationMs, Width, Height, ByteLength from MediaEntry where Hash = ?",
                hash.ToLowerInvariant()
            )
            |> Seq.tryHead
            |> Option.map (fun e -> e.ToMediaMetadata())

    member _.HasMedia(hash: string) : bool =
        if String.IsNullOrWhiteSpace(hash) then
            false
        else
            db.ExecuteScalar<int>("select count(*) from MediaEntry where Hash = ?", hash.ToLowerInvariant()) > 0

    member _.ListMetadata() : MediaMetadata array =
        db.Query<MediaEntry>(
            "select Hash, Kind, MimeType, FileExtension, LanguageTag, Source, SourceDescription, TtsProvider, TtsVoice, TtsModel, GeneratedAtUtc, DurationMs, Width, Height, ByteLength from MediaEntry"
        )
        |> Seq.map (fun e -> e.ToMediaMetadata())
        |> Array.ofSeq

    member _.DeleteMedia(hash: string) =
        if not (String.IsNullOrWhiteSpace(hash)) then
            let h = hash.ToLowerInvariant()
            db.Execute("delete from TtsCacheEntry where MediaHash = ?", h) |> ignore
            db.Execute("delete from MediaEntry where Hash = ?", h) |> ignore

    member this.PruneUnreferencedExtracted(referencedHashes: Set<string>) : int =
        let normalizedRefs = referencedHashes |> Set.map (fun h -> h.ToLowerInvariant())
        let extractedHashes =
            db.Query<MediaEntry>("select Hash from MediaEntry where Source = ?", int MediaSource.Extracted)
            |> Seq.map (fun e -> e.Hash)
            |> Array.ofSeq

        let toDelete =
            extractedHashes
            |> Array.filter (fun h -> not (normalizedRefs.Contains(h)))

        if toDelete.Length > 0 then
            db.RunInTransaction(fun () ->
                toDelete
                |> Array.iter (fun h ->
                    db.Execute("delete from MediaEntry where Hash = ? and Source = ?", h, int MediaSource.Extracted)
                    |> ignore
                )
            )
            this.Compact()

        toDelete.Length

    member this.TryGetTts(languageTag: string, text: string) : (MediaRecord * TtsProvenance) option =
        let cacheKey = computeTtsCacheKey languageTag text
        let cacheEntry =
            db.Query<TtsCacheEntry>("select * from TtsCacheEntry where CacheKey = ?", cacheKey)
            |> Seq.tryHead

        match cacheEntry with
        | None -> None
        | Some ce ->
            match this.TryGetMedia(ce.MediaHash) with
            | None -> None
            | Some media ->
                let provenance =
                    {
                        Provider = safeStr ce.TtsProvider
                        Voice = safeStr ce.TtsVoice
                        Model = safeStr ce.TtsModel
                        LanguageTag = safeStr ce.LanguageTag
                        GeneratedAtUtc = safeStr ce.GeneratedAtUtc
                    }
                Some (media, provenance)

    member this.PutTts(languageTag: string, text: string, record: MediaRecord) : string =
        let ttsRecord =
            {
                record with
                    Kind = MediaKind.Audio
                    Source = MediaSource.Tts
                    LanguageTag = languageTag
                    GeneratedAtUtc =
                        if String.IsNullOrWhiteSpace(record.GeneratedAtUtc) then
                            DateTime.UtcNow.ToString("O")
                        else
                            record.GeneratedAtUtc
            }

        let newHash = this.PutMedia(ttsRecord)
        let cacheKey = computeTtsCacheKey languageTag text
        let existingCache =
            db.Query<TtsCacheEntry>("select * from TtsCacheEntry where CacheKey = ?", cacheKey)
            |> Seq.tryHead

        let oldHashOpt = existingCache |> Option.map (fun c -> c.MediaHash)

        let entry = new TtsCacheEntry()
        entry.CacheKey <- cacheKey
        entry.LanguageTag <- languageTag
        entry.Text <- text.Trim()
        entry.MediaHash <- newHash
        entry.TtsProvider <- ttsRecord.TtsProvider
        entry.TtsVoice <- ttsRecord.TtsVoice
        entry.TtsModel <- ttsRecord.TtsModel
        entry.GeneratedAtUtc <- ttsRecord.GeneratedAtUtc

        match existingCache with
        | Some _ -> db.Update(entry) |> ignore
        | None -> db.Insert(entry) |> ignore

        match oldHashOpt with
        | Some oldHash when not (String.Equals(oldHash, newHash, StringComparison.OrdinalIgnoreCase)) ->
            tryDeleteUnreferencedTtsBlob oldHash
        | _ -> ()

        newHash

    member _.DeleteTts(languageTag: string, text: string) =
        let cacheKey = computeTtsCacheKey languageTag text
        let existing =
            db.Query<TtsCacheEntry>("select * from TtsCacheEntry where CacheKey = ?", cacheKey)
            |> Seq.tryHead
        match existing with
        | None -> ()
        | Some ce ->
            db.Execute("delete from TtsCacheEntry where CacheKey = ?", cacheKey) |> ignore
            tryDeleteUnreferencedTtsBlob ce.MediaHash

    member this.ClearTtsCache(languageTagOpt: string option) : int =
        let entries =
            match languageTagOpt with
            | Some lang ->
                db.Query<TtsCacheEntry>("select * from TtsCacheEntry where lower(LanguageTag) = ?", lang.Trim().ToLowerInvariant())
                |> Array.ofSeq
            | None ->
                db.Query<TtsCacheEntry>("select * from TtsCacheEntry")
                |> Array.ofSeq

        if entries.Length > 0 then
            db.RunInTransaction(fun () ->
                entries
                |> Array.iter (fun ce ->
                    db.Execute("delete from TtsCacheEntry where CacheKey = ?", ce.CacheKey) |> ignore
                    tryDeleteUnreferencedTtsBlob ce.MediaHash
                )
            )
            this.Compact()

        entries.Length

    member _.Count : int =
        db.ExecuteScalar<int>("select count(*) from MediaEntry")

    interface IMediaStore with
        member this.PutMedia(r) = this.PutMedia(r)
        member this.TryGetMedia(h) = this.TryGetMedia(h)
        member this.TryGetMetadata(h) = this.TryGetMetadata(h)
        member this.HasMedia(h) = this.HasMedia(h)
        member this.ListMetadata() = this.ListMetadata()
        member this.DeleteMedia(h) = this.DeleteMedia(h)
        member this.PruneUnreferencedExtracted(refs) = this.PruneUnreferencedExtracted(refs)
        member this.TryGetTts(lang, text) = this.TryGetTts(lang, text)
        member this.PutTts(lang, text, record) = this.PutTts(lang, text, record)
        member this.DeleteTts(lang, text) = this.DeleteTts(lang, text)
        member this.ClearTtsCache(langOpt) = this.ClearTtsCache(langOpt)
        member this.Count = this.Count

    interface IDisposable with
        member _.Dispose() = db.Dispose()
