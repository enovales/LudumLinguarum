module RGGStudioTests

open Expecto
open LLDatabase
open RGGStudio.ArmpBinFile
open RGGStudio.DragonEngineGames
open RGGStudio.LocalizedBinFile
open RGGStudio.OldEngineGames
open RGGStudio.ParArchive
open System
open System.IO
open System.IO.Compression
open System.Text

// Synthetic, representative content. Must not contain actual game text (see
// AGENTS.md); it mirrors the shape of the real files.

let private beBytes(value: int): byte array =
    [| byte((value >>> 24) &&& 0xFF)
       byte((value >>> 16) &&& 0xFF)
       byte((value >>> 8) &&& 0xFF)
       byte(value &&& 0xFF) |]

let private beShortBytes(value: int): byte array =
    [| byte((value >>> 8) &&& 0xFF); byte(value &&& 0xFF) |]

let private paddedNameBytes(name: string)(length: int): byte array =
    let bytes = Array.zeroCreate<byte> length
    let nameBytes = Encoding.ASCII.GetBytes(name)
    Array.Copy(nameBytes, bytes, min nameBytes.Length length)
    bytes

/// Encodes data as an SLLZ v1 stream using only literal tokens (flag byte 0x00
/// followed by up to eight literal bytes, repeated).
let private buildSllzV1Literal(data: byte array): byte array =
    // In SLLZ v1 streams, a flag byte precedes the first token, and a new
    // flag byte is inserted before every eighth token's payload thereafter,
    // matching the reference encoder/decoder pair.
    let payload = ResizeArray<byte>()
    payload.Add(0uy)
    let mutable bitCount = 0
    for b in data do
        bitCount <- bitCount + 1
        if bitCount = 8 then
            payload.Add(0uy)
            bitCount <- 0
        payload.Add(b)
    let payloadBytes = payload.ToArray()
    let header =
        Array.concat
            [|
                [| 0x53uy; 0x4Cuy; 0x4Cuy; 0x5Auy; 1uy; 1uy |]
                beShortBytes 16
                beBytes data.Length
                beBytes (16 + payloadBytes.Length)
            |]
    Array.concat [| header; payloadBytes |]

let private zlibCompress(data: byte array): byte array =
    use output = new MemoryStream()
    use zlibStream = new ZLibStream(output, CompressionLevel.Optimal)
    zlibStream.Write(data, 0, data.Length)
    zlibStream.Flush()
    output.ToArray()

/// Encodes data as an SLLZ v2 stream of zlib-compressed chunks.
let private buildSllzV2(data: byte array)(chunkSize: int): byte array =
    let chunks = [|
        for start in 0 .. chunkSize .. max (data.Length - 1) 0 ->
            let length = min chunkSize (data.Length - start)
            data.[start..start + length - 1]
    |]
    let payload =
        chunks
        |> Array.collect (fun chunk ->
            let compressed = zlibCompress chunk
            Array.concat
                [|
                    [| byte(((5 + compressed.Length) >>> 16) &&& 0xFF)
                       byte(((5 + compressed.Length) >>> 8) &&& 0xFF)
                       byte((5 + compressed.Length) &&& 0xFF) |]
                    beShortBytes (chunk.Length - 1)
                    compressed
                |])
    let header =
        Array.concat
            [|
                [| 0x53uy; 0x4Cuy; 0x4Cuy; 0x5Auy; 1uy; 2uy |]
                beShortBytes 16
                beBytes data.Length
                beBytes (16 + payload.Length)
            |]
    Array.concat [| header; payload |]

type private TestFile =
    {
        path: string
        data: byte array
        compress: bool
    }

