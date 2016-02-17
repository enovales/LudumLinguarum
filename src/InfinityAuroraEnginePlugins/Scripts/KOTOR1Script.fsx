#I __SOURCE_DIRECTORY__
#load "load-project-debug.fsx"

open InfinityAuroraEnginePlugins.KOTOR1Context
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.TwoDA
open System
open System.IO

(*
let extractKOTOR1Resources(path: string, extractpath: string) = 
    let k1context = new KOTOR1Context(path)
    let twoDAs = k1context.Resources |> Array.filter(fun t -> t.ResourceType = InfinityAuroraEnginePlugins.CommonTypes.ResType.Twoda)

    let extractResource(outPath: string)(r: IGenericResource) = 
        let s = r.GetStream
        use os = new FileStream(Path.Combine(outPath, filenameForResource(r)), FileMode.Create, FileAccess.ReadWrite, FileShare.Read)
        s.CopyTo(os)

    if (not(Directory.Exists(extractpath))) then
        Directory.CreateDirectory(extractpath) |> ignore

    twoDAs |> Array.iter(extractResource(extractpath))
*)

// Convert a single binary 2DA file into a CSV.
let convertBinary2DAToCSV(outdir: string)(binpath: string) = 
    let twoda = TwoDAFile.FromFile(binpath)
    twoda.WriteCSV(Path.Combine(outdir, Path.ChangeExtension(Path.GetFileName(binpath), ".csv")))

// Bulk convert all 2DAs from the game into CSVs.
let bulkConvert2DAsToCSVs(extractpath: string, csvpath: string) = 
    let binary2dafilenames = Directory.GetFiles(extractpath, "*.2da")
    if (not(Directory.Exists(csvpath))) then
        Directory.CreateDirectory(csvpath) |> ignore

    let exceptionHandlingConverter(outdir: string)(binpath: string) = 
        try
            convertBinary2DAToCSV(outdir)(binpath)
        with
            x -> Console.WriteLine("failed to convert " + binpath)

    binary2dafilenames |> Array.iter(exceptionHandlingConverter(csvpath))

let k1path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor"
let extractpath = @"C:\KOTOR1Extract2DAs"
let csvpath = Path.Combine(extractpath, "csv")

//extractKOTOR1Resources(k1path, extractpath)
bulkConvert2DAsToCSVs(extractpath, csvpath)
//convertBinary2DAToCSV(csvpath)(Path.Combine(extractpath, "bodybag.2da"))
