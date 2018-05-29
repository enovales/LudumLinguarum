module SrtToolsTests

open System
open System.Text

open Expecto
open SrtTools

let private srtEntry = {
    SrtEntry.SubtitleId = "1"
    Timecodes = "00:00:00,000 --> 00:00:01,000"
    Subtitle = "Single entry"
}

let private srtExtractorEntry = {
    SrtBlockExtractorEntry.Id = int64 1
    Languages = "en"
    RelativePath = @"DATA\VIDEOS\blah_en.srt"
    OverrideBaseKey = @"DATA\VIDEOS\blah.srt"
    SubtitleIdStart = 1
    SubtitleIdEnd = 1
}

let private languageToEncoding(l: string) = Encoding.UTF8


[<Tests>]
let tests = 
  testList "SrtTools tests" [
    testCase "Parsing a single .srt entry" <|
      fun () -> 
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000
Test Single Line Subtitle""".Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = "Test Single Line Subtitle"
            }
        |]

        Expect.equal expected results "unexpected parse result"

    testCase "Parsing multiple .srt entries" <|
      fun () -> 
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000
Test Single Line Subtitle

2
00:00:01,000 --> 00:00:02,000
Test Single Line Subtitle 2""".Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = "Test Single Line Subtitle"
            };
            {
                SrtEntry.SubtitleId = "2"
                Timecodes = "00:00:01,000 --> 00:00:02,000"
                Subtitle = "Test Single Line Subtitle 2"
            }
        |]

        Expect.equal expected results "unexpected parse result"

    testCase "Parsing an .srt entry that has more than one line in the subtitle" <|
      fun () ->
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000
Test Two Line Subtitle
which is a subtitle that has two parts""".Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = "Test Two Line Subtitle which is a subtitle that has two parts"
            }
        |]

        Expect.equal expected results "unexpected parse result"

    testCase "Parsing an incomplete .srt entry" <|
      fun () ->
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000
Test Single Line Subtitle

2"""            .Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = "Test Single Line Subtitle"
            }
        |]

        Expect.equal expected results "unexpected parse result"

    testCase "Parsing a single blank subtitle" <|
      fun () ->
        let unSplitSubtitleText = """1
00:00:00,000 --> 00:00:01,000

"""
        let subtitleText = unSplitSubtitleText.Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = ""
            }
        |]

        Expect.equal expected results "unexpected parse result"

    testCase "Parsing multiple .srt entries with a malformed first entry" <|
      fun () ->
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000

2
00:00:01,000 --> 00:00:02,000
Test Single Line Subtitle 2""".Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = ""
            };
            {
                SrtEntry.SubtitleId = "2"
                Timecodes = "00:00:01,000 --> 00:00:02,000"
                Subtitle = "Test Single Line Subtitle 2"
            }
        |]

        Expect.equal expected results "unexpected parse result"

    testCase "Extracting a .csv file containing a single .srt entry" <|
      fun () ->
        let extractor = new SrtBlockExtractor([| srtExtractorEntry |], (fun _ -> [| srtEntry |]), languageToEncoding)
        let extracted = extractor.Extract()
        Expect.equal 1 (extracted |> Seq.length) "unexpected length of extracted data"

    testCase "Parsing of a single entry .csv file with .srt extraction information" <|
      fun () ->
        let csvText = """File,MappedFile,StringId,Language,SubtitleIdStart,SubtitleIdEnd,Comment
DATA\VIDEOS\blah_en.srt,DATA\VIDEOS\blah.srt,1,en,1,1,comment
        """

        let entries = 
            SrtBlockExtractor.GenerateEntriesForLines(csvText.Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None))
        let expected = {
            SrtBlockExtractorEntry.Id = int64 1
            SrtBlockExtractorEntry.Languages = "en"
            SrtBlockExtractorEntry.OverrideBaseKey = @"DATA\VIDEOS\blah.srt"
            SrtBlockExtractorEntry.RelativePath = @"DATA\VIDEOS\blah_en.srt"
            SrtBlockExtractorEntry.SubtitleIdStart = 1
            SrtBlockExtractorEntry.SubtitleIdEnd = 1
        }

        Expect.equal 1 (entries |> Seq.length) "unexpected extracted entries length"
        Expect.equal expected entries.[0] "error during extraction"

    testCase "Parsing a single entry .csv file with an empty "SubtitleIdEnd" field, which should default to the "SubtitleIdStart" field" <|
      fun () ->
        let csvText = """File,MappedFile,StringId,Language,SubtitleIdStart,SubtitleIdEnd,Comment
DATA\VIDEOS\blah_en.srt,DATA\VIDEOS\blah.srt,1,en,1,,comment
        """

        let entries = 
            SrtBlockExtractor.GenerateEntriesForLines(csvText.Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None))
        let expected = {
            SrtBlockExtractorEntry.Id = int64 1
            SrtBlockExtractorEntry.Languages = "en"
            SrtBlockExtractorEntry.OverrideBaseKey = @"DATA\VIDEOS\blah.srt"
            SrtBlockExtractorEntry.RelativePath = @"DATA\VIDEOS\blah_en.srt"
            SrtBlockExtractorEntry.SubtitleIdStart = 1
            SrtBlockExtractorEntry.SubtitleIdEnd = 1
        }

        Expect.equal 1 (entries |> Seq.length) "unexpected extracted entries length"
        Expect.equal expected entries.[0] "error during extraction"

    testCase "Parsing a .csv file with multiple .srt entries, including carry-over of previous values" <|
      fun () ->
        let csvText = """File,MappedFile,StringId,Language,SubtitleIdStart,SubtitleIdEnd,Comment
DATA\VIDEOS\blah_en.srt,DATA\VIDEOS\blah.srt,1,en,1,1,comment
,,2,en,2,2,comment
        """

        let entries = 
            SrtBlockExtractor.GenerateEntriesForLines(csvText.Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None))
        let expected1 = {
            SrtBlockExtractorEntry.Id = int64 1
            SrtBlockExtractorEntry.Languages = "en"
            SrtBlockExtractorEntry.OverrideBaseKey = @"DATA\VIDEOS\blah.srt"
            SrtBlockExtractorEntry.RelativePath = @"DATA\VIDEOS\blah_en.srt"
            SrtBlockExtractorEntry.SubtitleIdStart = 1
            SrtBlockExtractorEntry.SubtitleIdEnd = 1
        }

        let expected2 = {
            SrtBlockExtractorEntry.Id = int64 2
            SrtBlockExtractorEntry.Languages = "en"
            SrtBlockExtractorEntry.OverrideBaseKey = @"DATA\VIDEOS\blah.srt"
            SrtBlockExtractorEntry.RelativePath = @"DATA\VIDEOS\blah_en.srt"
            SrtBlockExtractorEntry.SubtitleIdStart = 2
            SrtBlockExtractorEntry.SubtitleIdEnd = 2
        }

        Expect.equal 2 (entries |> Seq.length) "unexpected extracted entries length"
        Expect.equal expected1 entries.[0] "first entry not extracted correctly"
        Expect.equal expected2 entries.[1] "second entry not extracted correctly"
  ]
