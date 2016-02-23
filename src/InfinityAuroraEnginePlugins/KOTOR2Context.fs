module InfinityAuroraEnginePlugins.KOTOR2Context

open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.IAResourceManager

open System.IO

type KOTOR2Context(rootPath: string) = 
    class
        let rm = new ResourceManager()

        let erfPaths = 
            [| 
                [| Path.Combine(rootPath, @"patch.erf") |]
                Directory.GetFiles(Path.Combine(rootPath, @"lips"), "*.mod", SearchOption.AllDirectories);
                Directory.GetFiles(Path.Combine(rootPath, @"modules"), "*.erf", SearchOption.AllDirectories);
                Directory.GetFiles(Path.Combine(rootPath, @"TexturePacks"), "*.erf", SearchOption.AllDirectories)
            |] 
            |> Array.collect(fun t -> t) 
            |> Array.map(fun t -> t.Substring(rootPath.Length + 1))

        let rimPaths = 
            [|
                Directory.GetFiles(Path.Combine(rootPath, @"modules"), "*.rim", SearchOption.AllDirectories)
            |]
            |> Array.collect(fun t -> t)
            |> Array.map(fun t -> t.Substring(rootPath.Length + 1))

        let keyPaths = [|
            @"chitin.key"
        |]

        let buildPath(t) = Path.Combine(rootPath, t)
        let fileExistsFilter(t) = File.Exists(t)
        let existingErfs = erfPaths |> Array.map buildPath |> Array.filter fileExistsFilter
        let existingRims = rimPaths |> Array.map buildPath |> Array.filter fileExistsFilter
        let existingKeys = keyPaths |> Array.map buildPath |> Array.filter fileExistsFilter

        do
            existingErfs |> Array.map (fun t -> rm.AddERF(ERFFile.FromFilePath(t), false)) |> ignore
            existingRims |> Array.map (fun t -> rm.AddRIM(RIMFile.FromFilePath(t), false)) |> ignore
            existingKeys |> Array.map (fun t -> rm.AddBIFSet(BIFSet.FromKEYPath(t), false)) |> ignore
            rm.AddOverridePath(Path.Combine(rootPath, "override"), false)

            rm.RecalculateResources()

        member this.Resources = rm.Resources
    end
