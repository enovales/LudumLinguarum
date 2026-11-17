module RggEngine.Sllz

open StreamTools
open System
open System.IO
open System.IO.Compression
open System.Text

/// Endianness used by an SLLZ header.
type SllzEndianness =
    | LittleEndian = 0uy
    | BigEndian = 1uy

/// SLLZ compression algorithm version.
type SllzVersion =
    | Standard = 1uy
    | Zlib = 2uy

/// Parsed 16-byte SLLZ header.
type SllzHeader =
    {
        Magic: string
        Endianness: SllzEndianness
        Version: SllzVersion
        HeaderSize: uint16
        OriginalSize: uint32
        CompressedSize: uint32
    }

/// Checks whether a byte array starts with the "SLLZ" magic signature.
let isSllz (data: byte array) : bool =
    not (isNull data)
    && data.Length >= 16
    && data.[0] = byte 'S'
    && data.[1] = byte 'L'
    && data.[2] = byte 'L'
    && data.[3] = byte 'Z'

/// Reads an SLLZ header from the beginning of a stream.
let readHeader (stream: Stream) : SllzHeader =
    if stream.Length < 16L then
        raise (FormatException("SLLZ stream is shorter than the 16-byte header."))

    stream.Seek(0L, SeekOrigin.Begin) |> ignore
    use br = new BinaryReader(stream, Encoding.ASCII, true)
    let magic = Encoding.ASCII.GetString(br.ReadBytes(4))
    if magic <> "SLLZ" then
        raise (FormatException($"Invalid SLLZ magic '{magic}' (expected 'SLLZ')."))

    let endianByte = br.ReadByte()
    let versionByte = br.ReadByte()
    let rw =
        if endianByte = 0uy then
            LittleEndianReaderWrapper(br)
        elif endianByte = 1uy then
            BigEndianReaderWrapper(br)
        else
            raise (FormatException($"Unsupported SLLZ endianness byte 0x{endianByte:X2}."))

    let version =
        match versionByte with
        | 1uy -> SllzVersion.Standard
        | 2uy -> SllzVersion.Zlib
        | _ -> raise (FormatException($"Unsupported SLLZ compression version 0x{versionByte:X2}."))

    let headerSize = rw.ReadUInt16()
    let originalSize = rw.ReadUInt32()
    let compressedSize = rw.ReadUInt32()

    {
        Magic = magic
        Endianness = LanguagePrimitives.EnumOfValue endianByte
        Version = version
        HeaderSize = headerSize
        OriginalSize = originalSize
        CompressedSize = compressedSize
    }

/// Decompresses an SLLZ v1 (standard LZSS variant) payload.
let decompressStandardPayload (inputData: byte array, payloadOffset: int, payloadLength: int, decompressedSize: uint32) : byte array =
    if decompressedSize = 0u then
        [||]
    else
        if payloadLength <= 0 || payloadOffset >= inputData.Length then
            raise (FormatException("SLLZ v1 payload is empty for non-zero decompressed size."))

        let outputData = Array.zeroCreate<byte> (int decompressedSize)
        let payloadEnd = min inputData.Length (payloadOffset + payloadLength)
        let mutable inputPosition = payloadOffset
        let mutable outputPosition = 0

        let mutable flag = inputData.[inputPosition]
        inputPosition <- inputPosition + 1
        let mutable bitsRemaining = 8

        while outputPosition < int decompressedSize do
            let isBackRef = (flag &&& 0x80uy) = 0x80uy
            flag <- flag <<< 1
            bitsRemaining <- bitsRemaining - 1
            if bitsRemaining = 0 then
                if inputPosition < payloadEnd then
                    flag <- inputData.[inputPosition]
                    inputPosition <- inputPosition + 1
                bitsRemaining <- 8

            if isBackRef then
                if inputPosition + 1 >= payloadEnd then
                    raise (FormatException("Unexpected end of SLLZ v1 stream while reading back-reference."))

                let b0 = int inputData.[inputPosition]
                let b1 = int inputData.[inputPosition + 1]
                inputPosition <- inputPosition + 2

                let copyCount = (b0 &&& 0x0F) + 3
                let copyDistance = ((b0 >>> 4) ||| (b1 <<< 4)) + 1

                if outputPosition - copyDistance < 0 then
                    raise (FormatException("SLLZ v1 back-reference points before beginning of output buffer."))

                let limit = min (int decompressedSize) (outputPosition + copyCount)
                while outputPosition < limit do
                    outputData.[outputPosition] <- outputData.[outputPosition - copyDistance]
                    outputPosition <- outputPosition + 1
            else
                if inputPosition >= payloadEnd then
                    raise (FormatException("Unexpected end of SLLZ v1 stream while reading literal byte."))

                outputData.[outputPosition] <- inputData.[inputPosition]
                inputPosition <- inputPosition + 1
                outputPosition <- outputPosition + 1

        outputData

