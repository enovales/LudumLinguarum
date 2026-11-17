module RggEngine.ParArchive

open RggEngine.Sllz
open StreamTools
open System
open System.Collections.Generic
open System.IO
open System.Text

/// Metadata for a single file entry inside a PARC (.par) archive.
type ParFileEntry =
    {
        /// Virtual path inside the archive using '/' separators (e.g. "en/item.bin").
        Path: string
        /// Base file name (e.g. "item.bin").
        FileName: string
        Flags: uint32
        OriginalSize: uint32
        CompressedSize: uint32
        DataOffset: int64
        RawAttributes: uint32
        Timestamp: uint64
    }
    member this.IsCompressed = (this.Flags &&& 0x80000000u) <> 0u

/// Checks whether a byte array starts with the "PARC" magic signature.
let isParRaw (data: byte array) : bool =
    not (isNull data)
    && data.Length >= 32
    && data.[0] = byte 'P'
    && data.[1] = byte 'A'
    && data.[2] = byte 'R'
    && data.[3] = byte 'C'

/// Checks whether a byte array is a PARC archive (either directly or wrapped in SLLZ compression).
let isPar (data: byte array) : bool =
    isParRaw data || isSllz data

/// Checks whether a stream starts with the "PARC" or "SLLZ" magic signature.
let isParStream (stream: Stream) : bool =
    if isNull stream || not stream.CanSeek || stream.Length < 16L then
        false
    else
        let savedPos = stream.Position
        try
            stream.Seek(0L, SeekOrigin.Begin) |> ignore
            let magic = Array.zeroCreate<byte> 4
            let read = stream.Read(magic, 0, 4)
            read = 4
            && ((magic.[0] = byte 'P' && magic.[1] = byte 'A' && magic.[2] = byte 'R' && magic.[3] = byte 'C')
                || (magic.[0] = byte 'S' && magic.[1] = byte 'L' && magic.[2] = byte 'L' && magic.[3] = byte 'Z'))
        finally
            stream.Seek(savedPos, SeekOrigin.Begin) |> ignore

/// Ensures that if an entire .par file is SLLZ-compressed on disk, we unwrap it into a seekable stream.
let private unwrapOuterStream (stream: Stream) : Stream * bool =
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    let magic = Array.zeroCreate<byte> 4
    let bytesRead = stream.Read(magic, 0, 4)
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    if bytesRead = 4 && magic.[0] = byte 'S' && magic.[1] = byte 'L' && magic.[2] = byte 'L' && magic.[3] = byte 'Z' then
        use ms = new MemoryStream()
        stream.CopyTo(ms)
        let decompressed = Sllz.decompress (ms.ToArray())
        (new MemoryStream(decompressed, false) :> Stream, true)
    else
        (stream, false)