/// Builds a PAR (PARC) archive. Folders are emitted in breadth-first order so
/// that every folder's children occupy a contiguous index range, matching the
/// range-based tree walk the reader performs.
let private buildParc(files: TestFile array): byte array =
    let dirOf(path: string) =
        let segments = path.Split('/')
        if segments.Length = 1 then "" else String.Join("/", segments.[0..segments.Length - 2])
    let parentOf(folder: string) =
        let segments = folder.Split('/')
        if segments.Length = 1 then "" else String.Join("/", segments.[0..segments.Length - 2])
    let depthOf(folder: string) = folder.Split('/').Length
    let lastSegmentOf(folder: string) = folder.Split('/') |> Array.last

    let distinctFolders =
        files
        |> Array.map (fun f -> dirOf f.path)
        |> Array.filter (fun d -> d <> "")
        |> Array.collect (fun dir ->
            let segments = dir.Split('/')
            [| for i in 1 .. segments.Length -> String.Join("/", segments.[0..i - 1]) |])
        |> Array.distinct
        |> Array.sortBy (fun d -> (depthOf d, d))
    let totalFolderCount = 1 + distinctFolders.Length
    let folderIndexOf(folder: string) = 1 + (distinctFolders |> Array.findIndex (fun d -> d = folder))

    let sortedFiles =
        files
        |> Array.sortBy (fun f ->
            let dir = dirOf f.path
            (depthOf(if dir = "" then "" else dir), dir, Path.GetFileName(f.path)))

    let folderNamesBlock =
        Array.concat
            [|
                yield paddedNameBytes "" 0x40
                for folder in distinctFolders -> paddedNameBytes (lastSegmentOf folder) 0x40
            |]
    let fileNamesBlock =
        Array.concat [| for f in sortedFiles -> paddedNameBytes (Path.GetFileName(f.path)) 0x40 |]

    let folderInfoOffset = 32 + folderNamesBlock.Length + fileNamesBlock.Length
    let fileInfoOffset = folderInfoOffset + 32 * totalFolderCount
    let dataOffset = fileInfoOffset + 32 * sortedFiles.Length

    let fileDataBlocks =
        sortedFiles
        |> Array.map (fun f -> if f.compress then buildSllzV1Literal f.data else f.data)
    let dataSize = fileDataBlocks |> Array.sumBy (fun b -> b.Length)

    let header =
        Array.concat
            [|
                Encoding.ASCII.GetBytes("PARC")
                [| 1uy; 1uy; 0uy; 0uy |]
                beBytes 2
                beBytes dataSize
                beBytes totalFolderCount
                beBytes folderInfoOffset
                beBytes sortedFiles.Length
                beBytes fileInfoOffset
            |]

    let folderInfo =
        [|
            let allFolders = Array.append [| "" |] distinctFolders
            for folder in allFolders ->
                let childFolders = distinctFolders |> Array.filter (fun d -> parentOf d = folder)
                let childCount = childFolders.Length
                let firstChildIndex = if childCount = 0 then 0 else folderIndexOf childFolders.[0]
                let ownFiles = sortedFiles |> Array.filter (fun f -> dirOf f.path = folder)
                let fileCount = ownFiles.Length
                let firstFileIndex =
                    if fileCount = 0 then 0
                    else sortedFiles |> Array.findIndex (fun f -> dirOf f.path = folder)
                Array.concat
                    [|
                        beBytes childCount
                        beBytes firstChildIndex
                        beBytes fileCount
                        beBytes firstFileIndex
                        beBytes 0; beBytes 0; beBytes 0; beBytes 0
                    |]
        |]
        |> Array.concat

    let fileInfo =
        [|
            let mutable offset = dataOffset
            for (file, block) in Array.zip sortedFiles fileDataBlocks do
                yield
                    Array.concat
                        [|
                            (if file.compress then beBytes 0x80000000 else beBytes 0)
                            beBytes file.data.Length
                            beBytes block.Length
                            beBytes offset
                            beBytes 0
                            beBytes 0
                            Array.zeroCreate<byte> 8
                        |]
                offset <- offset + block.Length
        |]
        |> Array.concat

    Array.concat
        [|
            header
            folderNamesBlock
            fileNamesBlock
            folderInfo
            fileInfo
        |]
    |> Array.append <| Array.concat fileDataBlocks

type private BinParameterSpec =
    {
        name: string
        parameterType: int
        count: int
        block: byte array
    }

/// Builds the data block of a string parameter (type 0): one null-terminated
/// string per entry.
let private binStringBlock(values: string array): byte array =
    values
    |> Array.collect (fun v -> Array.append (Encoding.UTF8.GetBytes v) [| 0uy |])

/// Builds the data block of a string table parameter (type 1): distinct
/// null-terminated strings followed by a byte-per-entry index table.
let private binStringTableBlock(distinctValues: string array)(table: byte array): byte array =
    Array.concat
        [|
            binStringBlock distinctValues
            table
        |]

