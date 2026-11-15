module RggEngine.RggFormats

open RggEngine.Sllz
open StreamTools
open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions

/// Regex for stripping RGG formatting tags such as <color=...>, </color>, <scale=...>, <size=...>, <Sign:01>, etc.
let private formattingTagRegex =
    Regex(@"<\s*/?\s*(?:color|scale|size|font|ruby|rb|rt|icon|sign)(?:[:=][^>]*|\s+[^>]*)?>", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

/// Regex for collapsing consecutive whitespace characters into a single space.
let private whitespaceCollapseRegex =
    Regex(@"\s+", RegexOptions.Compiled)

/// Cleans and normalizes a raw localized string extracted from an RGG Studio file.
let cleanText (raw: string) : string =
    if String.IsNullOrEmpty(raw) then
        ""
    else
        let withoutNulls = raw.Replace("\000", "")
        let withoutTags = formattingTagRegex.Replace(withoutNulls, "")
        let normalizedLineBreaks = withoutTags.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ")
        whitespaceCollapseRegex.Replace(normalizedLineBreaks, " ").Trim()

/// Helper to read a bitmask of count booleans at the given offset in a stream.
let internal readBitmask (rw: EndianReaderWrapper, count: int, offset: int64) : bool array =
    if count <= 0 || offset <= 0L || offset >= rw.Length then
        [||]
    else
        let savedPos = rw.Position
        try
            rw.Seek(offset) |> ignore
            let result = Array.zeroCreate<bool> count
            let mutable current = 0uy
            let mutable bitsLeft = 0
            for i = 0 to count - 1 do
                if bitsLeft = 0 then
                    current <- if rw.Position < rw.Length then rw.ReadByte() else 0uy
                    bitsLeft <- 8
                result.[i] <- (current &&& 0x01uy) = 0x01uy
                current <- current >>> 1
                bitsLeft <- bitsLeft - 1
            result
        finally
            rw.Seek(savedPos) |> ignore

// ============================================================================
// ARMP Binary Table Format (Dragon Engine v1/v2 & Old Engine)
// ============================================================================
module Armp =

    /// Checks whether a byte array (after optional SLLZ decompression) has the "armp" magic signature.
    let isArmp (rawBytes: byte array) : bool =
        let data = Sllz.decompressIfNeeded rawBytes
        not (isNull data)
        && data.Length >= 20
        && data.[0] = byte 'a'
        && data.[1] = byte 'r'
        && data.[2] = byte 'm'
        && data.[3] = byte 'p'

    type private DeMemberInfo =
        {
            MemberType: int
            Position: int
            ArraySize: uint32
        }

    let private readStringOffsetList (rw: EndianReaderWrapper, count: int, tableOffset: int64, encoding: Encoding) : string array =
        if count <= 0 || tableOffset <= 0L || tableOffset + int64 (count * 4) > rw.Length then
            [||]
        else
            let savedPos = rw.Position
            try
                rw.Seek(tableOffset) |> ignore
                let offsets = Array.init count (fun _ -> rw.ReadInt32())
                offsets
                |> Array.map (fun strOffset ->
                    if strOffset > 0 && int64 strOffset < rw.Length then
                        rw.ReadNullTerminatedStringAt(int64 strOffset, encoding)
                    else
                        "")
            finally
                rw.Seek(savedPos) |> ignore

    /// Extracts (key, text) pairs from an Old Engine (big-endian) ARMP stream.
    let private extractOldEngineArmp (rw: EndianReaderWrapper, encoding: Encoding) : (string * string) array =
        rw.Seek(0x10L) |> ignore
        let entryCount = rw.ReadInt32()
        let ptrValidityIshin = rw.ReadUInt32()
        let ptrEntryNames = rw.ReadUInt32()
        let ptrValidityOe = rw.ReadUInt32()
        let _columnCount = rw.ReadInt32()
        let _ptrColumnNames = rw.ReadUInt32()
        let _ptrColumnTypes = rw.ReadUInt32()
        let _ptrColumnContent = rw.ReadUInt32()
        let _reserved = rw.ReadUInt32()
        let _ptrColumnMetadata = rw.ReadUInt32()
        let ptrTextOffsetTable = rw.ReadUInt32()
        let textCount = rw.ReadInt32()

        let _ = (entryCount, ptrValidityIshin, ptrEntryNames, ptrValidityOe)

        if textCount > 0 && ptrTextOffsetTable > 0u && ptrTextOffsetTable < 0xFFFFFFFFu then
            let strings = readStringOffsetList (rw, textCount, int64 ptrTextOffsetTable, encoding)
            strings
            |> Array.mapi (fun idx s ->
                let cleaned = cleanText s
                if String.IsNullOrWhiteSpace(cleaned) then None
                else Some($"Main/#{idx}", cleaned))
            |> Array.choose id
        else
            [||]

    /// Extracts (key, text) pairs from a Dragon Engine (v1 or v2) ARMP table and its subtables.
    let private extractDragonEngineArmp (rw: EndianReaderWrapper, version: int16, mainTableOffset: int64, encoding: Encoding) : (string * string) array =
        let results = ResizeArray<string * string>()
        let visitedTables = HashSet<int64>()

        let rec readTable (tableOffset: int64) (tablePath: string) =
            if tableOffset > 0L && tableOffset + 80L <= rw.Length && visitedTables.Add(tableOffset) then
                let savedPos = rw.Position
                try
                    rw.Seek(tableOffset) |> ignore
                    let recordCount = rw.ReadInt32()
                    let fieldCount = rw.ReadInt32()
                    let valueStringCount = rw.ReadInt32()
                    let _recordInvalid = rw.ReadInt32()
                    let recordIdPointer = rw.ReadInt32()
                    let recordExistencePointer = rw.ReadInt32()
                    let fieldTypePointer = rw.ReadInt32()
                    let valuesPointer = rw.ReadInt32()
                    let idAndFlags = rw.ReadUInt32()
                    let flags = byte (idAndFlags >>> 24)
                    let valueStringPointer = rw.ReadInt32()
                    let fieldIdPointer = rw.ReadInt32()
                    let _fieldInvalid = rw.ReadInt32()
                    let _recordOrderPointer = rw.ReadInt32()
                    let _fieldOrderPointer = rw.ReadInt32()
                    let fieldExistencePointer = rw.ReadInt32()
                    let indexerPointer = rw.ReadInt32()
                    let _gameVarFieldTypePointer = rw.ReadInt32()
                    let _emptyValuesPointer = rw.ReadInt32()
                    let rawRecordMemberInfoPointer = rw.ReadInt32()
                    let _fieldInfoPointer = rw.ReadInt32()

                    let storageMode =
                        if version >= 2s then int (flags &&& 0x01uy)
                        else 0

                    let valueStrings =
                        if valueStringCount > 0 && valueStringPointer > 0 then
                            readStringOffsetList (rw, valueStringCount, int64 valueStringPointer, encoding)
                        else
                            [||]

                    let recordIds =
                        if recordCount > 0 && recordIdPointer > 0 then
                            readStringOffsetList (rw, recordCount, int64 recordIdPointer, encoding)
                        else
                            [||]

                    let fieldIds =
                        if fieldCount > 0 && fieldIdPointer > 0 then
                            readStringOffsetList (rw, fieldCount, int64 fieldIdPointer, encoding)
                        else
                            [||]

                    let recordExistence =
                        if recordExistencePointer > 0 && recordCount > 0 then
                            Some(readBitmask (rw, recordCount, int64 recordExistencePointer))
                        else
                            None

                    let fieldExistence =
                        if fieldExistencePointer > 0 && fieldCount > 0 then
                            Some(readBitmask (rw, fieldCount, int64 fieldExistencePointer))
                        else
                            None

                    let isRecordValid (r: int) =
                        match recordExistence with
                        | Some mask when r < mask.Length -> mask.[r]
                        | _ -> true

                    let isFieldValid (c: int) =
                        match fieldExistence with
                        | Some mask when c < mask.Length -> mask.[c]
                        | _ -> true

                    let getRowKey (r: int) =
                        if r < recordIds.Length && not (String.IsNullOrWhiteSpace(recordIds.[r])) then
                            recordIds.[r]
                        else
                            string r

                    let getColKey (c: int) =
                        if c < fieldIds.Length && not (String.IsNullOrWhiteSpace(fieldIds.[c])) then
                            fieldIds.[c]
                        else
                            string c

                    let fieldTypes =
                        if fieldCount > 0 && fieldTypePointer > 0 && int64 fieldTypePointer + int64 fieldCount <= rw.Length then
                            rw.Seek(int64 fieldTypePointer) |> ignore
                            rw.ReadBytes(fieldCount)
                        else
                            [||]

                    let mutable emittedFromColumns = 0

                    if recordCount > 0 && fieldCount > 0 && valuesPointer > 0 && int64 valuesPointer < rw.Length then
                        if version >= 2s && storageMode = 1 && rawRecordMemberInfoPointer > 0 && int64 rawRecordMemberInfoPointer + int64 (fieldCount * 16) <= rw.Length then
                            // Dragon Engine v2 Structured storage mode
                            rw.Seek(int64 rawRecordMemberInfoPointer) |> ignore
                            let memberInfos =
                                Array.init fieldCount (fun _ ->
                                    let mType = rw.ReadInt32()
                                    let mPos = rw.ReadInt32()
                                    let mArrSize = rw.ReadUInt32()
                                    let _reserved = rw.ReadInt32()
                                    { MemberType = mType; Position = mPos; ArraySize = mArrSize })

                            rw.Seek(int64 valuesPointer) |> ignore
                            let rowPointers =
                                if int64 valuesPointer + int64 (recordCount * 4) <= rw.Length then
                                    Array.init recordCount (fun _ -> rw.ReadInt32())
                                else
                                    [||]

                            for r = 0 to rowPointers.Length - 1 do
                                let rowPtr = rowPointers.[r]
                                if isRecordValid r && rowPtr > 0 then
                                    let rowKey = getRowKey r
                                    for c = 0 to fieldCount - 1 do
                                        let mInfo = memberInfos.[c]
                                        if isFieldValid c && mInfo.Position >= 0 then
                                            let cellOffset = int64 rowPtr + int64 mInfo.Position
                                            if mInfo.MemberType = 12 && cellOffset + 4L <= rw.Length then
                                                rw.Seek(cellOffset) |> ignore
                                                let strIdx = rw.ReadInt32()
                                                if strIdx >= 0 && strIdx < valueStrings.Length then
                                                    let cleaned = cleanText valueStrings.[strIdx]
                                                    if not (String.IsNullOrWhiteSpace(cleaned)) then
                                                        let colKey = getColKey c
                                                        results.Add(($"{tablePath}/{rowKey}/{colKey}", cleaned))
                                                        emittedFromColumns <- emittedFromColumns + 1
                                            elif mInfo.MemberType = 13 && cellOffset + 8L <= rw.Length then
                                                rw.Seek(cellOffset) |> ignore
                                                let subTablePtr = rw.ReadInt64()
                                                if subTablePtr > 0L then
                                                    let colKey = getColKey c
                                                    readTable subTablePtr $"{tablePath}/{rowKey}/{colKey}"
                        else
                            // Column-oriented storage mode (Dragon Engine v1 and v2)
                            let v1MemberTypes =
                                if version < 2s && rawRecordMemberInfoPointer > 0 && int64 rawRecordMemberInfoPointer + int64 fieldCount <= rw.Length then
                                    rw.Seek(int64 rawRecordMemberInfoPointer) |> ignore
                                    rw.ReadBytes(fieldCount)
                                else
                                    [||]

                            let v2MemberTypes =
                                if version >= 2s && rawRecordMemberInfoPointer > 0 && int64 rawRecordMemberInfoPointer + int64 (fieldCount * 16) <= rw.Length then
                                    rw.Seek(int64 rawRecordMemberInfoPointer) |> ignore
                                    Array.init fieldCount (fun _ ->
                                        let mType = rw.ReadInt32()
                                        let _pos = rw.ReadInt32()
                                        let _arr = rw.ReadUInt32()
                                        let _res = rw.ReadInt32()
                                        mType)
                                else
                                    [||]

                            let isStringColumn (c: int) =
                                if c < v1MemberTypes.Length then
                                    v1MemberTypes.[c] = 0x0Cuy
                                elif c < v2MemberTypes.Length then
                                    v2MemberTypes.[c] = 12
                                elif c < fieldTypes.Length then
                                    if version >= 2s then fieldTypes.[c] = 13uy
                                    else fieldTypes.[c] = 0x0Cuy
                                else
                                    false

                            let isTableColumn (c: int) =
                                if c < v1MemberTypes.Length then
                                    v1MemberTypes.[c] = 0x0Duy
                                elif c < v2MemberTypes.Length then
                                    v2MemberTypes.[c] = 13
                                elif c < fieldTypes.Length then
                                    fieldTypes.[c] = 9uy || fieldTypes.[c] = 0x0Duy
                                else
                                    false

                            if int64 valuesPointer + int64 (fieldCount * 4) <= rw.Length then
                                rw.Seek(int64 valuesPointer) |> ignore
                                let colDataOffsets = Array.init fieldCount (fun _ -> rw.ReadInt32())

                                for c = 0 to fieldCount - 1 do
                                    let dataOffset = colDataOffsets.[c]
                                    if isFieldValid c && dataOffset > 0 then
                                        let colKey = getColKey c
                                        if isStringColumn c && int64 dataOffset + int64 (recordCount * 4) <= rw.Length then
                                            rw.Seek(int64 dataOffset) |> ignore
                                            let strIndices = Array.init recordCount (fun _ -> rw.ReadInt32())
                                            for r = 0 to recordCount - 1 do
                                                if isRecordValid r then
                                                    let strIdx = strIndices.[r]
                                                    if strIdx >= 0 && strIdx < valueStrings.Length then
                                                        let cleaned = cleanText valueStrings.[strIdx]
                                                        if not (String.IsNullOrWhiteSpace(cleaned)) then
                                                            let rowKey = getRowKey r
                                                            results.Add(($"{tablePath}/{rowKey}/{colKey}", cleaned))
                                                            emittedFromColumns <- emittedFromColumns + 1
                                        elif isTableColumn c && int64 dataOffset + int64 (recordCount * 8) <= rw.Length then
                                            rw.Seek(int64 dataOffset) |> ignore
                                            let subTablePtrs = Array.init recordCount (fun _ -> rw.ReadInt64())
                                            for r = 0 to recordCount - 1 do
                                                if isRecordValid r && subTablePtrs.[r] > 0L then
                                                    let rowKey = getRowKey r
                                                    readTable subTablePtrs.[r] $"{tablePath}/{rowKey}/{colKey}"

                    // Fallback: if this table has ValueStrings that were not emitted via column cells, emit ValueStrings directly.
                    if emittedFromColumns = 0 && valueStrings.Length > 0 then
                        for i = 0 to valueStrings.Length - 1 do
                            let cleaned = cleanText valueStrings.[i]
                            if not (String.IsNullOrWhiteSpace(cleaned)) then
                                results.Add(($"{tablePath}/#{i}", cleaned))

                    if indexerPointer > 0 then
                        readTable (int64 indexerPointer) $"{tablePath}_Idx"
                finally
                    rw.Seek(savedPos) |> ignore

        readTable mainTableOffset "Main"
        results.ToArray()

    /// Parses an ARMP byte array (decompressing SLLZ if needed) and returns (key, text) pairs.
    let parse (rawBytes: byte array) : (string * string) array =
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        let data = Sllz.decompressIfNeeded rawBytes
        if not (isArmp data) then
            raise (FormatException("Input is not a valid ARMP file."))

        use ms = new MemoryStream(data, false)
        use br = new BinaryReader(ms, Encoding.UTF8, true)
        let _magic = br.ReadBytes(4)
        let endianMarker = br.ReadInt32() // 258 (0x00000102 LE) indicates Old Engine Big-Endian
        if endianMarker = 258 then
            let rw = BigEndianReaderWrapper(br)
            let _version = rw.ReadInt16()
            let _revision = rw.ReadInt16()
            let _fileSize = rw.ReadInt32()
            // Old Engine Ishin uses Shift-JIS if 0x1C is 0, else UTF-8
            rw.Seek(0x1CL) |> ignore
            let validityPtr = rw.ReadInt32()
            let encoding =
                if validityPtr = 0 then Encoding.GetEncoding(932)
                else Encoding.UTF8
            extractOldEngineArmp (rw, encoding)
        else
            ms.Seek(5L, SeekOrigin.Begin) |> ignore
            let endianByte = br.ReadByte()
            let rw =
                if endianByte = 1uy then BigEndianReaderWrapper(br)
                else LittleEndianReaderWrapper(br)
            rw.Seek(8L) |> ignore
            let _revision = rw.ReadInt16()
            let version = rw.ReadInt16()
            let _fileSize = rw.ReadInt32()
            let mainTableOffset = int64 (rw.ReadUInt32())
            let effectiveVersion = if version <= 0s then 1s else version
            extractDragonEngineArmp (rw, effectiveVersion, mainTableOffset, Encoding.UTF8)

    /// Builds a synthetic Dragon Engine v1 ARMP binary table from rows of (rowId, (columnName, stringValue) array).
    let buildDragonEngineArmp (columns: string array, rows: (string * string array) array) : byte array =
        let recordCount = rows.Length
        let fieldCount = columns.Length

        // Collect unique non-empty strings into ValueStrings
        let valueStrings = ResizeArray<string>()
        let stringIndexMap = Dictionary<string, int>(StringComparer.Ordinal)

        let cellStringIndices =
            Array.init fieldCount (fun c ->
                Array.init recordCount (fun r ->
                    let rowCols = snd rows.[r]
                    let cellVal = if c < rowCols.Length then rowCols.[c] else ""
                    if String.IsNullOrEmpty(cellVal) then
                        -1
                    else
                        match stringIndexMap.TryGetValue(cellVal) with
                        | true, idx -> idx
                        | false, _ ->
                            let idx = valueStrings.Count
                            valueStrings.Add(cellVal)
                            stringIndexMap.[cellVal] <- idx
                            idx))

        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, Encoding.UTF8, true)

        // 0x00..0x0F: FileHeader ("armp", LE, Version 1)
        bw.Write(Encoding.ASCII.GetBytes("armp"))
        bw.Write(0) // platform/endianness = 0 (LE)
        bw.Write(0s) // Revision = 0
        bw.Write(1s) // Version = 1
        bw.Write(0) // Size

        // 0x10..0x13: mainTableOffset = 0x20
        bw.Write(0x20)
        // 0x14..0x1F: padding
        bw.Write(0)
        bw.Write(0)
        bw.Write(0)

        // 0x20..0x6F: 80-byte ArmpTableHeader placeholder
        let tableHeaderOffset = 0x20L
        bw.Write(Array.zeroCreate<byte> 80)

        let writeStringTable (strs: seq<string>) : int =
            let arr = Seq.toArray strs
            if arr.Length = 0 then
                0
            else
                let tablePos = int ms.Position
                bw.Write(Array.zeroCreate<byte> (arr.Length * 4))
                let offsets =
                    arr
                    |> Array.map (fun s ->
                        let strPos = int ms.Position
                        let bytes = Encoding.UTF8.GetBytes(if isNull s then "" else s)
                        bw.Write(bytes)
                        bw.Write(0uy)
                        strPos)
                let endPos = ms.Position
                ms.Seek(int64 tablePos, SeekOrigin.Begin) |> ignore
                for off in offsets do
                    bw.Write(off)
                ms.Seek(endPos, SeekOrigin.Begin) |> ignore
                tablePos

        let recordIdPtr = writeStringTable (rows |> Array.map fst)
        let fieldIdPtr = writeStringTable columns
        let valueStringPtr = writeStringTable valueStrings

        let fieldTypePtr =
            if fieldCount = 0 then 0
            else
                let pos = int ms.Position
                for _ = 0 to fieldCount - 1 do
                    bw.Write(0uy) // IDv1 = 0 for String
                pos

        let rawMemberInfoPtr =
            if fieldCount = 0 then 0
            else
                let pos = int ms.Position
                for _ = 0 to fieldCount - 1 do
                    bw.Write(0x0Cuy) // FieldType.String = 0x0C
                pos

        let valuesPtr =
            if fieldCount = 0 then 0
            else
                let ptrTablePos = int ms.Position
                bw.Write(Array.zeroCreate<byte> (fieldCount * 4))
                let colOffsets =
                    Array.init fieldCount (fun c ->
                        let colPos = int ms.Position
                        for r = 0 to recordCount - 1 do
                            bw.Write(cellStringIndices.[c].[r])
                        colPos)
                let endPos = ms.Position
                ms.Seek(int64 ptrTablePos, SeekOrigin.Begin) |> ignore
                for off in colOffsets do
                    bw.Write(off)
                ms.Seek(endPos, SeekOrigin.Begin) |> ignore
                ptrTablePos

        // Fill in the 80-byte ArmpTableHeader at 0x20
        ms.Seek(tableHeaderOffset, SeekOrigin.Begin) |> ignore
        bw.Write(recordCount)
        bw.Write(fieldCount)
        bw.Write(valueStrings.Count)
        bw.Write(0) // RecordInvalid
        bw.Write(recordIdPtr)
        bw.Write(-1) // RecordExistencePointer (-1 = all valid)
        bw.Write(fieldTypePtr)
        bw.Write(valuesPtr)
        bw.Write(0u) // Id and Flags (StorageMode = 0 Column)
        bw.Write(valueStringPtr)
        bw.Write(fieldIdPtr)
        bw.Write(0) // FieldInvalid
        bw.Write(0) // RecordOrderPointer
        bw.Write(0) // FieldOrderPointer
        bw.Write(-1) // FieldExistencePointer (-1 = all valid)
        bw.Write(0) // IndexerPointer
        bw.Write(0) // GameVarFieldTypePointer
        bw.Write(0) // EmptyValuesPointer
        bw.Write(rawMemberInfoPtr)
        bw.Write(0) // FieldInfoPointer

        ms.ToArray()

