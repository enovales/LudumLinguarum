module StreamTools

open System
open System.IO

type ReaderWrapper(br: BinaryReader) = 
    let needToConvert = BitConverter.IsLittleEndian
    member this.Seek(offset: int64) = br.BaseStream.Seek(offset, SeekOrigin.Begin)
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
