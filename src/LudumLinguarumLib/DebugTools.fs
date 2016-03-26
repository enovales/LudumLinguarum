module DebugTools

open System
open System.Text
open System.IO
open Trie

// Tools useful for development of new plugins.

type TextScannerRangeEndType = 
    | Undefined
    | Null

/// <summary>
/// Configuration for the text-scanning utility.
/// </summary>
[<CommandLine.Verb("scan-for-text", HelpText = "Scan for text in files in a path")>]
type TextScannerConfiguration() = 
    [<CommandLine.Option(Required = true)>]
    member val Path = "" with get, set

    [<CommandLine.Option("character-width", Required = false)>]
    member val CharacterWidth = 1 with get, set

    [<CommandLine.Option("scanner-range-end", Required = false, HelpText = "Rule used to mark the end of a matched range. Used to cut down on the number of output results.")>]
    member val ScannerRangeEnd = TextScannerRangeEndType.Undefined with get, set

    [<CommandLine.Option("minimum-length", Required = false, HelpText = "The minimum length of string to match")>]
    member val MinimumLength = 4 with get, set

    [<CommandLine.Option("maximum-length", Required = false, HelpText = "The maximum length of string to match")>]
    member val MaximumLength = 10 with get, set

    [<CommandLine.Option("dictionary-file", Required = false, HelpText = "Dictionary used to match words in files")>]
    member val DictionaryFile = "dictionary.txt" with get, set

type StringHasher = 
    abstract member Seed: byte array * int -> StringHasher
    abstract member Hash: int64
    abstract member Next: unit -> StringHasher

type MapHasher(m: (string * int64) array, ba: byte array, l: int, startIndex: int, endIndex: int) = 
    new(m: (string * int64) array) = new MapHasher(m, [||], 0, 0, 0)
    interface StringHasher with
        member this.Seed(ba: byte array, l: int): StringHasher = new MapHasher(m, ba, l, 0, l - 1) :> StringHasher
        member this.Hash: int64 = 
            let s = Encoding.UTF8.GetString(ba, startIndex, endIndex - startIndex + 1)
            match (m |> Array.tryFind(fun (t, h) -> t = s)) with
            | Some(t, h) -> h
            | _ -> int64 0
        member this.Next() = new MapHasher(m, ba, l, startIndex + 1, endIndex + 1) :> StringHasher

type RabinFingerprintHasher = 
    {
        ba: byte array;
        mutable startIndex: int64;
        mutable endIndex: int64;
        mutable h: int64;
        hashBase: int
    }
    with
        override this.ToString() = 
            let contents = Encoding.UTF8.GetString(this.ba, int this.startIndex, int (this.endIndex - this.startIndex))
            "RabinFingerprintHasher(contents = \"" + contents + "\", startIndex = " + this.startIndex.ToString() + ", endIndex = " + this.endIndex.ToString() + ", h = " + this.h.ToString() + ", hashBase = " + this.hashBase.ToString() + ")"

        static member Seed(ba: byte array, stringLength: int, hashBase: int): StringHasher = 
            let root = { RabinFingerprintHasher.ba = ba; startIndex = int64 0; endIndex = int64 stringLength; h = int64 0; hashBase = hashBase } :> StringHasher
            root.Seed(ba, stringLength)

        interface StringHasher with
            member this.Hash: int64 = this.h
            member this.Seed(ba: byte array, stringLength: int): StringHasher = 
                let hash: int64 = (ba |> Array.take(stringLength) |> Seq.fold(fun acc c -> (acc + int64 c) * int64 this.hashBase)(int64 0)) / (int64 this.hashBase)
                {
                    RabinFingerprintHasher.ba = ba;
                    startIndex = int64 0;
                    endIndex = int64 stringLength;
                    h = int64 hash;
                    hashBase = this.hashBase
                } :> StringHasher

            member this.Next(): StringHasher = 
                // remove first character, and strip it from the rolling hash
                let mutable hashBaseToRemove: int64 = int64 1
                for i = 1 to int(this.endIndex - this.startIndex - int64 1) do
                    hashBaseToRemove <- hashBaseToRemove * int64 this.hashBase
                let hashToRemove = int64 this.ba.[int this.startIndex] * hashBaseToRemove

                this.h <- ((this.h - hashToRemove) * (int64 this.hashBase)) + int64 this.ba.[int this.endIndex]
                this.startIndex <- this.startIndex + int64 1
                this.endIndex <- this.endIndex + int64 1
                this :> StringHasher

type StringOffsetPair = {
        s: string;
        o: int64
    }
    with
        override this.ToString(): string = 
            "StringOffsetPair('" + this.s + "', " + this.o.ToString() + ")"
    