// ============================================================================
// 20070319 Binary Table Format (Old Engine .bin / .bin_c / .bin_j)
// ============================================================================
module Bin20070319 =

    [<Literal>]
    let MagicNumber = 0x20070319

    /// Checks whether a byte array (after optional SLLZ decompression) has the 0x20070319 magic signature.
    let isBin20070319 (rawBytes: byte array) : bool =
        let data = Sllz.decompressIfNeeded rawBytes
        not (isNull data)
        && data.Length >= 16
        && data.[0] = 0x20uy
        && data.[1] = 0x07uy
        && data.[2] = 0x03uy
        && data.[3] = 0x19uy

    /// Column data types in the 20070319 format.
    type ColumnDataType =
        | String = 0
        | StringTbl = 1
        | StringIdx = 2
        | Value = 3
        | ValueTbl = 4
        | ValueIdx = 5
        | ScenarioCategory = 6
        | ScenarioStatus = 7
        | StageId = 8
        | ScenarioCompare = 9
        | SpecialValue = 10
        | ItemId = 11
        | Comment = 12
        | BgmId = 13
        | UseCounter = 15
        | EntityUid = 16

    type private ColumnHeader =
        {
            Name: string
            DataType: int
            AdditionalDataCount: int
            SectionSize: int
        }

    /// Parses a 20070319 binary table and returns (key, text) pairs keyed by "{rowIndex}/{columnName}".
    let parse (rawBytes: byte array, encoding: Encoding) : (string * string) array =
        let data = Sllz.decompressIfNeeded rawBytes
        if not (isBin20070319 data) then
            raise (FormatException("Input does not have the 0x20070319 magic header."))

        use ms = new MemoryStream(data, false)
        use br = new BinaryReader(ms, encoding, true)
        let rw = BigEndianReaderWrapper(br)

        let magic = rw.ReadInt32()
        if magic <> MagicNumber then
            raise (FormatException($"Invalid 20070319 magic 0x{magic:X8}."))

        let columnCount = rw.ReadInt32()
        let entryCount = rw.ReadInt32()
        let _padding = rw.ReadInt32()

        let columns =
            Array.init columnCount (fun i ->
                let colHeaderOffset = 0x10L + int64 (i * 0x40)
                rw.Seek(colHeaderOffset) |> ignore
                let colName = rw.ReadFixedString(0x30, Encoding.UTF8).TrimEnd('\000')
                rw.Seek(colHeaderOffset + 0x30L) |> ignore
                let dataType = rw.ReadInt32()
                let additionalCount = rw.ReadInt32()
                let sectionSize = rw.ReadInt32()
                let _unused = rw.ReadInt32()
                {
                    Name = colName
                    DataType = dataType
                    AdditionalDataCount = additionalCount
                    SectionSize = sectionSize
                })

        let mutable sectionStartOffset = 0x10L + int64 (columnCount * 0x40)
        let results = ResizeArray<string * string>()

        for col in columns do
            if sectionStartOffset <= rw.Length then
                rw.Seek(sectionStartOffset) |> ignore
                match col.DataType with
                | 0 | 3 ->
                    // String / Value: entryCount consecutive null-terminated strings
                    for r = 0 to entryCount - 1 do
                        if rw.Position < rw.Length then
                            let s = cleanText (rw.ReadNullTerminatedString(encoding))
                            if not (String.IsNullOrWhiteSpace(s)) then
                                results.Add(($"{r}/{col.Name}", s))
                | 1 | 4 ->
                    // String_tbl / Value_tbl: AdditionalDataCount strings followed by entryCount byte indices
                    let strTable =
                        Array.init (max 0 col.AdditionalDataCount) (fun _ ->
                            if rw.Position < rw.Length then rw.ReadNullTerminatedString(encoding)
                            else "")
                    for r = 0 to entryCount - 1 do
                        if rw.Position < rw.Length then
                            let idx = int (rw.ReadByte())
                            if idx >= 0 && idx < strTable.Length then
                                let s = cleanText strTable.[idx]
                                if not (String.IsNullOrWhiteSpace(s)) then
                                    results.Add(($"{r}/{col.Name}", s))
                | 2 | 5 ->
                    // String_idx / Value_idx: AdditionalDataCount pairs of (uint16 rowId, null-terminated string)
                    for _ = 0 to col.AdditionalDataCount - 1 do
                        if rw.Position + 2L <= rw.Length then
                            let rowId = int (rw.ReadUInt16())
                            let s = cleanText (rw.ReadNullTerminatedString(encoding))
                            if not (String.IsNullOrWhiteSpace(s)) then
                                results.Add(($"{rowId}/{col.Name}", s))
                | _ ->
                    // Non-translatable numeric / special column; skip using SectionSize
                    ()

                sectionStartOffset <- sectionStartOffset + int64 col.SectionSize

        results.ToArray()

    /// Builds a synthetic 20070319 table with String (type 0) columns for testing.
    let build (columns: string array, rows: string array array, encoding: Encoding) : byte array =
        let colCount = columns.Length
        let rowCount = rows.Length

        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, encoding, true)

        let writeInt32BE (v: int) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        // Header (16 bytes)
        writeInt32BE MagicNumber
        writeInt32BE colCount
        writeInt32BE rowCount
        writeInt32BE 0

        // Prepare each column's binary data section
        let colDataSections =
            Array.init colCount (fun c ->
                use colMs = new MemoryStream()
                for r = 0 to rowCount - 1 do
                    let row = rows.[r]
                    let v = if c < row.Length && not (isNull row.[c]) then row.[c] else ""
                    let bytes = encoding.GetBytes(v)
                    colMs.Write(bytes, 0, bytes.Length)
                    colMs.WriteByte(0uy)
                colMs.ToArray())

        // Column headers (64 bytes each)
        for c = 0 to colCount - 1 do
            let nameBuf = Array.zeroCreate<byte> 0x30
            let nameBytes = Encoding.UTF8.GetBytes(columns.[c])
            Array.Copy(nameBytes, 0, nameBuf, 0, min 0x2F nameBytes.Length)
            bw.Write(nameBuf)
            writeInt32BE 0 // DataType.String = 0
            writeInt32BE rowCount
            writeInt32BE colDataSections.[c].Length
            writeInt32BE 0

        // Column data sections
        for section in colDataSections do
            bw.Write(section)

        ms.ToArray()

