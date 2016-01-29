module SrtTools

open LLDatabase
open System
open System.Globalization
open System.IO
open System.Text

// Tools for dealing with .srt subtitle files.
type SrtEntry = {
        SubtitleId: string
        Timecodes: string
        Subtitle: string
    }
    with
        override self.ToString() = 
            "SrtEntry(id = " + self.SubtitleId + ", timecodes = " + self.Timecodes + ", subtitle = " + self.Subtitle + ")"

type SrtBlockExtractorEntry = {
        RelativePath: string
        OverrideBaseKey: string
        Id: int64
        Languages: string
        SubtitleIdStart: int
        SubtitleIdEnd: int
    }

type SrtBlockExtractedEntry = {
        Entry: SrtBlockExtractorEntry
        Language: string
        Text: string
    }

let parseSrtSubtitles(lines: string array) = 
    let unfoldNextEntry(s: string list): (SrtEntry * string list) option = 
        let nextBlock = s |> List.takeWhile(String.IsNullOrWhiteSpace >> not)
        match nextBlock with
        | subtitleLine :: (timecodesLine :: subtitleText) when not(subtitleText |> List.isEmpty) -> 
            let processedSubtitle = String.Join(" ", subtitleText |> Array.ofList).Replace(Environment.NewLine, "")
            let nextList = s |> List.skip(nextBlock |> List.length) |> List.skipWhile(String.IsNullOrWhiteSpace)

            Some(({
                    SrtEntry.SubtitleId = subtitleLine
                    Timecodes = timecodesLine
                    Subtitle = processedSubtitle
            }, nextList))
        | _ -> None
    Seq.unfold unfoldNextEntry (lines |> List.ofArray)


type SrtBlockExtractor(entries: SrtBlockExtractorEntry seq, entryGenerator: SrtBlockExtractorEntry -> SrtEntry array) = 
    static member private DefaultEntryGenerator(rootPath: string)(entry: SrtBlockExtractorEntry): SrtEntry array = 
        File.ReadAllLines(Path.Combine(rootPath, entry.RelativePath)) |> parseSrtSubtitles |> Array.ofSeq   

    static member internal GenerateEntriesForLines(csvLines: string array) = 
        let tryParseAsHex(s: string): int64 = 
            if (s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase)) then
                Int64.Parse(s.Substring(2), NumberStyles.HexNumber)
            else
                Int64.Parse(s)

        // Allow for sparse specification of some fields.
        let mutable lastRelativePath = ""
        let mutable lastID = ""
        let getOrLast(s: string, os: string) =
            if String.IsNullOrWhiteSpace(s) then
                os
            else
                s

        let dataLinesSplitIntoFields = 
            csvLines 
            |> Array.skip(1) 
            |> Array.map(fun t -> t.Split(',') |> Array.map(fun f -> f.Trim()))
            |> Array.filter(fun t -> t.Length >= 5)

        dataLinesSplitIntoFields |> Array.map(fun fields ->
                lastRelativePath <- getOrLast(fields.[0], lastRelativePath)
                lastID <- getOrLast(fields.[2], lastID)

                let currentSubtitleIdStart = fields.[4]

                {
                    SrtBlockExtractorEntry.RelativePath = lastRelativePath
                    OverrideBaseKey = fields.[1]
                    Id = tryParseAsHex(lastID)
                    Languages = fields.[3]
                    SubtitleIdStart = Int32.Parse(currentSubtitleIdStart)
                    SubtitleIdEnd = Int32.Parse(getOrLast(fields.[5], currentSubtitleIdStart))
                }
            )

    private new (rootPath: string, csvLines: string array) = 
        new SrtBlockExtractor(SrtBlockExtractor.GenerateEntriesForLines(csvLines), SrtBlockExtractor.DefaultEntryGenerator(rootPath))

    new(rootPath: string, sourceCsvStream: Stream) = 
        let toRead = sourceCsvStream.Length - sourceCsvStream.Position
        let csvBytes = Array.zeroCreate<byte>(int toRead)
        let csvRead = sourceCsvStream.Read(csvBytes, 0, int toRead)
        let csvText = Encoding.UTF8.GetString(csvBytes)
        let csvLines = csvText.Split([| Environment.NewLine |], StringSplitOptions.RemoveEmptyEntries) |> Array.skip(1)
        new SrtBlockExtractor(rootPath, csvLines)

    new(rootPath: string, sourceCsvPath: string) = 
        // read in the source csv, and skip the header line
        let csvLines = File.ReadAllLines(sourceCsvPath) |> Array.skip(1)
        new SrtBlockExtractor(rootPath, csvLines)

    member this.Extract(): SrtBlockExtractedEntry seq = 
        let createExtractedEntry(e: SrtBlockExtractorEntry, t: string, l: string) = 
            {
                SrtBlockExtractedEntry.Entry = e
                Language = l
                Text = t 
            }

        // Basic idea: split the sequence of entries into ordered subsequences per file. (Order has to be preserved in
        // order to ensure that the strings are read and assigned in the correct order.) We can't use groupBy because
        // it doesn't guarantee ordering of the subsequences.
        let foldNextByFile s e = 
            match (s |> Map.tryFind(e.RelativePath)) with
            | Some(l) -> s |> Map.add e.RelativePath (e :: l)
            | _ -> s |> Map.add e.RelativePath [e]

        let entriesByFile = 
            entries 
            |> Array.ofSeq
            |> Array.fold foldNextByFile Map.empty
            |> Map.map (fun _ v -> v |> List.rev) |> Map.toArray

        // unfold function. the previous starting offset is passed in -- if it hasn't changed for this entry,
        // then we skip seeking the stream, and instead just continue to read.
        let createEntriesForLanguages(e: SrtBlockExtractorEntry, s: string) = 
            e.Languages.Split([| ';' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map(fun l -> createExtractedEntry(e, s, l.Trim()))

        let readNext(entries: SrtEntry array) = fun(state: SrtBlockExtractorEntry list) -> 
            match state with
            | n :: rest -> 
                let nextSubtitleEntries = Array.sub entries (n.SubtitleIdStart - 1) (n.SubtitleIdEnd - n.SubtitleIdStart + 1)
                let nextString = String.Join(" ", nextSubtitleEntries |> Array.map(fun s -> s.Subtitle) |> Array.filter(String.IsNullOrWhiteSpace >> not))
                Some((createEntriesForLanguages(n, nextString), rest))
            | [] -> None

        let extractEntriesForFile(s: string * SrtBlockExtractorEntry list) = 
            match s with
            | (fn: string, entries: SrtBlockExtractorEntry list) -> 
                let srtEntries = entryGenerator(entries |> List.head)
                List.unfold (readNext(srtEntries)) entries |> List.toArray |> Array.collect(fun t -> t)

        entriesByFile |> (Array.map extractEntriesForFile) |> Array.collect(fun t -> t) |> Seq.ofArray

let convertSrtBlockExtractedEntriesToCardRecords(lid: int)(extracted: SrtBlockExtractedEntry seq): CardRecord seq =
    let createCardRecordForEntry(e: SrtBlockExtractedEntry) = 
        let baseKey = e.Entry.RelativePath + "\\" + e.Entry.Id.ToString()
        {
            CardRecord.ID = 0
            LessonID = lid
            Text = e.Text
            Gender = "masculine"
            Key = baseKey
            GenderlessKey = baseKey
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = e.Language
            Reversible = true
        }

    extracted |> Seq.map createCardRecordForEntry