/// Decompresses a single zlib chunk inside an SLLZ v2 payload.
let private zlibDecompressChunk (data: byte array, index: int, count: int) : byte array =
    use inputMs = new MemoryStream(data, index, count, false)
    use zlibStream = new ZLibStream(inputMs, CompressionMode.Decompress)
    use outputMs = new MemoryStream()
    zlibStream.CopyTo(outputMs)
    outputMs.ToArray()

/// Decompresses an SLLZ v2 (chunked zlib) payload.
let decompressZlibPayload (inputData: byte array, payloadOffset: int, payloadLength: int, decompressedSize: uint32) : byte array =
    if decompressedSize = 0u then
        [||]
    else
        let outputData = Array.zeroCreate<byte> (int decompressedSize)
        let payloadEnd = min inputData.Length (payloadOffset + payloadLength)
        let mutable inputPosition = payloadOffset
        let mutable outputPosition = 0

        while outputPosition < int decompressedSize do
            if inputPosition + 5 > payloadEnd then
                raise (FormatException("Unexpected end of SLLZ v2 stream while reading chunk header."))

            let rawCompressedChunkSize =
                (uint32 inputData.[inputPosition] <<< 16)
                ||| (uint32 inputData.[inputPosition + 1] <<< 8)
                ||| uint32 inputData.[inputPosition + 2]

            let decompressedChunkSize =
                int (((uint32 inputData.[inputPosition + 3] <<< 8) ||| uint32 inputData.[inputPosition + 4]) + 1u)

            let isCompressed = (rawCompressedChunkSize &&& 0x00800000u) = 0u
            let compressedChunkSize = int (rawCompressedChunkSize &&& 0x007FFFFFu)

            if compressedChunkSize < 5 || inputPosition + compressedChunkSize > payloadEnd then
                raise (FormatException("Invalid SLLZ v2 chunk size."))

            if isCompressed then
                let decompressedChunk = zlibDecompressChunk (inputData, inputPosition + 5, compressedChunkSize - 5)
                if decompressedChunk.Length <> decompressedChunkSize then
                    raise (FormatException($"SLLZ v2 decompressed chunk size mismatch (expected {decompressedChunkSize}, got {decompressedChunk.Length})."))
                Array.Copy(decompressedChunk, 0, outputData, outputPosition, decompressedChunkSize)
            else
                Array.Copy(inputData, inputPosition + 5, outputData, outputPosition, decompressedChunkSize)

            inputPosition <- inputPosition + compressedChunkSize
            outputPosition <- outputPosition + decompressedChunkSize

        outputData

/// Decompresses an SLLZ-compressed byte array (either v1 Standard or v2 Zlib).
let decompress (data: byte array) : byte array =
    use ms = new MemoryStream(data, false)
    let header = readHeader ms
    let payloadOffset = int header.HeaderSize
    let payloadLength =
        if header.CompressedSize >= uint32 header.HeaderSize then
            min (data.Length - payloadOffset) (int header.CompressedSize - payloadOffset)
        else
            data.Length - payloadOffset

    match header.Version with
    | SllzVersion.Standard ->
        decompressStandardPayload (data, payloadOffset, payloadLength, header.OriginalSize)
    | SllzVersion.Zlib ->
        decompressZlibPayload (data, payloadOffset, payloadLength, header.OriginalSize)
    | _ ->
        raise (FormatException($"Unsupported SLLZ version {header.Version}."))

/// Decompresses the input if it has an SLLZ header; otherwise returns the input unchanged.
let decompressIfNeeded (data: byte array) : byte array =
    if isSllz data then
        decompress data
    else
        data

/// Writes a 16-byte SLLZ header + compressed payload into a final byte array.
let private writeHeaderAndPayload (endianness: SllzEndianness, version: SllzVersion, originalSize: uint32, payload: byte array) : byte array =
    let totalSize = 16 + payload.Length
    use ms = new MemoryStream(totalSize)
    use bw = new BinaryWriter(ms, Encoding.ASCII, true)
    bw.Write(Encoding.ASCII.GetBytes("SLLZ"))
    bw.Write(byte endianness)
    bw.Write(byte version)

    let writeU16 (v: uint16) =
        let b = BitConverter.GetBytes(v)
        let needSwap =
            (endianness = SllzEndianness.BigEndian && BitConverter.IsLittleEndian)
            || (endianness = SllzEndianness.LittleEndian && not BitConverter.IsLittleEndian)
        if needSwap then Array.Reverse(b)
        bw.Write(b)

    let writeU32 (v: uint32) =
        let b = BitConverter.GetBytes(v)
        let needSwap =
            (endianness = SllzEndianness.BigEndian && BitConverter.IsLittleEndian)
            || (endianness = SllzEndianness.LittleEndian && not BitConverter.IsLittleEndian)
        if needSwap then Array.Reverse(b)
        bw.Write(b)

    writeU16 0x0010us
    writeU32 originalSize
    writeU32 (uint32 totalSize)
    bw.Write(payload)
    ms.ToArray()

