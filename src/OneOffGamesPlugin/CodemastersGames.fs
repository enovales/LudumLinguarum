module CodemastersGames

open LLDatabase
open StreamTools
open System
open System.Text
open System.IO
open StreamTools

type BinaryBlob = string * byte array
type ChunkContainer = string * BinaryBlob array

// read null-terminated strings
let readNextString(br: BinaryReader) = 
    let readNextChar(c: char) = 
        if c = char 0 then
            None
        else
            Some((c, br.ReadChar()))
    new string(Seq.unfold(readNextChar)(br.ReadChar()) |> Seq.toArray)

let readNextNullTerminatedString(br: BinaryReader, endPosition: int64)(currentPosition: int64) = 
    if (currentPosition < endPosition) then
        Some((readNextString(br), br.BaseStream.Position))
    else
        None

let internal readSIDBChunk(bytes: byte array) = 
    let r = new BinaryReader(new MemoryStream(bytes))
    Array.unfold (readNextNullTerminatedString(r, int64(bytes.Length - 1))) (int64 0)
    
let internal readLNGBChunk(bytes: byte array) = 
    let r = new BinaryReader(new MemoryStream(bytes))
    Array.unfold (readNextNullTerminatedString(r, int64(bytes.Length - 1))) (int64 0)

let rec internal readNextChunk(r: ReaderWrapper, endPosition: int64)(currentPosition: int64) = 
    if (currentPosition < endPosition) then
        let typeString = Encoding.ASCII.GetString(r.ReadBytes(4))
        let size = r.ReadUInt32()

        // Some chunk types have extra header information that we need to skip, because
        // the size doesn't account for them.
        if typeString = "SIDA" then
            r.ReadUInt32() |> ignore

        let next: BinaryBlob = (typeString, r.ReadBytes(int size))
        Some((next, r.Position))
    else
        None

let internal exteriorChunkFromReader(r: ReaderWrapper): ChunkContainer = 
    let typeString = Encoding.ASCII.GetString(r.ReadBytes(4))
    let endPosition = int64(r.ReadUInt32())
    let internalChunks = Array.unfold (readNextChunk(r, endPosition)) (r.Position)

    (typeString, internalChunks)

let internal readStringsFromReader(br: BinaryReader) = 
    let wrapper = ReaderWrapper(br)
    let (_, chunks) = exteriorChunkFromReader(wrapper)

    let stringIds = 
        chunks
        |> Array.find(fun (ct, _) -> ct = "SIDB")
        |> snd 
        |> readSIDBChunk
        |> Array.map(fun s -> s.Trim())

    let stringValues = 
        chunks
        |> Array.find(fun (ct, _) -> ct = "LNGB")
        |> snd
        |> readLNGBChunk

    Array.zip stringIds stringValues
    |> Map.ofArray

let internal cardsForLanguage(lid: int)(fpl: string * (string * Encoding)) = 
    let (filePath, (language, encoding)) = fpl
    use br = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), encoding)
    readStringsFromReader(br)
    |> AssemblyResourceTools.createCardRecordForStrings(lid, "", language, "masculine")


let ExtractF1RaceStars(path: string) = 
    let filePaths = 
        [|
            @"data_win\localisation\language_bra.lng"
            @"data_win\localisation\language_eng.lng"
            @"data_win\localisation\language_fre.lng"
            @"data_win\localisation\language_ger.lng"
            @"data_win\localisation\language_ita.lng"
            @"data_win\localisation\language_jap.lng"
            @"data_win\localisation\language_pol.lng"
            @"data_win\localisation\language_spa.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.GetEncoding("shift_jis"))
            ("pl", Encoding.UTF8)
            ("es", Encoding.UTF8)
        |]

    let filePathsLanguagesAndEncodings = 
        Array.zip filePaths languagesAndEncodings

    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = 
            filePathsLanguagesAndEncodings
            |> Array.collect(cardsForLanguage(lessonEntry.ID))
    }