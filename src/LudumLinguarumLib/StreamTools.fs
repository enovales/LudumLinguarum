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

// Wrapper for reading data written in big-endian order, regardless of the endianness of the execution platform.
let BigEndianReaderWrapper(br: BinaryReader) = new EndianReaderWrapper(br, BitConverter.IsLittleEndian)

// Wrapper for reading data written in little-endian order, regardless of the endianness of the execution platform.
let LittleEndianReaderWrapper(br: BinaryReader) = new EndianReaderWrapper(br, not BitConverter.IsLittleEndian)