/// Builds the data block of a string index parameter (type 2):
/// (int16BE entry id, null-terminated string) pairs.
let private binStringIndexBlock(entries: (int * string) array): byte array =
    entries
    |> Array.collect (fun (id, value) ->
        Array.concat
            [|
                beShortBytes id
                Encoding.UTF8.GetBytes value
                [| 0uy |]
            |])

let private buildBinFile(entryAmount: int)(parameters: BinParameterSpec array): byte array =
    let header =
        Array.concat
            [|
                binMagic
                beBytes parameters.Length
                beBytes entryAmount
                Array.zeroCreate<byte> 4
            |]
    let parameterHeaders =
        parameters
        |> Array.collect (fun p ->
            Array.concat
                [|
                    paddedNameBytes p.name 48
                    beBytes p.parameterType
                    beBytes p.count
                    beBytes p.block.Length
                    Array.zeroCreate<byte> 4
                |])
    Array.concat
        [|
            header
            parameterHeaders
            [| for p in parameters do yield! p.block |]
        |]

let private withTempGameDir(files: (string * byte array) array)(f: string -> unit): unit =
    let dir = Path.Combine(Path.GetTempPath(), "rggstudio-tests-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(dir) |> ignore
    try
        files
        |> Array.iter (fun (name, contents) ->
            let fullPath = Path.Combine(dir, name)
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)) |> ignore
            File.WriteAllBytes(fullPath, contents))
        f(dir)
    finally
        Directory.Delete(dir, true)

[<Tests>]
let sllzTests =
  testList "RGGStudio SLLZ tests" [
    testCase "v1 literal-only data decompresses" <|
      fun () ->
        use stream = new MemoryStream(buildSllzV1Literal(Encoding.UTF8.GetBytes("hello world")))
        let decompressed = decompressSllz stream
        Expect.equal "hello world" (Encoding.UTF8.GetString decompressed) ""

    testCase "v1 copy tokens are expanded" <|
      fun () ->
        // flag 0x10: three literals, then one copy token (distance 3, count 5)
        let payload = [| 0x10uy; 97uy; 98uy; 99uy; 0x22uy; 0x00uy |]
        let header =
            Array.concat
                [|
                    [| 0x53uy; 0x4Cuy; 0x4Cuy; 0x5Auy; 1uy; 1uy |]
                    beShortBytes 16
                    beBytes 8
                    beBytes (16 + payload.Length)
                |]
        use stream = new MemoryStream(Array.concat [| header; payload |])
        let decompressed = decompressSllz stream
        Expect.equal "abcabcab" (Encoding.UTF8.GetString decompressed) ""

    testCase "v2 zlib chunks decompress" <|
      fun () ->
        let data = Encoding.UTF8.GetBytes(String.replicate 200 "abcdefgh")
        use stream = new MemoryStream(buildSllzV2 data 64)
        let decompressed = decompressSllz stream
        Expect.equal data decompressed ""

    testCase "truncated data raises" <|
      fun () ->
        use stream = new MemoryStream(Encoding.ASCII.GetBytes("SLLZ"))
        Expect.throws (fun () -> decompressSllz stream |> ignore) ""

    testProperty "v1 literal streams round-trip any payload" <|
      fun (data: byte array) ->
        let nonEmpty = if data.Length = 0 then [| 1uy |] else data
        use stream = new MemoryStream(buildSllzV1Literal nonEmpty)
        decompressSllz stream = nonEmpty

    testProperty "v2 streams round-trip any payload" <|
      fun (data: byte array) ->
        let nonEmpty = if data.Length = 0 then [| 1uy |] else data
        use stream = new MemoryStream(buildSllzV2 nonEmpty 32)
        decompressSllz stream = nonEmpty
  ]