type FixedLengthRabinKarpStringScanner(data: byte array, d: string array, l: int, hasher: StringHasher) = 
    let stringLength = d |> Array.tryHead |> Option.map(fun t -> t.Length)
    let hashesAndWords = d |> Array.groupBy(fun t -> hasher.Seed(Encoding.UTF8.GetBytes(t), l).Hash) |> Map.ofArray
    let rec scan(offset: int64, h: StringHasher, acc: StringOffsetPair list): StringOffsetPair list = 
        match (hashesAndWords.TryFind(h.Hash)) with 
        | Some(words) ->
            let currString = Encoding.UTF8.GetString(data, int offset - l, l)
            let foundWordOpt = words |> Array.tryFind(fun t -> t = currString)
            match (foundWordOpt, (offset < data.LongLength)) with
            | (Some(fw), true) ->
                scan(offset + int64 1, h.Next(), { StringOffsetPair.s = fw; o = offset - int64 fw.Length } :: acc)
            | (Some(fw), false) ->
                { StringOffsetPair.s = fw; o = offset - int64 fw.Length } :: acc
            | (None, true) ->
                scan(offset + int64 1, h.Next(), acc)
            | (None, false) ->
                acc
        | None ->
            match (offset < data.LongLength) with
            | true -> 
                scan(offset + int64 1, h.Next(), acc)
            | _ -> acc
    
    member this.GetStrings() = 
        match stringLength with
        | Some(l) ->
            // seed string and begin matching
            let seedChars = data |> Array.truncate(l) |> Encoding.UTF8.GetString
            if (seedChars.Length = l) then
                // enough chars, flatten and construct the hasher
                let hasher = hasher.Seed(data, l)
                scan(int64 l, hasher, [])
            else
                // not enough chars
                []
        | None -> []

type RabinKarpStringScanner(data: byte array, d: string array, hasher: StringHasher) = 
    let wordsByLength = d |> Array.groupBy(fun t -> t.Length)
    let scanners = wordsByLength |> Array.sortBy(fun (l, _) -> l) |> Array.map(fun (l, t) -> new FixedLengthRabinKarpStringScanner(data, t, l, hasher))

    member this.GetStrings(): StringOffsetPair array = 
        // run the scanner for each string length in parallel
        let foundStrings = 
            scanners |> Seq.mapi(fun l s -> 
                async { 
                    let strings = s.GetStrings()
                    return strings
                }) |> Async.Parallel |> Async.RunSynchronously |> Seq.collect(fun t -> t) |> Array.ofSeq

        //let foundStrings = 
        //    scanners |> Seq.map(fun s -> s.GetStrings()) |> Seq.collect(fun t -> t) |> Array.ofSeq

        // filter out contained spans
        // sort by offset and length
        // take first entry, then skip until at end of that span
        let stringsByOffset = foundStrings |> Array.groupBy(fun t -> t.o) |> Array.sortBy(fun (o, _) -> o)
        let sortedStrings = stringsByOffset |> Array.map(fun (_, t) -> t |> Array.sortByDescending(fun u -> u.s.Length)) |> Array.collect(fun u -> u)
        let nextSpan(s: StringOffsetPair seq): (StringOffsetPair * StringOffsetPair seq) option = 
            s |> Seq.truncate(1) |> Seq.tryHead |> 
                Option.map(fun nextString -> (nextString, s |> Seq.skip(1) |> Seq.skipWhile(fun t -> (t.o + int64 t.s.Length) <= (nextString.o + int64 nextString.s.Length))))

        let sortedStringsSeq = sortedStrings |> Seq.ofArray
        Seq.unfold(nextSpan)(sortedStringsSeq) |> Seq.toArray


type StreamStringScanner(nextChar: unit -> char option, t: Trie) = 
    member this.GetString(): string option = 
        let rec nextMove(acc: StringBuilder, nextNodes: Trie array, atTerminal: bool) = 
            match (nextChar(), atTerminal) with
            | (None, true) -> Some(acc.ToString())
            | (None, false) -> None
            | (Some(c), _) ->
                let nextNode = nextNodes |> Array.tryFind(fun t -> Trie.IsLetter(t, c))
                match nextNode with
                | Some(Trie.Node(_, nn, nextTerminal)) -> nextMove(acc.Append(c), nn, nextTerminal)
                | None when atTerminal -> Some(acc.ToString())
                | _ -> None

        match t with
        | Trie.Node(_, _, startTerminal) -> nextMove(new StringBuilder(), [| t |], startTerminal)
        | Trie.Root(nodes) -> nextMove(new StringBuilder(), nodes, false)

type FoundString = {
    offset: int64;
    value: string
    }
    with 
        override this.ToString() = this.offset.ToString("X8") + ": " + this.value

