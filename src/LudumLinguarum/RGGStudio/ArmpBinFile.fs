module RGGStudio.ArmpBinFile

open System
open System.IO
open System.Text

// Dragon Engine RGG Studio games (Yakuza 6, Kiwami 2, Judgment, Yakuza: Like
// a Dragon, ...) store localized text as "armp" database .bin files. The
// header is little-endian: a version (1 = Yakuza 6/Kiwami 2 era, 2 = Judgment
// / Like a Dragon era) at 0x0A and a pointer to the main table at 0x10. The
// main table describes rows, columns, and a shared pool of null-terminated
// UTF-8 strings; string-valued columns hold per-row indices into that pool.
// Reference: Ret-HZ/reARMP.

type ArmpColumn =
    {
        name: string
        values: string array
    }

type ArmpFile =
    {
        columns: ArmpColumn array
    }

let private readInt32LE(data: byte array)(offset: int): int =
    int(data.[offset]) ||| (int(data.[offset + 1]) <<< 8) ||| (int(data.[offset + 2]) <<< 16) ||| (int(data.[offset + 3]) <<< 24)

let private readInt16LE(data: byte array)(offset: int): int16 =
    int16(int(data.[offset]) ||| (int(data.[offset + 1]) <<< 8))

/// Reads a null-terminated UTF-8 string at the given offset.
let private readNullTerminatedString(data: byte array)(offset: int): string =
    let mutable endOffset = offset
    while endOffset < data.Length && data.[endOffset] <> 0uy do
        endOffset <- endOffset + 1
    Encoding.UTF8.GetString(data, offset, endOffset - offset)

/// Reads a table of count little-endian u32 values at the given offset.
let private readOffsetTable(data: byte array)(offset: int)(count: int): int array =
    if offset < 0 || offset + count * 4 > data.Length then
        raise(InvalidDataException("armp: offset table out of bounds"))
    Array.init count (fun i -> readInt32LE data (offset + i * 4))

/// Reads count null-terminated strings via an offset table.
let private readStringPool(data: byte array)(offsetTableOffset: int)(count: int): string array =
    if count = 0 then [||]
    else
        let offsets = readOffsetTable data offsetTableOffset count
        offsets
        |> Array.map (fun offset ->
            if offset < 0 || offset >= data.Length then ""
            else readNullTerminatedString data offset)

/// Returns true if the data looks like an armp .bin file.
let isArmpBinFile(data: byte array): bool =
    if data.Length < 0x14 then false
    else
        let version = readInt16LE data 0x0A
        if version <> 1s && version <> 2s then
            false
        else
            let pointerToMainTable = readInt32LE data 0x10
            pointerToMainTable > 0 && pointerToMainTable + 0x50 <= data.Length

/// Parses an armp .bin file and returns the string values of every
/// string-valued column, indexed by row.
let parseArmpBinFile(data: byte array): ArmpFile =
    if not(isArmpBinFile data) then raise(InvalidDataException("armp: bad header"))

    let version = readInt16LE data 0x0A
    let mainTable = readInt32LE data 0x10

    let rowCount = readInt32LE data (mainTable + 0x0)
    let columnCount = readInt32LE data (mainTable + 0x4)
    let textCount = readInt32LE data (mainTable + 0x8)
    let pointerToColumnDataTypes = readInt32LE data (mainTable + 0x18)
    let pointerToColumnContent = readInt32LE data (mainTable + 0x1C)
    let storageMode = int(data.[mainTable + 0x23])
    let pointerToTextOffsetTable = readInt32LE data (mainTable + 0x24)
    let pointerToColumnNames = readInt32LE data (mainTable + 0x28)
    let pointerToColumnDataTypesAux = readInt32LE data (mainTable + 0x48)

    if rowCount < 0 || rowCount > 1000000 || columnCount < 0 || columnCount > 10000 then
        raise(InvalidDataException("armp: implausible counts"))

    // Per-row storage (v2 storage mode 1) uses a different, more complex
    // layout that is not needed for the string tables that hold localized
    // prose; such tables are skipped.
    if storageMode <> 0 then
        { columns = [||] }
    else
        let textTable =
            if textCount > 0 && pointerToTextOffsetTable > 0 then
                readStringPool data pointerToTextOffsetTable textCount
            else
                [||]

        let columnNames =
            if pointerToColumnNames > 0 then
                readStringPool data pointerToColumnNames columnCount
            else
                Array.init columnCount (fun i -> string i)

        let columnTypes =
            if pointerToColumnDataTypes > 0 && pointerToColumnDataTypes + columnCount <= data.Length then
                data.[pointerToColumnDataTypes..pointerToColumnDataTypes + columnCount - 1]
            else
                Array.zeroCreate<byte> columnCount

        // v1 files keep the effective type of each column in the auxiliary
        // type table; v2 files use the primary one. String is type 12 in v1
        // and 13 in v2.
        let effectiveTypes =
            if version = 1s && pointerToColumnDataTypesAux > 0 && pointerToColumnDataTypesAux + columnCount <= data.Length then
                data.[pointerToColumnDataTypesAux..pointerToColumnDataTypesAux + columnCount - 1]
            else
                columnTypes

        let stringColumnType = if version = 1s then 12uy else 13uy

        let contentOffsets =
            if columnCount > 0 && pointerToColumnContent > 0 then
                readOffsetTable data pointerToColumnContent columnCount
            else
                Array.zeroCreate<int> columnCount

        let columns = ResizeArray<ArmpColumn>()

        if rowCount > 0 then
            for columnIndex = 0 to columnCount - 1 do
                let contentOffset = contentOffsets.[columnIndex]
                if contentOffset > 0 && contentOffset + rowCount * 4 <= data.Length && effectiveTypes.[columnIndex] = stringColumnType then
                    let indices = readOffsetTable data contentOffset rowCount
                    let values =
                        indices
                        |> Array.map (fun index ->
                            if index >= 0 && index < textTable.Length then textTable.[index] else "")
                    columns.Add({ name = columnNames.[columnIndex]; values = values })

        {
            columns = columns.ToArray()
        }
