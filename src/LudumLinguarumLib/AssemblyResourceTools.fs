module AssemblyResourceTools

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

let extractResourcesFromAssembly(a: Assembly, c: CultureInfo) = 
    let rm = new ResourceManager(a.GetName().Name, a)
    let rs = rm.GetResourceSet(c, true, true)
    let strings = collectStringResources(rs.GetEnumerator(), [])
    strings |> Map.ofList

let loadAssemblyAndExtractResources(path: string, baseName: string, c: CultureInfo) = 
    let a = Assembly.LoadFile(path)
    extractResourcesFromAssembly(a, c)