/// Compresses a byte array using SLLZ v1 (standard LZSS variant).
let compressStandard (inputData: byte array, endianness: SllzEndianness) : byte array =
    if isNull inputData || inputData.Length = 0 then
        writeHeaderAndPayload (endianness, SllzVersion.Standard, 0u, [||])
    else
        let maxWindowSize = 4096
        let maxEncodedLength = 18
        let outputBuffer = ResizeArray<byte>(inputData.Length + (inputData.Length / 8) + 16)

        let mutable flag = 0uy
        let mutable bitsRemaining = 8
        let mutable flagPosition = 0
        outputBuffer.Add(0uy)

        let mutable inputPosition = 0
        while inputPosition < inputData.Length do
            let windowSize = min inputPosition maxWindowSize
            let maxLen = min (inputData.Length - inputPosition) maxEncodedLength

            let mutable bestLen = 0
            let mutable bestDistance = 0

            if maxLen >= 3 && windowSize > 0 then
                let startSearch = inputPosition - windowSize
                for candidatePos = inputPosition - 1 downto startSearch do
                    if bestLen < maxLen && inputData.[candidatePos] = inputData.[inputPosition] then
                        let mutable len = 1
                        while len < maxLen && inputData.[candidatePos + len] = inputData.[inputPosition + len] do
                            len <- len + 1
                        if len >= 3 && len > bestLen then
                            bestLen <- len
                            bestDistance <- inputPosition - candidatePos

            if bestLen >= 3 then
                flag <- (flag <<< 1) ||| 0x01uy
                bitsRemaining <- bitsRemaining - 1
                if bitsRemaining = 0 then
                    outputBuffer.[flagPosition] <- flag
                    flag <- 0uy
                    bitsRemaining <- 8
                    flagPosition <- outputBuffer.Count
                    outputBuffer.Add(0uy)

                let encodedOffset = uint16 ((bestDistance - 1) <<< 4)
                let encodedLength = uint16 ((bestLen - 3) &&& 0x0F)
                let tuple = encodedOffset ||| encodedLength
                outputBuffer.Add(byte (tuple &&& 0xFFus))
                outputBuffer.Add(byte (tuple >>> 8))
                inputPosition <- inputPosition + bestLen
            else
                flag <- flag <<< 1
                bitsRemaining <- bitsRemaining - 1
                if bitsRemaining = 0 then
                    outputBuffer.[flagPosition] <- flag
                    flag <- 0uy
                    bitsRemaining <- 8
                    flagPosition <- outputBuffer.Count
                    outputBuffer.Add(0uy)

                outputBuffer.Add(inputData.[inputPosition])
                inputPosition <- inputPosition + 1

        flag <- flag <<< bitsRemaining
        outputBuffer.[flagPosition] <- flag

        writeHeaderAndPayload (endianness, SllzVersion.Standard, uint32 inputData.Length, outputBuffer.ToArray())

/// Compresses a byte array using SLLZ v2 (chunked zlib).
let compressZlib (inputData: byte array, endianness: SllzEndianness) : byte array =
    if isNull inputData || inputData.Length = 0 then
        writeHeaderAndPayload (endianness, SllzVersion.Zlib, 0u, [||])
    else
        use payloadMs = new MemoryStream()
        let mutable inputPosition = 0
        let mutable remaining = inputData.Length

        while remaining > 0 do
            let chunkLen = min remaining 0x10000
            let compressedChunk =
                use compMs = new MemoryStream()
                do
                    use zlibStream = new ZLibStream(compMs, CompressionLevel.Optimal, true)
                    zlibStream.Write(inputData, inputPosition, chunkLen)
                compMs.ToArray()

            let decompMinusOne = chunkLen - 1
            if compressedChunk.Length > chunkLen then
                let totalChunkSize = chunkLen + 5
                payloadMs.WriteByte(byte ((totalChunkSize >>> 16) ||| 0x80))
                payloadMs.WriteByte(byte (totalChunkSize >>> 8))
                payloadMs.WriteByte(byte totalChunkSize)
                payloadMs.WriteByte(byte (decompMinusOne >>> 8))
                payloadMs.WriteByte(byte decompMinusOne)
                payloadMs.Write(inputData, inputPosition, chunkLen)
            else
                let totalChunkSize = compressedChunk.Length + 5
                payloadMs.WriteByte(byte (totalChunkSize >>> 16))
                payloadMs.WriteByte(byte (totalChunkSize >>> 8))
                payloadMs.WriteByte(byte totalChunkSize)
                payloadMs.WriteByte(byte (decompMinusOne >>> 8))
                payloadMs.WriteByte(byte decompMinusOne)
                payloadMs.Write(compressedChunk, 0, compressedChunk.Length)

            inputPosition <- inputPosition + chunkLen
            remaining <- remaining - chunkLen

        writeHeaderAndPayload (endianness, SllzVersion.Zlib, uint32 inputData.Length, payloadMs.ToArray())
