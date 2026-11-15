module RGGStudio.ParArchive

open System
open System.IO
open System.IO.Compression
open System.Text

// PAR (PARC) archives package the data of RGG Studio games (Yakuza / Like a
// Dragon). The whole archive or individual entries can be compressed with the
// custom SLLZ scheme (v1: LZ77-style, v2: zlib chunks). Format references:
// Kaplas80/ParManager (MIT) and gibbed/Gibbed.Yakuza0.

type ParEntry =
    {
        path: string
        data: byte array
    }

type private ParNode =
    | ParDirectory of ParDirectoryInfo
    | ParFile of ParFileInfo

and private ParDirectoryInfo =
    {
        name: string
        folderCount: int
        firstFolderIndex: int
        fileCount: int
        firstFileIndex: int
        attributes: int
        unused1: int
        unused2: int
        unused3: int
    }

and private ParFileInfo =
    {
        name: string
        isCompressed: bool
        offset: int64
        compressedSize: uint32
        decompressedSize: uint32
    }

let private decompressSllzV1(data: byte array)(decompressedSize: int): byte array =
    let output = Array.zeroCreate<byte> decompressedSize
    let mutable inputPosition = 0
    let mutable outputPosition = 0
    if data.Length = 0 then raise(InvalidDataException("SLLZ: empty compressed data"))

    let mutable flag = data.[inputPosition]
    inputPosition <- inputPosition + 1
    let mutable flagCount = 8

    let readFlagBit() =
        flag <- (flag <<< 1)
        flagCount <- flagCount - 1
        if flagCount = 0 then
            if inputPosition >= data.Length then raise(InvalidDataException("SLLZ: truncated flag stream"))
            flag <- data.[inputPosition]
            inputPosition <- inputPosition + 1
            flagCount <- 8

    while outputPosition < decompressedSize do
        if (flag &&& 0x80uy) = 0x80uy then
            readFlagBit()
            if inputPosition + 1 >= data.Length then raise(InvalidDataException("SLLZ: truncated copy token"))
            let copyFlags = uint16(int(data.[inputPosition]) ||| (int(data.[inputPosition + 1]) <<< 8))
            inputPosition <- inputPosition + 2
            let copyDistance = 1 + (int copyFlags >>> 4)
            let copyCount = 3 + (int copyFlags &&& 0xF)
            if copyDistance > outputPosition then raise(InvalidDataException("SLLZ: copy distance before start of output"))
            if outputPosition + copyCount > decompressedSize then raise(InvalidDataException("SLLZ: copy overruns output buffer"))
            for _ = 1 to copyCount do
                output.[outputPosition] <- output.[outputPosition - copyDistance]
                outputPosition <- outputPosition + 1
        else
            readFlagBit()
            if inputPosition >= data.Length then raise(InvalidDataException("SLLZ: truncated literal stream"))
            output.[outputPosition] <- data.[inputPosition]
            inputPosition <- inputPosition + 1
            outputPosition <- outputPosition + 1

    output

let private zlibDecompress(data: byte array)(index: int)(count: int): byte array =
    use input = new MemoryStream(data, index, count)
    use output = new MemoryStream()
    use zlibStream = new ZLibStream(input, CompressionMode.Decompress)
    zlibStream.CopyTo(output)
    output.ToArray()

