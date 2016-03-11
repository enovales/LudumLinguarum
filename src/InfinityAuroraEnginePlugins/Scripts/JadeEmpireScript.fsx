#I __SOURCE_DIRECTORY__
#load "load-project-debug.fsx"

open InfinityAuroraEnginePlugins
open InfinityAuroraEnginePlugins.ArchiveFiles
open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFF
open InfinityAuroraEnginePlugins.GFFFileTypes
open InfinityAuroraEnginePlugins.IAResourceManager
open InfinityAuroraEnginePlugins.JadeEmpireContext
open InfinityAuroraEnginePlugins.SerializedGFF
open InfinityAuroraEnginePlugins.TalkTable
open InfinityAuroraEnginePlugins.TwoDA
open LLDatabase
open LudumLinguarumPlugins
open System
open System.Diagnostics
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

let rootDataPathGerman = @"C:\TempGameCopies\Jade Empire (German)"
let rootDataPathEnglish = @"C:\Program Files (x86)\Steam\steamapps\common\Jade Empire"
let extractpath = @"E:\JadeEmpireExtract"
let twoDAExtractPath = @"c:\JadeEmpireExtract2DAs"
let csvpath = Path.Combine(extractpath, "csv")

let testExtractBinary2DA() = 
    let sample2DABinaryFilePath = Path.Combine(extractpath, "1000cutsas.2da")
    let twoDA = TwoDAFile.FromFile(sample2DABinaryFilePath)
    Trace.Assert(twoDA.RowCount > 0)
    Trace.Assert(twoDA.ColumnCount > 0)
        
    // try and retrieve every data cell
    let dataCells =
        [
            for i in 1..twoDA.RowCount do
                yield [
                    for j in 1..twoDA.ColumnCount do
                        yield twoDA.Value(i - 1, j - 1)
                ]
        ]

    ()

let tryCreatingContext() = 
    let contextGerman = new JadeEmpireContext(rootDataPathGerman)
    let contextEnglish = new JadeEmpireContext(rootDataPathEnglish)

    Trace.Assert(contextGerman.Resources.Length > 0)
    Trace.Assert(contextEnglish.Resources.Length > 0)

let extractOneLanguageDialoguesToDatabase() = 
    let contextGerman = new JadeEmpireContext(rootDataPathGerman)
    let englishTalkTable = TalkTableV4.FromFilePath(Path.Combine(rootDataPathEnglish, "dialog.tlk"))
    let dialogueResources = contextGerman.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Dlg)
    let dialoguesAndResources = dialogueResources |> Array.map (fun t -> (LoadDialogue(t.Name, contextGerman.Resources), t))

    System.Console.WriteLine("loaded " + dialoguesAndResources.Length.ToString() + " dialogues")

    let stringsAndKeys = 
        dialoguesAndResources |> 
        Array.collect(fun (t, dialogueResource) -> 
            let extracted = ExtractStringsFromDialogue(t, LanguageType.English, Gender.MasculineOrNeutral, englishTalkTable, englishTalkTable)
            AugmentExtractedStringKeys(extracted, dialogueResource.Name, dialogueResource.OriginDesc, Gender.MasculineOrNeutral) |> Array.ofSeq
            )

    let lldb = new LLDatabase(":memory:")
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
        lldb.AddCard(newCard) |> ignore)

    Trace.Assert(lldb.Cards |> Array.ofSeq |> Array.length = stringsAndKeys.Length)

let extract2DAs() = 
    let contextEnglish = new JadeEmpireContext(rootDataPathEnglish)
    let twoDAResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Twoda)

    if (not(Directory.Exists(twoDAExtractPath))) then
        Directory.CreateDirectory(twoDAExtractPath) |> ignore

    twoDAResources |> Array.iter (fun t ->
        let twoDAPath = Path.Combine(twoDAExtractPath, t.Name.Value + ".2da")
        use br = t.GetBinaryReader
        File.WriteAllBytes(twoDAPath, br.ReadBytes(int br.BaseStream.Length))
    )

let export2DAsAsCSVs() = 
    let contextEnglish = new JadeEmpireContext(rootDataPathEnglish)
    let twoDAResources = contextEnglish.Resources |> Array.filter(fun t -> t.ResourceType = ResType.Twoda)
    let extractPath = Path.Combine(twoDAExtractPath, "CSVs")

    if (not(Directory.Exists(extractPath))) then
        Directory.CreateDirectory(extractPath) |> ignore

    twoDAResources |> Array.iter (fun t ->
        let twoDAPath = Path.Combine(extractPath, t.Name.Value + ".csv")
        let twoDAFile = TwoDAFile.FromStream(t.GetStream)
        twoDAFile.WriteCSV(twoDAPath)
    )

//extractJadeEmpireResources(rootDataPathEnglish, extractpath)
//bulkConvert2DAsToCSVs(extractpath, csvpath)
//convertBinary2DAToCSV(csvpath)(Path.Combine(extractpath, "bodybag.2da"))

