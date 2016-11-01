﻿module StarWarsGBS

open CommandLine
open FSharp.Core
open LLDatabase
open LudumLinguarumPlugins
open OneOffGamesData
open PInvoke
open System
open System.IO

// don't warn about native conversions
#nowarn "9"

type GBSPluginSettings() = 
    [<CommandLine.Option("language-tag", Default = "en", Required = false)>]
    member val LanguageTag = "en" with get, set

let private getResourceNames(languageDll: Kernel32.SafeLibraryHandle) = 
    let mutable resourceNameList = []
    let enumNameProc = 
        fun (hModule: nativeint)(lpszType: nativeptr<char>)(lpszName: nativeptr<char>)(lParam: nativeint) ->
            if (Kernel32.IS_INTRESOURCE(lpszName)) then
                resourceNameList <- NativeInterop.NativePtr.toNativeInt(lpszName) :: resourceNameList

            true

    let enumNameDelegate = new Kernel32.EnumResNameProc(enumNameProc)
    let namesSuccess = Kernel32.EnumResourceNames(languageDll, Kernel32.RT_STRING, enumNameDelegate, nativeint 0)
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



(***************************************************************************)
(****************** Star Wars: Galactic Battlegrounds Saga *****************)
(***************************************************************************)
let private runExtractGBS(path: string, db: LLDatabase, g: GameRecord)(settings: GBSPluginSettings) = 
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

    let extractCardsForModule(language: string)((modulePath: string, lid: int)) = 
        let moduleHandle = PInvoke.Kernel32.LoadLibraryEx(modulePath, nativeint 0, PInvoke.Kernel32.LoadLibraryExFlags.LOAD_LIBRARY_AS_IMAGE_RESOURCE)
        let resourceNames = getResourceNames(moduleHandle)
        let stringIds = 
            resourceNames
            |> List.map int
            |> List.collect (fun n -> [(n - 1) * 16 .. ((n - 1) * 16) + 15])

        stringIds
        |> Array.ofList
        |> Array.collect (fun sid ->
                                let mutable sb: nativeptr<char> = NativeInterop.NativePtr.ofNativeInt<char>(nativeint 0)
                                let charsOutput = User32.LoadString(moduleHandle.DangerousGetHandle(), uint32 sid, &sb, 0)
                                if charsOutput > 0 then
                                    let localizedString = (new string(sb)).Substring(0, charsOutput)
                                    AssemblyResourceTools.createCardRecordForStrings(
                                        lid, 
                                        Path.GetFileNameWithoutExtension(modulePath), 
                                        language, 
                                        "masculine"
                                    )(Map([| (sid.ToString(), localizedString) |]))
                                else
                                    [||]
                                )

    modulesToExtract
    |> Array.collect(extractCardsForModule(settings.LanguageTag))
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

let ExtractGalacticBattlegroundsSaga(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let parser = new CommandLine.Parser(fun t ->
        t.HelpWriter <- System.Console.Out
        t.IgnoreUnknownArguments <- true)

    parser
        .ParseArguments<GBSPluginSettings>(args)
        .WithParsed(new Action<GBSPluginSettings>(runExtractGBS(path, db, g)))

type StarWarsGBSPlugin() = 
    let mutable outStream: TextWriter option = None

    interface IPlugin with
        member this.Load(tw: TextWriter, [<ParamArray>] args: string[]) = 
            ()
        member this.Name = "starwarsgbs"
        member this.Parameters = 
            [|
                new GBSPluginSettings()
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
            ExtractGalacticBattlegroundsSaga(path, db, gameEntryWithId, args)
            ()                

    member private this.LogWrite(s: string) = 
        outStream |> Option.map(fun t -> t.Write(s))
    member private this.LogWriteLine(s: string) = 
        outStream |> Option.map(fun t -> t.WriteLine(s))
