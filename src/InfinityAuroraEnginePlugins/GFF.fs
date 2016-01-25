module InfinityAuroraEnginePlugins.GFF

open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.SerializedGFF
open System.IO
open System.Text

////////////////////////////////////////////////////////////////////////////////////
// GFF object graph representation.

type GFFField = 
    | Byte of byte
    | Char of sbyte
    | Word of uint16
    | Short of int16
    | Dword of uint32
    | Int of int32
    | Dword64 of uint64
    | Int64 of int64
    | Float of single
    | Double of float
    | String of GFFRawCExoString
    | Resref of ResRef
    | LocString of GFFRawCExoLocString
    | Bytes of array<byte>
    | Struct of GFFStruct
    | List of GFFList
and GFFStruct(fields: list<GFFNamedField>) = 
    member this.Fields = fields
    member private this.GetField(name: string): GFFField option = 
        fields |> List.tryFind(fun t -> (t.Name = name)) |> Option.map (fun t -> t.Field)
    member this.GetByte(name: string): byte option =
        match this.GetField(name) with
        | Some(GFFField.Byte(v)) -> Some(v)
        | _ -> None
    member this.GetChar(name: string): sbyte option =
        match this.GetField(name) with
        | Some(GFFField.Char(v)) -> Some(v)
        | _ -> None
    member this.GetWord(name: string): uint16 option =
        match this.GetField(name) with
        | Some(GFFField.Word(v)) -> Some(v)
        | _ -> None
    member this.GetShort(name: string): int16 option =
        match this.GetField(name) with
        | Some(GFFField.Short(v)) -> Some(v)
        | _ -> None
    member this.GetDword(name: string): uint32 option =
        match this.GetField(name) with
        | Some(GFFField.Dword(v)) -> Some(v)
        | _ -> None
    member this.GetInt(name: string): int32 option =
        match this.GetField(name) with
        | Some(GFFField.Int(v)) -> Some(v)
        | _ -> None
    member this.GetDword64(name: string): uint64 option =
        match this.GetField(name) with
        | Some(GFFField.Dword64(v)) -> Some(v)
        | _ -> None
    member this.GetInt64(name: string): int64 option =
        match this.GetField(name) with
        | Some(GFFField.Int64(v)) -> Some(v)
        | _ -> None
    member this.GetFloat(name: string): single option =
        match this.GetField(name) with
        | Some(GFFField.Float(v)) -> Some(v)
        | _ -> None
    member this.GetDouble(name: string): float option =
        match this.GetField(name) with
        | Some(GFFField.Double(v)) -> Some(v)
        | _ -> None
    member this.GetString(name: string): GFFRawCExoString option =
        match this.GetField(name) with
        | Some(GFFField.String(v)) -> Some(v)
        | _ -> None
    member this.GetResRef(name: string): ResRef option =
        match this.GetField(name) with
        | Some(GFFField.Resref(v)) -> Some(v)
        | _ -> None
    member this.GetLocString(name: string): GFFRawCExoLocString option =
        match this.GetField(name) with
        | Some(GFFField.LocString(v)) -> Some(v)
        | _ -> None
    member this.GetBytes(name: string): byte array option =
        match this.GetField(name) with
        | Some(GFFField.Bytes(v)) -> Some(v)
        | _ -> None
    member this.GetStruct(name: string): GFFStruct option =
        match this.GetField(name) with
        | Some(GFFField.Struct(v)) -> Some(v)
        | _ -> None
    member this.GetList(name: string): GFFList option =
        match this.GetField(name) with
        | Some(GFFField.List(v)) -> Some(v)
        | _ -> None
and GFFList(structs: list<GFFStruct>) = 
    member this.Structs = structs
and GFFNamedField(name: string, fieldData: GFFField) = 
    member this.Name = name
    member this.Field = fieldData

