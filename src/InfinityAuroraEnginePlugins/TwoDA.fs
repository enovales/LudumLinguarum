module InfinityAuroraEnginePlugins.TwoDA

open System
open System.IO
open System.Text

type TwoDAFile(columnHeaders: string array, defaultValue: string option, rowData: string array array) = 
    member private this.translateEntryText(s: string option) = 
        match (s, defaultValue) with
        | (None, Some(d)) -> d
        | (Some("****"), _) -> ""
        | (Some(v), _) -> v
        | (None, None) -> ""

    member this.Value 
        with get(row: int, column: int) = 
            let attemptedExtract = 
                rowData |> Array.tryItem(row) |> Option.bind (fun t -> t |> Array.tryItem(column))
            this.translateEntryText(attemptedExtract)

    member this.Value 
        with get(row: int, column: string) = 
            let columnToUse = columnHeaders |> Array.tryFindIndex(fun t -> t = column)
            let attemptedExtract = 
                rowData |> Array.tryItem(row) |> 
                Option.bind (fun t -> columnToUse |> Option.bind (fun c -> t |> Array.tryItem(c)))
            this.translateEntryText(attemptedExtract)

    member private this.ValueIntInternal(s: string) = 
        let mutable res: int = 0
        match Int32.TryParse(s, &res) with
        | true -> Some(res)
        | false -> None

    member private this.ValueFloatInternal(s: string) =
        let mutable res = 0.0f
        match Single.TryParse(s, &res) with
        | true -> Some(res)
        | false -> None

    member this.ValueInt
        with get(row: int, column: int) = 
            this.ValueIntInternal(this.Value(row, column))

    member this.ValueInt
        with get(row: int, column: string) = 
            this.ValueIntInternal(this.Value(row, column))

    member this.ValueFloat
        with get(row: int, column: int) = 
            this.ValueFloatInternal(this.Value(row, column))

    member this.ValueFloat
        with get(row: int, column: string) = 
            this.ValueFloatInternal(this.Value(row, column))

    member this.RowCount = rowData.Length
    member this.ColumnCount = columnHeaders.Length

    member this.RowsWithValueInt
        with get(column: int) = 
            seq { for i in 0..(this.RowCount - 1) do yield (i, this.ValueInt(i, column)) } |>
                Seq.filter(fun (i, v) -> v.IsSome) |> Seq.map(fun (i, v) -> (i, v.Value)) |> Array.ofSeq

    member this.RowsWithValueInt
        with get(column: string) = 
            seq { for i in 0..(this.RowCount - 1) do yield (i, this.ValueInt(i, column)) } |>
                Seq.filter(fun (i, v) -> v.IsSome) |> Seq.map(fun (i, v) -> (i, v.Value)) |> Array.ofSeq

    member this.RowsWithValueFloat
        with get(column: int) = 
            seq { for i in 0..(this.RowCount - 1) do yield (i, this.ValueFloat(i, column)) } |>
                Seq.filter(fun (i, v) -> v.IsSome) |> Seq.map(fun (i, v) -> (i, v.Value)) |> Array.ofSeq

    member this.RowsWithValueFloat
        with get(column: string) = 
            seq { for i in 0..(this.RowCount - 1) do yield (i, this.ValueFloat(i, column)) } |>
                Seq.filter(fun (i, v) -> v.IsSome) |> Seq.map(fun (i, v) -> (i, v.Value)) |> Array.ofSeq

    member private this.WriteTextWithDelimiter(filePath: string, delimiter: char): unit = 
        let delimiterString = new String(delimiter, 1)
        use sw = new StreamWriter(filePath, false, Encoding.ASCII)
        sw.WriteLine("2DA V2.0")
        match defaultValue with
        | Some(dv) -> sw.WriteLine("DEFAULT: " + dv)
        | _ -> sw.WriteLine()

        let buildColumns (acc: StringBuilder)(t: string) = acc.Append(t + delimiterString)
        let acc = new StringBuilder()
        let headerRow = delimiterString + (Array.fold buildColumns acc columnHeaders).ToString().TrimEnd(delimiter)
        sw.WriteLine(headerRow)

        // Now write each row, prepended with a row label.
        for i = 0 to (this.RowCount - 1) do
            let rowValues = (i.ToString()) :: (seq { for j in 0..(this.ColumnCount - 1) do yield this.Value(i, j)} |> List.ofSeq)
            let rowText = (List.fold(fun acc t -> acc + t + delimiterString) "" rowValues).TrimEnd(delimiter)

            // TODO: handle quoting text where necessary
            sw.WriteLine(rowText)
        ()

    member this.WriteText2DA(filePath: string): unit = 
        this.WriteTextWithDelimiter(filePath, ' ')

    member this.WriteCSV(filePath: string): unit = 
        this.WriteTextWithDelimiter(filePath, ',')

    static member private AccumulateQuotedStrings
        (curString: string, curList: string list, l: string list, inQuote: bool): string list = 
        if (l.IsEmpty = true) then
            curString.Trim('"') :: curList
        else
            match (inQuote, l.Head) with
            | (true, s) when s.EndsWith("\"") -> 
                // end this string, and continue
                TwoDAFile.AccumulateQuotedStrings("", (curString + s).Trim('"') :: curList, l.Tail, false)
            | (true, s) -> 
                // continue accumulating quoted string
                TwoDAFile.AccumulateQuotedStrings(curString + s, curList, l.Tail, inQuote)
            | (false, s) when s.StartsWith("\"") -> 
                // start quoted string
                TwoDAFile.AccumulateQuotedStrings(s, curList, l.Tail, true)
            | (false, s) -> 
                // standard string
                TwoDAFile.AccumulateQuotedStrings("", s :: curList, l.Tail, false)

    static member private ParseDataLine(t: string): array<string> = 
        // first, split by quotes. odd-indexed strings are inside quotes.
        let splitByQuotes = t.Split([| '"' |], StringSplitOptions.RemoveEmptyEntries)
        let (unquotedWithIndex, quotedWithIndex) = 
            splitByQuotes |> Array.mapi(fun i t -> (i, t)) |> 
            Array.partition(fun (i, t) -> (i % 2) = 0)

        // for the unquoted strings, generate an array of space-separated strings.
        // then, combine it with the quoted strings, and (stably) sort by the index using Seq.sort.
        let generatedUnquotedArrays = 
            unquotedWithIndex |> Array.map(fun (i, t) -> (i, t.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)))

        let generatedAllUnquoted = generatedUnquotedArrays |> Array.collect(fun (i, t) -> t |> Array.map(fun u -> (i, u)))
        let sorted = 
            Array.concat([| generatedAllUnquoted; quotedWithIndex |]) |> 
            Seq.ofArray |> Seq.sortBy(fun (i, t) -> i) |> 
            Array.ofSeq |> Array.map(fun (i, t) -> t)

        if (sorted.Length > 1) then
            sorted |> Array.skip(1)
        else
            sorted

    static member private Read2DAAscii(tr: TextReader): (string array * string option * string array array) =
        let defaultPrefix = "DEFAULT: "
        let defaultLine = tr.ReadLine()
        let defaultValue =
            if (defaultLine.StartsWith(defaultPrefix)) then
                Some(defaultLine.Remove(0, defaultPrefix.Length))
            else
                None

        let columnHeaders = tr.ReadLine().Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        let restOfText = tr.ReadToEnd().Split([| Environment.NewLine; new String(char 10, 1); new String(char 13, 1) |], StringSplitOptions.RemoveEmptyEntries)
        let rowData = restOfText |> Array.map (fun t -> TwoDAFile.ParseDataLine(t)) |> Array.filter(fun t -> t.Length > 0)
        (columnHeaders, defaultValue, rowData)

    static member private ReadNext2DABinaryString(br: BinaryReader, ?acc0: StringBuilder): char * string = 
        let acc = defaultArg acc0 (new StringBuilder())
        match char(br.ReadChar()) with
        | c when (c = char 0) || (c = '\t') -> (c, acc.ToString())
        | c -> TwoDAFile.ReadNext2DABinaryString(br, acc.Append(c))

    static member private Read2DABinaryColumnSet(br: BinaryReader, ?acc0: string list): string list = 
        let acc = defaultArg acc0 []
        match TwoDAFile.ReadNext2DABinaryString(br) with
        | ('\t', col) -> TwoDAFile.Read2DABinaryColumnSet(br, col :: acc)
        | (c, col) when c = char 0 -> 
            if (col <> "") then 
                (col :: acc) |> List.rev
            else
                acc |> List.rev
        | _ -> raise(exn("invalid 2DA binary file"))

    static member private Read2DABinary(br: BinaryReader): (string array * string option * string array array) = 
        let columns = TwoDAFile.Read2DABinaryColumnSet(br)
        let rowCount = br.ReadUInt32()
        let rowLabels = [ 
            for i in 1u..rowCount do 
                yield snd(TwoDAFile.ReadNext2DABinaryString(br))
        ]
        let dataOffsets = 
            [ 
                for i in 1u..rowCount do
                    yield [
                        for j in 1u..(uint32 columns.Length) do
                            yield br.ReadUInt16()
                    ] |> Array.ofSeq
            ] |> Array.ofSeq
        br.ReadUInt16() |> ignore

        let startOfDataSection = br.BaseStream.Position
        let getRowData(i: int, j: int) = 
            let rowDataOffset = int64 (dataOffsets.[i - 1].[j - 1])
            let finalPosition = startOfDataSection + rowDataOffset
            br.BaseStream.Seek(finalPosition, SeekOrigin.Begin) |> ignore
            snd(TwoDAFile.ReadNext2DABinaryString(br))
        let rowData = 
            [
                for i in 1..(int rowCount) do
                    yield [
                            for j in 1..columns.Length do
                                yield getRowData(i, j)
                          ] |> Array.ofSeq
            ] |> Array.ofSeq

        // WIP
        (columns |> Array.ofList, None, rowData)

    static member private FromReadersInternal(trOpt: TextReader option, brOpt: BinaryReader option) = 
        let (columnHeaders, defaultValue, rowData) = 
            match (trOpt, brOpt) with
            | (Some(tr), None) ->
                // Building this from a string. Binary 2DA is not possible.
                tr.ReadLine() |> ignore
                TwoDAFile.Read2DAAscii(tr)
            | (Some(tr), Some(br)) ->
                // Read enough to determine if this is a binary 2DA.
                let binary2DAString = "2DA V2.b\n"
                let headerLine = Encoding.ASCII.GetString(br.ReadBytes(binary2DAString.Length))
                match headerLine with
                | x when x = binary2DAString ->
                    TwoDAFile.Read2DABinary(br)
                | _ ->
                    // rewind, and proceed with normal text 2DA reading.
                    br.BaseStream.Seek(int64 0, SeekOrigin.Begin) |> ignore
                    tr.ReadLine() |> ignore
                    TwoDAFile.Read2DAAscii(tr)
            | _ -> raise(exn("invalid set of readers"))

        new TwoDAFile(columnHeaders, defaultValue, rowData)

    static member private FromStreamInternal(fs: Stream) = 
        use sr = new StreamReader(fs, Encoding.ASCII, false, 64 * 1024, true)
        use br = new BinaryReader(fs, Encoding.ASCII, true)
        TwoDAFile.FromReadersInternal(Some(sr :> TextReader), Some(br))

    static member FromStream(fs: Stream) = 
        TwoDAFile.FromStreamInternal(fs)

    static member FromFile(fn: string) = 
        use fs = new FileStream(fn, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        TwoDAFile.FromStreamInternal(fs)

    static member FromString(s: string) = 
        use tr = new StringReader(s)
        TwoDAFile.FromReadersInternal(Some(tr :> TextReader), None)
