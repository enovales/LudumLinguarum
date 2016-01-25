module TalkTableTests

open InfinityAuroraEnginePlugins.TalkTable
open NUnit.Framework
open System.IO

[<TestFixture>]
type TalkTableTests() = 
    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>] 
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()


    [<Test>]
    member this.TestLoadNWNTalkTable(): Unit = 
        let talkTable = TalkTableV3.FromFilePath(@"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\dialog.tlk")
        let fTalkTable = TalkTableV3.FromFilePath(@"C:\GOG Games\Neverwinter Nights Diamond Edition (German)\dialogF.tlk")

        Assert.IsNotEmpty((talkTable :> ITalkTable<TalkTableV3String>).Strings)
        Assert.IsNotEmpty((fTalkTable :> ITalkTable<TalkTableV3String>).Strings)

        ()