let private decompressSllzV2(data: byte array)(decompressedSize: int): byte array =
    let output = Array.zeroCreate<byte> decompressedSize
    let mutable inputPosition = 0
    let mutable outputPosition = 0

    while outputPosition < decompressedSize do
        if inputPosition + 5 > data.Length then raise(InvalidDataException("SLLZ: truncated chunk header"))
        let compressedChunkSize = (int(data.[inputPosition]) <<< 16) ||| (int(data.[inputPosition + 1]) <<< 8) ||| int(data.[inputPosition + 2])
        let decompressedChunkSize = ((int(data.[inputPosition + 3]) <<< 8) ||| int(data.[inputPosition + 4])) + 1
        let isCompressed = (compressedChunkSize &&& 0x00800000) = 0

        if isCompressed then
            let chunk = zlibDecompress data (inputPosition + 5) (compressedChunkSize - 5)
            if chunk.Length <> decompressedChunkSize then raise(InvalidDataException("SLLZ: wrong decompressed chunk size"))
            if outputPosition + chunk.Length > decompressedSize then raise(InvalidDataException("SLLZ: chunk overruns output buffer"))
            Array.Copy(chunk, 0, output, outputPosition, chunk.Length)
            inputPosition <- inputPosition + compressedChunkSize
        else
            let chunkSize = compressedChunkSize &&& 0xFF7FFFFF
            if inputPosition + 5 + decompressedChunkSize > data.Length then raise(InvalidDataException("SLLZ: truncated uncompressed chunk"))
            if outputPosition + decompressedChunkSize > decompressedSize then raise(InvalidDataException("SLLZ: chunk overruns output buffer"))
            Array.Copy(data, inputPosition + 5, output, outputPosition, decompressedChunkSize)
            inputPosition <- inputPosition + chunkSize

        outputPosition <- outputPosition + decompressedChunkSize

    output

let internal decompressSllz(stream: Stream): byte array =
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    if stream.Length < 0x10L then raise(InvalidDataException("SLLZ: stream too short"))
    use reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen = true)
    let magic = reader.ReadBytes(4)
    if magic <> [| 0x53uy; 0x4Cuy; 0x4Cuy; 0x5Auy |] then raise(InvalidDataException("SLLZ: bad magic"))

    let endianness = reader.ReadByte()
    let version = reader.ReadByte()
    let isLittleEndian = endianness = 0uy
    let readInt32() =
        let b = reader.ReadBytes(4)
        if isLittleEndian <> BitConverter.IsLittleEndian then Array.Reverse(b)
        BitConverter.ToInt32(b, 0)
    let readInt16() =
        let b = reader.ReadBytes(2)
        if isLittleEndian <> BitConverter.IsLittleEndian then Array.Reverse(b)
        BitConverter.ToInt16(b, 0)

    let _headerSize = readInt16()
    let decompressedSize = readInt32()
    let compressedSize = readInt32()

    stream.Seek(int64 _headerSize, SeekOrigin.Begin) |> ignore
    let payloadLength = compressedSize - int _headerSize
    if payloadLength <= 0 || payloadLength > int(stream.Length - int64 _headerSize) then
        raise(InvalidDataException("SLLZ: invalid compressed size"))
    let payload = reader.ReadBytes(payloadLength)

    match version with
    | 1uy -> decompressSllzV1 payload decompressedSize
    | 2uy -> decompressSllzV2 payload decompressedSize
    | _ -> raise(InvalidDataException(sprintf "SLLZ: unknown version %d" (int version)))

type private ParcReader(br: BinaryReader, isLittleEndian: bool) =
    let readBytes(count: int) = br.ReadBytes(count)
    let readInt32() =
        let b = readBytes 4
        if isLittleEndian <> BitConverter.IsLittleEndian then Array.Reverse(b)
        BitConverter.ToInt32(b, 0)
    let readUInt32() =
        let b = readBytes 4
        if isLittleEndian <> BitConverter.IsLittleEndian then Array.Reverse(b)
        BitConverter.ToUInt32(b, 0)

    member this.ReadInt32() = readInt32()
    member this.ReadUInt32() = readUInt32()
    member this.ReadStringFixed(count: int) =
        let bytes = readBytes count
        let mutable length = 0
        while length < bytes.Length && bytes.[length] <> 0uy do
            length <- length + 1
        Encoding.ASCII.GetString(bytes, 0, length)

