module InfinityAuroraEngine.SerializedGFF

open InfinityAuroraEngine.CommonTypes
open InfinityAuroraEngine.TalkTable
open System
open System.IO
open System.Text

////////////////////////////////////////////////////////////////////////////////////
// Raw structs used to deserialize the GFF files.

type GFFHeader =
    {
        fileType: FourCC;
        fileVersion: FourCC;
        structOffset: uint32;
        structCount: uint32;
        fieldOffset: uint32;
        fieldCount: uint32;
        labelOffset: uint32;
        labelCount: uint32;
        fieldDataOffset: uint32;
        fieldDataCount: uint32;
        fieldIndicesOffset: uint32;
        fieldIndicesCount: uint32;
        listIndicesOffset: uint32;
        listIndicesCount: uint32
    }
    static member FromBinaryReader(br: BinaryReader) = 
        {
            GFFHeader.fileType = FourCC.FromBinaryReader(br);
            fileVersion = FourCC.FromBinaryReader(br);
            structOffset = br.ReadUInt32();
            structCount = br.ReadUInt32();
            fieldOffset = br.ReadUInt32();
            fieldCount = br.ReadUInt32();
            labelOffset = br.ReadUInt32();
            labelCount = br.ReadUInt32();
            fieldDataOffset = br.ReadUInt32();
            fieldDataCount = br.ReadUInt32();
            fieldIndicesOffset = br.ReadUInt32();
            fieldIndicesCount = br.ReadUInt32();
            listIndicesOffset = br.ReadUInt32();
            listIndicesCount = br.ReadUInt32()
        }

type GFFFieldIndex = 
    | FieldIndex of uint32

type GFFFieldIndexOrFieldIndicesBlockOffset = 
    | FieldIndex of GFFFieldIndex
    | FieldIndicesBlockOffset of uint32

type GFFRawStructData = 
    {
        structType: uint32;
        contents: GFFFieldIndexOrFieldIndicesBlockOffset;
        fieldCount: uint32
    }
    static member FromBinaryReader(br: BinaryReader) = 
        let rawStructType = br.ReadUInt32()
        let rawDataOrDataOffset = br.ReadUInt32()
        let rawFieldCount = br.ReadUInt32()

        {
            GFFRawStructData.structType = rawStructType;
            contents = 
                if rawFieldCount = 1u then
                    GFFFieldIndexOrFieldIndicesBlockOffset.FieldIndex(GFFFieldIndex.FieldIndex(rawDataOrDataOffset))
                else
                    GFFFieldIndexOrFieldIndicesBlockOffset.FieldIndicesBlockOffset(rawDataOrDataOffset)
                ;
            fieldCount = rawFieldCount
        }

type GFFStructFieldIndices = 
    { fieldIndices: list<GFFFieldIndex> }
    static member FromBinaryReader(br: BinaryReader, count: uint32) = 
        {
            GFFStructFieldIndices.fieldIndices = 
                [ for i in 1u..count -> GFFFieldIndex.FieldIndex(br.ReadUInt32()) ]
        }

type GFFFieldType = 
    | Byte                      = 0u
    | Char                      = 1u
    | Word                      = 2u
    | Short                     = 3u
    | Dword                     = 4u
    | Int                       = 5u
    | Dword64                   = 6u
    | Int64                     = 7u
    | Float                     = 8u
    | Double                    = 9u
    | CExoString                = 10u
    | ResRef                    = 11u
    | CExoLocString             = 12u
    | UntypedBytes              = 13u
    | Struct                    = 14u
    | List                      = 15u
    | Orientation               = 16u
    | Position                  = 17u
    | StringRef                 = 18u

exception UnknownFieldType

type GFFRawValOffsetUnion = 
    | Byte of byte
    | Char of sbyte
    | Word of uint16
    | Short of int16
    | Dword of uint32
    | Int of int32
    | Float of single
    | FieldDataOffset of uint32
    | StructArrayIndex of uint32
    | ListIndicesBlockOffset of uint32
    | Vector4 of (single * single * single * single)
    | Vector3 of (single * single * single)

