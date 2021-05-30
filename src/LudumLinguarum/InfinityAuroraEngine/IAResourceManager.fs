module InfinityAuroraEnginePlugins.IAResourceManager

open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFF
open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.SerializedGFF
open InfinityAuroraEnginePlugins.TalkTable
open System.IO

type IGenericResource =
    interface
        abstract member Name: ResRef
        abstract member ResourceType: ResType
        abstract member GetStream: Stream
        abstract member GetBinaryReader: BinaryReader

        /// <summary>
        /// String key used to disambiguate this resource from others with the same resref in other
        /// resource containers.
        /// </summary>
        abstract member OriginDesc: string
    end

let filenameForResource(r: IGenericResource): string = 
    r.Name.Value + "." + CommonTypes.ExtensionForResType(r.ResourceType)

type IAOverrideResource = 
    {
        resRef: ResRef;
        resType: ResType;
        filePath: string
    }
    with
        interface IGenericResource with
            member this.Name = this.resRef
            member this.ResourceType = this.resType
            member this.GetStream = new FileStream(this.filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream
            member this.GetBinaryReader = new BinaryReader((this :> IGenericResource).GetStream)
            member this.OriginDesc = "override"
        end

type IABIFResource =
    {
        resourceEntry: KEYTableEntry;
        bifSet: BIFSet
    }
    with
        interface IGenericResource with
            member this.Name = this.resourceEntry.resource.ResRef
            member this.ResourceType = this.resourceEntry.resource.ResType
            member this.GetStream = this.bifSet.GetBinaryReader(this.resourceEntry).BaseStream
            member this.GetBinaryReader = this.bifSet.GetBinaryReader(this.resourceEntry)
            member this.OriginDesc = this.bifSet.KEY.FileName
        end

type IAERFResource = 
    {
        resourceEntry: ERFResourceEntry;
        erfFile: ERFFile
    }
    with
        interface IGenericResource with
            member this.Name = this.resourceEntry.resRef
            member this.ResourceType = this.resourceEntry.resType
            member this.GetStream = this.erfFile.GetBinaryReader(this.resourceEntry).BaseStream
            member this.GetBinaryReader = this.erfFile.GetBinaryReader(this.resourceEntry)
            member this.OriginDesc = this.erfFile.FileName
        end

type IARIMResource = 
    {
        resourceEntry: RIMResourceEntry;
        rimFile: RIMFile
    }
    with
        interface IGenericResource with
            member this.Name = this.resourceEntry.resRef
            member this.ResourceType = this.resourceEntry.resType
            member this.GetStream = this.rimFile.GetBinaryReader(this.resourceEntry).BaseStream
            member this.GetBinaryReader = this.rimFile.GetBinaryReader(this.resourceEntry)
            member this.OriginDesc = this.rimFile.FileName
        end

type ResourceManager() = 
    let mutable bifSets: BIFSet list = []
    let mutable erfs: ERFFile list = []
    let mutable rims: RIMFile list = []
    let mutable overrideFiles: IAOverrideResource list = []
    let mutable resourceList: IGenericResource array = [||]

    member this.RecalculateResources() =
        let bifResources = bifSets |> List.map (fun t -> t.KEY.ResourceEntries |> List.map (fun u -> { IABIFResource.resourceEntry = u; bifSet = t } :> IGenericResource) |> List.toArray) |> Array.concat
        let erfResources = erfs |> List.map (fun t -> t.Resources |> Array.map (fun u -> {IAERFResource.resourceEntry = u; erfFile = t} :> IGenericResource)) |> Array.concat
        let rimResources = rims |> List.map (fun t -> t.Resources |> Array.map (fun u -> {IARIMResource.resourceEntry = u; rimFile = t} :> IGenericResource)) |> Array.concat
        let genericOverrideResources = overrideFiles |> List.map (fun t -> t :> IGenericResource) |> List.toArray
        let combinedResources = [genericOverrideResources; erfResources; rimResources; bifResources] |> Array.concat

        resourceList <- combinedResources |> Array.distinctBy(fun t -> t.Name.Value.ToString() + t.ResourceType.ToString())

    member this.AddBIFSet(bs: BIFSet, ?recalculateOpt: bool) = 
        let recalculate = defaultArg recalculateOpt true
        bifSets <- bs :: bifSets
        if (recalculate) then
            this.RecalculateResources()

    member this.AddERF(erf: ERFFile, ?recalculateOpt: bool) = 
        let recalculate = defaultArg recalculateOpt true
        erfs <- erf :: erfs
        if (recalculate) then
            this.RecalculateResources()

    member this.AddRIM(rim: RIMFile, ?recalculateOpt: bool) = 
        let recalculate = defaultArg recalculateOpt true
        rims <- rim :: rims
        if (recalculate) then
            this.RecalculateResources()

    member this.AddOverridePath(overrideDir: string, ?recalculateOpt: bool) = 
        let recalculate = defaultArg recalculateOpt true
        let files = Directory.GetFiles(overrideDir)
        let newEntries = files |> Array.filter (fun t -> ResTypeFromFilename(t).IsSome) |> 
                            Array.map (fun t -> 
                            {
                                IAOverrideResource.resRef = { ResRef.Value = Path.GetFileNameWithoutExtension(t).Substring(0, 16) };
                                resType = ResTypeFromFilename(t).Value;
                                filePath = t
                            }) |> Array.toList
        overrideFiles <- List.concat([overrideFiles; newEntries])
        if (recalculate) then
            this.RecalculateResources()

    member this.Resources: IGenericResource array = resourceList

// Putting these here for right now until I find a better place to stick them.

let LoadGFF(rr: ResRef, rt: ResType, resources: IGenericResource seq): GFF = 
    let resOpt = resources |> Seq.tryFind(fun t -> (t.Name = rr) && (t.ResourceType = rt)) |> Option.map(fun t -> t.GetStream)
    let gffFile = GFFFile.FromStream(resOpt.Value)
    let gff = GFF.FromSerializedGFF(gffFile)
    gff

let LoadDialogue(rr: ResRef, resources: IGenericResource seq): Dialogue =
    let serializedDialogue = SerializedDialogue.FromGFF(LoadGFF(rr, ResType.Dlg, resources))
    Dialogue.FromSerialized(serializedDialogue)