[<Tests>]
let parArchiveTests =
  testList "RGGStudio ParArchive tests" [
    testCase "Uncompressed entries are read back with full paths" <|
      fun () ->
        let files =
            [|
                { path = "w64/text/sample_text.bin_c"; data = Encoding.UTF8.GetBytes("first"); compress = false }
                { path = "plain.dat"; data = Encoding.UTF8.GetBytes("second"); compress = false }
            |]
        use stream = new MemoryStream(buildParc files)
        let entries = readParArchive stream
        Expect.equal 2 entries.Length ""
        Expect.equal ("w64/text/sample_text.bin_c", "first") (entries.[0].path, Encoding.UTF8.GetString entries.[0].data) ""
        Expect.equal ("plain.dat", "second") (entries.[1].path, Encoding.UTF8.GetString entries.[1].data) ""

    testCase "SLLZ-compressed entries are decompressed" <|
      fun () ->
        let files =
            [|
                { path = "compressed.bin_j"; data = Encoding.UTF8.GetBytes("some longer text that compresses"); compress = true }
            |]
        use stream = new MemoryStream(buildParc files)
        let entries = readParArchive stream
        Expect.equal "some longer text that compresses" (Encoding.UTF8.GetString entries.[0].data) ""

    testCase "A whole-archive SLLZ wrapper is transparent" <|
      fun () ->
        let files =
            [|
                { path = "a.bin_c"; data = Encoding.UTF8.GetBytes("x"); compress = false }
            |]
        let wrapped = buildSllzV1Literal(buildParc files)
        use stream = new MemoryStream(wrapped)
        let entries = readParArchive stream
        Expect.equal 1 entries.Length ""
        Expect.equal "x" (Encoding.UTF8.GetString entries.[0].data) ""

    testCase "Non-PARC data raises" <|
      fun () ->
        use stream = new MemoryStream(Encoding.ASCII.GetBytes("NOPE"))
        Expect.throws (fun () -> readParArchive stream |> ignore) ""

    testProperty "Entries round-trip through a built archive" <|
      fun (data: byte array) ->
        let files =
            [|
                { path = "data.bin"; data = data; compress = false }
            |]
        use stream = new MemoryStream(buildParc files)
        let entries = readParArchive stream
        entries.Length = 1 && entries.[0].data = data
  ]

[<Tests>]
let localizedBinFileTests =
  testList "RGGStudio LocalizedBinFile tests" [
    testCase "String parameters are parsed per entry" <|
      fun () ->
        let binFile =
            buildBinFile 3
                [|
                    { name = "body"; parameterType = 0; count = 0; block = binStringBlock [| "alpha"; "beta"; "gamma" |] }
                |]
            |> parseLocalizedBinFile
        Expect.equal 1 binFile.parameters.Length ""
        let parameter = binFile.parameters.[0]
        Expect.equal "body" parameter.name ""
        Expect.equal [| "alpha"; "beta"; "gamma" |] parameter.values ""

    testCase "String table parameters are resolved through the index table" <|
      fun () ->
        let binFile =
            buildBinFile 4
                [|
                    { name = "label"; parameterType = 1; count = 2; block = binStringTableBlock [| "first"; "second" |] [| 0uy; 1uy; 0uy; 1uy |] }
                |]
            |> parseLocalizedBinFile
        Expect.equal [| "first"; "second"; "first"; "second" |] binFile.parameters.[0].values ""

    testCase "String index parameters are applied to their entries" <|
      fun () ->
        let binFile =
            buildBinFile 3
                [|
                    { name = "note"; parameterType = 2; count = 2; block = binStringIndexBlock [| (2, "third"); (0, "zeroth") |] }
                |]
            |> parseLocalizedBinFile
        Expect.equal [| "zeroth"; ""; "third" |] binFile.parameters.[0].values ""

    testCase "Non-string parameters are skipped" <|
      fun () ->
        let binFile =
            buildBinFile 2
                [|
                    { name = "body"; parameterType = 0; count = 0; block = binStringBlock [| "alpha"; "beta" |] }
                    { name = "itemid"; parameterType = 11; count = 0; block = Array.concat [| beBytes 1; beBytes 2 |] }
                |]
            |> parseLocalizedBinFile
        Expect.equal 1 binFile.parameters.Length ""
        Expect.equal "body" binFile.parameters.[0].name ""

    testCase "Bad magic raises" <|
      fun () ->
        Expect.throws (fun () -> parseLocalizedBinFile(Array.zeroCreate<byte> 64) |> ignore) ""

    testCase "CP932 data falls back from UTF-8" <|
      fun () ->
        let cp932 = Encoding.GetEncoding(932)
        let binFile =
            buildBinFile 1
                [|
                    { name = "body"; parameterType = 0; count = 0; block = Array.append (cp932.GetBytes "こんにちは") [| 0uy |] }
                |]
            |> parseLocalizedBinFile
        Expect.equal "こんにちは" binFile.parameters.[0].values.[0] ""
  ]