// ============================================================================
// StringTbl Format (string_tbl.bin / string_tbl.bin_c / string_tbl.bin_j)
// ============================================================================
module StringTbl =

    /// Parses an Old Engine string_tbl.bin file and returns ("{section}/{index}", text) pairs.
    let parse (rawBytes: byte array, encoding: Encoding) : (string * string) array =
        let data = Sllz.decompressIfNeeded rawBytes
        if isNull data || data.Length < 8 then
            [||]
        else
            use ms = new MemoryStream(data, false)
            use br = new BinaryReader(ms, encoding, true)
            let rw = BigEndianReaderWrapper(br)

            let sectionCount = rw.ReadInt32()
            let _reserved = rw.ReadInt32()

            if sectionCount <= 0 || sectionCount > 0x10000 || 8L + int64 (sectionCount * 8) > rw.Length then
                [||]
            else
                let sections =
                    Array.init sectionCount (fun _ ->
                        let count = rw.ReadInt32()
                        let offset = rw.ReadInt32()
                        (count, offset))

                let results = ResizeArray<string * string>()
                for sIdx = 0 to sections.Length - 1 do
                    let (count, offset) = sections.[sIdx]
                    if count > 0 && offset > 0 && int64 offset + int64 (count * 4) <= rw.Length then
                        rw.Seek(int64 offset) |> ignore
                        let strOffsets = Array.init count (fun _ -> rw.ReadInt32())
                        for j = 0 to strOffsets.Length - 1 do
                            let strOff = strOffsets.[j]
                            if strOff > 0 && int64 strOff < rw.Length then
                                let s = cleanText (rw.ReadNullTerminatedStringAt(int64 strOff, encoding))
                                if not (String.IsNullOrWhiteSpace(s)) then
                                    results.Add(($"{sIdx}/{j}", s))
                results.ToArray()

    /// Builds a synthetic string_tbl.bin file from an array of string sections.
    let build (sections: string array array, encoding: Encoding) : byte array =
        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, encoding, true)

        let writeInt32BE (v: int) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        writeInt32BE sections.Length
        writeInt32BE 0

        let headerEnd = 8 + (sections.Length * 8)
        let totalPointers = sections |> Array.sumBy (fun s -> s.Length)
        let stringDataStart = headerEnd + (totalPointers * 4)

        // Write section descriptors
        let mutable currentPtrTableOffset = headerEnd
        for sec in sections do
            writeInt32BE sec.Length
            writeInt32BE currentPtrTableOffset
            currentPtrTableOffset <- currentPtrTableOffset + (sec.Length * 4)

        // Encode strings and record their offsets
        use strMs = new MemoryStream()
        for sec in sections do
            for s in sec do
                if String.IsNullOrEmpty(s) then
                    writeInt32BE 0
                else
                    let strOffset = stringDataStart + int strMs.Position
                    writeInt32BE strOffset
                    let bytes = encoding.GetBytes(s)
                    strMs.Write(bytes, 0, bytes.Length)
                    strMs.WriteByte(0uy)

        let strBytes = strMs.ToArray()
        bw.Write(strBytes)
        ms.ToArray()

