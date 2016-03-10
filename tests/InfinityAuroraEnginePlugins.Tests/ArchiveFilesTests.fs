namespace InfinityAuroraEnginePluginsTests

open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.CommonTypes
open NUnit.Framework
open System.IO

[<TestFixture>]
type ArchiveFilesTests() = 
    let keyFilePath = @"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\chitin.key"
    let rootDataPath = @"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\"

    [<Test; Explicit>]
    member this.TestExtractNWN(): Unit = 
        let keyFile = KEYFile.FromFilePath(keyFilePath)
        let bifFiles = [for i in 0..(keyFile.BIFFilenames.Length - 1) -> BIFFile.FromKEYFile(keyFile, rootDataPath, i)]
        let bifSet = BIFSet.FromKEYAndBifs(keyFile, bifFiles)

        let outputPath = @"C:\TestNWNExtract"
        ignore(Directory.CreateDirectory(outputPath))

        ignore(keyFile.ResourceEntries |> List.map(fun kte -> bifSet.Extract(kte, outputPath)))

        ()

    [<Test; Explicit>]
    member this.ReadDLGs(): Unit = 
        let keyFile = KEYFile.FromFilePath(keyFilePath)
        let bifFiles = [for i in 0..(keyFile.BIFFilenames.Length - 1) -> BIFFile.FromKEYFile(keyFile, rootDataPath, i)]
        let bifSet = BIFSet.FromKEYAndBifs(keyFile, bifFiles)

        let dialogues = keyFile.ResourceEntries |> List.filter(fun r -> r.resource.ResType = ResType.Dlg )
        ignore (dialogues |> List.map(fun d -> bifSet.GetBinaryReader(d).Dispose()))

    [<Test; Explicit>]
    member this.ReadERF(): Unit = 
        let erfFile = ERFFile.FromFilePath(Path.Combine(rootDataPath, @"nwm\Chapter1.nwm"))
        Assert.IsNotEmpty(erfFile.Resources)
        //erfFile.Resources |> Array.map (fun t -> System.Console.WriteLine(t.resRef.Value + "." + t.resType.ToString())) |> ignore
        ()
