module InfinityAuroraEnginePlugins.NWN1Context

open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.IAResourceManager

open System.IO

type NWN1Context(rootPath: string) = 
    class
        let rm = new ResourceManager()

        let erfPaths = [
            @"nwm\Prelude.nwm";
            @"nwm\Chapter1.nwm";
            @"nwm\Chapter2.nwm";
            @"nwm\Chapter3.nwm";
            @"nwm\Chapter4.nwm";
            @"nwm\Chapter1E.nwm";
            @"nwm\Chapter2E.nwm";
            @"nwm\XP1-Chapter 1.nwm";
            @"nwm\XP1-Chapter 2.nwm";
            @"nwm\XP1-Interlude.nwm";
            @"nwm\XP2_Chapter1.nwm";
            @"nwm\XP2_Chapter2.nwm";
            @"nwm\XP2_Chapter3.nwm";
            @"nwm\Neverwinter Nights - Kingmaker.nwm";
            @"nwm\Neverwinter Nights - ShadowGuard.nwm";
            @"nwm\Neverwinter Nights - Witch's Wake.nwm"
        ]

        let keyPaths = [
            @"chitin.key";
            @"xp1.key";
            @"xp2.key";
            @"xp1patch.key";
            @"xp2patch.key"
        ]
        let existingErfs = erfPaths |> List.map (fun t -> Path.Combine(rootPath, t)) |> List.filter (fun t -> File.Exists(t))
        let existingKeys = keyPaths |> List.map (fun t -> Path.Combine(rootPath, t)) |> List.filter (fun t -> File.Exists(t))

        do
            existingErfs |> List.map (fun t -> rm.AddERF(ERFFile.FromFilePath(t), false)) |> ignore
            existingKeys |> List.map (fun t -> rm.AddBIFSet(BIFSet.FromKEYPath(t), false)) |> ignore
            rm.AddOverridePath(Path.Combine(rootPath, "override"), false)

            rm.RecalculateResources()

        member this.Resources = rm.Resources
    end