// ============================================================================
// Msg Format (Old Engine .msg dialogue files inside wdr.par)
// ============================================================================
module Msg =

    /// Parses an Old Engine .msg dialogue script file and returns (key, text) pairs.
    let parse (rawBytes: byte array, encoding: Encoding) : (string * string) array =
        let data = Sllz.decompressIfNeeded rawBytes
        if isNull data || data.Length < 24 then
            [||]
        else
            use ms = new MemoryStream(data, false)
            use br = new BinaryReader(ms, encoding, true)
            let rw = BigEndianReaderWrapper(br)

            let _header0 = rw.ReadBytes(3)
            let groupCount = int (rw.ReadByte())
            let _header4 = rw.ReadInt32()
            let _pointer1 = rw.ReadInt32()
            let _countPointer1 = rw.ReadInt16()
            let numTalkers = int (rw.ReadInt16())
            let pointerTalkers = rw.ReadInt32()
            let _pointerRemainder = rw.ReadInt32()

            if 24L + int64 (groupCount * 16) > rw.Length then
                [||]
            else
                let groups =
                    Array.init groupCount (fun _ ->
                        let _u0 = rw.ReadInt32()
                        let groupOffset = rw.ReadInt32()
                        let _u1 = rw.ReadByte()
                        let stringCount = int (rw.ReadByte())
                        let _u2 = rw.ReadBytes(6)
                        (groupOffset, stringCount))

                let results = ResizeArray<string * string>()

                for gIdx = 0 to groups.Length - 1 do
                    let (groupOffset, stringCount) = groups.[gIdx]
                    if stringCount > 0 && groupOffset > 0 && int64 groupOffset + int64 (stringCount * 12) <= rw.Length then
                        rw.Seek(int64 groupOffset) |> ignore
                        let strOffsets =
                            Array.init stringCount (fun _ ->
                                let _strLength = rw.ReadInt16()
                                let _propCount = rw.ReadByte()
                                let _zero = rw.ReadByte()
                                let strOffset = rw.ReadInt32()
                                let _propOffset = rw.ReadInt32()
                                strOffset)
                        for sIdx = 0 to strOffsets.Length - 1 do
                            let strOff = strOffsets.[sIdx]
                            if strOff > 0 && int64 strOff < rw.Length then
                                let text = cleanText (rw.ReadNullTerminatedStringAt(int64 strOff, encoding))
                                if not (String.IsNullOrWhiteSpace(text)) then
                                    results.Add(($"line/{gIdx}/{sIdx}", text))

                if results.Count > 0 && numTalkers > 0 && pointerTalkers > 0 && int64 pointerTalkers + int64 (numTalkers * 4) <= rw.Length then
                    rw.Seek(int64 pointerTalkers) |> ignore
                    let talkerOffsets = Array.init numTalkers (fun _ -> rw.ReadInt32())
                    for tIdx = 0 to talkerOffsets.Length - 1 do
                        let tOff = talkerOffsets.[tIdx]
                        if tOff > 0 && int64 tOff < rw.Length then
                            let talkerName = cleanText (rw.ReadNullTerminatedStringAt(int64 tOff, encoding))
                            if not (String.IsNullOrWhiteSpace(talkerName)) then
                                results.Add(($"talker/{tIdx}", talkerName))

                results.ToArray()

    /// Builds a synthetic .msg binary file from dialogue groups and talker names.
    let build (groups: string array array, talkers: string array, encoding: Encoding) : byte array =
        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, encoding, true)

        let writeInt16BE (v: int16) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        let writeInt32BE (v: int) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        let groupCount = min 255 groups.Length
        let groupTableStart = 24
        let subTablesStart = groupTableStart + (groupCount * 16)
        let totalGroupLines =
            groups
            |> Array.take groupCount
            |> Array.sumBy (fun g -> min 255 g.Length)
        let talkersTableStart = subTablesStart + (totalGroupLines * 12)
        let stringsStart = talkersTableStart + (talkers.Length * 4)

        // 24-byte header
        bw.Write([| 0uy; 0uy; 0uy |])
        bw.Write(byte groupCount)
        writeInt32BE 0
        writeInt32BE subTablesStart
        writeInt16BE 0s
        writeInt16BE (int16 talkers.Length)
        writeInt32BE (if talkers.Length > 0 then talkersTableStart else 0)
        writeInt32BE stringsStart

        // Group table (16 bytes per group)
        let mutable curSubTableOffset = subTablesStart
        for gIdx = 0 to groupCount - 1 do
            let lineCount = min 255 groups.[gIdx].Length
            writeInt32BE 0
            writeInt32BE curSubTableOffset
            bw.Write(0uy)
            bw.Write(byte lineCount)
            bw.Write(Array.zeroCreate<byte> 6)
            curSubTableOffset <- curSubTableOffset + (lineCount * 12)

        // Sub-tables (12 bytes per line) & string pool
        use strMs = new MemoryStream()
        let allocString (s: string) =
            let off = stringsStart + int strMs.Position
            let bytes = encoding.GetBytes(if isNull s then "" else s)
            strMs.Write(bytes, 0, bytes.Length)
            strMs.WriteByte(0uy)
            (off, bytes.Length)

        for gIdx = 0 to groupCount - 1 do
            let lines = groups.[gIdx]
            let lineCount = min 255 lines.Length
            for sIdx = 0 to lineCount - 1 do
                let (strOff, byteLen) = allocString lines.[sIdx]
                writeInt16BE (int16 byteLen)
                bw.Write(0uy)
                bw.Write(0uy)
                writeInt32BE strOff
                writeInt32BE 0

        for t in talkers do
            let (tOff, _) = allocString t
            writeInt32BE tOff

        bw.Write(strMs.ToArray())
        ms.ToArray()

