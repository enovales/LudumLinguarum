module InfinityAuroraEnginePlugins.ArchiveFiles

open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.TalkTable
open System.IO
open System.Text

type KEYHeader = 
    {
        fileType: FourCC; 
        fileVersion: FourCC; 
        bifCount: uint32; 
        keyCount: uint32; 
        offsetToFileTable: uint32; 
        offsetToKeyTable: uint32; 
        buildYear: uint32; 
        buildDay: uint32; 
        reserved32: array<byte>
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            KEYHeader.fileType = FourCC.FromBinaryReader(r);
            fileVersion = FourCC.FromBinaryReader(r);
            bifCount = r.ReadUInt32();
            keyCount = r.ReadUInt32();
            offsetToFileTable = r.ReadUInt32();
            offsetToKeyTable = r.ReadUInt32();
            buildYear = r.ReadUInt32();
            buildDay = r.ReadUInt32();
            reserved32 = r.ReadBytes(32)
        }
        

type KEYFileEntry = 
    {
        fileSize: uint32;
        filenameOffset: uint32;
        filenameSize: uint16;
        drives: uint16;
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            KEYFileEntry.fileSize = r.ReadUInt32();
            filenameOffset = r.ReadUInt32();
            filenameSize = r.ReadUInt16();
            drives = r.ReadUInt16()
        }

type KEYFilenameEntry = 
    {
        fileName: string
    }
    static member FromStreamAndBinaryReader(s: Stream, r: BinaryReader, kfe: KEYFileEntry) = 
        ignore(s.Seek(int64 kfe.filenameOffset, SeekOrigin.Begin))
        { KEYFilenameEntry.fileName = Encoding.ASCII.GetString(r.ReadBytes(int kfe.filenameSize - 1))}

type KEYTableEntry = 
    {
        resource: Resource
        resid: uint32
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            KEYTableEntry.resource = Resource.FromBinaryReader(r);
            resid = r.ReadUInt32()
        }

type KEYFile private (fileName: string, header: KEYHeader, fileTable: list<KEYFileEntry>, filenameTable: list<KEYFilenameEntry>, keyTableEntries: list<KEYTableEntry>) = 
    class
        member this.BIFFilenames = filenameTable
        member this.ResourceEntries = keyTableEntries
        member this.FileName = fileName

        static member FromStream(fileName: string, s: Stream) = 
            use br = new BinaryReader(s)

            let header = KEYHeader.FromBinaryReader(br)
            ignore(s.Seek(int64 header.offsetToFileTable, SeekOrigin.Begin))
            let bifEntries = [ for i in 1..(int header.bifCount) -> KEYFileEntry.FromBinaryReader(br) ]
            let bifFilenames = [ for i in 0..(int header.bifCount - 1) -> KEYFilenameEntry.FromStreamAndBinaryReader(s, br, bifEntries.[i]) ]
            ignore(s.Seek(int64 header.offsetToKeyTable, SeekOrigin.Begin))
            let keyEntries = [ for i in 1..(int header.keyCount) -> KEYTableEntry.FromBinaryReader(br)]

            new KEYFile(fileName, header, bifEntries, bifFilenames, keyEntries)
            
        static member FromFilePath(path: string) = 
            use fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            KEYFile.FromStream(Path.GetFileName(path).ToLowerInvariant(), fs)
    end

type BIFHeader = 
    {
        fileType: FourCC;
        version: FourCC;
        variableResourceCount: uint32;
        fixedResourceCount: uint32;
        variableTableOffset: uint32
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            BIFHeader.fileType = FourCC.FromBinaryReader(r);
            version = FourCC.FromBinaryReader(r);
            variableResourceCount = r.ReadUInt32();
            fixedResourceCount = r.ReadUInt32();
            variableTableOffset = r.ReadUInt32()
        }

type BIFVariableResourceEntry =
    {
        id: uint32;
        offset: uint32;
        fileSize: uint32;
        restype: ResType;
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            // mask off the top 12 bits, because apparently this is some distinction that doesn't matter for the tools.
            BIFVariableResourceEntry.id = r.ReadUInt32() &&& uint32 0xFFFFF;
            offset = r.ReadUInt32();
            fileSize = r.ReadUInt32();
            restype = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint16, ResType>(uint16(r.ReadUInt32()))
        }