[<Tests>]
let oldEngineGamesTests =
  testList "RGGStudio OldEngineGames tests" [
    testCase "Localized bin entries become language-tagged cards" <|
      fun () ->
        let enBin =
            buildBinFile 2
                [|
                    { name = "body"; parameterType = 0; count = 0; block = binStringBlock [| "A calm evening."; "A busy street." |] }
                |]
        let jaBin =
            buildBinFile 2
                [|
                    { name = "body"; parameterType = 0; count = 0; block = binStringBlock [| "穏やかな夕方。"; "賑やかな通り。" |] }
                |]
        let bootPar =
            buildParc
                [|
                    { path = "w64/text/sample_text.bin_c"; data = enBin; compress = true }
                    { path = "w64/text/sample_text.bin_j"; data = jaBin; compress = false }
                    { path = "w64/text/other.bin_c"; data = Encoding.UTF8.GetBytes("not a bin file"); compress = false }
                |]
        withTempGameDir
            [| ("media/data/bootpar/boot.par", bootPar) |]
            (fun dir ->
                let content = ExtractYakuza0(dir)
                Expect.equal 1 content.lessons.Length ""
                Expect.equal "sample_text" content.lessons.[0].Name ""
                let enCards = content.cards |> Array.filter (fun c -> c.LanguageTag = "en")
                let jaCards = content.cards |> Array.filter (fun c -> c.LanguageTag = "ja")
                Expect.equal 2 enCards.Length ""
                Expect.equal 2 jaCards.Length ""
                Expect.equal "A calm evening." enCards.[0].Text ""
                Expect.equal "body/0" enCards.[0].Key ""
                Expect.equal "穏やかな夕方。" jaCards.[0].Text ""
                Expect.equal "body/0" jaCards.[0].Key "")

    testCase "Whitespace in extracted text is collapsed" <|
      fun () ->
        let enBin =
            buildBinFile 1
                [|
                    { name = "body"; parameterType = 0; count = 0; block = binStringBlock [| "line one\r\nline two" |] }
                |]
        let bootPar =
            buildParc
                [| { path = "w64/text/sample_text.bin_c"; data = enBin; compress = false } |]
        withTempGameDir
            [| ("media/data/bootpar/boot.par", bootPar) |]
            (fun dir ->
                let content = ExtractYakuzaKiwami(dir)
                Expect.equal 1 content.cards.Length ""
                Expect.equal "line one line two" content.cards.[0].Text "")

    testCase "Missing boot.par raises" <|
      fun () ->
        withTempGameDir [||] (fun dir ->
            Expect.throws (fun () -> ExtractYakuza0(dir) |> ignore) "")
  ]

// ---------------------------------------------------------------------------
// armp .bin (Dragon Engine) helpers and tests
// ---------------------------------------------------------------------------

let private leBytes(value: int): byte array =
    [| byte(value &&& 0xFF)
       byte((value >>> 8) &&& 0xFF)
       byte((value >>> 16) &&& 0xFF)
       byte((value >>> 24) &&& 0xFF) |]

let private leShortBytes(value: int): byte array =
    [| byte(value &&& 0xFF); byte((value >>> 8) &&& 0xFF) |]

let private armpStringBytes(value: string): byte array =
    Array.append (Encoding.UTF8.GetBytes value) [| 0uy |]

type private ArmpColumnSpec =
    {
        name: string
        values: string array
    }