type GFFRawFieldData = 
    {
        fieldType: GFFFieldType;
        labelIndex: uint32;
        contents: GFFRawValOffsetUnion
    }
    static member FromBinaryReader(br: BinaryReader) = 
        let rawFieldType = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, GFFFieldType>(br.ReadUInt32())
        let rawLabelIndex = br.ReadUInt32()
        let rawDataOrDataOffset = br.ReadUInt32()

        // write out the rawDataOrDataOffset again so it can be reinterpreted
        use ms = new MemoryStream(4)
        use msbw = new BinaryWriter(ms)
        use msbr = new BinaryReader(ms)

        ignore(msbw.Write(rawDataOrDataOffset))
        ignore(ms.Seek(int64 0, SeekOrigin.Begin))

        let dataOrDataOffset = 
            match rawFieldType with
            | GFFFieldType.Byte -> GFFRawValOffsetUnion.Byte(msbr.ReadByte())
            | GFFFieldType.Char -> GFFRawValOffsetUnion.Char(msbr.ReadSByte())
            | GFFFieldType.Word -> GFFRawValOffsetUnion.Word(msbr.ReadUInt16())
            | GFFFieldType.Short -> GFFRawValOffsetUnion.Short(msbr.ReadInt16())
            | GFFFieldType.Dword -> GFFRawValOffsetUnion.Dword(msbr.ReadUInt32())
            | GFFFieldType.Int -> GFFRawValOffsetUnion.Int(msbr.ReadInt32())
            | GFFFieldType.Float -> GFFRawValOffsetUnion.Float(msbr.ReadSingle())
            | GFFFieldType.Struct -> GFFRawValOffsetUnion.StructArrayIndex(msbr.ReadUInt32())
            | GFFFieldType.List -> GFFRawValOffsetUnion.ListIndicesBlockOffset(msbr.ReadUInt32())
            | x when System.Enum.IsDefined(typeof<GFFFieldType>, x) -> GFFRawValOffsetUnion.FieldDataOffset(msbr.ReadUInt32())
            | _ -> raise <| UnknownFieldType
        {
            GFFRawFieldData.fieldType = rawFieldType;
            labelIndex = rawLabelIndex;
            contents = dataOrDataOffset
        }

type GFFRawLabel = 
    { label: string }
    static member FromBinaryReader(br: BinaryReader) = 
        {
            GFFRawLabel.label = Encoding.UTF8.GetString(br.ReadBytes(16)).TrimEnd(char 0)
        }

type GFFRawListIndex = 
    | StructIndex of uint32

type GFFRawList = 
    {
        listSize: uint32;
        indices: list<GFFRawListIndex>
    }
    static member FromBinaryReader(br: BinaryReader) = 
        let listSize = br.ReadUInt32();
        {
            GFFRawList.listSize = listSize;
            indices = [ for i in 1u..listSize -> GFFRawListIndex.StructIndex(br.ReadUInt32()) ]
        }

////////////////////////////////////////////////////////////////////////////////////
// Complex GFF data types
type GFFDword64 = uint64
type GFFInt64 = int64
type GFFDouble = float
type GFFOrientation = (single * single * single * single)
type GFFPosition = (single * single * single)
type GFFRawCExoString = 
    {
        size: uint32;
        value: string
    }
    static member FromBinaryReader(br: BinaryReader) = 
        let rawSize = br.ReadUInt32();
        {
            size = rawSize;
            value = Encoding.UTF8.GetString(br.ReadBytes(int rawSize))
        }
type GFFRawCExoLocSubStringLanguageAndGender = 
    {
        stringId: uint32
    }
    with
        member this.Language : LanguageType = 
            Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, LanguageType>(this.stringId / 2u)
        member this.Gender : Gender = 
            Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, Gender>(this.stringId % 2u)
        static member FromBinaryReader(br: BinaryReader) = 
            {
                GFFRawCExoLocSubStringLanguageAndGender.stringId = br.ReadUInt32()
            }
type GFFRawCExoLocSubString = 
    {
        languageAndGender: GFFRawCExoLocSubStringLanguageAndGender;
        length: uint32;
        value: string
    }
    static member FromBinaryReader(br: BinaryReader) = 
        let rawLanguageAndGender = GFFRawCExoLocSubStringLanguageAndGender.FromBinaryReader(br)
        let rawLength = br.ReadUInt32()
        {
            GFFRawCExoLocSubString.languageAndGender = rawLanguageAndGender;
            length = rawLength;
            value = Encoding.UTF8.GetString(br.ReadBytes(int rawLength))
        }
