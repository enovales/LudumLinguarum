module RGGStudio.LocalizedBinFile

open System
open System.IO
open System.Text

// RGG Studio games (Yakuza / Like a Dragon) store localized database text in
// ".bin" files whose header starts with the date 2007/03/19 packed as the four
// bytes 20 07 03 19 (big-endian). The file describes a table: a list of named
// parameters (columns), each with a type, and one entry (row) per item. Only
// the string-bearing parameter types carry localized prose:
//   0: string      - entryAmount consecutive null-terminated strings
//   1: string_tbl  - parameterCount distinct null-terminated strings, then
//                    a byte-per-entry index table into them
//   2: string_idx  - (int16BE entry id, null-terminated string) pairs
// Reference: SlowpokeVG/Yakuza-2007.03.19-bin-file-exporter-importer.

type BinParameter =
    {
        name: string
        values: string array
    }

type BinFile =
    {
        parameters: BinParameter array
    }

let internal binMagic = [| 0x20uy; 0x07uy; 0x03uy; 0x19uy |]

let private decodeString(bytes: byte array): string =
    let utf8 = UTF8Encoding(false, true)
    try
        utf8.GetString(bytes)
    with
    | _ ->
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Encoding.GetEncoding(932).GetString(bytes)

let private readInt32BE(data: byte array)(offset: int): int =
    (int data.[offset] <<< 24) ||| (int data.[offset + 1] <<< 16) ||| (int data.[offset + 2] <<< 8) ||| int data.[offset + 3]

let private readInt16BE(data: byte array)(offset: int): int16 =
    int16((int16 data.[offset] <<< 8) ||| int16 data.[offset + 1])

/// Returns the null-terminated byte range starting at the given offset, along
/// with the offset just past the terminating null.
let private readNullTerminated(data: byte array)(offset: int): byte array * int =
    let mutable endOffset = offset
    while endOffset < data.Length && data.[endOffset] <> 0uy do
        endOffset <- endOffset + 1
    let value = if endOffset > offset then data.[offset..endOffset - 1] else [||]
    (value, endOffset + 1)

let private readStringParameter(block: byte array)(entryAmount: int)(parameterCount: int): string array =
    let values = Array.create entryAmount ""
    let mutable offset = 0
    for i = 0 to entryAmount - 1 do
        if offset < block.Length then
            let (bytes, nextOffset) = readNullTerminated block offset
            values.[i] <- decodeString bytes
            offset <- nextOffset
    values

let private readStringTableParameter(block: byte array)(entryAmount: int)(parameterCount: int): string array =
    if parameterCount <= 0 || entryAmount = 0 || block.Length < entryAmount then
        Array.create entryAmount ""
    else
        let distinct = Array.zeroCreate<string> parameterCount
        let mutable offset = 0
        for i = 0 to parameterCount - 1 do
            if offset < block.Length then
                let (bytes, nextOffset) = readNullTerminated block offset
                distinct.[i] <- decodeString bytes
                offset <- nextOffset

        let tableStart = offset
        if tableStart + entryAmount > block.Length then
            raise(InvalidDataException("20070319 bin: string table parameter has no index table"))

        Array.init entryAmount (fun i ->
            let index = int block.[tableStart + i]
            if index >= 0 && index < parameterCount then distinct.[index] else "")

let private readStringIndexParameter(block: byte array)(entryAmount: int)(parameterCount: int): string array =
    let values = Array.create entryAmount ""
    let mutable offset = 0
    let mutable wordCount = 0
    while wordCount < parameterCount && offset + 2 <= block.Length do
        let entryId = readInt16BE block offset
        offset <- offset + 2
        let (bytes, nextOffset) = readNullTerminated block offset
        if entryId >= 0s && int entryId < entryAmount then
            values.[int entryId] <- decodeString bytes
        offset <- nextOffset
        wordCount <- wordCount + 1
    values

/// Parses a 20070319 .bin file and returns the string values of every
/// string-bearing parameter, indexed by entry.
let parseLocalizedBinFile(data: byte array): BinFile =
    if data.Length < 16 then raise(InvalidDataException("20070319 bin: file too short"))
    if data.[0..3] <> binMagic then raise(InvalidDataException("20070319 bin: bad magic"))

    let parameterAmount = readInt32BE data 4
    let entryAmount = readInt32BE data 8
    if parameterAmount < 0 || parameterAmount > 1024 || entryAmount < 0 || entryAmount > 1000000 then
        raise(InvalidDataException("20070319 bin: implausible counts"))

    let headerEnd = 16 + 64 * parameterAmount
    if headerEnd > data.Length then raise(InvalidDataException("20070319 bin: truncated parameter header"))

    let parameters = ResizeArray<BinParameter>()
    let mutable dataOffset = headerEnd

    for currentParam = 0 to parameterAmount - 1 do
        let paramHeaderStart = 16 + 64 * currentParam
        let nameBytes = data.[paramHeaderStart..paramHeaderStart + 47]
        let mutable nameLength = 0
        while nameLength < nameBytes.Length && nameBytes.[nameLength] <> 0uy do
            nameLength <- nameLength + 1
        let parameterName = Encoding.ASCII.GetString(nameBytes, 0, nameLength)
        let parameterType = readInt32BE data (paramHeaderStart + 48)
        let parameterCount = readInt32BE data (paramHeaderStart + 52)
        let parameterSize = readInt32BE data (paramHeaderStart + 56)

        if parameterSize < 0 || dataOffset + parameterSize > data.Length then
            raise(InvalidDataException(sprintf "20070319 bin: parameter '%s' exceeds file bounds" parameterName))

        let values =
            match parameterType with
            | 0 -> readStringParameter data.[dataOffset..dataOffset + parameterSize - 1] entryAmount parameterCount
            | 1 -> readStringTableParameter data.[dataOffset..dataOffset + parameterSize - 1] entryAmount parameterCount
            | 2 -> readStringIndexParameter data.[dataOffset..dataOffset + parameterSize - 1] entryAmount parameterCount
            | _ -> [||]

        if values.Length > 0 then
            parameters.Add({ name = parameterName; values = values })

        dataOffset <- dataOffset + parameterSize

    {
        parameters = parameters.ToArray()
    }
