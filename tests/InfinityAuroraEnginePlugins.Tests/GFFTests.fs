module GFFTests

open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.SerializedGFF
open InfinityAuroraEnginePlugins.GFF
open NUnit.Framework
open System.IO

[<TestFixture>]
type GFFTests() = 
    let keyFilePath = @"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\chitin.key"

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>] 
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()


    [<Test>]
    member this.TestLoadGFF(): Unit = 
        let bifSet = BIFSet.FromKEYPath(keyFilePath)
        let resOpt = bifSet.GetBinaryReader({ ResRef.Value = "nw_hen_bod" }, ResType.Dlg)
        let gffFile = GFFFile.FromStream(resOpt.Value.BaseStream)
        let gff = GFF.FromSerializedGFF(gffFile)
        Assert.IsNotEmpty(gff.Members.Fields)
        ()

    [<Test>]
    member this.DumpResourceTypes(): Unit = 
        let bifSet = BIFSet.FromKEYPath(keyFilePath)
        let resourceGroups = bifSet.KEY.ResourceEntries |> List.toArray |> Array.groupBy (fun t -> t.resource.ResType)
        resourceGroups |> Array.map (fun (restype, l) -> System.Console.WriteLine(restype.ToString() + ": " + l.Length.ToString())) |> ignore
        ()