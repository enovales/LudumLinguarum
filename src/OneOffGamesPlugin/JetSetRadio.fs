module JetSetRadio

open LLDatabase
open StringExtractors
open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.IO
open System.Resources
open System.Text
open System.Text.RegularExpressions

type ReaderWrapper(br: BinaryReader) = 
    let needToConvert = BitConverter.IsLittleEndian
    member this.Seek(offset: int64) = br.BaseStream.Seek(offset, SeekOrigin.Begin)
    member this.ReadByte() = br.ReadByte()
    member this.ReadBytes(count: int) = br.ReadBytes(count)
    member this.ReadUInt16() = 
        let bytes = br.ReadBytes(2)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt16(bytes, 0)

    member this.ReadUInt32() = 
        let bytes = br.ReadBytes(4)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt32(bytes, 0)

// **************************************************************************
// .srt subtitle files from modern re-release
// **************************************************************************

// **************************************************************************
// AFS archive code
// **************************************************************************
type AFSArchiveRawFileEntry = {
        Offset: uint32
        Length: uint32
    }

type AFSArchiveRawFilenameEntry = {
        Name: string
        Other: byte array
    }

type AFSArchiveFileEntry = {
        Name: string
        Offset: uint32
        Length: uint32
    }

type AFSArchive(s: Stream, br: BinaryReader, entries: AFSArchiveFileEntry array) = 
    new(s: Stream) = 
        let br = new BinaryReader(s, Encoding.UTF8, true)

        // read magic header
        if br.ReadBytes(4) <> [| byte 0x41; byte 0x46; byte 0x53; byte 0x00 |] then
            raise(exn("invalid magic header"))

        // read number of files
        let numberOfFiles = br.ReadInt32()

        // read file entries.
        // theoretical maximum file entry count: (0x80000 - 0x4 (header) - 0x8 (offset of file name table)) / 0x8
        let maximumFileEntries = (0x80000 - 0x4 - 0x8) / 0x8
        let readNextFileEntry = (fun (n: int) -> 
                if (n + 1) < maximumFileEntries then
                    let nextRecord = { AFSArchiveRawFileEntry.Offset = br.ReadUInt32(); Length = br.ReadUInt32() }
                    match nextRecord with
                    | nr when nr.Offset = uint32 0 && nr.Length = uint32 0 -> None
                    | _ -> Some((nextRecord, n + 1))
                else
                    None
            )
        let rawFileEntries = Seq.unfold readNextFileEntry 0 |> Array.ofSeq

        // read filename entries
        let filenameTableOffsetOffset = int64 0x7FFF8
        if s.Seek(filenameTableOffsetOffset, SeekOrigin.Begin) <> filenameTableOffsetOffset then
            raise(exn("invalid filename table offset offset"))
        let filenameTableOffset = br.ReadUInt32()

        if s.Seek(int64 filenameTableOffset, SeekOrigin.Begin) <> int64 filenameTableOffset then
            raise(exn("invalid filename table offset"))

        let readNextFilenameEntry(numEntries: int) = (fun (n: int) ->
                if n >= numEntries then
                    None
                else
                    Some(({ AFSArchiveRawFilenameEntry.Name = Encoding.UTF8.GetString(br.ReadBytes(32)).TrimEnd(char 0); Other = br.ReadBytes(16) }, n + 1))
            )
        let rawFilenameEntries = Seq.unfold(readNextFilenameEntry(numberOfFiles))(0) |> Array.ofSeq

        if (rawFilenameEntries.Length <> rawFileEntries.Length) then
            raise(exn("mismatch in number of file entries and filename entries"))

        let fileEntries = rawFilenameEntries |> Array.zip(rawFileEntries) |> Array.map(fun (fe, fne) -> 
            {
                AFSArchiveFileEntry.Name = fne.Name
                Offset = fe.Offset
                Length = fe.Length
            })
        new AFSArchive(s, br, fileEntries)

    new(filename: string) = 
        new AFSArchive(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))

    member this.FileEntries with get() = entries
    member this.GetStream(fe: AFSArchiveFileEntry) = 
        Debug.Assert(entries |> Array.contains(fe), "file entry doesn't belong to this archive")
        let newPos = s.Seek(int64 fe.Offset, SeekOrigin.Begin)
        if (newPos <> int64 fe.Offset) then
            raise(exn(sprintf "could not seek to file offset %d in file %s" fe.Offset fe.Name))

        let resultBytes = br.ReadBytes(int fe.Length)
        if (resultBytes.Length <> int fe.Length) then
            raise(exn(sprintf "could not read entire file size %d in file %s" fe.Length fe.Name))

        new MemoryStream(resultBytes)

    member this.GetBinaryReader(fe: AFSArchiveFileEntry) = 
        new BinaryReader(this.GetStream(fe))

    interface IDisposable with
        member this.Dispose() = 
            s.Dispose()

