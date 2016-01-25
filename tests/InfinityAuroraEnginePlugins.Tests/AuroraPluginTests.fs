module AuroraPluginTests

open LLDatabase
open LudumLinguarumPlugins
open InfinityAuroraEnginePlugins
open NUnit.Framework
open System.IO

[<TestFixture>]
type AuroraPluginTests() = 
    let rootPathGerman = @"C:\GOG Games\Neverwinter Nights Diamond Edition (German)"
    let rootPathEnglish = @"C:\GOG Games\Neverwinter Nights Diamond Edition"
    let mutable plugin: AuroraPlugin = new AuroraPlugin()
    let mutable db: LLDatabase = new LLDatabase(":memory:")

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>] 
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = 
        plugin <- new AuroraPlugin()
        db <- new LLDatabase(":memory:")
        ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test; Explicit>]
    member this.TestExtractNWN1() = 
        (plugin :> IGameExtractorPlugin).Load(System.Console.Out)
        (plugin :> IGameExtractorPlugin).ExtractAll("Neverwinter Nights", rootPathEnglish, db)

        Assert.IsNotEmpty(db.Games)
        Assert.IsNotEmpty(db.Lessons)
        Assert.IsNotEmpty(db.Cards)

        System.Console.WriteLine("extracted " + db.Cards.Length.ToString() + " cards from NWN1") |> ignore
        ()