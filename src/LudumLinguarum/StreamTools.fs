module StreamTools

open System
open System.IO

type EndianReaderWrapper(br: BinaryReader, needToConvert: bool) = 
    member this.Seek(offset: int64) = br.BaseStream.Seek(offset, SeekOrigin.Begin)
    member this.Position = br.BaseStream.Position
    member this.ReadByte() = br.ReadByte()
    member this.ReadBytes(count: int) = br.ReadBytes(count)
    member this.ReadUInt16() = 
        let bytes = br.ReadBytes(2)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt16(bytes, 0)

    member this.ReadUInt32() = 
        let bytes = br.ReadBytes(4)
        if needToConvert then Array.Reverse(bytes)
        BitConverter.ToUInt32(bytes, 0)

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

// Wrapper for reading data written in big-endian order, regardless of the endianness of the execution platform.
let BigEndianReaderWrapper(br: BinaryReader) = new EndianReaderWrapper(br, BitConverter.IsLittleEndian)

// Wrapper for reading data written in little-endian order, regardless of the endianness of the execution platform.
let LittleEndianReaderWrapper(br: BinaryReader) = new EndianReaderWrapper(br, not BitConverter.IsLittleEndian)
