module WormsArmageddon

open LLDatabase
open System
open System.IO
open System.Text.RegularExpressions

type StringExtractState = 
    {
        AccumulatingKey: string option
        Accumulated: string list
        Complete: (string * string) list
    }
    with
        override this.ToString() = 
            let ac = this.Accumulated.ToString()
            let c = this.Complete.ToString()
            let ak =
                match this.AccumulatingKey with
                | Some(s) -> s
                | None -> "(none)"

            "StringExtractState(AccumulatingKey = " + ak + ", Accumulated = " + ac + ", Complete = " + c + ")"

let internal singleLineRegex = new Regex("([0-9a-zA-Z_]+)\\s+\"(.*)\"")
let internal partialIdentifierRegex = new Regex(@"^(\S+)")
let internal partialStringRegex = new Regex("^\s+\"(.*)\"")

// returns any multi-part string that has accumulated. Used when a state switch occurs, or
// when the fold is completed.
let internal getAccumulatedString(state: StringExtractState) = 
    state.AccumulatingKey
    |> Option.map (fun ak -> (ak, String.Join(" ", state.Accumulated |> Array.ofList |> Array.rev)))
    |> Option.toList

let internal foldStringLines(state: StringExtractState)(l: string): StringExtractState = 
    match (singleLineRegex.Match(l), partialIdentifierRegex.Match(l), partialStringRegex.Match(l)) with
    | (slr, _, _) when slr.Success ->
        // if we were accumulating a value, add it.
        {
            StringExtractState.Complete = (slr.Groups.[1].Value, slr.Groups.[2].Value) :: (getAccumulatedString(state) @ state.Complete)
            AccumulatingKey = None
            Accumulated = []
        }

    | (_, pir, _) when pir.Success -> 
        // begin accumulating, and add any previously accumulated strings
        {
            StringExtractState.Complete = getAccumulatedString(state) @ state.Complete
            AccumulatingKey = Some(pir.Groups.[1].Value)
            Accumulated = []
        }
    | (_, _, psr) when psr.Success ->
        {
            state with Accumulated = psr.Groups.[1].Value :: state.Accumulated
        }
    | _ -> failwith "line did not match any of the regexes"

let extractStringsFromLines(lessonID: int)(lines: string array, language: string) = 
    // first line is the language name, skip it. Also, filter out comment and empty lines.
    let dataLines = 
        lines
        |> Array.skip(1)
        |> Array.filter(fun l -> not(String.IsNullOrWhiteSpace(l)) && not(l.StartsWith("#")))

    let initialState = 
        {
            StringExtractState.Accumulated = []
            AccumulatingKey = None
            Complete = []
        }

    let foldedState = 
        dataLines
        |> Array.fold foldStringLines initialState

    let finalState = 
        {
            StringExtractState.Accumulated = []
            AccumulatingKey = None
            Complete = getAccumulatedString(foldedState) @ foldedState.Complete
        }

    finalState.Complete
    |> Array.ofList
    |> Array.map(fun (k, v) -> (k, v.Replace(@"\n", "").Replace("\t", "").Trim()))
    |> Map.ofArray
    |> AssemblyResourceTools.createCardRecordForStrings(lessonID, "gametext", language, "masculine")

let ExtractWormsArmageddon(path: string) = 
    let lessonGameTextEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }
    let stringFilesAndLanguages = 
        [|
            ("Dutch.txt", "nl")
            ("English.txt", "en")
            ("French.txt", "fr")
            ("German.txt", "de")
            ("Italian.txt", "it")
            ("Portuguese.txt", "pt")
            ("Russian.txt", "ru")
            ("Spanish.txt", "es")
            ("Swedish.txt", "sv")
        |]
        |> Array.map (fun (p, language) -> (Path.Combine(Path.Combine(path, @"DATA\User\Languages\3.7.2.1"), p), language))
    
    let cards = 
        stringFilesAndLanguages
        |> Array.collect((fun (p, language) -> (File.ReadAllLines(p), language)) >> extractStringsFromLines(lessonGameTextEntry.ID))

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonGameTextEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
