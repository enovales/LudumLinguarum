module WormsGames
open LLDatabase
open LLUtils
open StreamTools
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

module Armageddon = 

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
            |> Array.map (fun (p, language) -> (Path.Combine(Path.Combine(path, FixPathSeps @"DATA\User\Languages\3.7.2.1"), p), language))
    
        let cards = 
            stringFilesAndLanguages
            |> Array.collect((fun (p, language) -> (File.ReadAllLines(p), language)) >> extractStringsFromLines(lessonGameTextEntry.ID))

        {
            LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonGameTextEntry |]
            LudumLinguarumPlugins.ExtractedContent.cards = cards
        }

module CrazyGolf = 
    type Header =
        {
            magic: uint32
            remainderBytes: uint32
            numEntries: uint32
            entryLength: uint32
            unknown1: uint32
            unknown2: uint32
        }
        with
            static member Read(br: EndianReaderWrapper) = 
                {
                    magic = br.ReadUInt32()
                    remainderBytes = br.ReadUInt32()
                    numEntries = br.ReadUInt32()
                    entryLength = br.ReadUInt32()
                    unknown1 = br.ReadUInt32()
                    unknown2 = br.ReadUInt32()
                }

    type StringEntry = 
        {
            unknownHash: uint32
            position: uint32
        }
        with
            static member Read(br: EndianReaderWrapper) = 
                {
                    unknownHash = br.ReadUInt32()
                    position = br.ReadUInt32()
                }

    let ExtractWormsCrazyGolf(path: string) = 
        // Extract game files to a temporary path, but try cleaning it first.
        let tempPath = Path.Combine(Path.GetTempPath(), @"LudumLinguarumWCG")
        try Directory.Delete(tempPath) with | _ -> ()

        use archive = new Ionic.Zip.ZipFile(Path.Combine(path, "zip.zip"))
        let languagesAndFileNames = 
            [|
                ("AllTextCze.bin", "cs")
                //("AllTextDan.bin", "da")          // empty localization file
                ("AllTextEng.bin", "en-GB")
                //("AllTextFin.bin", "fi")          // empty localization file
                ("AllTextFra.bin", "fr")
                ("AllTextGer.bin", "de")
                ("AllTextIta.bin", "it")
                //("AllTextJap.bin", "ja")          // empty localization file
                //("AllTextNed.bin", "nl")          // empty localization file
                //("AllTextNor.bin", "no")          // empty localization file
                ("AllTextPol.bin", "pl")
                //("AllTextRus.bin", "ru")          // empty localization file
                ("AllTextSpa.bin", "es")
                //("AllTextSwe.bin", "sv")          // empty localization file
                ("AllTextUsa.bin", "en-US")
            |]

        let cardsForLanguage(fileName: string, language: string) = 
            let stringsFilePath = Path.Combine(tempPath, FixPathSeps(@"language\" + fileName))
            let stringsFileBytes = File.ReadAllBytes(stringsFilePath)
            use stringsMemoryStream = new MemoryStream(stringsFileBytes)
            use stringsBinaryReader = new BinaryReader(stringsMemoryStream)
            let endianReader = StreamTools.LittleEndianReaderWrapper(stringsBinaryReader)

            // Read the header, then the string table.
            let header = Header.Read(endianReader)
            let stringTableEntries = Array.init(int header.numEntries)(fun _ -> StringEntry.Read(endianReader))
            let stripNulls(i: int, s: string) = 
                (i, s.Replace("\0", ""))
                
            let filterOutEmpties(_: int, s: string) = 
                let nonZeroLength = (s.Length > 0)
                let notNullOrWhiteSpace = not(String.IsNullOrWhiteSpace(s))
                nonZeroLength && notNullOrWhiteSpace

            // For each entry, move to its position, and then read the null-terminated string.
            stringTableEntries
            |> Array.mapi(fun i e -> (i, e))
            |> Array.collect(fun (i: int, entry: StringEntry) -> readNullTerminatedString(stringsFileBytes, Encoding.UTF8)(int entry.position + 8) |> Option.map (fun entry -> (i, entry)) |> Option.toArray)
            |> Array.map stripNulls
            |> Array.filter filterOutEmpties
            |> Array.map(fun (i, s) -> (i.ToString(), s.Replace(@"\n", "")))
            |> Map.ofArray
            |> AssemblyResourceTools.createCardRecordForStrings(0, "", language, "masculine")

        let cards = 
            try
                try
                    archive.ExtractSelectedEntries("*.*", "language", tempPath)
                    languagesAndFileNames |> Array.collect cardsForLanguage
                with
                | _ -> [||]
             finally
                try Directory.Delete(tempPath, true) with | _ -> ()

        {
            LudumLinguarumPlugins.ExtractedContent.lessons = [| { LessonRecord.ID = 0; Name = "Game Text" } |]
            LudumLinguarumPlugins.ExtractedContent.cards = cards
        }