let private readParc(stream: Stream): ParEntry array =
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    use reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen = true)
    let magic = Encoding.ASCII.GetString(reader.ReadBytes(4))
    if magic <> "PARC" then raise(InvalidDataException("PARC: bad magic"))

    let _platformId = reader.ReadByte()
    let endianness = reader.ReadByte()
    let isLittleEndian = endianness = 0uy
    reader.ReadBytes(2) |> ignore

    let pr = ParcReader(reader, isLittleEndian)
    let _version = pr.ReadInt32()
    let _dataSize = pr.ReadInt32()

    let totalFolderCount = pr.ReadInt32()
    let folderInfoOffset = pr.ReadInt32()
    let totalFileCount = pr.ReadInt32()
    let fileInfoOffset = pr.ReadInt32()

    if totalFolderCount < 1 || totalFolderCount > 65536 || totalFileCount < 0 || totalFileCount > 1000000 then
        raise(InvalidDataException("PARC: implausible counts"))

    let folderNames =
        [| for _ in 1 .. totalFolderCount do
            let name = pr.ReadStringFixed(0x40)
            if String.IsNullOrEmpty(name) then "." else name |]
    let fileNames = [| for _ in 1 .. totalFileCount -> pr.ReadStringFixed(0x40) |]

    stream.Seek(int64 folderInfoOffset, SeekOrigin.Begin) |> ignore
    let folders =
        [| for i in 0 .. totalFolderCount - 1 ->
            {
                name = folderNames.[i]
                folderCount = pr.ReadInt32()
                firstFolderIndex = pr.ReadInt32()
                fileCount = pr.ReadInt32()
                firstFileIndex = pr.ReadInt32()
                attributes = pr.ReadInt32()
                unused1 = pr.ReadInt32()
                unused2 = pr.ReadInt32()
                unused3 = pr.ReadInt32()
            } : ParDirectoryInfo |]

    stream.Seek(int64 fileInfoOffset, SeekOrigin.Begin) |> ignore
    let files =
        [| for i in 0 .. totalFileCount - 1 ->
            let compressionFlag = pr.ReadUInt32()
            let size = pr.ReadUInt32()
            let compressedSize = pr.ReadUInt32()
            let baseOffset = pr.ReadUInt32()
            let _attributes = pr.ReadInt32()
            let extendedOffset = pr.ReadUInt32()
            let _timestamp = reader.ReadBytes(8)
            let offset = int64(uint64(extendedOffset) <<< 32 ||| uint64(baseOffset)) &&& 0x00FFFFFFFFFFFFFFL
            {
                name = fileNames.[i]
                isCompressed = compressionFlag = 0x80000000u
                offset = offset
                compressedSize = compressedSize
                decompressedSize = size
            } : ParFileInfo |]

    let readEntryData(fileInfo: ParFileInfo) =
        stream.Seek(fileInfo.offset, SeekOrigin.Begin) |> ignore
        let compressedBytes = reader.ReadBytes(int fileInfo.compressedSize)
        if fileInfo.isCompressed then
            use compressedStream = new MemoryStream(compressedBytes)
            decompressSllz compressedStream
        else
            compressedBytes

    let entries = ResizeArray<ParEntry>()

    let rec walkFolder(index: int)(parentPath: string) =
        let folder = folders.[index]
        let folderPath =
            if parentPath = "" then folder.name else parentPath + "/" + folder.name
        let pathPrefix = if folderPath = "." then "" else folderPath

        for i = folder.firstFolderIndex to folder.firstFolderIndex + folder.folderCount - 1 do
            if i < 0 || i >= folders.Length then raise(InvalidDataException("PARC: folder index out of range"))
            walkFolder i pathPrefix

        for i = folder.firstFileIndex to folder.firstFileIndex + folder.fileCount - 1 do
            if i < 0 || i >= files.Length then raise(InvalidDataException("PARC: file index out of range"))
            let fileInfo = files.[i]
            let entryPath =
                let fileName = if String.IsNullOrEmpty(fileInfo.name) then sprintf "file_%d" i else fileInfo.name
                if pathPrefix = "" then fileName else pathPrefix + "/" + fileName
            entries.Add({ path = entryPath; data = readEntryData fileInfo })

    walkFolder 0 ""
    entries.ToArray()

/// Reads all entries of a PAR archive. The archive may itself be SLLZ
/// compressed; individual entries may be SLLZ compressed.
let readParArchive(stream: Stream): ParEntry array =
    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    let magicBytes = Array.zeroCreate<byte> 4
    let read = stream.Read(magicBytes, 0, 4)
    if read < 4 then raise(InvalidDataException("Not a PAR archive"))

    if Encoding.ASCII.GetString(magicBytes) = "SLLZ" then
        use decompressed = new MemoryStream(decompressSllz stream)
        readParc decompressed
    else
        readParc stream

let readParArchiveFromFile(path: string): ParEntry array =
    use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
    readParArchive stream
