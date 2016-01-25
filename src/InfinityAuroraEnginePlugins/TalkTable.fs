module InfinityAuroraEnginePlugins.TalkTable

open InfinityAuroraEnginePlugins.CommonTypes
open System.IO
open System.Text

type LanguageType = 
    | English               = 0u
    | French                = 1u
    | German                = 2u
    | Italian               = 3u
    | Spanish               = 4u
    | Polish                = 5u
    | Korean                = 128u
    | ChineseTraditional    = 129u
    | ChineseSimplified     = 130u
    | Japanese              = 131u

let LanguageTypeFromIETFLanguageTag tag = 
    match tag with
    | "en" -> LanguageType.English
    | "fr" -> LanguageType.French
    | "de" -> LanguageType.German
    | "it" -> LanguageType.Italian
    | "es" -> LanguageType.Spanish
    | "pl" -> LanguageType.Polish
    | "ko" -> LanguageType.Korean
    | "zh_CN" -> LanguageType.ChineseSimplified
    | "zh_HANS" -> LanguageType.ChineseSimplified
    | "zh_TW" -> LanguageType.ChineseTraditional
    | "zh_HANT" -> LanguageType.ChineseTraditional
    | "ja" -> LanguageType.Japanese
    | _ -> raise(exn("Unknown language type"))

type Gender = 
    | MasculineOrNeutral    = 0u
    | Feminine              = 1u

type TalkTableHeaderV3 = 
    {
        fileType: FourCC;
        fileVersion: FourCC;
        languageID: LanguageType;
        stringCount: uint32;
        stringEntriesOffset: uint32;
    }
    static member FromBinaryReader(br: BinaryReader) = 
        {
            TalkTableHeaderV3.fileType = FourCC.FromBinaryReader(br);
            fileVersion = FourCC.FromBinaryReader(br);
            languageID = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, LanguageType>(br.ReadUInt32());
            stringCount = br.ReadUInt32();
            stringEntriesOffset = br.ReadUInt32()
        }

type TalkTableHeaderV4 = 
    {
        fileType: FourCC;
        fileVersion: FourCC;
        languageID: LanguageType;
        stringCount: uint32;
        stringDataOffset: uint32;
        stringEntriesOffset: uint32
    }
    static member FromBinaryReader(br: BinaryReader) = 
        {
            TalkTableHeaderV4.fileType = FourCC.FromBinaryReader(br);
            fileVersion = FourCC.FromBinaryReader(br);
            languageID = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, LanguageType>(br.ReadUInt32());
            stringCount = br.ReadUInt32();
            stringDataOffset = br.ReadUInt32();
            stringEntriesOffset = br.ReadUInt32()
        }

type StringFlags = 
    | TextPresent        = 0b0001u
    | SoundPresent       = 0b0010u
    | SoundLengthPresent = 0b0100u

type TalkTableStringMetadataV3 = 
    {
        flags: StringFlags;
        soundResRef: ResRef;
        volumeVariance: uint32;
        pitchVariance: uint32;
        offsetToString: uint32;
        stringSize: uint32;
        soundLength: single
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            TalkTableStringMetadataV3.flags = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, StringFlags>(r.ReadUInt32());
            soundResRef = ResRef.FromBinaryReader(r);
            volumeVariance = r.ReadUInt32();
            pitchVariance = r.ReadUInt32();
            offsetToString = r.ReadUInt32();
            stringSize = r.ReadUInt32();
            // ***FIXME: TLK files before version 3 are missing this field, and should default to 0
            soundLength = r.ReadSingle()
        }

type TalkTableStringMetadataV4 = 
    {
        flags: StringFlags;
        offsetToString: uint32;
        stringSize: uint16;
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            TalkTableStringMetadataV4.flags = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, StringFlags>(r.ReadUInt32());
            offsetToString = r.ReadUInt32();
            stringSize = r.ReadUInt16()
        }

