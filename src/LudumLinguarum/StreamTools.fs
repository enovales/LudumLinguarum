module StreamTools

open System
open System.IO
open System.Text

type EndianReaderWrapper(br: BinaryReader, needToConvert: bool) = 
    member this.BaseStream = br.BaseStream
    member this.Length = br.BaseStream.Length
    member this.Seek(offset: int64) = br.BaseStream.Seek(offset, SeekOrigin.Begin)
    member this.Position = br.BaseStream.Position
    member this.ReadByte() = br.ReadByte()
    member this.ReadSByte() = br.ReadSByte()
    member this.ReadBytes(count: int) = br.ReadBytes(count)
    member this.ReadInt16() =
        let bytes = br.ReadBytes(2)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToInt16(bytes, 0)

    member this.ReadUInt16() = 
        let bytes = br.ReadBytes(2)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt16(bytes, 0)

    member this.ReadInt32() =
        let bytes = br.ReadBytes(4)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToInt32(bytes, 0)

    member this.ReadUInt32() = 
        let bytes = br.ReadBytes(4)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt32(bytes, 0)

    member this.ReadInt64() =
        let bytes = br.ReadBytes(8)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToInt64(bytes, 0)

    member this.ReadUInt64() =
        let bytes = br.ReadBytes(8)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt64(bytes, 0)

    member this.ReadSingle() =
        let bytes = br.ReadBytes(4)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToSingle(bytes, 0)

    member this.ReadDouble() =
        let bytes = br.ReadBytes(8)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToDouble(bytes, 0)

    member this.ReadFixedString(byteCount: int, encoding: Encoding) =
        let bytes = br.ReadBytes(byteCount)
        let nullIdx = Array.IndexOf(bytes, 0uy)
        let len = if nullIdx >= 0 then nullIdx else bytes.Length
        encoding.GetString(bytes, 0, len)

    member this.ReadNullTerminatedString(encoding: Encoding) =
        use ms = new MemoryStream()
        let mutable keepReading = true
        while keepReading && br.BaseStream.Position < br.BaseStream.Length do
            let b = br.ReadByte()
            if b = 0uy then
                keepReading <- false
            else
                ms.WriteByte(b)
        encoding.GetString(ms.ToArray())

    member this.ReadNullTerminatedStringAt(offset: int64, encoding: Encoding) =
        let savedPos = br.BaseStream.Position
        try
            br.BaseStream.Seek(offset, SeekOrigin.Begin) |> ignore
            this.ReadNullTerminatedString(encoding)
        finally
            br.BaseStream.Seek(savedPos, SeekOrigin.Begin) |> ignore

// Read-only view over a seekable stream, treating the given offset as position
// zero. Useful for reading data (such as a ZIP archive) that is embedded at an
// offset within a larger file, e.g. a LÖVE2D executable with a fused game archive.
type OffsetStream(baseStream: Stream, offset: int64) =
    inherit Stream()
    override _.CanRead = baseStream.CanRead
    override _.CanSeek = baseStream.CanSeek
    override _.CanWrite = false
    override _.Length = baseStream.Length - offset
    override _.Position
        with get() = baseStream.Position - offset
        and set(value) = baseStream.Position <- offset + value
    override _.Flush() = ()
    override _.Read(buffer, offset', count) = baseStream.Read(buffer, offset', count)
    override _.Seek(position, origin) =
        let target =
            match origin with
            | SeekOrigin.Begin -> position
            | SeekOrigin.Current -> baseStream.Position - offset + position
            | _ -> baseStream.Length - offset + position
        baseStream.Seek(offset + target, SeekOrigin.Begin)
    override _.SetLength(_) = raise(NotSupportedException())
    override _.Write(_, _, _) = raise(NotSupportedException())

// Bounded read-only view over a slice [offset .. offset + length) of a seekable stream.
type SubStream(baseStream: Stream, offset: int64, length: int64) =
    inherit Stream()
    let mutable pos = 0L
    override _.CanRead = baseStream.CanRead
    override _.CanSeek = baseStream.CanSeek
    override _.CanWrite = false
    override _.Length = length
    override _.Position
        with get() = pos
        and set(value) = pos <- max 0L (min length value)
    override _.Flush() = ()
    override _.Read(buffer, bufferOffset, count) =
        let remaining = int (max 0L (length - pos))
        let toRead = min count remaining
        if toRead <= 0 then
            0
        else
            baseStream.Seek(offset + pos, SeekOrigin.Begin) |> ignore
            let bytesRead = baseStream.Read(buffer, bufferOffset, toRead)
            pos <- pos + int64 bytesRead
            bytesRead
    override _.Seek(position, origin) =
        let target =
            match origin with
            | SeekOrigin.Begin -> position
            | SeekOrigin.Current -> pos + position
            | _ -> length + position
        pos <- max 0L (min length target)
        pos
    override _.SetLength(_) = raise(NotSupportedException())
    override _.Write(_, _, _) = raise(NotSupportedException())

// Wrapper for reading data written in big-endian order, regardless of the endianness of the execution platform.
let BigEndianReaderWrapper(br: BinaryReader) = new EndianReaderWrapper(br, BitConverter.IsLittleEndian)

// Wrapper for reading data written in little-endian order, regardless of the endianness of the execution platform.
let LittleEndianReaderWrapper(br: BinaryReader) = new EndianReaderWrapper(br, not BitConverter.IsLittleEndian)