/// Builds a synthetic armp .bin file (version 1 or 2) with the given string
/// columns and, optionally, one integer column to verify that non-string
/// columns are ignored. Layout: header, main table, then string and offset
/// tables, all little-endian.
let private buildArmpBinFile(version: int)(columns: ArmpColumnSpec array)(withIntColumn: bool): byte array =
    let rowCount = if columns.Length > 0 then columns.[0].values.Length else 0
    let stringColumnCount = columns.Length
    let columnCount = stringColumnCount + (if withIntColumn then 1 else 0)

    let textPool = ResizeArray<string>()
    let poolIndexOf(value: string): int =
        let existing = textPool.IndexOf(value)
        if existing >= 0 then existing
        else
            textPool.Add(value)
            textPool.Count - 1

    let indicesPerColumn =
        columns
        |> Array.map (fun column ->
            column.values
            |> Array.map (fun value -> if isNull value then -1 else poolIndexOf value))

    let mainTableOffset = 0x14
    let bodyStart = mainTableOffset + 0x50
    let chunks = ResizeArray<byte array>()
    let mutable offset = bodyStart

    let append(chunk: byte array): int =
        let at = offset
        chunks.Add(chunk)
        offset <- offset + chunk.Length
        at

    let stringOffsets = textPool |> Seq.map (fun s -> append(armpStringBytes s)) |> Array.ofSeq
    let textOffsetTableOffset = append(Array.collect leBytes stringOffsets)

    let columnNameOffsets = columns |> Array.map (fun c -> append(armpStringBytes c.name))
    let columnNamesOffset = append(Array.collect leBytes columnNameOffsets)

    let contentOffsets = indicesPerColumn |> Array.map (fun indices -> append(Array.collect leBytes indices))

    let intColumnOffset =
        if withIntColumn then
            Some(append(Array.collect leBytes [| for i in 1 .. rowCount -> i |]))
        else
            None

    let stringType = if version = 1 then 12uy else 13uy
    let intType = if version = 1 then 6uy else 3uy
    let columnTypes =
        [|
            yield! Array.init columns.Length (fun _ -> stringType)
            if withIntColumn then yield intType
        |]
    let columnTypesOffset = append(columnTypes)

    let auxTypes =
        [|
            yield! Array.init columns.Length (fun _ -> stringType)
            if withIntColumn then yield intType
        |]
    let auxTypesOffset = append(auxTypes)

    let columnContentOffset =
        append
            (Array.concat
                [|
                    yield! Array.init columns.Length (fun i -> leBytes(contentOffsets.[i]))
                    if withIntColumn then yield (leBytes(intColumnOffset.Value))
                |])

    let mainTable = Array.zeroCreate<byte> 0x50
    Array.blit (leBytes rowCount) 0 mainTable 0x00 4
    Array.blit (leBytes columnCount) 0 mainTable 0x04 4
    Array.blit (leBytes textPool.Count) 0 mainTable 0x08 4
    Array.blit (leBytes columnTypesOffset) 0 mainTable 0x18 4
    Array.blit (leBytes columnContentOffset) 0 mainTable 0x1C 4
    mainTable.[0x23] <- 0uy
    Array.blit (leBytes textOffsetTableOffset) 0 mainTable 0x24 4
    Array.blit (leBytes columnNamesOffset) 0 mainTable 0x28 4
    Array.blit (leBytes auxTypesOffset) 0 mainTable 0x48 4

    let header = Array.zeroCreate<byte> 0x14
    Array.blit (leShortBytes 0) 0 header 0x08 2
    Array.blit (leShortBytes version) 0 header 0x0A 2
    Array.blit (leBytes mainTableOffset) 0 header 0x10 4

    Array.concat
        [|
            header
            mainTable
            [| for chunk in chunks do yield! chunk |]
        |]

[<Tests>]
let armpBinFileTests =
  testList "RGGStudio ArmpBinFile tests" [
    testCase "v1 string columns are extracted and non-string columns skipped" <|
      fun () ->
        let binFile =
            buildArmpBinFile 1
                [| { name = "body"; values = [| "first"; null; "third" |] } |]
                true
            |> parseArmpBinFile
        Expect.equal 1 binFile.columns.Length ""
        Expect.equal "body" binFile.columns.[0].name ""
        Expect.equal [| "first"; ""; "third" |] binFile.columns.[0].values ""

    testCase "v2 string columns are extracted" <|
      fun () ->
        let binFile =
            buildArmpBinFile 2
                [|
                    { name = "label"; values = [| "alpha"; "beta" |] }
                    { name = "note"; values = [| "one"; "two" |] }
                |]
                true
            |> parseArmpBinFile
        Expect.equal 2 binFile.columns.Length ""
        Expect.equal [| "alpha"; "beta" |] binFile.columns.[0].values ""
        Expect.equal [| "one"; "two" |] binFile.columns.[1].values ""

    testCase "Non-armp data is rejected" <|
      fun () ->
        Expect.isFalse(isArmpBinFile(Array.zeroCreate<byte> 64)) ""

    testCase "Per-row storage mode tables yield no columns" <|
      fun () ->
        let data = buildArmpBinFile 2 [| { name = "body"; values = [| "x" |] } |] false
        data.[0x14 + 0x23] <- 1uy
        let binFile = parseArmpBinFile(data)
        Expect.equal 0 binFile.columns.Length ""

    testProperty "String values round-trip through the parser" <|
      fun (values: string array) ->
        // armp strings are null-terminated, so embedded NUL bytes cannot be
        // represented by the format.
        let sanitized =
            values
            |> Array.map (fun v -> (if isNull v then "" else v).Replace("\000", ""))
        let nonEmpty = if sanitized.Length = 0 then [| "" |] else sanitized
        let binFile =
            buildArmpBinFile 2 [| { name = "body"; values = nonEmpty } |] false
            |> parseArmpBinFile
        binFile.columns.Length = 1 && binFile.columns.[0].values = nonEmpty
  ]