/// Reads the directory and file index from a PARC stream and returns all file entries with their virtual paths.
let readEntries (inputStream: Stream) : ParFileEntry array =
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
    let stream, ownsStream = unwrapOuterStream inputStream
    try
        if stream.Length < 32L then
            raise (FormatException("PARC stream is shorter than the 32-byte header and index."))

        stream.Seek(0L, SeekOrigin.Begin) |> ignore
        use br = new BinaryReader(stream, Encoding.UTF8, true)
        let magic = Encoding.ASCII.GetString(br.ReadBytes(4))
        if magic <> "PARC" then
            raise (FormatException($"Invalid PARC magic '{magic}' (expected 'PARC')."))

        let _platformId = br.ReadByte()
        let endianByte = br.ReadByte()
        let _sizeExtended = br.ReadByte()
        let _relocated = br.ReadByte()

        let rw =
            if endianByte = 0uy then
                LittleEndianReaderWrapper(br)
            else
                BigEndianReaderWrapper(br)

        let _version = rw.ReadUInt32()
        let _size = rw.ReadUInt32()

        let directoryCount = rw.ReadUInt32()
        let directoryStartOffset = rw.ReadUInt32()
        let fileCount = rw.ReadUInt32()
        let fileStartOffset = rw.ReadUInt32()

        let nameEncoding = Encoding.UTF8

        let readFileEntry (fileIdx: uint32) (parentPath: string) : ParFileEntry =
            let nameOffset = 0x20L + (0x40L * int64 directoryCount) + (int64 fileIdx * 0x40L)
            rw.Seek(nameOffset) |> ignore
            let fileName = rw.ReadFixedString(0x40, nameEncoding).TrimEnd('\000')

            let infoOffset = int64 fileStartOffset + (int64 fileIdx * 0x20L)
            rw.Seek(infoOffset) |> ignore
            let flags = rw.ReadUInt32()
            let originalSize = rw.ReadUInt32()
            let compressedSize = rw.ReadUInt32()
            let dataOffset = rw.ReadUInt32()
            let rawAttributes = rw.ReadUInt32()
            let extendedOffset = rw.ReadUInt32()
            let timestamp = rw.ReadUInt64()

            let fullOffset = (int64 extendedOffset <<< 32) ||| int64 dataOffset
            let virtualPath =
                if String.IsNullOrEmpty(parentPath) then fileName
                else parentPath + "/" + fileName

            {
                Path = virtualPath
                FileName = fileName
                Flags = flags
                OriginalSize = originalSize
                CompressedSize = compressedSize
                DataOffset = fullOffset
                RawAttributes = rawAttributes
                Timestamp = timestamp
            }

        let results = ResizeArray<ParFileEntry>(int fileCount)
        let visitedDirs = HashSet<uint32>()

        let rec processDirectory (dirIdx: uint32) (parentPath: string) =
            if dirIdx < directoryCount && visitedDirs.Add(dirIdx) then
                let dirNameOffset = 0x20L + (int64 dirIdx * 0x40L)
                rw.Seek(dirNameOffset) |> ignore
                let rawDirName = rw.ReadFixedString(0x40, nameEncoding).TrimEnd('\000')
                let currentPath =
                    if dirIdx = 0u && (String.IsNullOrEmpty(rawDirName) || rawDirName = ".") then
                        parentPath
                    elif String.IsNullOrEmpty(rawDirName) || rawDirName = "." then
                        parentPath
                    elif String.IsNullOrEmpty(parentPath) then
                        rawDirName
                    else
                        parentPath + "/" + rawDirName

                let dirInfoOffset = int64 directoryStartOffset + (int64 dirIdx * 0x20L)
                rw.Seek(dirInfoOffset) |> ignore
                let subDirCount = rw.ReadUInt32()
                let subDirStartIdx = rw.ReadUInt32()
                let dirFileCount = rw.ReadUInt32()
                let dirFileStartIdx = rw.ReadUInt32()
                let _rawAttrs = rw.ReadUInt32()

                if subDirCount > 0u then
                    for childDirIdx in subDirStartIdx .. subDirStartIdx + subDirCount - 1u do
                        processDirectory childDirIdx currentPath

                if dirFileCount > 0u then
                    for fileIdx in dirFileStartIdx .. dirFileStartIdx + dirFileCount - 1u do
                        if fileIdx < fileCount then
                            results.Add(readFileEntry fileIdx currentPath)

        if directoryCount > 0u then
            processDirectory 0u ""
        elif fileCount > 0u then
            for fileIdx in 0u .. fileCount - 1u do
                results.Add(readFileEntry fileIdx "")

        results.ToArray()
    finally
        if ownsStream then
            stream.Dispose()

/// Reads the bytes for a single ParFileEntry from the underlying PARC stream, optionally decompressing SLLZ data.
let readEntryBytes (inputStream: Stream, entry: ParFileEntry, decompress: bool) : byte array =
    let stream, ownsStream = unwrapOuterStream inputStream
    try
        let byteLen =
            if entry.CompressedSize > 0u then int entry.CompressedSize
            else int entry.OriginalSize

        if byteLen = 0 then
            [||]
        else
            if entry.DataOffset < 0L || entry.DataOffset + int64 byteLen > stream.Length then
                raise (FormatException($"PARC file entry '{entry.Path}' data range [{entry.DataOffset}..{entry.DataOffset + int64 byteLen}) exceeds stream length {stream.Length}."))

            stream.Seek(entry.DataOffset, SeekOrigin.Begin) |> ignore
            let rawBytes = Array.zeroCreate<byte> byteLen
            let mutable totalRead = 0
            while totalRead < byteLen do
                let r = stream.Read(rawBytes, totalRead, byteLen - totalRead)
                if r = 0 then
                    totalRead <- byteLen
                else
                    totalRead <- totalRead + r

            if decompress && (entry.IsCompressed || Sllz.isSllz rawBytes) then
                Sllz.decompressIfNeeded rawBytes
            else
                rawBytes
    finally
        if ownsStream then
            stream.Dispose()