// ============================================================================
// CmnBin Format (Cutscene subtitles in cmn.bin inside auth/hact .par archives)
// ============================================================================
module CmnBin =

    /// Shift-JIS bytes for "字幕" (subtitles), used as block marker inside cmn.bin files.
    let SubtitleMarker = [| 0x8Euy; 0x9Auy; 0x96uy; 0x8Buy |]

    let private findPattern (data: byte array, pattern: byte array, startIndex: int) : int =
        if isNull data || isNull pattern || pattern.Length = 0 || startIndex < 0 || startIndex + pattern.Length > data.Length then
            -1
        else
            let lastStart = data.Length - pattern.Length
            let mutable pos = startIndex
            let mutable found = -1
            while found = -1 && pos <= lastStart do
                let mutable matched = true
                let mutable j = 0
                while matched && j < pattern.Length do
                    if data.[pos + j] <> pattern.[j] then
                        matched <- false
                    else
                        j <- j + 1
                if matched then
                    found <- pos
                else
                    pos <- pos + 1
            found

    /// Parses a cmn.bin cutscene subtitle file and returns ("sub/{blockIdx}/{subIdx}", text) pairs.
    /// In localized cmn.bin files, each subtitle block stores a Japanese subtitle array followed by an
    /// English/localized subtitle array. When preferLocalizedSlot is true and the localized array is non-empty,
    /// strings are taken from the second slot; otherwise from the first slot.
    let parse (rawBytes: byte array, encoding: Encoding, preferLocalizedSlot: bool) : (string * string) array =
        let data = Sllz.decompressIfNeeded rawBytes
        if isNull data || data.Length < 32 then
            [||]
        else
            use ms = new MemoryStream(data, false)
            use br = new BinaryReader(ms, encoding, true)
            let rw = BigEndianReaderWrapper(br)

            let results = ResizeArray<string * string>()
            let mutable searchPos = 0
            let mutable blockIdx = 0

            let mutable markerPos = findPattern (data, SubtitleMarker, searchPos)
            while markerPos >= 0 do
                let afterMarker = int64 (markerPos + SubtitleMarker.Length)
                if afterMarker + 20L <= rw.Length then
                    rw.Seek(afterMarker + 12L) |> ignore
                    let blockType = rw.ReadUInt64()

                    if blockType = 0UL then
                        // Long subtitles (256-byte text fields)
                        if rw.Position + 64L <= rw.Length then
                            rw.Seek(rw.Position + 40L) |> ignore
                            let numJpnSubs = rw.ReadInt32()
                            let numEngSubs = rw.ReadInt32()
                            if numJpnSubs >= 0 && numJpnSubs <= 0x1000 && numEngSubs >= 0 && numEngSubs <= 0x1000 then
                                rw.Seek(rw.Position + 16L) |> ignore
                                let jpnNeeded = int64 numJpnSubs * (16L + 256L)
                                let engNeeded = int64 numEngSubs * (16L + 256L)
                                if rw.Position + jpnNeeded + engNeeded <= rw.Length then
                                    let jpnSubs =
                                        Array.init numJpnSubs (fun _ ->
                                            rw.Seek(rw.Position + 16L) |> ignore
                                            cleanText (rw.ReadFixedString(256, encoding)))
                                    let engSubs =
                                        Array.init numEngSubs (fun _ ->
                                            rw.Seek(rw.Position + 16L) |> ignore
                                            cleanText (rw.ReadFixedString(256, encoding)))

                                    let chosen =
                                        if preferLocalizedSlot && engSubs |> Array.exists (not << String.IsNullOrWhiteSpace) then
                                            engSubs
                                        elif jpnSubs |> Array.exists (not << String.IsNullOrWhiteSpace) then
                                            jpnSubs
                                        else
                                            engSubs

                                    for sIdx = 0 to chosen.Length - 1 do
                                        if not (String.IsNullOrWhiteSpace(chosen.[sIdx])) then
                                            results.Add(($"sub/{blockIdx}/{sIdx}", chosen.[sIdx]))
                                    blockIdx <- blockIdx + 1
                    else
                        // Short subtitles (128-byte text fields)
                        if rw.Position + 298L <= rw.Length then
                            rw.Seek(rw.Position + 266L) |> ignore
                            let numJpnSubs = rw.ReadInt32()
                            rw.Seek(rw.Position + 28L) |> ignore
                            if numJpnSubs >= 0 && numJpnSubs <= 0x1000 && rw.Position + int64 numJpnSubs * (16L + 128L) <= rw.Length then
                                let jpnSubs =
                                    Array.init numJpnSubs (fun _ ->
                                        rw.Seek(rw.Position + 16L) |> ignore
                                        cleanText (rw.ReadFixedString(128, encoding)))

                                let engSubs =
                                    if rw.Position + 32L <= rw.Length then
                                        let numEngSubs = rw.ReadInt32()
                                        rw.Seek(rw.Position + 28L) |> ignore
                                        if numEngSubs >= 0 && numEngSubs <= 0x1000 && rw.Position + int64 numEngSubs * (16L + 128L) <= rw.Length then
                                            Array.init numEngSubs (fun _ ->
                                                rw.Seek(rw.Position + 16L) |> ignore
                                                cleanText (rw.ReadFixedString(128, encoding)))
                                        else
                                            [||]
                                    else
                                        [||]

                                let chosen =
                                    if preferLocalizedSlot && engSubs |> Array.exists (not << String.IsNullOrWhiteSpace) then
                                        engSubs
                                    elif jpnSubs |> Array.exists (not << String.IsNullOrWhiteSpace) then
                                        jpnSubs
                                    else
                                        engSubs

                                for sIdx = 0 to chosen.Length - 1 do
                                    if not (String.IsNullOrWhiteSpace(chosen.[sIdx])) then
                                        results.Add(($"sub/{blockIdx}/{sIdx}", chosen.[sIdx]))
                                blockIdx <- blockIdx + 1

                searchPos <- int (max (int64 (markerPos + 4)) rw.Position)
                markerPos <- findPattern (data, SubtitleMarker, searchPos)

            results.ToArray()

    /// Builds a synthetic cmn.bin byte array containing one long-subtitle block with Japanese and English arrays.
    let build (japaneseSubtitles: string array, englishSubtitles: string array, encoding: Encoding) : byte array =
        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, encoding, true)

        let writeInt32BE (v: int) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        bw.Write(SubtitleMarker)
        bw.Write(Array.zeroCreate<byte> 12)
        bw.Write(Array.zeroCreate<byte> 8) // blockType = 0UL (long subtitles)
        bw.Write(Array.zeroCreate<byte> 40)
        writeInt32BE japaneseSubtitles.Length
        writeInt32BE englishSubtitles.Length
        bw.Write(Array.zeroCreate<byte> 16)

        let writeSubArray (subs: string array) =
            for s in subs do
                bw.Write(Array.zeroCreate<byte> 16) // 16-byte timing info
                let strBuf = Array.zeroCreate<byte> 256
                let bytes = encoding.GetBytes(if isNull s then "" else s)
                Array.Copy(bytes, 0, strBuf, 0, min 255 bytes.Length)
                bw.Write(strBuf)

        writeSubArray japaneseSubtitles
        writeSubArray englishSubtitles
        ms.ToArray()