// **************************************************************************
// String block extraction code
// **************************************************************************

/// <summary>
/// Function intended for use with unfold, which reads from the provided binary reader
/// until the specified number of null bytes is encountered.
/// </summary>
/// <param name="r">the binary reader</param>
/// <param name="maxNulls">the number of null bytes that will terminate the unfold</param>
/// <param name="skipTrailingNulls">whether or not trailing nulls beyond the required ones should be skipped</param>
/// <param name="currentNulls">the current number of nulls encountered</param>
let readUntilTooManyNulls(r: BinaryReader, maxNulls: int, skipTrailingNulls: bool)(currentNulls: int) = 
    if (r.BaseStream.Position + int64 1) >= r.BaseStream.Length then
        None
    else
        match r.ReadByte() with
        | b when (b = byte 0) && ((currentNulls + 1) >= maxNulls) -> 
            if skipTrailingNulls then
                // JSR files pad to 4 bytes with nulls, it seems.
                while (r.BaseStream.Position % int64 4 <> int64 0) do 
                    r.ReadByte() |> ignore
            None
        | b when (b = byte 0) -> Some((b, currentNulls + 1))
        | b -> Some((b, 0))

type StringBlock = {
    Languages: string array
}

type StringBlockSet = {
    StringBlocks: StringBlock array
    Id: int64
} with
    static member FromCSVLines(csvLines: string array): StringBlockSet = 
        { StringBlockSet.StringBlocks = [||]; Id = int64 0 }

type StringBlockExtractorEntry = {
        RelativePath: string
        OverrideBaseKey: string
        StartingOffset: int64
        Id: int64
        Languages: string
        NumConsecutiveStrings: int
        Gender: string
        SoundResource: string
        Reversible: bool
    }

type StringBlockExtractedEntry = {
        Entry: StringBlockExtractorEntry
        Language: string
        Text: string
    }