type GFF(topLevelStruct: GFFStruct) = 
    class
        member this.Members = topLevelStruct

        static member private hydrateFromFieldIndices(f: GFFFile, fieldIndicesOffset: uint32, fieldCount: uint32) = 
            ignore (f.FieldIndicesStream.Seek(int64 fieldIndicesOffset, SeekOrigin.Begin))
            let fieldIndices = [| for i in 1u..fieldCount -> f.FieldIndicesReader.ReadUInt32() |]
            
            [ for i in fieldIndices -> 
                GFF.hydrateField(f, f.Fields.[int i])
            ]

        static member private hydrateStruct (f: GFFFile, structData: GFFRawStructData) = 
            let hydratedFields = 
                match structData.contents with
                | GFFFieldIndexOrFieldIndicesBlockOffset.FieldIndex(GFFFieldIndex.FieldIndex(index)) -> [GFF.hydrateField(f, f.Fields.[int index])]
                | GFFFieldIndexOrFieldIndicesBlockOffset.FieldIndicesBlockOffset(offset) -> GFF.hydrateFromFieldIndices(f, offset, structData.fieldCount)
            new GFFStruct(hydratedFields)

        static member private hydrateField (f: GFFFile, field: GFFRawFieldData) = 
            let label = f.Labels.[int field.labelIndex].label
            let fieldData = 
                match field.fieldType, field.contents with
                | _, GFFRawValOffsetUnion.Byte(data) -> GFFField.Byte(data)
                | _, GFFRawValOffsetUnion.Char(data) -> GFFField.Char(data)
                | _, GFFRawValOffsetUnion.Word(data) -> GFFField.Word(data)
                | _, GFFRawValOffsetUnion.Short(data) -> GFFField.Short(data)
                | _, GFFRawValOffsetUnion.Dword(data) -> GFFField.Dword(data)
                | _, GFFRawValOffsetUnion.Int(data) -> GFFField.Int(data)
                | _, GFFRawValOffsetUnion.Float(data) -> GFFField.Float(data)
                | GFFFieldType.Dword64, GFFRawValOffsetUnion.FieldDataOffset(offset) -> 
                    ignore(f.FieldDataStream.Seek(int64 offset, SeekOrigin.Begin))
                    GFFField.Dword64(f.FieldDataReader.ReadUInt64())
                | GFFFieldType.Int64, GFFRawValOffsetUnion.FieldDataOffset(offset) ->
                    ignore(f.FieldDataStream.Seek(int64 offset, SeekOrigin.Begin))
                    GFFField.Int64(f.FieldDataReader.ReadInt64())
                | GFFFieldType.Double, GFFRawValOffsetUnion.FieldDataOffset(offset) ->
                    ignore(f.FieldDataStream.Seek(int64 offset, SeekOrigin.Begin))
                    GFFField.Double(f.FieldDataReader.ReadDouble())
                | GFFFieldType.CExoString, GFFRawValOffsetUnion.FieldDataOffset(offset) ->
                    ignore(f.FieldDataStream.Seek(int64 offset, SeekOrigin.Begin))
                    GFFField.String(GFFRawCExoString.FromBinaryReader(f.FieldDataReader))
                | GFFFieldType.ResRef, GFFRawValOffsetUnion.FieldDataOffset(offset) ->
                    ignore(f.FieldDataStream.Seek(int64 offset, SeekOrigin.Begin))
                    GFFField.Resref(GFFResRef.FromBinaryReader(f.FieldDataReader).resref)
                | GFFFieldType.CExoLocString, GFFRawValOffsetUnion.FieldDataOffset(offset) ->
                    ignore(f.FieldDataStream.Seek(int64 offset, SeekOrigin.Begin))
                    GFFField.LocString(GFFRawCExoLocString.FromBinaryReader(f.FieldDataReader))
                | _, GFFRawValOffsetUnion.StructArrayIndex(structIndex) -> GFFField.Struct(GFF.hydrateStruct(f, f.Structs.[int structIndex]))
                | _, GFFRawValOffsetUnion.ListIndicesBlockOffset(listIndicesOffset) -> 
                    ignore(f.ListIndicesStream.Seek(int64 listIndicesOffset, SeekOrigin.Begin))
                    let rawList = GFFRawList.FromBinaryReader(f.ListIndicesReader)
                    let hydratedStructs = [for i in 0..int (rawList.listSize - 1u) -> 
                                            match rawList.indices.[i] with
                                            | GFFRawListIndex.StructIndex(index) -> GFF.hydrateStruct(f, f.Structs.[int index])]
                    GFFField.List(new GFFList(hydratedStructs))
                | _, _ -> raise <| UnknownFieldType

            new GFFNamedField(label, fieldData)


        static member FromSerializedGFF(f: GFFFile) = 
            // find the top-level struct (id 0xFFFFFFFF)
            let tls = f.Structs |> Array.head
            let hydratedTls = GFF.hydrateStruct(f, tls)
            new GFF(hydratedTls)
    end