// ============================================================================
// Epmb, Mail, and OffsetTable Binary Text Formats
// ============================================================================
module Epmb =

    /// Parses an encounter_pupup_message.bin* file (16-byte header, BE count, followed by (int32 id, 64-byte string) entries).
    let parse (rawBytes: byte array, encoding: Encoding) : (string * string) array =
        let data = Sllz.decompressIfNeeded rawBytes
        if isNull data || data.Length < 20 then
            [||]
        else
            use ms = new MemoryStream(data, false)
            use br = new BinaryReader(ms, encoding, true)
            let rw = BigEndianReaderWrapper(br)

            rw.Seek(16L) |> ignore
            let count = rw.ReadInt32()
            if count <= 0 || count > 0x10000 || 20L + int64 count * 68L > rw.Length then
                [||]
            else
                let results = ResizeArray<string * string>(count)
                for i = 0 to count - 1 do
                    let _id = rw.ReadInt32()
                    let s = cleanText (rw.ReadFixedString(64, encoding))
                    if not (String.IsNullOrWhiteSpace(s)) then
                        results.Add(($"{i}", s))
                results.ToArray()

    /// Builds a synthetic encounter_pupup_message.bin file.
    let build (messages: string array, encoding: Encoding) : byte array =
        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, encoding, true)

        let writeInt32BE (v: int) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        bw.Write(Encoding.ASCII.GetBytes("EPMB"))
        bw.Write(Array.zeroCreate<byte> 12)
        writeInt32BE messages.Length

        for i = 0 to messages.Length - 1 do
            writeInt32BE i
            let buf = Array.zeroCreate<byte> 64
            let bytes = encoding.GetBytes(if isNull messages.[i] then "" else messages.[i])
            Array.Copy(bytes, 0, buf, 0, min 63 bytes.Length)
            bw.Write(buf)

        ms.ToArray()

