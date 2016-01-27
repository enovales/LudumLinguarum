module SrtTools

open System
open System.IO

// Tools for dealing with .srt subtitle files.
type SrtEntry = {
        SubtitleId: string
        Timecodes: string
        Subtitle: string
    }
    with
        override self.ToString() = 
            "SrtEntry(id = " + self.SubtitleId + ", timecodes = " + self.Timecodes + ", subtitle = " + self.Subtitle + ")"

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