type BIFFixedResourceEntry =
    {
        id: uint32;
        offset: uint32;
        partCount: uint32;
        fileSize: uint32;
        resType: ResType
    }
    static member FromBinaryReader(r: BinaryReader) = 
        {
            BIFFixedResourceEntry.id = r.ReadUInt32();
            offset = r.ReadUInt32();
            partCount = r.ReadUInt32();
            fileSize = r.ReadUInt32();
            resType = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint16, ResType>(r.ReadUInt16())
        }

type BIFFile private (header: BIFHeader, variableResourceTable: Map<uint32, BIFVariableResourceEntry>, fixedResourceTable: Map<uint32, BIFFixedResourceEntry>, backingStream: Stream, streamOwned: bool) = 
    class
        interface System.IDisposable with
            member this.Dispose() = 
                if streamOwned then
                    backingStream.Dispose()
                end

        static member private FromStreamInternal(k: KEYFile, s: Stream, owned: bool) = 
            use br = new BinaryReader(s, Encoding.ASCII, owned)
            let header = BIFHeader.FromBinaryReader(br)
            ignore(s.Seek(int64 header.variableTableOffset, SeekOrigin.Begin))
            let variableResources = [ for i in 1..(int header.variableResourceCount) -> BIFVariableResourceEntry.FromBinaryReader(br) ]
            let variableResourcesMap = variableResources |> List.map (fun r -> (r.id, r)) |> Map.ofList
            let fixedResources = [ for i in 1..(int header.fixedResourceCount) -> BIFFixedResourceEntry.FromBinaryReader(br) ]
            let fixedResourcesMap = fixedResources |> List.map (fun r -> (r.id, r)) |> Map.ofList
            new BIFFile(header, variableResourcesMap, fixedResourcesMap, s, owned)

        static member FromKEYFile(k: KEYFile, s: Stream) = BIFFile.FromStreamInternal(k, s, false)

        static member FromKEYFile(k: KEYFile, rootPath: string, bifIndex: int) = 
            let fileNameFromKey = k.BIFFilenames.[bifIndex].fileName
            let bifFileName =
                if (Path.IsPathRooted(fileNameFromKey)) then
                    fileNameFromKey
                else
                    Path.Combine(rootPath, fileNameFromKey)
            let fs = new FileStream(bifFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            BIFFile.FromStreamInternal(k, fs, true)

        member this.Contains(kte: KEYTableEntry): bool = 
            variableResourceTable |> Map.containsKey(kte.resid)

        member this.GetBinaryReader(kte: KEYTableEntry): BinaryReader = 
            let entry = variableResourceTable.[kte.resid]
            ignore(backingStream.Seek(int64 entry.offset, SeekOrigin.Begin))
            let fileMem = Array.zeroCreate<byte>(int entry.fileSize)
            let ms = new MemoryStream(fileMem)
            let read = backingStream.Read(fileMem, 0, int entry.fileSize)
            new BinaryReader(ms)

        member this.Extract(kte: KEYTableEntry, directory: string): Unit = 
            let entry = variableResourceTable.[kte.resid]
            use fs = new FileStream(Path.Combine(directory, kte.resource.Filename), FileMode.Create, FileAccess.Write, FileShare.ReadWrite)
            ignore(backingStream.Seek(int64 entry.offset, SeekOrigin.Begin))
            
            let rec writeNextChunk(remaining: int): Unit = 
                let bufferSize = 1024 * 1024
                let toWrite = System.Math.Min(bufferSize, remaining)
                let afterRemaining = System.Math.Max(0, remaining - toWrite)
                let buffer = Array.zeroCreate<byte>(bufferSize)
                let wasRead = backingStream.Read(buffer, 0, toWrite)
                let wasWritten = fs.Write(buffer, 0, wasRead)

                if (afterRemaining <> 0) then
                    writeNextChunk(afterRemaining)
            
            writeNextChunk(int entry.fileSize)
    end

type BIFSet private (keyFile: KEYFile, bifs: list<BIFFile>) = 
    member this.BIFs = bifs
    member this.KEY = keyFile

    member this.Extract(kte: KEYTableEntry, directory: string): Unit = 
        let bifToCheck = (kte.resid &&& 0xFFF00000u) >>> 20
        let kteForBif = { kte with resid = (kte.resid &&& 0xFFFFFu) }
        bifs.[int bifToCheck].Extract(kteForBif, directory)

    member this.GetBinaryReader(kte: KEYTableEntry): BinaryReader = 
        let bifToCheck = (kte.resid &&& 0xFFF00000u) >>> 20
        let kteForBif = { kte with resid = (kte.resid &&& 0xFFFFFu) }
        bifs.[int bifToCheck].GetBinaryReader(kteForBif)

    member this.GetBinaryReader(rr: ResRef, rt: ResType): BinaryReader option = 
        let kte = keyFile.ResourceEntries |> List.tryFind (fun t -> (t.resource.ResRef = rr) && (t.resource.ResType = rt))
        kte |> Option.map (fun k -> this.GetBinaryReader(k))

    static member FromKEYAndBifs(key: KEYFile, bifs: list<BIFFile>) = new BIFSet(key, bifs)
    static member FromKEYPath(keyPath: string) = 
        let keyFile = KEYFile.FromFilePath(keyPath)
        let bifFiles = [for i in 0..(keyFile.BIFFilenames.Length - 1) -> BIFFile.FromKEYFile(keyFile, Path.GetDirectoryName(keyPath), i)]
        BIFSet.FromKEYAndBifs(keyFile, bifFiles)

type ERFHeader =
    {
        fileType: FourCC;
        version: FourCC;
        stringCount: uint32;
        stringBlockBytes: uint32;
        entryCount: uint32;
        offsetToLocalizedString: uint32;
        offsetToKeyList: uint32;
        offsetToResourceList: uint32;
        buildYear: uint32;
        buildDay: uint32;
        descriptionStrRef: StrRef
    }
    with
        static member FromBinaryReader(br: BinaryReader) = 
            let header = {
                ERFHeader.fileType = FourCC.FromBinaryReader(br);
                version = FourCC.FromBinaryReader(br);
                stringCount = br.ReadUInt32();
                stringBlockBytes = br.ReadUInt32();
                entryCount = br.ReadUInt32();
                offsetToLocalizedString = br.ReadUInt32();
                offsetToKeyList = br.ReadUInt32();
                offsetToResourceList = br.ReadUInt32();
                buildYear = br.ReadUInt32();
                buildDay = br.ReadUInt32();
                descriptionStrRef = br.ReadUInt32()
            }
            br.ReadBytes(116) |> ignore

            header
            
type ERFString = 
    {
        languageID: LanguageType;
        gender: Gender;
        stringSize: uint32;
        stringValue: string
    }
    with
        static member FromBinaryReader(br: BinaryReader) = 
            let rawLanguageID = br.ReadUInt32()
            let rawSize = br.ReadUInt32()
            let rawString = Encoding.UTF8.GetString(br.ReadBytes(int rawSize))
            {
                ERFString.languageID = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, LanguageType>(rawLanguageID / uint32 2);
                gender = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint32, Gender>(rawLanguageID % uint32 2);
                stringSize = rawSize;
                stringValue = rawString
            }

type ERFRawKey = 
    {
        resRef: ResRef;
        resID: uint32;
        resType: ResType
    }
    with
        static member FromBinaryReader(br: BinaryReader) = 
            let key = {
                ERFRawKey.resRef = ResRef.FromBinaryReader(br);
                resID = br.ReadUInt32();
                resType = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint16, ResType>(br.ReadUInt16())
            }
            br.ReadUInt16() |> ignore
            key

type ERFRawResourceEntry = 
    {
        offset: uint32;
        size: uint32
    }
    with
        static member FromBinaryReader(br: BinaryReader) = 
            {
                ERFRawResourceEntry.offset = br.ReadUInt32();
                size = br.ReadUInt32()
            }

type ERFResourceEntry = 
    {
        resRef: ResRef;
        resID: uint32;
        resType: ResType;
        offset: uint32;
        size: uint32
    }
    with
        static member FromRaw(key: ERFRawKey, resource: ERFRawResourceEntry) = 
            {
                ERFResourceEntry.resRef = key.resRef;
                resID = key.resID;
                resType = key.resType;
                offset = resource.offset;
                size = resource.size
            }

type ERFFile private (fileName: string, header: ERFHeader, strings: ERFString list, 
                      resources: ERFResourceEntry array, backingStream: Stream, streamOwned: bool) =
    class
        interface System.IDisposable with
            member this.Dispose() = 
                if streamOwned then
                    backingStream.Dispose()
                end

        member this.Resources = resources
        member this.FileName = fileName

        member this.GetBinaryReader(entry: ERFResourceEntry): BinaryReader = 
            ignore(backingStream.Seek(int64 entry.offset, SeekOrigin.Begin))
            let fileMem = Array.zeroCreate<byte>(int entry.size)
            let ms = new MemoryStream(fileMem)
            let read = backingStream.Read(fileMem, 0, int entry.size)
            new BinaryReader(ms)

        static member FromStream(fileName: string, s: Stream, owned: bool) = 
            let br = new BinaryReader(s)

            let header = ERFHeader.FromBinaryReader(br)
            s.Seek(int64 header.offsetToLocalizedString, SeekOrigin.Begin) |> ignore
            let strings = [ for i in 1u..header.stringCount -> ERFString.FromBinaryReader(br) ]
            s.Seek(int64 header.offsetToKeyList, SeekOrigin.Begin) |> ignore
            let rawKeys = [| for i in 1u..header.entryCount -> ERFRawKey.FromBinaryReader(br) |]
            s.Seek(int64 header.offsetToResourceList, SeekOrigin.Begin) |> ignore
            let rawResources = [| for i in 1u..header.entryCount -> ERFRawResourceEntry.FromBinaryReader(br) |]
            let resources = [| for i in 1u..(header.entryCount) -> ERFResourceEntry.FromRaw(rawKeys.[(int i) - 1], rawResources.[(int i) - 1]) |]

            new ERFFile(fileName, header, strings, resources, s, owned)
            
        static member FromFilePath(path: string) = 
            let fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            ERFFile.FromStream(Path.GetFileName(path).ToLowerInvariant(), fs, true)
    end

type RIMHeader = 
    {
        fileType: FourCC;
        version: FourCC;
        reserved: uint32;
        entryCount: uint32;
        offsetToResourceList: uint32;
    }
    with
        static member FromBinaryReader(br: BinaryReader) = 
            let header = {
                RIMHeader.fileType = FourCC.FromBinaryReader(br);
                version = FourCC.FromBinaryReader(br);
                reserved = br.ReadUInt32();
                entryCount = br.ReadUInt32();
                offsetToResourceList = br.ReadUInt32();
            }
            br.ReadBytes(130) |> ignore

            header

type RIMResourceEntry = 
    {
        resRef: ResRef;
        resType: ResType;
        resID: uint32;
        offset: uint32;
        size: uint32
    }
    with
        static member FromBinaryReader(br: BinaryReader) = 
            {
                RIMResourceEntry.resRef = ResRef.FromBinaryReader(br);
                resType = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint16, ResType>(uint16(br.ReadUInt32()));
                resID = br.ReadUInt32();
                offset = br.ReadUInt32();
                size = br.ReadUInt32()
            }

type RIMFile private (fileName: string, header: RIMHeader, 
                      resources: RIMResourceEntry array, backingStream: Stream, streamOwned: bool) =
    class
        interface System.IDisposable with
            member this.Dispose() = 
                if streamOwned then
                    backingStream.Dispose()
                end

        member this.Resources = resources
        member this.FileName = fileName

        member this.GetBinaryReader(entry: RIMResourceEntry): BinaryReader = 
            ignore(backingStream.Seek(int64 entry.offset, SeekOrigin.Begin))
            let fileMem = Array.zeroCreate<byte>(int entry.size)
            let ms = new MemoryStream(fileMem)
            let read = backingStream.Read(fileMem, 0, int entry.size)
            new BinaryReader(ms)

        static member FromStream(fileName: string, s: Stream, owned: bool) = 
            let br = new BinaryReader(s)

            let header = RIMHeader.FromBinaryReader(br)
            s.Seek(int64 header.offsetToResourceList, SeekOrigin.Begin) |> ignore
            let resources = [| for i in 1u..header.entryCount -> RIMResourceEntry.FromBinaryReader(br) |]
            new RIMFile(fileName, header, resources, s, owned)
            
        static member FromFilePath(path: string) = 
            let fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            RIMFile.FromStream(Path.GetFileName(path).ToLowerInvariant(), fs, true)
    end
    