/// Checks whether a file on disk starts with the "PARC" or "SLLZ" magic signature without reading the entire file.
let isParFile (parFilePath: string) : bool =
    if String.IsNullOrWhiteSpace(parFilePath) || not (File.Exists(parFilePath)) then
        false
    else
        try
            use fs = new FileStream(parFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            isParStream fs
        with _ ->
            false

/// Extracts matching files from a PARC stream into (virtualPath, decompressedBytes) pairs.
/// Only file entries satisfying fileFilter are read and decompressed, and when expandNestedPars is true,
/// only nested .par entries satisfying nestedParFilter are recursively expanded.
let rec extractFilteredFromStream
    (inputStream: Stream, expandNestedPars: bool, fileFilter: string -> bool, nestedParFilter: string -> bool)
    : (string * byte array) array =
    let stream, ownsStream = unwrapOuterStream inputStream
    try
        let entries = readEntries stream
        let output = ResizeArray<string * byte array>()

        for entry in entries do
            if entry.FileName.EndsWith(".par", StringComparison.OrdinalIgnoreCase) then
                if expandNestedPars && nestedParFilter entry.FileName then
                    let bytes = readEntryBytes (stream, entry, true)
                    if isPar bytes then
                        let prefix = entry.Path.Substring(0, entry.Path.Length - 4)
                        use nestedMs = new MemoryStream(bytes, false)
                        let nestedFiles = extractFilteredFromStream (nestedMs, true, fileFilter, nestedParFilter)
                        for (nestedPath, nestedBytes) in nestedFiles do
                            let combinedPath =
                                if String.IsNullOrEmpty(prefix) then nestedPath
                                else prefix + "/" + nestedPath
                            output.Add((combinedPath, nestedBytes))
                    elif fileFilter entry.FileName then
                        output.Add((entry.Path, bytes))
                elif fileFilter entry.FileName then
                    let bytes = readEntryBytes (stream, entry, true)
                    output.Add((entry.Path, bytes))
            elif fileFilter entry.FileName then
                let bytes = readEntryBytes (stream, entry, true)
                output.Add((entry.Path, bytes))

        output.ToArray()
    finally
        if ownsStream then
            stream.Dispose()

/// Extracts matching files from a PARC (.par) file on disk.
let extractFilteredFromFile
    (parFilePath: string, expandNestedPars: bool, fileFilter: string -> bool, nestedParFilter: string -> bool)
    : (string * byte array) array =
    use fs = new FileStream(parFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    extractFilteredFromStream (fs, expandNestedPars, fileFilter, nestedParFilter)

/// Extracts all files from a PARC stream into (virtualPath, decompressedBytes) pairs.
/// When expandNestedPars is true, any nested .par files are recursively extracted under their parent path/stem.
let extractAllFromStream (inputStream: Stream, expandNestedPars: bool) : (string * byte array) array =
    extractFilteredFromStream (inputStream, expandNestedPars, (fun _ -> true), (fun _ -> true))

/// Extracts all files from a PARC (.par) file on disk.
let extractAllFromFile (parFilePath: string, expandNestedPars: bool) : (string * byte array) array =
    extractFilteredFromFile (parFilePath, expandNestedPars, (fun _ -> true), (fun _ -> true))

type private BuilderDirNode =
    {
        Name: string
        Subdirs: Dictionary<string, BuilderDirNode>
        SubdirOrder: ResizeArray<string>
        Files: ResizeArray<string * byte array * bool>
    }

/// Builds a binary PARC (.par) archive in memory from a sequence of (virtualPath, rawData, compressWithSllz) entries.
let buildParArchive (files: (string * byte array * bool) seq, bigEndian: bool) : byte array =
    let root =
        {
            Name = "."
            Subdirs = Dictionary<string, BuilderDirNode>(StringComparer.Ordinal)
            SubdirOrder = ResizeArray<string>()
            Files = ResizeArray<string * byte array * bool>()
        }

    for (rawPath, data, compress) in files do
        let normalized = rawPath.Replace('\\', '/').Trim('/')
        let segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
        if segments.Length > 0 then
            let mutable current = root
            for i = 0 to segments.Length - 2 do
                let dirName = segments.[i]
                match current.Subdirs.TryGetValue(dirName) with
                | true, child -> current <- child
                | false, _ ->
                    let child =
                        {
                            Name = dirName
                            Subdirs = Dictionary<string, BuilderDirNode>(StringComparer.Ordinal)
                            SubdirOrder = ResizeArray<string>()
                            Files = ResizeArray<string * byte array * bool>()
                        }
                    current.Subdirs.[dirName] <- child
                    current.SubdirOrder.Add(dirName)
                    current <- child
            let fileName = segments.[segments.Length - 1]
            current.Files.Add((fileName, data, compress))

    // Flatten directories in BFS order so each directory's child directories form a contiguous index range.
    let allDirs = ResizeArray<BuilderDirNode>()
    let queue = Queue<BuilderDirNode>()
    allDirs.Add(root)
    queue.Enqueue(root)

    let dirSubdirRanges = Dictionary<BuilderDirNode, uint32 * uint32>()
    let dirFileRanges = Dictionary<BuilderDirNode, uint32 * uint32>()
    let allFiles = ResizeArray<string * byte array * bool>()

    while queue.Count > 0 do
        let dir = queue.Dequeue()
        let subStart = uint32 allDirs.Count
        let subCount = uint32 dir.SubdirOrder.Count
        for subName in dir.SubdirOrder do
            let child = dir.Subdirs.[subName]
            allDirs.Add(child)
            queue.Enqueue(child)
        dirSubdirRanges.[dir] <- (subCount, if subCount = 0u then 0u else subStart)

        let fileStart = uint32 allFiles.Count
        let fileCount = uint32 dir.Files.Count
        for f in dir.Files do
            allFiles.Add(f)
        dirFileRanges.[dir] <- (fileCount, if fileCount = 0u then 0u else fileStart)

    let dirCount = uint32 allDirs.Count
    let fileCount = uint32 allFiles.Count
    let dirInfoStartOffset = 0x20u + (0x40u * (dirCount + fileCount))
    let fileInfoStartOffset = dirInfoStartOffset + (0x20u * dirCount)
    let dataSectionStartOffset = fileInfoStartOffset + (0x20u * fileCount)

    let sllzEndianness =
        if bigEndian then SllzEndianness.BigEndian
        else SllzEndianness.LittleEndian

    let preparedFiles =
        allFiles.ToArray()
        |> Array.map (fun (name, rawBytes, shouldCompress) ->
            let origSize = uint32 rawBytes.Length
            let storedBytes, flags =
                if shouldCompress then
                    let comp = Sllz.compressStandard (rawBytes, sllzEndianness)
                    (comp, 0x80000000u)
                else
                    (rawBytes, 0x00000000u)
            (name, origSize, uint32 storedBytes.Length, flags, storedBytes))

    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms, Encoding.UTF8, true)

    let needSwap =
        (bigEndian && BitConverter.IsLittleEndian)
        || (not bigEndian && not BitConverter.IsLittleEndian)

    let writeU32 (v: uint32) =
        let b = BitConverter.GetBytes(v)
        if needSwap then Array.Reverse(b)
        bw.Write(b)

    let writeU64 (v: uint64) =
        let b = BitConverter.GetBytes(v)
        if needSwap then Array.Reverse(b)
        bw.Write(b)

    let writeFixedName (name: string) =
        let buf = Array.zeroCreate<byte> 0x40
        let nameBytes = Encoding.UTF8.GetBytes(name)
        let copyLen = min 0x3F nameBytes.Length
        Array.Copy(nameBytes, 0, buf, 0, copyLen)
        bw.Write(buf)

    // 0x00..0x0F: FileHeader
    bw.Write(Encoding.ASCII.GetBytes("PARC"))
    bw.Write(0x02uy) // PlayStation3 / standard PARC platform ID
    bw.Write(if bigEndian then 0x01uy else 0x00uy)
    bw.Write(0x00uy)
    bw.Write(0x00uy)
    writeU32 0x00020001u
    writeU32 0u

    // 0x10..0x1F: ParIndex
    writeU32 dirCount
    writeU32 dirInfoStartOffset
    writeU32 fileCount
    writeU32 fileInfoStartOffset

    // Directory names (0x40 bytes each)
    for dir in allDirs do
        writeFixedName dir.Name

    // File names (0x40 bytes each)
    for (fileName, _, _, _, _) in preparedFiles do
        writeFixedName fileName

    // Directory info entries (0x20 bytes each)
    for dir in allDirs do
        let (subCount, subStart) = dirSubdirRanges.[dir]
        let (fCount, fStart) = dirFileRanges.[dir]
        writeU32 subCount
        writeU32 subStart
        writeU32 fCount
        writeU32 fStart
        writeU32 0x00000010u
        writeU32 0u
        writeU32 0u
        writeU32 0u

    // File info entries (0x20 bytes each)
    let mutable currentDataOffset = int64 dataSectionStartOffset
    for (_, origSize, compSize, flags, _) in preparedFiles do
        let lowOffset = uint32 (currentDataOffset &&& 0xFFFFFFFFL)
        let highOffset = uint32 ((currentDataOffset >>> 32) &&& 0xFFFFFFFFL)
        writeU32 flags
        writeU32 origSize
        writeU32 compSize
        writeU32 lowOffset
        writeU32 0x00000020u
        writeU32 highOffset
        writeU64 0UL
        currentDataOffset <- currentDataOffset + int64 compSize

    // File data payloads
    for (_, _, _, _, storedBytes) in preparedFiles do
        bw.Write(storedBytes)

    ms.ToArray()
