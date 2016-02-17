#I __SOURCE_DIRECTORY__
#load "load-project-debug.fsx"

open InfinityAuroraEnginePlugins.JadeEmpireContext
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.TwoDA
open System
open System.IO

let extractJadeEmpireResources(path: string, extractpath: string) = 
    let jecontext = new JadeEmpireContext(path)
    let is2DAOrDialogue(r: IGenericResource) = 
        (r.ResourceType = InfinityAuroraEnginePlugins.CommonTypes.ResType.Twoda) || (r.ResourceType = InfinityAuroraEnginePlugins.CommonTypes.ResType.Dlg)

    let toExtract = jecontext.Resources |> Array.filter is2DAOrDialogue

    let extractResource(outPath: string)(r: IGenericResource) = 
        let s = r.GetStream
        use os = new FileStream(Path.Combine(outPath, filenameForResource(r)), FileMode.Create, FileAccess.ReadWrite, FileShare.Read)
        s.CopyTo(os)

    if (not(Directory.Exists(extractpath))) then
        Directory.CreateDirectory(extractpath) |> ignore

    toExtract |> Array.iter(extractResource(extractpath))

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

let jepath = @"E:\Users\enova\SteamLibrary\steamapps\common\Jade Empire"
let extractpath = @"E:\JadeEmpireExtract"
let csvpath = Path.Combine(extractpath, "csv")

extractJadeEmpireResources(jepath, extractpath)
bulkConvert2DAsToCSVs(extractpath, csvpath)
//convertBinary2DAToCSV(csvpath)(Path.Combine(extractpath, "bodybag.2da"))