type ITalkTableString = 
    interface
        abstract member StrRef: uint32
        abstract member Value: string
    end

type TalkTableV3String private (md: TalkTableStringMetadataV3, stringData: string, index: StrRef) = 
    class
        interface ITalkTableString with
            member this.StrRef = index
            member this.Value = stringData
        member this.Metadata = md
        static member Build(metadata: TalkTableStringMetadataV3, stringStream: Stream, br: BinaryReader, offset: uint32, index: StrRef) = 
            ignore(stringStream.Seek(int64 (metadata.offsetToString + offset), SeekOrigin.Begin))
            let ansiEncoding = Encoding.GetEncoding(1252)
            let stringData = ansiEncoding.GetString(br.ReadBytes(int metadata.stringSize))
            new TalkTableV3String(metadata, stringData, index)
    end

type TalkTableV4String private (md: TalkTableStringMetadataV4, stringData: string, index: StrRef) = 
    class
        interface ITalkTableString with
            member this.StrRef = index
            member this.Value = stringData
        member this.Metadata = md
        static member Build(metadata: TalkTableStringMetadataV4, stringStream: Stream, br: BinaryReader, index: StrRef) = 
            ignore(stringStream.Seek(int64 metadata.offsetToString, SeekOrigin.Begin))
            //let ansiEncoding = Encoding.GetEncoding(1252)
            let stringData = Encoding.UTF8.GetString(br.ReadBytes(int metadata.stringSize))
            new TalkTableV4String(metadata, stringData, index)
    end

type ITalkTable<'T when 'T :> ITalkTableString> =
    interface
        abstract member Language: LanguageType
        abstract member Strings: array<'T>
    end

type TalkTableV3 private (header: TalkTableHeaderV3, strings: array<TalkTableV3String>) = 
    class
        interface ITalkTable<TalkTableV3String> with
            member this.Language = header.languageID
            member this.Strings = strings

        static member FromStream(s: Stream) = 
            let buf: byte array = Array.zeroCreate(int s.Length)
            let bytesRead = s.Read(buf, 0, int s.Length)
            use ms = new MemoryStream(buf)
            use br = new BinaryReader(ms)

            let header = TalkTableHeaderV3.FromBinaryReader(br)
            let metadata = [| for i in 1u..header.stringCount -> 
                                TalkTableStringMetadataV3.FromBinaryReader(br) |]
            let metadataWithIndices = metadata |> Array.mapi (fun i x -> (i, x))            
            let stringData = metadataWithIndices |> Array.map (fun (i, x) -> TalkTableV3String.Build(x, s, br, header.stringEntriesOffset, uint32 i))

            new TalkTableV3(header, stringData)

        static member FromFilePath(path: string) = 
            use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            TalkTableV3.FromStream(fs)
    end

type TalkTableV4 private (header: TalkTableHeaderV4, strings: array<TalkTableV4String>) =
    class
        interface ITalkTable<TalkTableV4String> with
            member this.Language = header.languageID
            member this.Strings = strings

        static member FromStream(s: Stream) = 
            let buf: byte array = Array.zeroCreate(int s.Length)
            let bytesRead = s.Read(buf, 0, int s.Length)
            use ms = new MemoryStream(buf)
            use br = new BinaryReader(ms)

            let header = TalkTableHeaderV4.FromBinaryReader(br)

            ms.Seek(int64 header.stringDataOffset, SeekOrigin.Begin) |> ignore
            let metadata = [| for i in 1u..header.stringCount -> 
                                TalkTableStringMetadataV4.FromBinaryReader(br) |]
            let metadataWithIndices = metadata |> Array.mapi (fun i x -> (i, x))            
            let stringData = metadataWithIndices |> Array.map (fun (i, x) -> TalkTableV4String.Build(x, s, br, uint32 i))

            new TalkTableV4(header, stringData)

        static member FromFilePath(path: string) = 
            use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            TalkTableV4.FromStream(fs)
    end