module OffsetTableBin =

    /// Parses a binary file consisting of a big-endian offset table starting at startOffset (such as dispose_string.bin).
    let parse (rawBytes: byte array, startOffset: int64, encoding: Encoding) : (string * string) array =
        let data = Sllz.decompressIfNeeded rawBytes
        if isNull data || int64 data.Length < startOffset + 4L then
            [||]
        else
            use ms = new MemoryStream(data, false)
            use br = new BinaryReader(ms, encoding, true)
            let rw = BigEndianReaderWrapper(br)

            rw.Seek(startOffset) |> ignore
            let firstOffset = int64 (rw.ReadInt32())
            if firstOffset <= startOffset || firstOffset > rw.Length || (firstOffset - startOffset) % 4L <> 0L then
                [||]
            else
                rw.Seek(startOffset) |> ignore
                let count = int ((firstOffset - startOffset) / 4L)
                let offsets = Array.init count (fun _ -> rw.ReadInt32())
                let results = ResizeArray<string * string>(count)
                for i = 0 to offsets.Length - 1 do
                    let off = offsets.[i]
                    if off > 0 && int64 off < rw.Length then
                        let s = cleanText (rw.ReadNullTerminatedStringAt(int64 off, encoding))
                        if not (String.IsNullOrWhiteSpace(s)) then
                            results.Add(($"{i}", s))
                results.ToArray()

    /// Builds a synthetic big-endian offset table binary file (such as dispose_string.bin).
    let build (strings: string array, startOffset: int64, encoding: Encoding) : byte array =
        use ms = new MemoryStream()
        use bw = new BinaryWriter(ms, encoding, true)

        let writeInt32BE (v: int) =
            let b = BitConverter.GetBytes(v)
            if BitConverter.IsLittleEndian then Array.Reverse(b)
            bw.Write(b)

        if startOffset > 0L then
            bw.Write(Array.zeroCreate<byte> (int startOffset))

        let stringDataStart = int startOffset + (strings.Length * 4)
        use strMs = new MemoryStream()
        for s in strings do
            let off = stringDataStart + int strMs.Position
            writeInt32BE off
            let bytes = encoding.GetBytes(if isNull s then "" else s)
            strMs.Write(bytes, 0, bytes.Length)
            strMs.WriteByte(0uy)

        bw.Write(strMs.ToArray())
        ms.ToArray()

