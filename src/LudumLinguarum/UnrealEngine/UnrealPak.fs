module UnrealEngine.UnrealPak

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Security.Cryptography
open System.Text

/// Magic signature in an Unreal Engine .pak footer (0x5A6F12E1).
[<Literal>]
let PakMagic = 0x5A6F12E1u

/// Metadata for a single file entry inside an Unreal Engine .pak archive index.
type PakEntryRef =
    {
        /// Virtual path inside the .pak archive using '/' separators (without leading '/').
        Path: string
        /// Byte offset into the EncodedPakEntries buffer (used in Pak v8+ FullDirectoryIndex).
        EncodedOffset: int
        /// Direct byte offset in the .pak stream (used when decoded directly from the primary index).
        DirectOffset: int64 option
    }

/// Parsed directory index and metadata for an Unreal Engine .pak archive.
type PakArchiveIndex =
    {
        Version: int
        MountPoint: string
        CompressionMethods: string array
        EncodedPakEntries: byte array
        Files: PakEntryRef array
    }

/// Reads a length-prefixed Unreal FString (UTF-8 when length >= 0, UTF-16LE when length < 0).
let readFString (br: BinaryReader) : string =
    let len = br.ReadInt32()
    if len = 0 then
        ""
    elif len > 0 then
        let bytes = br.ReadBytes(len)
        let actualLen =
            if bytes.Length > 0 && bytes.[bytes.Length - 1] = 0uy then bytes.Length - 1
            else bytes.Length
        Encoding.UTF8.GetString(bytes, 0, actualLen)
    else
        let charCount = -len
        let byteCount = charCount * 2
        let bytes = br.ReadBytes(byteCount)
        let actualByteLen =
            if bytes.Length >= 2 && bytes.[bytes.Length - 2] = 0uy && bytes.[bytes.Length - 1] = 0uy then
                bytes.Length - 2
            else
                bytes.Length
        Encoding.Unicode.GetString(bytes, 0, actualByteLen)

/// Writes a length-prefixed Unreal FString (ASCII/UTF-8 if all ASCII, otherwise UTF-16LE with negative length).
let writeFString (bw: BinaryWriter, value: string) : unit =
    if String.IsNullOrEmpty(value) then
        bw.Write(0)
    else
        let isAscii = value |> Seq.forall (fun c -> int c < 128)
        if isAscii then
            let bytes = Encoding.UTF8.GetBytes(value)
            bw.Write(bytes.Length + 1)
            bw.Write(bytes)
            bw.Write(0uy)
        else
            let bytes = Encoding.Unicode.GetBytes(value)
            let charCount = (bytes.Length / 2) + 1
            bw.Write(-charCount)
            bw.Write(bytes)
            bw.Write(0us)

/// Attempts to locate the FPakInfo footer near the end of a stream and returns (version, indexOffset, indexSize, encryptedIndex, compressionMethods).
let private tryFindFooter (stream: Stream) : (int * int64 * int64 * bool * string array) option =
    if isNull stream || not stream.CanSeek || stream.Length < 44L then
        None
    else
        let tailLen = int (min 512L stream.Length)
        let tailStart = stream.Length - int64 tailLen
        stream.Seek(tailStart, SeekOrigin.Begin) |> ignore
        let buf = Array.zeroCreate<byte> tailLen
        let mutable totalRead = 0
        while totalRead < tailLen do
            let r = stream.Read(buf, totalRead, tailLen - totalRead)
            if r = 0 then totalRead <- tailLen
            else totalRead <- totalRead + r

        let mutable foundPos = -1
        for i = tailLen - 24 downto 0 do
            if foundPos = -1
               && buf.[i] = 0xE1uy
               && buf.[i + 1] = 0x12uy
               && buf.[i + 2] = 0x6Fuy
               && buf.[i + 3] = 0x5Auy then
                let ver = BitConverter.ToInt32(buf, i + 4)
                let idxOff = BitConverter.ToInt64(buf, i + 8)
                let idxSz = BitConverter.ToInt64(buf, i + 16)
                if ver >= 1 && ver <= 12 && idxOff >= 0L && idxSz >= 0L && idxOff + idxSz <= stream.Length then
                    foundPos <- i

        if foundPos < 0 then
            None
        else
            let version = BitConverter.ToInt32(buf, foundPos + 4)
            let indexOffset = BitConverter.ToInt64(buf, foundPos + 8)
            let indexSize = BitConverter.ToInt64(buf, foundPos + 16)
            let encryptedIndex =
                if version >= 4 && foundPos >= 1 then buf.[foundPos - 1] <> 0uy
                else false

            let compMethods = ResizeArray<string>()
            let compStart =
                if version = 9 then foundPos + 45 // 4 magic + 4 ver + 8 off + 8 sz + 20 hash + 1 frozen
                else foundPos + 44 // 4 magic + 4 ver + 8 off + 8 sz + 20 hash
            let mutable cPos = compStart
            while cPos + 32 <= tailLen do
                let name = Encoding.ASCII.GetString(buf, cPos, 32).TrimEnd('\000')
                if not (String.IsNullOrWhiteSpace(name)) then
                    compMethods.Add(name)
                cPos <- cPos + 32

            if compMethods.Count = 0 then
                compMethods.Add("Zlib")

            Some(version, indexOffset, indexSize, encryptedIndex, compMethods.ToArray())

