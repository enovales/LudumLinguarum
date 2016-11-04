module SonicAdventureDX

open LLDatabase
open System
open System.IO
open System.Text

/// <summary>
/// Reads the 0xFFFFFFFF-terminated offset block at the start of a simple bin file.
/// </summary>
/// <param name="br"></param>
let internal readOffsetsFromSimpleBin(br: BinaryReader) = 
    let rec readOffsets(prev: uint32 list) = 
        match br.ReadUInt32() with
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

        br.ReadBytes(int l) |> encoding.GetChars

    offsetsAndLengths
    |> Array.map readString

let ExtractSonicAdventureDX(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

    let cards = [||]

    cards
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
