module StringExtractors

open LLDatabase

open FSharp.Data
open System
open System.Globalization
open System.IO
open System.Text

type CSVExtractorEntry = {
        RelativePath: string
        Id: int64
        Language: string
        Offset: int64
        Length: int64
        Gender: string
        SoundResource: string
        Reversible: bool
    }

type CSVExtractedEntry = {
        Entry: CSVExtractorEntry
        Text: string
    }

type CSVExtractor(entries: CSVExtractorEntry seq, streamGenerator: CSVExtractorEntry -> Stream) = 
    static member private DefaultFileStreamGenerator(rootPath: string)(entry: CSVExtractorEntry): Stream = 
        new FileStream(Path.Combine(rootPath, entry.RelativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite) :> Stream

    private new (rootPath: string, csvLines: string array) = 
        let tryParseAsHex(s: string): int64 = 
            if (s.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase)) then
                Int64.Parse(s.Substring(2), NumberStyles.HexNumber)
            else
                Int64.Parse(s)

        let mutable lastRelativePath = ""
        let csvEntries = csvLines |> Array.map(fun line ->
                let fields = line.Split(',') |> Array.map(fun f -> f.Trim())
                let rawRecord = {
                    CSVExtractorEntry.RelativePath = fields.[0]
                    Id = tryParseAsHex(fields.[1])
                    Language = fields.[2]
                    Offset = tryParseAsHex(fields.[3])
                    Length = tryParseAsHex(fields.[4])
                    Gender = 
                        match fields |> Array.tryItem(5) with
                        | Some(g) when not(String.IsNullOrWhiteSpace(g)) -> g
                        | _ -> "masculine"
                    SoundResource = 
                        match fields |> Array.tryItem(6) with
                        | Some(sr) when not(String.IsNullOrWhiteSpace(sr)) -> sr
                        | _ -> ""
                    Reversible = 
                        match fields |> Array.tryItem(7) with
                        | Some(r) when not(String.IsNullOrWhiteSpace(r)) -> Boolean.Parse(r)
                        | _ -> true
                }

                // If there was no relative path specified, just use the last one we found.
                // This will make the CSV less visually noisy (for hand-editing) and shorter.
                if String.IsNullOrWhiteSpace(rawRecord.RelativePath) then
                    { rawRecord with RelativePath = lastRelativePath }
                else
                    lastRelativePath <- rawRecord.RelativePath
                    rawRecord
            )
        new CSVExtractor(csvEntries, CSVExtractor.DefaultFileStreamGenerator(rootPath))

    new(rootPath: string, sourceCsvStream: Stream) = 
        let toRead = sourceCsvStream.Length - sourceCsvStream.Position
        let csvBytes = Array.zeroCreate<byte>(int toRead)
        let csvRead = sourceCsvStream.Read(csvBytes, 0, int toRead)
        let csvText = Encoding.UTF8.GetString(csvBytes)
        let csvLines = csvText.Split([| Environment.NewLine |], StringSplitOptions.RemoveEmptyEntries) |> Array.skip(1)
        new CSVExtractor(rootPath, csvLines)

    new(rootPath: string, sourceCsvPath: string) = 
        // read in the source csv, and skip the header line
        let csvLines = File.ReadAllLines(sourceCsvPath) |> Array.skip(1)
        new CSVExtractor(rootPath, csvLines)

    member this.Extract(): CSVExtractedEntry seq = 
        let createExtractedEntry(e: CSVExtractorEntry, t: string) = 
            {
                CSVExtractedEntry.Entry = e
                Text = t 
            }

        entries |> Seq.map(fun entry ->
            use file = streamGenerator(entry)
            let br = new BinaryReader(file)

            if (br.BaseStream.Seek(entry.Offset, SeekOrigin.Begin) <> entry.Offset) then
                raise(exn("couldn't seek to offset " + entry.Offset.ToString("X8") + " in file " + entry.RelativePath))

            createExtractedEntry(entry, Encoding.UTF8.GetString(br.ReadBytes(int entry.Length)))
        )

let convertCSVExtractedEntriesToCardRecords(lid: int)(extracted: CSVExtractedEntry seq): CardRecord seq =
    let createCardRecordForEntry(e: CSVExtractedEntry) = 
        let baseKey = e.Entry.RelativePath + "\\" + e.Entry.Id.ToString()
        {
            CardRecord.ID = 0
            LessonID = lid
            Text = e.Text
            Gender = e.Entry.Gender
            Key = baseKey + e.Entry.Gender
            GenderlessKey = baseKey
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = e.Entry.SoundResource
            LanguageTag = e.Entry.Language
            Reversible = e.Entry.Reversible
        }

    extracted |> Seq.map createCardRecordForEntry