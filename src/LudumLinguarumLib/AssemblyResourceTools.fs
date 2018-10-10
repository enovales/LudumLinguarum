module AssemblyResourceTools

open LLDatabase
open Mono.Cecil
open System.Collections
open System.Globalization
open System.IO
open System.Resources

// Tools for extracting localized resources from .NET assemblies and satellite assemblies.
let rec collectStringResources(e: IDictionaryEnumerator, acc: (string * string) list) = 
    match (e.Key, e.Value) with
    | ((:? string as key), (:? string as sr)) -> 
        if (e.MoveNext()) then
            collectStringResources(e, (key, sr) :: acc)
        else
            (key, sr) :: acc
    | _ -> 
        if (e.MoveNext()) then
            collectStringResources(e, acc)
        else
            acc

let extractResourcesFromAssemblyViaCecil(filename: string, c: CultureInfo, resourcesName: string) = 
    let moduleDefinition = ModuleDefinition.ReadModule(filename)

    let isMatchingEmbeddedResource(r: Resource) = 
        match r with
        | :? EmbeddedResource as er when er.Name = resourcesName -> Some(er.GetResourceStream())
        | _ -> None

    let getStringsForStream(s: Stream) = 
        let resReader = new ResourceReader(s)
        let e = resReader.GetEnumerator()
        match e.MoveNext() with
        | true -> collectStringResources(e, []) |> Map.ofList
        | _ -> Map.empty

    let resourceStreamOpt = 
        moduleDefinition.Resources
        |> Seq.tryPick isMatchingEmbeddedResource

    resourceStreamOpt
    |> Option.map getStringsForStream
    |> Option.defaultValue(Map.empty)

let createCardRecordForStrings(lid: int, keyRoot: string, language: string, gender: string)(strings: Map<string, string>) = 
    let createCardRecordForMapEntry (k, v) = 
        {
            CardRecord.ID = 0
            LessonID = lid
            Text = v
            Gender = gender
            Key = keyRoot + k + gender
            GenderlessKey = keyRoot + k
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = language
            Reversible = true
        }
    strings |> Map.toArray |> Array.map createCardRecordForMapEntry