type StringBlockExtractor(entries: StringBlockExtractorEntry seq, streamGenerator: StringBlockExtractorEntry -> Stream) = 
    static member private DefaultFileStreamGenerator(rootPath: string)(entry: StringBlockExtractorEntry): Stream = 
        new FileStream(Path.Combine(rootPath, entry.RelativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream

    private new (rootPath: string, csvLines: string array) = 
        let tryParseAsHex(s: string): int64 = 
            if (s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase)) then
                Int64.Parse(s.Substring(2), NumberStyles.HexNumber)
            else
                Int64.Parse(s)

        // Allow for sparse specification of relative path, starting offset, and ID, to reduce
        // duplication in the file.
        let mutable lastRelativePath = ""
        let mutable lastStartingOffset = ""
        let mutable lastID = ""
        let getOrLast(s: string, os: string) =
            if String.IsNullOrWhiteSpace(s) then
                os
            else
                s

        let csvEntries = csvLines |> Array.map(fun line ->
                let fields = line.Split(',') |> Array.map(fun f -> f.Trim())
                
                lastRelativePath <- getOrLast(fields.[0], lastRelativePath)
                lastStartingOffset <- getOrLast(fields.[2], lastStartingOffset)
                lastID <- getOrLast(fields.[3], lastID)

                let rawRecord = {
                    StringBlockExtractorEntry.RelativePath = lastRelativePath
                    OverrideBaseKey = fields.[1]
                    StartingOffset = tryParseAsHex(lastStartingOffset)
                    Id = tryParseAsHex(lastID)
                    Languages = fields.[4]
                    NumConsecutiveStrings = 
                        match fields |> Array.tryItem(5) with
                        | Some(ncs) when not(String.IsNullOrWhiteSpace(ncs)) -> Int32.Parse(ncs)
                        | _ -> 1
                    Gender = 
                        match fields |> Array.tryItem(6) with
                        | Some(g) when not(String.IsNullOrWhiteSpace(g)) -> g
                        | _ -> "masculine"
                    SoundResource = 
                        match fields |> Array.tryItem(7) with
                        | Some(sr) when not(String.IsNullOrWhiteSpace(sr)) -> sr
                        | _ -> ""
                    Reversible = 
                        match fields |> Array.tryItem(8) with
                        | Some(r) when not(String.IsNullOrWhiteSpace(r)) -> Boolean.Parse(r)
                        | _ -> true
                }

                rawRecord
            )
        new StringBlockExtractor(csvEntries, StringBlockExtractor.DefaultFileStreamGenerator(rootPath))

    new(rootPath: string, sourceCsvStream: Stream) = 
        let toRead = sourceCsvStream.Length - sourceCsvStream.Position
        let csvBytes = Array.zeroCreate<byte>(int toRead)
        let csvRead = sourceCsvStream.Read(csvBytes, 0, int toRead)
        let csvText = Encoding.UTF8.GetString(csvBytes)
        let csvLines = csvText.Split([| Environment.NewLine |], StringSplitOptions.RemoveEmptyEntries) |> Array.skip(1)
        new StringBlockExtractor(rootPath, csvLines)

    new(rootPath: string, sourceCsvPath: string) = 
        // read in the source csv, and skip the header line
        let csvLines = File.ReadAllLines(sourceCsvPath) |> Array.skip(1)
        new StringBlockExtractor(rootPath, csvLines)

    member this.Extract(): StringBlockExtractedEntry seq = 
        let createExtractedEntry(e: StringBlockExtractorEntry, t: string, l: string) = 
            {
                StringBlockExtractedEntry.Entry = e
                Language = l
                Text = t 
            }

        // Basic idea: split the sequence of entries into ordered subsequences per file. (Order has to be preserved in
        // order to ensure that the strings are read and assigned in the correct order.) We can't use groupBy because
        // it doesn't guarantee ordering of the subsequences.
        let foldNextByFile s e = 
            match (s |> Map.tryFind(e.RelativePath)) with
            | Some(l) -> s |> Map.add e.RelativePath (e :: l)
            | _ -> s |> Map.add e.RelativePath [e]

        let entriesByFile = 
            entries 
            |> Array.ofSeq
            |> Array.fold foldNextByFile Map.empty
            |> Map.map (fun _ v -> v |> List.rev) |> Map.toArray

        let readNextNullTerminatedAndPaddedStrings(br: BinaryReader, ncs: int) = 
            let rec readNext(acc: string list, remaining: int): string = 
                if remaining = 0 then
                    String.Join(" ", acc |> Array.ofList |> Array.rev)
                else
                    readNext(
                        Encoding.UTF8.GetString(Array.unfold(readUntilTooManyNulls(br, 1, true))(0)).TrimEnd(char 0) :: acc,
                        remaining - 1)

            readNext([], ncs)

        // unfold function. the previous starting offset is passed in -- if it hasn't changed for this entry,
        // then we skip seeking the stream, and instead just continue to read.
        let createEntriesForLanguages(e: StringBlockExtractorEntry, s: string) = 
            e.Languages.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map(fun l -> createExtractedEntry(e, s, l.Trim()))

        let readNext(br: BinaryReader) = fun(state: int64 option * StringBlockExtractorEntry list) -> 
            match state with
            | (lastOffsetOpt, entries) ->
                // create an extracted entry for each language that each read entry provides.
                match (lastOffsetOpt, entries |> List.tryHead) with
                | (Some(lastOffset), Some(nextEntry)) when (lastOffset = nextEntry.StartingOffset) ->
                    // same as previous offset. do not seek.
                    let nextString = readNextNullTerminatedAndPaddedStrings(br, nextEntry.NumConsecutiveStrings)
                    Some(createEntriesForLanguages(nextEntry, nextString), (Some(lastOffset), entries |> List.tail))
                | (_, Some(nextEntry)) ->
                    // not the same as previous offset, so go ahead and seek.
                    if (br.BaseStream.Seek(nextEntry.StartingOffset, SeekOrigin.Begin) <> nextEntry.StartingOffset) then
                        failwith("couldn't seek to offset " + nextEntry.StartingOffset.ToString("X8") + " in file " + nextEntry.RelativePath)

                    let nextString = readNextNullTerminatedAndPaddedStrings(br, nextEntry.NumConsecutiveStrings)
                    Some(createEntriesForLanguages(nextEntry, nextString), (Some(nextEntry.StartingOffset), entries |> List.tail))
                | (_, None) ->
                    None

        let extractEntriesForFile(s: string * StringBlockExtractorEntry list) = 
            match s with
            | (fn: string, entries: StringBlockExtractorEntry list) -> 
                use file = streamGenerator(entries |> List.head)
                let br = new BinaryReader(file)

                List.unfold (readNext(br)) (None, entries) |> List.toArray |> Array.collect(fun t -> t)

        entriesByFile |> (Array.map extractEntriesForFile) |> Array.collect(fun t -> t) |> Seq.ofArray

let convertStringBlockExtractedEntriesToCardRecords(lid: int)(extracted: StringBlockExtractedEntry seq): CardRecord seq =
    let createCardRecordForEntry(e: StringBlockExtractedEntry) = 
        let baseKey = e.Entry.RelativePath + "\\" + e.Entry.Id.ToString()
        {
            CardRecord.ID = 0
            LessonID = lid
            Text = e.Text
            Gender = e.Entry.Gender
            Key = baseKey + e.Entry.Gender
            GenderlessKey = baseKey
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = e.Entry.SoundResource
            LanguageTag = e.Language
            Reversible = e.Entry.Reversible
        }

    extracted |> Seq.map createCardRecordForEntry

/// <summary>
/// Read double-null terminated strings up to a specified limit
/// </summary>
/// <param name="r">the binary reader to use</param>
/// <param name="n">the number of double-null terminated strings to read</param>
let readStringSet(r: BinaryReader, n: int)(_: unit) = 
    if (r.BaseStream.Position < r.BaseStream.Length) then
        let nextStrings = [| for i in 1..n do yield Encoding.UTF8.GetString(Array.unfold(readUntilTooManyNulls(r, 2, false))(0)).TrimEnd(char 0) |]
        if (nextStrings |> Array.forall(fun t -> String.IsNullOrWhiteSpace(t))) then
            None
        else
            Some((nextStrings, ()))
    else
        None


/// <summary>
/// Reads out a set of game strings from the reader that is passed in. The
/// format expected is:
///   string bytes
///   two nulls
///   ... (string ordering: japanese, english, french, german, spanish)
///   three nulls
/// </summary>
/// <param name="br"></param>
let readStringsFromBin(br: BinaryReader): string array array = 
    let stringBlock = Array.unfold (readUntilTooManyNulls(br, 3, false))(0)
    use ssr = new BinaryReader(new MemoryStream(stringBlock))

    Array.unfold(readStringSet(ssr, 5)) ()
        
// **************************************************************************
// Modern-release-specific string code
// **************************************************************************

type LanguageType = 
    | English       = 0
    | Spanish       = 1
    | Japanese      = 2
    | English2      = 3
    | French        = 4
    | German        = 5
    | Japanese2     = 6
    | Unknown       = 7

type JSRStringsHeader = 
    {
        languageCount: uint8;
        stringCount: uint16;
    }
    static member FromBinaryReader(br: ReaderWrapper) = 
        {
            JSRStringsHeader.languageCount = br.ReadByte();
            stringCount = br.ReadUInt16()
        }

type JSRStringTableEntry = 
    {
        offset: uint32;
        substringLengths: uint16 array
    }
    static member FromBinaryReader(br: ReaderWrapper) = 
        let offset = br.ReadUInt32()
        let languageCount = Enum.GetValues(typeof<LanguageType>).Length

        // skip zero entry at end
        let substringLengths = seq { for i in 0..languageCount do yield br.ReadUInt16() } |> Seq.take(languageCount) |> Array.ofSeq

        {
            JSRStringTableEntry.offset = offset;
            substringLengths = substringLengths
        }

type JSRString =
    {
        substrings: string array
    }
    static member FromBinaryReader(br: ReaderWrapper, ste: JSRStringTableEntry) = 
        let encoding = new UnicodeEncoding(true, false, false)
        br.Seek(int64 ste.offset) |> ignore
        {
            JSRString.substrings = ste.substringLengths |> Array.map(fun t -> 
                let stringBytes = br.ReadBytes((int t) * 2)
                let decodedString = encoding.GetString(stringBytes)
                decodedString.TrimEnd(char 0))
        }


// decodes the binary string blob for the game, and contains a 
type JSRStringsBinary(stringsByLanguage: JSRString array) =
    static member FromFile(path: string) = 
        use fs = new MemoryStream(File.ReadAllBytes(path))
        use br = new BinaryReader(fs)
        let rw = new ReaderWrapper(br)

        // read the header, then each string table entry, followed by all of the strings.
        let header = JSRStringsHeader.FromBinaryReader(rw)
        let stringTableEntries = seq { for i in 1..int header.stringCount do yield JSRStringTableEntry.FromBinaryReader(rw) } |> Array.ofSeq
        let strings = stringTableEntries |> Array.map(fun t -> JSRString.FromBinaryReader(rw, t))
        new JSRStringsBinary(strings)

    member this.Strings = stringsByLanguage

type JetSetRadio = 
    /// <summary>
    /// Extracts the STRINGS.STR file inside Jet Set Radio.
    /// </summary>
    /// <param name="path">game path</param>
    /// <param name="lessonID">lesson ID to use for the generated cards</param>
    static member private ExtractJSRStringsDotStr(path: string, lessonID: int) = 
        let jsr = JSRStringsBinary.FromFile(Path.Combine(path, @"CUSTOM\METRO\STRINGS.STR"))
        jsr.Strings |> Array.mapi(fun i t -> (i, t)) |> Array.collect(fun (i, t) -> 
                let key = "string" + i.ToString()
                t.substrings |> Array.mapi(fun l u ->
                    // map the language index to an ISO language code
                    let langOpt = 
                        match enum<LanguageType>(l) with
                        | LanguageType.English -> Some("en")
                        | LanguageType.English2 -> None
                        | LanguageType.French -> Some("fr")
                        | LanguageType.German -> Some("de")
                        | LanguageType.Japanese -> Some("jp")
                        | LanguageType.Japanese2 -> None
                        | LanguageType.Spanish -> Some("es")
                        | LanguageType.Unknown -> None
                        | _ -> raise(exn("Unknown language type"))

                    langOpt |> Option.map(fun lang -> 
                        {
                            CardRecord.Gender = "";
                            GenderlessKey = key;
                            GenderlessKeyHash = 0;
                            ID = 0;
                            Key = key;
                            KeyHash = 0;
                            LanguageTag = lang
                            LessonID = lessonID;
                            Reversible = true;
                            SoundResource = "";
                            Text = u
                        })
                ) |> Array.collect(fun t -> Option.toArray(t))
            )

    static member private ExtractStringsFromBinaries(path: string, lessonId: int) = 
        let formatCharFilter(e: StringBlockExtractedEntry) = 
            { e with StringBlockExtractedEntry.Text = Regex.Replace(e.Text, @"\s*\$n\s*", " ").Trim() }
        let extractor = new StringBlockExtractor(path, OneOffGamesData.DataAssembly.GetManifestResourceStream(@"OneOffGamesData.JetSetRadio.StringBlockExtraction.csv"))
        extractor.Extract()
            |> Seq.map formatCharFilter
            |> convertStringBlockExtractedEntriesToCardRecords(lessonId)
            |> Array.ofSeq

    static member ExtractJetSetRadio(path: string, db: LLDatabase, gameEntryWithId: GameRecord, args: string[]) = 
        let lessonEntry = {
            LessonRecord.GameID = gameEntryWithId.ID;
            ID = 0;
            Name = "Game Text"
        }
        let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
        let extractedCards = 
            Array.concat(
                [|
                    JetSetRadio.ExtractJSRStringsDotStr(path, lessonEntryWithId.ID);
                    // TBD: extract CUSTOM\instructions*.txt
                    // TBD: extract subtitles from DATA\VIDEOS\*.srt
                    JetSetRadio.ExtractStringsFromBinaries(path, lessonEntryWithId.ID)
                    // TBD: extract .NET satellite assembly localized strings
                |])

        // filter out empty cards.
        let allCards = extractedCards |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))

        db.CreateOrUpdateCards(allCards)

        ()
