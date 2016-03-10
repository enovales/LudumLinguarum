﻿module AssemblyResourceTools

open LLDatabase
open System.Collections
open System.Globalization
open System.Reflection
open System.Resources
open System.Threading

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


let createResourceManagerForAssembly(a: Assembly, baseName: string): ResourceManager = 
    new ResourceManager(baseName, a)

let extractResourcesFromAssemblyViaResourceReader(a: Assembly, c: CultureInfo, resourcesName: string) = 
    use stream = a.GetManifestResourceStream(resourcesName)
    let resReader = new ResourceReader(stream)
    let e = resReader.GetEnumerator()
    e.MoveNext() |> ignore
    let strings = collectStringResources(e, [])
    strings |> Map.ofList

let createCardRecordForStrings(lid: int, keyRoot: string, language: string)(strings: Map<string, string>) = 
    let createCardRecordForMapEntry (k, v) = 
        {
            CardRecord.ID = 0
            LessonID = lid
            Text = v
            Gender = "masculine"
            Key = keyRoot + k
            GenderlessKey = keyRoot + k
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = language
            Reversible = true
        }
    strings |> Map.toArray |> Array.map createCardRecordForMapEntry