module SpaceChannel5

open LLDatabase
open System
open System.IO
open System.Text

let private suffixesAndLanguages = 
    [|
        ("", "jp", Encoding.GetEncoding("shift_jis"))
        ("_e", "en", Encoding.GetEncoding("Windows-1252"))
        ("_f", "fr", Encoding.GetEncoding("Windows-1252"))
        ("_g", "de", Encoding.GetEncoding("Windows-1252"))
        ("_i", "it", Encoding.GetEncoding("Windows-1252"))
        ("_s", "es", Encoding.GetEncoding("Windows-1252"))
    |]
let private dgcpFiles = 
    [|
        "coscap"
        "r01cap"
        "r10cap"
        "r11cap"
        "r12cap"
        "r20cap"
        "r21cap"
        "r22cap"
        "r23cap"
        "r30cap"
        "r31cap"
        "r40cap"
        "r41cap"
        "r42cap"
        "r43cap"
        "r44cap"
        "r50cap"
        "r51cap"
        "r52cap"
        "r60cap"
        "r61cap"
        "r62cap"
        "titcap"
        "warn_cap"
        "warncap"
    |]

let private cardsForDGCPFile(language: string, keyRoot: string, lessonID: int, path: string, encoding: Encoding) = 
    if (File.Exists(path)) then
        let dgcp = new DGCP.DGCPFile(path, encoding)
            
        dgcp.Entries
        |> Array.mapi (fun i t -> (i.ToString(), t))
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "masculine")
    else
        [||]

let private cardsForDGCPSuffix(baseName: string, rootPath: string, lessonID: int)(t: string * string * Encoding) =
    let (suffix, language, encoding) = t
    let filePath = Path.Combine(rootPath, baseName + suffix + ".bin")
    cardsForDGCPFile(language, baseName, lessonID, filePath, encoding)

let private cardsForDGCPBaseName(rootPath: string, lessonID: int)(baseName: string) = 
    suffixesAndLanguages
    |> Array.collect(cardsForDGCPSuffix(baseName, rootPath, lessonID))

let internal getNextDocTextBlock(s: string seq) = 
    let isFiller(t: string) = t.StartsWith("#") || t.StartsWith("^") || t.StartsWith("_")
    let n = s |> Seq.skipWhile isFiller
    let strings = n |> Seq.takeWhile(fun t -> not(t.StartsWith("#")) && not(t.StartsWith("_")))
    let newState = n |> Seq.skipWhile(isFiller >> not)
    match Seq.isEmpty(strings) with
    | true -> None
    | false -> Some(String.Join(Environment.NewLine, strings), newState)

let internal cardsForDocText(d: string, language: string, keyRoot: string, lessonID: int, encoding: Encoding) = 
    let lines = d.Split([| Environment.NewLine |], StringSplitOptions.RemoveEmptyEntries)
            
    lines 
    |> Seq.ofArray 
    |> Seq.unfold getNextDocTextBlock 
    |> Seq.mapi (fun i t -> (i.ToString(), t))
    |> Array.ofSeq
    |> Map.ofArray
    |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "masculine")

let ExtractSpaceChannel5Part2(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

    // read text from ctl_text archives
    let ctlTextCards = [||]

    // read text from DGCP files
    let dgcpCards = 
        dgcpFiles
        |> Array.collect(cardsForDGCPBaseName(path, lessonEntryWithId.ID))

    Array.concat [| ctlTextCards; dgcpCards |]
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