[<Tests>]
let dragonEngineLanguageTests =
  testList "RGGStudio DragonEngine language code tests" [
    testCase "Language codes map to IETF tags" <|
      fun () ->
        let cases =
            [|
                ("en", Some("en"))
                ("e", Some("en"))
                ("ja", Some("ja"))
                ("j", Some("ja"))
                ("zhs", Some("zh-CN"))
                ("zh", Some("zh-TW"))
                ("pt", Some("pt-BR"))
                ("ru", Some("ru"))
                ("xx", None)
            |]
        cases
        |> Array.iter (fun (code, expected) -> Expect.equal expected (languageTagFromCode code) code)
  ]

[<Tests>]
let dragonEngineGamesTests =
  testList "RGGStudio DragonEngineGames tests" [
    testCase "Language-suffixed par archives are extracted (Like a Dragon layout)" <|
      fun () ->
        let enBin =
            buildArmpBinFile 2
                [| { name = "body"; values = [| "A calm evening."; "A busy street." |] } |]
                false
        let jaBin =
            buildArmpBinFile 2
                [| { name = "body"; values = [| "穏やかな夕方。"; "賑やかな通り。" |] } |]
                false
        let enPar =
            buildParc
                [|
                    { path = "en/sample_text.bin"; data = enBin; compress = true }
                    { path = "en/other.bin"; data = Encoding.ASCII.GetBytes("not an armp file"); compress = false }
                |]
        let jaPar =
            buildParc
                [| { path = "ja/sample_text.bin"; data = jaBin; compress = false } |]
        withTempGameDir
            [|
                ("runtime/media/data/db.yazawa.en.par", enPar)
                ("runtime/media/data/db.yazawa.ja.par", jaPar)
            |]
            (fun dir ->
                let content = ExtractYakuzaLikeADragon(dir)
                Expect.equal 1 content.lessons.Length ""
                Expect.equal "sample_text" content.lessons.[0].Name ""
                let enCards = content.cards |> Array.filter (fun c -> c.LanguageTag = "en")
                let jaCards = content.cards |> Array.filter (fun c -> c.LanguageTag = "ja")
                Expect.equal 2 enCards.Length ""
                Expect.equal 2 jaCards.Length ""
                Expect.equal "A calm evening." enCards.[0].Text ""
                Expect.equal "body/0" enCards.[0].Key ""
                Expect.equal "穏やかな夕方。" jaCards.[0].Text "")

    testCase "Loose db language directories are extracted (Yakuza 6 layout)" <|
      fun () ->
        let enBin =
            buildArmpBinFile 1
                [| { name = "body"; values = [| "An old town." |] } |]
                false
        withTempGameDir
            [| ("data/db/e/sample_text.bin", enBin) |]
            (fun dir ->
                let content = ExtractYakuza6(dir)
                Expect.equal 1 content.cards.Length ""
                Expect.equal "en" content.cards.[0].LanguageTag ""
                Expect.equal "An old town." content.cards.[0].Text "")

    testCase "Missing data directories raise" <|
      fun () ->
        withTempGameDir [||] (fun dir ->
            Expect.throws (fun () -> ExtractJudgment(dir) |> ignore) "")
  ]