type GFFRawCExoLocString = 
    {
        sizeOfOtherData: uint32;
        stringRef: StrRef;
        stringCount: uint32;
        substrings: list<GFFRawCExoLocSubString>
    }
    with
    static member FromBinaryReader(br: BinaryReader) = 
        let rawSizeOfOtherData = br.ReadUInt32()
        let rawStringRef = br.ReadUInt32()
        let rawStringCount = br.ReadUInt32()
        {
            GFFRawCExoLocString.sizeOfOtherData = rawSizeOfOtherData;
            stringRef = rawStringRef;
            stringCount = rawStringCount;
            substrings = [ for i in 1u..rawStringCount -> GFFRawCExoLocSubString.FromBinaryReader(br) ]
        }
    override this.ToString() = 
        "GFFRawCExoLocString(" + 
            "sizeOfOtherData = " + this.sizeOfOtherData.ToString() + 
            ", stringRef = " + this.stringRef.ToString() + 
            ", stringCount = " + this.stringCount.ToString() + 
            ", substrings = " + this.substrings.ToString() + 
            ")"

type GFFResRef = 
    {
        resref: ResRef
    }
    static member FromBinaryReader(br: BinaryReader) = 
        let rawSize = br.ReadByte()
        { GFFResRef.resref = { ResRef.Value = (Encoding.ASCII.GetString(br.ReadBytes(int rawSize))) } }

type GFFFile(header: GFFHeader, 
             structs: GFFRawStructData array, 
             fields: GFFRawFieldData array,
             labels: GFFRawLabel array,
             fieldDataBlob: byte array,
             fieldIndicesBlob: byte array,
             listIndicesBlob: byte array) = 
    class
        let fieldDataStream = new MemoryStream(fieldDataBlob)
        let fieldIndicesStream = new MemoryStream(fieldIndicesBlob)
        let listIndicesStream = new MemoryStream(listIndicesBlob)
        let fieldDataReader = new BinaryReader(fieldDataStream)
        let fieldIndicesReader = new BinaryReader(fieldIndicesStream)
        let listIndicesReader = new BinaryReader(listIndicesStream)

        interface IDisposable with
            member this.Dispose() = 
                fieldDataStream.Dispose()
                fieldIndicesStream.Dispose()
                listIndicesStream.Dispose()
                fieldDataReader.Dispose()
                fieldIndicesReader.Dispose()
                listIndicesReader.Dispose()

        member this.Structs = structs
        member this.Fields = fields
        member this.Labels = labels
        member this.FieldDataStream = fieldDataStream
        member this.FieldIndicesStream = fieldIndicesStream
        member this.ListIndicesStream = listIndicesStream
        member this.FieldDataReader = fieldDataReader
        member this.FieldIndicesReader = fieldIndicesReader
        member this.ListIndicesReader = listIndicesReader

        static member FromStream(s: Stream) =
            use br = new BinaryReader(s)
            let header = GFFHeader.FromBinaryReader(br)

            ignore(s.Seek(int64 header.structOffset, SeekOrigin.Begin))
            let structs = [| for i in 1u..header.structCount -> GFFRawStructData.FromBinaryReader(br) |]

            ignore(s.Seek(int64 header.fieldOffset, SeekOrigin.Begin))
            let fields = [| for i in 1u..header.fieldCount -> GFFRawFieldData.FromBinaryReader(br) |]

            ignore(s.Seek(int64 header.labelOffset, SeekOrigin.Begin))
            let labels = [| for i in 1u..header.labelCount -> GFFRawLabel.FromBinaryReader(br) |]

            ignore(s.Seek(int64 header.fieldDataOffset, SeekOrigin.Begin))
            let fieldDataBlob = br.ReadBytes(int header.fieldDataCount)

            ignore(s.Seek(int64 header.fieldIndicesOffset, SeekOrigin.Begin))
            let fieldIndicesBlob = br.ReadBytes(int header.fieldIndicesCount)

            ignore(s.Seek(int64 header.listIndicesOffset, SeekOrigin.Begin))
            let listIndicesBlob = br.ReadBytes(int header.listIndicesCount)

            new GFFFile(header = header, 
                        structs = structs,
                        fields = fields,
                        labels = labels,
                        fieldDataBlob = fieldDataBlob,
                        fieldIndicesBlob = fieldIndicesBlob,
                        listIndicesBlob = listIndicesBlob)

        static member FromFilePath(filePath: string) = 
            use fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            GFFFile.FromStream(fs)
    end