module JadeEmpireContextTests

open LLDatabase
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.JadeEmpireContext
open InfinityAuroraEnginePlugins.TalkTable
open InfinityAuroraEnginePlugins.TwoDA
open NUnit.Framework
open System.IO

[<TestFixture>]
type JadeEmpireContextTests() = 
    let rootPathGerman = @"C:\TempGameCopies\Jade Empire (German)"
    let rootPathEnglish = @"C:\Program Files (x86)\Steam\steamapps\common\Jade Empire"

    let englishTalkTable = TalkTableV4.FromFilePath(@"C:\Program Files (x86)\Steam\steamapps\common\Jade Empire\dialog.tlk")
    let germanTalkTable = TalkTableV4.FromFilePath(@"C:\TempGameCopies\Jade Empire (German)\dialog.tlk")
    let germanTalkTableF = TalkTableV4.FromFilePath(@"C:\TempGameCopies\Jade Empire (German)\dialogF.tlk")

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>] 
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test; Explicit>]
    member this.TryCreatingContext(): Unit = 
        let contextGerman = new JadeEmpireContext(rootPathGerman)
        let contextEnglish = new JadeEmpireContext(rootPathEnglish)

        Assert.IsNotEmpty(contextGerman.Resources)
        Assert.IsNotEmpty(contextEnglish.Resources)
        ()

    [<Test; Explicit>]
    member this.ExtractOneLanguageDialoguesToDatabase(): Unit = 
        let contextGerman = new JadeEmpireContext(rootPathGerman)
        let dialogueResources = contextGerman.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Dlg)
        let dialoguesAndResources = dialogueResources |> Array.map (fun t -> (LoadDialogue(t.Name, contextGerman.Resources), t))

        System.Console.WriteLine("loaded " + dialoguesAndResources.Length.ToString() + " dialogues")

        let stringsAndKeys = 
            dialoguesAndResources |> 
            Array.collect(fun (t, dialogueResource) -> 
                let extracted = ExtractStringsFromDialogue(t, LanguageType.English, Gender.MasculineOrNeutral, englishTalkTable, englishTalkTable)
                AugmentExtractedStringKeys(extracted, dialogueResource.Name, dialogueResource.OriginDesc, Gender.MasculineOrNeutral) |> Array.ofSeq
                )

        let LLDatabase = new LLDatabase(":memory:")
        stringsAndKeys |> Array.iter (fun (t, k, genderlessKey, g) -> 
            let newCard = {
                CardRecord.Gender = g;
                ID = 0;
                LanguageTag = "de";
                LessonID = 0;
                Text = t;
                Key = k;
                GenderlessKey = genderlessKey;
                KeyHash = k.GetHashCode();
                GenderlessKeyHash = genderlessKey.GetHashCode();
                Reversible = true;
                SoundResource = ""
            }
            LLDatabase.AddCard(newCard) |> ignore)

        Assert.AreEqual(LLDatabase.Cards |> Array.ofSeq |> Array.length, stringsAndKeys.Length)
        ()

    [<Test; Explicit>]
    member this.Extract2DAs(): Unit = 
        let contextEnglish = new JadeEmpireContext(rootPathEnglish)
        let twoDAResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Twoda)
        let extractPath = @"c:\JadeEmpireExtract2DAs"

        if (not(Directory.Exists(extractPath))) then
            Directory.CreateDirectory(extractPath) |> ignore

        twoDAResources |> Array.iter (fun t ->
            let twoDAPath = Path.Combine(extractPath, t.Name.Value + ".2da")
            use br = t.GetBinaryReader
            File.WriteAllBytes(twoDAPath, br.ReadBytes(int br.BaseStream.Length))
        )
        ()

    [<Test; Explicit>]
    member this.Export2DAsAsCSVs(): Unit = 
        let contextEnglish = new JadeEmpireContext(rootPathEnglish)
        let twoDAResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Twoda)
        let extractPath = @"c:\JadeEmpireExtract2DAs\CSVs"

        if (not(Directory.Exists(extractPath))) then
            Directory.CreateDirectory(extractPath) |> ignore

        twoDAResources |> Array.iter (fun t ->
            let twoDAPath = Path.Combine(extractPath, t.Name.Value + ".csv")
            let twoDAFile = TwoDAFile.FromStream(t.GetStream)
            twoDAFile.WriteCSV(twoDAPath)
        )
        ()