type StringScanner(path: string, dictionary: string array) = 
    new(config: TextScannerConfiguration) = 
        let validWords = 
            File.ReadAllLines(config.DictionaryFile) |> 
            Array.map(fun t -> t.Trim()) |> 
            Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t)) && (t.Length >= config.MinimumLength) && (t.Length <= config.MaximumLength))
        new StringScanner(config.Path, validWords)

    member this.ScanBytes(fileContents: byte array): FoundString array = 
        let scanner = new RabinKarpStringScanner(fileContents, dictionary, RabinFingerprintHasher.Seed([||], 0, 101))
        let foundStrings = scanner.GetStrings()
        foundStrings |> Array.map(fun fs -> { FoundString.offset = fs.o; value = fs.s })

    member private this.ScanFile(filePath: string) = 
        Console.WriteLine("scanning " + filePath + "...")
        let fileContents = File.ReadAllBytes(filePath)
        this.ScanBytes(fileContents)

    member this.Scan() = 
        let files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
        //let workers = files |> Array.map(fun t -> async { return (t, this.ScanFile(t)) }) |> Seq.ofArray
        //let scannedStrings = Async.Parallel(workers) |> Async.RunSynchronously
        let scannedStrings = files |> Array.map(fun t -> (t, this.ScanFile(t)))
        scannedStrings |> Array.filter(fun (_, str) -> str.Length > 0)

/// <summary>
/// Utility code to scan files in a path for things that match strings in a provided dictionary. The locations of these candidate
/// strings are then printed, for further analysis.
/// </summary>
type NaiveTextScanner(config: TextScannerConfiguration, dictionary: Trie) = 
    new(config: TextScannerConfiguration) = 
        // load the dictionary from the disk, and build the trie
        let d = Trie.Build(File.ReadAllLines(config.DictionaryFile))
        new NaiveTextScanner(config, d)

    member this.ScanBytes(fileContents: byte array): FoundString array =
        use ms = new MemoryStream(fileContents)
        let readNextStreamChar() =
            match ms.ReadByte() with
            | -1 -> None
            | c -> Some(char c)
        let scanner = new StreamStringScanner(readNextStreamChar, dictionary)
        let rec scanFileContents(idx: int64, acc: FoundString list): FoundString list = 
            match scanner.GetString() with
            | Some(str) when str.Length >= config.MinimumLength -> 
                let newIndex = idx + int64 str.Length
                let newRecord = { FoundString.offset = idx; value = str }
                if (ms.Seek(newIndex, SeekOrigin.Begin) < fileContents.LongLength) then
                    scanFileContents(newIndex, newRecord :: acc)
                else
                    newRecord :: acc
            | _ -> 
                let newIndex = idx + int64 1
                if (ms.Seek(newIndex, SeekOrigin.Begin) < fileContents.LongLength) then
                    scanFileContents(newIndex, acc)
                else
                    acc

        scanFileContents(int64 0, []) |> Array.ofList |> Seq.rev |> Array.ofSeq

    member private this.ScanFile(filePath: string) = 
        Console.WriteLine("scanning " + filePath + "...")
        let fileContents = File.ReadAllBytes(filePath)
        this.ScanBytes(fileContents)

    member this.Scan() = 
        let files = Directory.GetFiles(config.Path, "*.*", SearchOption.AllDirectories)
        let workers = files |> Array.map(fun t -> async { return (t, this.ScanFile(t)) }) |> Seq.ofArray
        let scannedStrings = Async.Parallel(workers) |> Async.RunSynchronously
        scannedStrings |> Array.filter(fun (_, str) -> str.Length > 0)

/// <summary>
/// Utility code to scan files in a path for things that look like filenames. The locations of these candidate
/// strings are then printed, for further analysis.
/// </summary>
type FilenameScanner(config: TextScannerConfiguration) = 
    let byteIsFilenameChar b = 
        let c = char b
        ((c >= 'A') && (c <= 'Z')) || ((c >= 'a') && (c <= 'z')) || ((c >= '0') && (c <= '9')) || (c = '.')
    let byteIsNotFilenameChar b = not(byteIsFilenameChar(b))
    let byteIsTextChar b = 
        let c = char b
        byteIsFilenameChar(b) || (c = ' ') || (c = ',') || (c = ':') || (c = '"')

    member private this.ScanFile(filePath: string, otw: TextWriter) = 
        let fileContents = File.ReadAllBytes(filePath)
        let rec getNextFilenameLength(t: byte array, i: int, acc: int) = 
            if ((i < t.Length) && byteIsFilenameChar(t.[i])) then
                getNextFilenameLength(t, i + 1, acc + 1)
            else
                acc
        let rec recScan2(t: byte array, i: int, acc: string list) = 
            if (i >= t.Length) then
                acc
            else
                let l = getNextFilenameLength(t, i, 0)
                match l with
                | 0 -> recScan2(t, i + 1, acc)
                | l when l <= config.MinimumLength -> recScan2(t, i + l, acc)
                | _ -> 
                    let candidateString = Encoding.ASCII.GetString(t, i, l)
                    if (candidateString.Contains(".")) then
                        recScan2(t, i + l, candidateString :: acc)
                    else
                        recScan2(t, i + l, acc)

        let foundFilenames = recScan2(fileContents, 0, [])
        match foundFilenames with 
        | [] -> ()
        | fn -> 
            otw.WriteLine("Found filenames in " + filePath + ":")
            fn |> List.iter(fun t -> otw.WriteLine(t))
            ()
        ()

    member this.Scan(otw: TextWriter) = 
        Directory.GetFiles(config.Path, "*.*", SearchOption.AllDirectories) |> Array.iter(fun t -> this.ScanFile(t, otw))