/// Checks whether a stream is an unencrypted Unreal Engine .pak archive.
let isPakStream (stream: Stream) : bool =
    if isNull stream || not stream.CanSeek || stream.Length < 44L then
        false
    else
        let savedPos = stream.Position
        try
            match tryFindFooter stream with
            | Some(_, _, _, encrypted, _) -> not encrypted
            | None -> false
        finally
            stream.Seek(savedPos, SeekOrigin.Begin) |> ignore

/// Checks whether a file on disk is an unencrypted Unreal Engine .pak archive without reading the entire file.
let isPakFile (pakFilePath: string) : bool =
    if String.IsNullOrWhiteSpace(pakFilePath) || not (File.Exists(pakFilePath)) then
        false
    else
        try
            use fs = new FileStream(pakFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            isPakStream fs
        with _ ->
            false

/// Skips an unencoded FPakEntry in a stream and returns its data offset.
let private readUnencodedPakEntryOffset (br: BinaryReader, version: int) : int64 =
    let offset = br.ReadInt64()
    let _compressedSize = br.ReadInt64()
    let _uncompressedSize = br.ReadInt64()
    let compressionMethod =
        if version < 8 then
            uint32 (br.ReadInt32())
        else
            br.ReadUInt32()
    if version <= 1 then
        let _timestamp = br.ReadInt64()
        ()
    let _hash = br.ReadBytes(20)
    if version >= 3 then
        if compressionMethod <> 0u then
            let blockCount = br.ReadInt32()
            br.ReadBytes(blockCount * 16) |> ignore
        let _encrypted = br.ReadByte()
        let _compressionBlockSize = br.ReadUInt32()
        ()
    offset

/// Reads the directory index from an Unreal Engine .pak stream.
let readIndex (stream: Stream) : PakArchiveIndex =
    match tryFindFooter stream with
    | None ->
        raise (FormatException("Stream does not contain a valid Unreal Engine .pak footer."))
    | Some(_, _, _, true, _) ->
        raise (FormatException("Encrypted Unreal Engine .pak archives are not supported."))
    | Some(version, indexOffset, _indexSize, false, compMethods) ->
        stream.Seek(indexOffset, SeekOrigin.Begin) |> ignore
        use br = new BinaryReader(stream, Encoding.UTF8, true)
        let mountPoint = readFString br
        let numEntries = br.ReadInt32()

        if version >= 10 then
            let _pathHashSeed = br.ReadUInt64()
            let hasPathHashIndex = br.ReadInt32()
            if hasPathHashIndex <> 0 then
                let _pathHashIndexOffset = br.ReadInt64()
                let _pathHashIndexSize = br.ReadInt64()
                let _pathHashIndexHash = br.ReadBytes(20)
                ()

            let hasFullDirIndex = br.ReadInt32()
            let fullDirOffset, fullDirSize =
                if hasFullDirIndex <> 0 then
                    let o = br.ReadInt64()
                    let s = br.ReadInt64()
                    let _h = br.ReadBytes(20)
                    (o, s)
                else
                    (0L, 0L)

            let encodedEntriesSize = br.ReadInt32()
            let encodedBytes =
                if encodedEntriesSize > 0 then br.ReadBytes(encodedEntriesSize)
                else [||]

            let _unencodedFilesNum = br.ReadInt32()

            let files = ResizeArray<PakEntryRef>(max 0 numEntries)
            if hasFullDirIndex <> 0 && fullDirSize > 0L then
                stream.Seek(fullDirOffset, SeekOrigin.Begin) |> ignore
                let dirCount = br.ReadInt32()
                for _ = 0 to dirCount - 1 do
                    let dirName = readFString(br).Replace('\\', '/').TrimStart('/')
                    let fileCount = br.ReadInt32()
                    for _ = 0 to fileCount - 1 do
                        let fileName = readFString(br).Replace('\\', '/').TrimStart('/')
                        let encOffset = br.ReadInt32()
                        let combinedPath =
                            if String.IsNullOrEmpty(dirName) || dirName = "/" then fileName
                            elif dirName.EndsWith("/", StringComparison.Ordinal) then dirName + fileName
                            else dirName + "/" + fileName
                        if encOffset >= 0 && encOffset < encodedBytes.Length then
                            files.Add({ Path = combinedPath; EncodedOffset = encOffset; DirectOffset = None })

            {
                Version = version
                MountPoint = mountPoint
                CompressionMethods = compMethods
                EncodedPakEntries = encodedBytes
                Files = files.ToArray()
            }
        else
            let files = ResizeArray<PakEntryRef>(max 0 numEntries)
            for _ = 0 to numEntries - 1 do
                let filePath = readFString(br).Replace('\\', '/').TrimStart('/')
                let directOffset = readUnencodedPakEntryOffset (br, version)
                files.Add({ Path = filePath; EncodedOffset = -1; DirectOffset = Some directOffset })

            {
                Version = version
                MountPoint = mountPoint
                CompressionMethods = compMethods
                EncodedPakEntries = [||]
                Files = files.ToArray()
            }

/// Decodes the file data offset in the .pak stream from an EncodedPakEntries offset.
let decodeEntryOffset (encodedBytes: byte array, encodedOffset: int) : int64 =
    if isNull encodedBytes || encodedOffset < 0 || encodedOffset + 8 > encodedBytes.Length then
        raise (ArgumentOutOfRangeException(nameof encodedOffset, "EncodedPakEntries offset is out of range."))
    let flags = BitConverter.ToUInt32(encodedBytes, encodedOffset)
    let mutable pos = encodedOffset + 4
    // When bits 0..5 are 0x3F, a 32-bit CompressionBlockSize precedes the Offset field.
    if (flags &&& 0x3Fu) = 0x3Fu then
        pos <- pos + 4
    let isOffset32BitSafe = (flags &&& (1u <<< 31)) <> 0u
    if isOffset32BitSafe then
        int64 (BitConverter.ToUInt32(encodedBytes, pos))
    else
        BitConverter.ToInt64(encodedBytes, pos)

/// Reads and decompresses the bytes for a single PakEntryRef from the underlying .pak stream.
let readEntryBytes (stream: Stream, index: PakArchiveIndex, entry: PakEntryRef) : byte array =
    let entryOffset =
        match entry.DirectOffset with
        | Some off -> off
        | None -> decodeEntryOffset (index.EncodedPakEntries, entry.EncodedOffset)

    stream.Seek(entryOffset, SeekOrigin.Begin) |> ignore
    use br = new BinaryReader(stream, Encoding.UTF8, true)
    let _recordOffset = br.ReadInt64()
    let _compressedSize = br.ReadInt64()
    let uncompressedSize = br.ReadInt64()
    let compressionMethod =
        if index.Version < 8 then uint32 (br.ReadInt32())
        else br.ReadUInt32()
    if index.Version <= 1 then
        br.ReadInt64() |> ignore
    let _hash = br.ReadBytes(20)

    if uncompressedSize <= 0L then
        [||]
    elif compressionMethod = 0u then
        if index.Version >= 3 then
            let _encrypted = br.ReadByte()
            let _compBlockSize = br.ReadUInt32()
            ()
        br.ReadBytes(int uncompressedSize)
    else
        let blockCount = br.ReadInt32()
        let blocks = Array.init blockCount (fun _ -> (br.ReadInt64(), br.ReadInt64()))
        let encrypted = br.ReadByte()
        let _compBlockSize = br.ReadUInt32()
        if encrypted <> 0uy then
            raise (FormatException($"Pak entry '{entry.Path}' is encrypted."))

        use outMs = new MemoryStream(int uncompressedSize)
        for (blockStart, blockEnd) in blocks do
            let actualStart =
                if index.Version >= 8 then entryOffset + blockStart
                else blockStart
            let compLen = int (blockEnd - blockStart)
            if compLen > 0 then
                stream.Seek(actualStart, SeekOrigin.Begin) |> ignore
                let compBuf = br.ReadBytes(compLen)
                use inMs = new MemoryStream(compBuf, false)
                use zlib = new ZLibStream(inMs, CompressionMode.Decompress)
                zlib.CopyTo(outMs)
        outMs.ToArray()

/// Extracts all files matching fileFilter from a .pak file on disk into (virtualPath, decompressedBytes) pairs.
let extractFilteredFromFile (pakFilePath: string, fileFilter: string -> bool) : (string * byte array) array =
    use fs = new FileStream(pakFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    let index = readIndex fs
    let results = ResizeArray<string * byte array>()
    for entry in index.Files do
        if fileFilter entry.Path then
            let bytes = readEntryBytes (fs, index, entry)
            results.Add((entry.Path, bytes))
    results.ToArray()

/// Builds a binary Unreal Engine v11 .pak archive in memory from (virtualPath, rawData, compressWithZlib) entries.
let buildPakArchive (entries: (string * byte array * bool) seq) : byte array =
    let entryList = entries |> Seq.toArray
    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms, Encoding.UTF8, true)

    use encodedEntriesMs = new MemoryStream()
    use encodedBw = new BinaryWriter(encodedEntriesMs, Encoding.UTF8, true)

    let dirGroups = Dictionary<string, ResizeArray<string * int>>(StringComparer.Ordinal)

    for (rawPath, rawBytes, shouldCompress) in entryList do
        let normPath = rawPath.Replace('\\', '/').Trim('/')
        let lastSlash = normPath.LastIndexOf('/')
        let dirName, fileName =
            if lastSlash >= 0 then
                (normPath.Substring(0, lastSlash + 1), normPath.Substring(lastSlash + 1))
            else
                ("/", normPath)

        let entryOffset = ms.Position
        let uncompBytes = if isNull rawBytes then [||] else rawBytes
        let uncompSize = uncompBytes.Length

        let compBytes =
            if shouldCompress && uncompSize > 0 then
                use cMs = new MemoryStream()
                do
                    use z = new ZLibStream(cMs, CompressionLevel.Optimal, true)
                    z.Write(uncompBytes, 0, uncompSize)
                cMs.ToArray()
            else
                uncompBytes

        let isCompressed = shouldCompress && uncompSize > 0
        let compSize = compBytes.Length
        let hash = SHA1.HashData(compBytes)

        // Write unencoded FPakEntry header + data block into the .pak stream
        bw.Write(0L) // Offset (0 in local header)
        bw.Write(int64 compSize)
        bw.Write(int64 uncompSize)
        bw.Write(if isCompressed then 1u else 0u)
        bw.Write(hash)
        if isCompressed then
            bw.Write(1) // 1 compression block
            let headerSize = 8L + 8L + 8L + 4L + 20L + 4L + 16L + 1L + 4L // 73 bytes
            bw.Write(headerSize)
            bw.Write(headerSize + int64 compSize)
        bw.Write(0uy) // not encrypted
        bw.Write(if isCompressed then uint32 uncompSize else 0u)
        bw.Write(compBytes)

        // Record encoded FPakEntry in EncodedPakEntries
        let encOffset = int encodedEntriesMs.Position
        let compMethodBits = (if isCompressed then 1u else 0u) <<< 23
        let blockCountBits = (if isCompressed then 1u else 0u) <<< 6
        let flags =
            (1u <<< 31) // bIsOffset32BitSafe
            ||| (1u <<< 30) // bIsUncompressedSize32BitSafe
            ||| (1u <<< 29) // bIsSize32BitSafe
            ||| compMethodBits
            ||| blockCountBits
            ||| (if isCompressed then 0x3Fu else 0u)

        encodedBw.Write(flags)
        if isCompressed then
            encodedBw.Write(uint32 uncompSize) // CompressionBlockSize
        encodedBw.Write(uint32 entryOffset)
        encodedBw.Write(uint32 uncompSize)
        if isCompressed then
            encodedBw.Write(uint32 compSize)

        match dirGroups.TryGetValue(dirName) with
        | true, list -> list.Add((fileName, encOffset))
        | false, _ ->
            let list = ResizeArray<string * int>()
            list.Add((fileName, encOffset))
            dirGroups.[dirName] <- list

    let encodedBytes = encodedEntriesMs.ToArray()

    // Build FullDirectoryIndex bytes
    use fullDirMs = new MemoryStream()
    use fullDirBw = new BinaryWriter(fullDirMs, Encoding.UTF8, true)
    fullDirBw.Write(dirGroups.Count)
    for kvp in dirGroups do
        writeFString (fullDirBw, kvp.Key)
        fullDirBw.Write(kvp.Value.Count)
        for (fName, encOff) in kvp.Value do
            writeFString (fullDirBw, fName)
            fullDirBw.Write(encOff)
    let fullDirBytes = fullDirMs.ToArray()
    let fullDirHash = SHA1.HashData(fullDirBytes)

    // Write Primary Index
    let indexOffset = ms.Position
    use primaryMs = new MemoryStream()
    use primaryBw = new BinaryWriter(primaryMs, Encoding.UTF8, true)
    writeFString (primaryBw, "../../../")
    primaryBw.Write(entryList.Length)
    primaryBw.Write(0UL) // PathHashSeed
    primaryBw.Write(0) // bReaderHasPathHashIndex = 0
    primaryBw.Write(1) // bReaderHasFullDirectoryIndex = 1

    // Calculate where FullDirectoryIndex will sit (immediately after Primary Index)
    // Primary index remaining fields: 8 (fullDirOff) + 8 (fullDirSize) + 20 (fullDirHash) + 4 (encSize) + encodedBytes.Length + 4 (filesNum)
    let primaryHeaderFixed = int primaryMs.Position + 8 + 8 + 20 + 4 + encodedBytes.Length + 4
    let fullDirOffset = indexOffset + int64 primaryHeaderFixed
    primaryBw.Write(fullDirOffset)
    primaryBw.Write(int64 fullDirBytes.Length)
    primaryBw.Write(fullDirHash)
    primaryBw.Write(encodedBytes.Length)
    primaryBw.Write(encodedBytes)
    primaryBw.Write(0) // Unencoded files count = 0

    let primaryBytes = primaryMs.ToArray()
    let primaryHash = SHA1.HashData(primaryBytes)
    bw.Write(primaryBytes)
    bw.Write(fullDirBytes)

    // Write v11 FPakInfo footer (221 bytes total: 16 GUID + 1 encrypted + 4 magic + 4 version + 8 indexOff + 8 indexSize + 20 hash + 5*32 compression names)
    bw.Write(Array.zeroCreate<byte> 16) // EncryptionKeyGuid
    bw.Write(0uy) // bEncryptedIndex = 0
    bw.Write(PakMagic)
    bw.Write(11) // PakVersion = 11
    bw.Write(indexOffset)
    bw.Write(int64 primaryBytes.Length)
    bw.Write(primaryHash)

    let compNameBuf = Array.zeroCreate<byte> 160
    let zlibNameBytes = Encoding.ASCII.GetBytes("Zlib")
    Array.Copy(zlibNameBytes, 0, compNameBuf, 0, zlibNameBytes.Length)
    bw.Write(compNameBuf)

    ms.ToArray()
