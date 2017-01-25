module StarWarsGBS

open Argu
open CommandLine
open FSharp.Core
open LLDatabase
open LudumLinguarumPlugins
open OneOffGamesData
open PInvoke
open System
open System.IO
open System.Text.RegularExpressions

// don't warn about native conversions
#nowarn "9"

/// <summary>
/// Plugin settings, because we need to be able to specify a language that is
/// being extracted.
/// </summary>
type internal GBSPluginArgs =
    | [<Mandatory; EqualsAssignment>] Language_Tag of string
    with
        interface IArgParserTemplate with
            member this.Usage = 
                match this with
                | Language_Tag _ -> "The installed language of the game."

let private getResourceNames(languageDll: Kernel32.SafeLibraryHandle) = 
    let mutable resourceNameList = []
    let enumNameProc = 
        fun (hModule: nativeint)(lpszType: nativeptr<char>)(lpszName: nativeptr<char>)(lParam: nativeint) ->
            if (Kernel32.IS_INTRESOURCE(lpszName)) then
                resourceNameList <- NativeInterop.NativePtr.toNativeInt(lpszName) :: resourceNameList

            true

    let enumNameDelegate = new Kernel32.EnumResNameProc(enumNameProc)
    if not(Kernel32.EnumResourceNames(languageDll, Kernel32.RT_STRING, enumNameDelegate, nativeint 0)) then
        failwith "unable to enumerate string resource names"

    resourceNameList

let private getLanguagesForNamedResource(languageDll: Kernel32.SafeLibraryHandle)(name: nativeint) = 
    let stub = new OneOffGamesData.PInvokeStubs.EnumResourceLanguagesCallback()
    let langsSuccess = OneOffGamesData.PInvokeStubs.EnumResourceLanguages(languageDll, Kernel32.RT_STRING, Kernel32.MAKEINTRESOURCE(int name), stub)
    stub.Languages |> List.ofSeq

let private getResourceStrings(languageDll: Kernel32.SafeLibraryHandle)(name: nativeint, langs: Kernel32.LANGID list) = 
    let flags = Kernel32.FormatMessageFlags.FORMAT_MESSAGE_FROM_HMODULE ||| Kernel32.FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS
    let getResourceStringForLang(lang: Kernel32.LANGID) = 
        Kernel32.FormatMessage(flags, languageDll.DangerousGetHandle(), int name, int lang.Data, [||], 0)
    langs
    |> List.map getResourceStringForLang
    |> Array.ofList


let private formattingRegexes = 
    [|
        new Regex(@"\<.*\>")
    |]

let private stripFormattingTags(s: string) = 
    Array.fold (fun (a: string)(r: Regex) -> r.Replace(a, "")) s formattingRegexes

// Filters for strings that we don't want to extract.
let private stringEndsInDotTxt(s: string) = s.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase)
let private stringIsJustNumbersAndCommas(s: string) = s.ToCharArray() |> Array.forall(fun c -> (c = ',') || (c |> Char.IsDigit))
let private stringIsTooShort(s: string) = s.Length <= 1
let private stringIsParenOrBracketChar(s: string) = 
    match s with
    | _ when (s.Length = 3) && (s.Chars(0) = '(') && (s.Chars(2) = ')') -> true
    | _ when (s.Length = 3) && (s.Chars(0) = '[') && (s.Chars(2) = ']') -> true
    | _ -> false

let private runExtractGBS(path: string, db: LLDatabase, g: GameRecord)(settings: ParseResults<GBSPluginArgs>) = 
    let mainLessonEntry = {
        LessonRecord.GameID = g.ID
        ID = 0
        Name = "Main Game Text"
    }
    let mainLessonEntryWithId = { mainLessonEntry with ID = db.CreateOrUpdateLesson(mainLessonEntry) }
    let x1LessonEntry = {
        LessonRecord.GameID = g.ID
        ID = 0
        Name = "Expansion 1 Text"
    }
    let x1LessonEntryWithId = { x1LessonEntry with ID = db.CreateOrUpdateLesson(x1LessonEntry) }

    let modulesToExtract = 
        [|
            (Path.Combine(path, @"game\language.dll"), mainLessonEntryWithId.ID)
            (Path.Combine(path, @"game\language_x1.dll"), x1LessonEntryWithId.ID)
        |]

    let extractCardsForModule(language: string)(modulePath: string, lid: int) = 
        let moduleHandle = PInvoke.Kernel32.LoadLibraryEx(modulePath, nativeint 0, PInvoke.Kernel32.LoadLibraryExFlags.LOAD_LIBRARY_AS_IMAGE_RESOURCE)
        let resourceNames = getResourceNames(moduleHandle)
        let stringIds = 
            resourceNames
            |> List.map int
            |> List.collect (fun n -> [(n - 1) * 16 .. ((n - 1) * 16) + 15])

        let createCardForStringId(sid: int) = 
            let mutable sb: nativeptr<char> = NativeInterop.NativePtr.ofNativeInt<char>(nativeint 0)
            let charsOutput = User32.LoadString(moduleHandle.DangerousGetHandle(), uint32 sid, &sb, 0)
            if charsOutput > 0 then
                let localizedString = 
                    ((new string(sb)).Substring(0, charsOutput)
                    |> stripFormattingTags).Trim()

                let invalidityTests = 
                    [|
                        stringEndsInDotTxt
                        stringIsJustNumbersAndCommas
                        stringIsTooShort
                        stringIsParenOrBracketChar
                    |]

                if (invalidityTests |> Array.exists(fun t -> t(localizedString))) then
                    [||]
                else
                    [| (sid, localizedString) |]
            else
                [||]

        stringIds
        |> Array.ofList
        |> Array.collect createCardForStringId
        |> Array.distinctBy(fun (_, s) -> s)
        |> Array.collect(fun (sid, localizedString) -> 
                            AssemblyResourceTools.createCardRecordForStrings(
                                lid, 
                                Path.GetFileNameWithoutExtension(modulePath) + "_", 
                                language, 
                                "masculine"
                            )(Map([| (sid.ToString(), localizedString) |]))
                        )

    modulesToExtract
    |> Array.collect(extractCardsForModule(settings.GetResult(<@ Language_Tag @>)))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractGalacticBattlegroundsSaga(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let parser = ArgumentParser.Create<GBSPluginArgs>(errorHandler = new ProcessExiter())
    let results = parser.Parse(args)

    if (results.IsUsageRequested) || (results.GetAllResults() |> List.isEmpty) then
        Console.WriteLine(parser.PrintUsage())
    else
        runExtractGBS(path, db, g)(results)

type StarWarsGBSPlugin() = 
    let mutable outStream: TextWriter option = None

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            ()
        member this.Name = "starwarsgbs"
        member this.Parameters = 
            [|
            |]
    interface IGameExtractorPlugin with
        member this.SupportedGames: string array = 
            [| 
                "Star Wars Galactic Battlegrounds Saga"
            |]

        member this.ExtractAll(game: string, path: string, db: LLDatabase, [<ParamArray>] args: string[]) = 
            // create game entry, and then run handler
            let gameEntry = {
                GameRecord.Name = game;
                ID = 0
            }
            let gameEntryWithId = { gameEntry with ID = db.CreateOrUpdateGame(gameEntry) }
            this.LogWriteLine("Game entry for " + game + " updated.") |> ignore
            ExtractGalacticBattlegroundsSaga(path, db, gameEntryWithId, args) |> ignore
            ()                

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))
