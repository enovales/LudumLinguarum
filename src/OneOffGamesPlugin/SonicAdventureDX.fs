module SonicAdventureDX

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

/// <summary>
/// Reads the 0xFFFFFFFF-terminated offset block at the start of a simple bin file.
/// </summary>
/// <param name="br"></param>
let internal readOffsetsFromSimpleBin(br: BinaryReader) = 
    let rw = new StreamTools.ReaderWrapper(br)
    let rec readOffsets(prev: uint32 list) = 
        match rw.ReadUInt32() with
        | n when n = 0xFFFFFFFFu -> prev
        | n -> readOffsets(n :: prev)

    readOffsets([])
    |> Array.ofList
    |> Array.rev

/// <summary>
/// Calculates the string lengths for a simple bin file, given the offsets and 
/// the overall length of the file.
/// </summary>
/// <param name="offsets">the offsets read from the header block</param>
/// <param name="streamLength">the overall stream length</param>
let internal calculateStringLengthsForSimpleBin(offsets: uint32 array, streamLength: uint32) = 
    let pairwiseStringLengths = 
        offsets
        |> Array.pairwise
        |> Array.map (fun (o1, o2) -> o2 - o1)

    offsets |> Array.tryLast |> Option.map (fun l -> streamLength - l) |> Option.toArray
    |> Array.append pairwiseStringLengths
    
/// <summary>
/// Reads all strings from a simple bin file (containing a header with offsets, terminated
/// by 0xFFFFFFFF, and then the text).
/// </summary>
/// <param name="stream">the stream to process</param>
let internal readStringsFromSimpleBin(stream: Stream, encoding: Encoding) = 
    use br = new BinaryReader(stream, encoding, true)
    let offsets = readOffsetsFromSimpleBin(br)
    let stringLengths = calculateStringLengthsForSimpleBin(offsets, uint32 stream.Length)
    let offsetsAndLengths = 
        stringLengths
        |> Array.zip offsets

    let readString(o: uint32, l: uint32) = 
        if stream.Seek(int64 o, SeekOrigin.Begin) <> int64 o then
            failwith "couldn't seek to correct location"

        let chars = br.ReadBytes(int l) |> encoding.GetChars
        new String(chars)

    offsetsAndLengths
    |> Array.map readString

type ExtractionFunction = int * Encoding * string * string -> CardRecord array

/// <summary>
/// Used to normalize any whitespace in strings to a single space.
/// </summary>
/// <param name="s">string to normalize</param>
let private cleanupString(s: string) = 
    Regex.Replace(s, @"\s+", " ")

/// <summary>
/// Extracts a set of cards from a simple bin string file.
/// </summary>
/// <param name="lid">lesson ID to use</param>
/// <param name="encoding">text encoding to use</param>
/// <param name="language">language being extracted</param>
/// <param name="path">path to the file</param>
let private extractCardsForSimpleBin(lid: int, encoding: Encoding, language: string, path: string) = 
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)

    readStringsFromSimpleBin(fs, encoding)
    |> Array.distinct
    |> Array.map cleanupString
    |> Array.mapi (fun i s -> (i.ToString(), s.TrimStart([| char 0 |]).Trim()))
    |> Map.ofArray
    |> AssemblyResourceTools.createCardRecordForStrings(lid, "", language, "masculine")

let ExtractSonicAdventureDX(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    // make a lesson for each file that we need to extract.
    let filesToExtract: (string * ExtractionFunction) array = 
        [|
            (@"system\CHAODX_MESSAGE_BLACKMARKET_{0}.BIN", extractCardsForSimpleBin)
            (@"system\CHAODX_MESSAGE_HINT_{0}.BIN", extractCardsForSimpleBin)
            (@"system\CHAODX_MESSAGE_ITEM_{0}.BIN", extractCardsForSimpleBin)
            (@"system\CHAODX_MESSAGE_ODEKAKE_{0}.BIN", extractCardsForSimpleBin)
            (@"system\CHAODX_MESSAGE_PLAYERACTION_{0}.BIN", extractCardsForSimpleBin)
            (@"system\CHAODX_MESSAGE_RACE_{0}.BIN", extractCardsForSimpleBin)
            (@"system\CHAODX_MESSAGE_SYSTEM_{0}.BIN", extractCardsForSimpleBin)
            (@"system\MSGALITEM_{0}.BIN", extractCardsForSimpleBin)
            (@"system\MSGALKINDERBL_{0}.BIN", extractCardsForSimpleBin)
            (@"system\MSGALKINDERPR_{0}.BIN", extractCardsForSimpleBin)
            (@"system\MSGALWARN_{0}.BIN", extractCardsForSimpleBin)
            // TODO: add support for other file types, including files like
            // PAST_MES_*_*.BIN
            // MR_MES_*_*.BIN
            // SS_MES_*_*.BIN
        |]

    let makeLesson(name: string) = 
        let lessonEntry = {
                LessonRecord.GameID = g.ID;
                ID = 0;
                Name = name
            }

        { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

    let getCardsForTuple(t: string * ExtractionFunction * LessonRecord) = 
        let (fileRelativePath, fn, lesson) = t
        let languages = 
            [|
                ("E", "en", Encoding.GetEncoding("Windows-1252"))
                ("F", "fr", Encoding.GetEncoding("Windows-1252"))
                ("G", "de", Encoding.GetEncoding("Windows-1252"))
                ("J", "ja", Encoding.GetEncoding("shift_jis"))
                ("S", "es", Encoding.GetEncoding("Windows-1252"))
            |]

        let getCardsForLanguage(suffix: string, language: string, encoding: Encoding) = 
            fn(lesson.ID, encoding, language, Path.Combine(path, String.Format(fileRelativePath, suffix)))

        languages
        |> Array.collect getCardsForLanguage

    filesToExtract
    |> Array.map (fun (path, fn) -> (path, fn, makeLesson(path)))
    |> Array.collect getCardsForTuple
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
