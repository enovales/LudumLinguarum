module SrtToolsTests

open System
open System.Text

open NUnit.Framework
open SrtTools

[<TestFixture>]
type SrtToolsTests() =
    let srtEntry = {
        SrtEntry.SubtitleId = "1"
        Timecodes = "00:00:00,000 --> 00:00:01,000"
        Subtitle = "Single entry"
    }

    let srtExtractorEntry = {
        SrtBlockExtractorEntry.Id = int64 1
        Languages = "en"
        RelativePath = @"DATA\VIDEOS\blah_en.srt"
        OverrideBaseKey = @"DATA\VIDEOS\blah.srt"
        SubtitleIdStart = 1
        SubtitleIdEnd = 1
    }

    let languageToEncoding(l: string) = Encoding.UTF8

    [<Test>]
    member this.``Parsing a single .srt entry``() = 
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

        Assert.AreEqual(expected, results)

    [<Test>]
    member this.``Parsing multiple .srt entries``() = 
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

        Assert.AreEqual(expected, results)

    [<Test>]
    member this.``Parsing an .srt entry that has more than one line in the subtitle``() = 
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

        Assert.AreEqual(expected, results)

    [<Test>]
    member this.``Parsing an incomplete .srt entry``() = 
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

        Assert.AreEqual(expected, results)

    [<Test>]
    member this.``Parsing a single blank subtitle``() = 
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

        Assert.AreEqual(expected, results)        

    [<Test>]
    member this.``Parsing multiple .srt entries with a malformed first entry``() = 
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

        Assert.AreEqual(expected, results)

    [<Test>]
    member this.``Extracting a .csv file containing a single .srt entry``() = 
        let extractor = new SrtBlockExtractor([| srtExtractorEntry |], (fun _ -> [| srtEntry |]), languageToEncoding)
        let extracted = extractor.Extract()
        Assert.AreEqual(1, extracted |> Seq.length)

    [<Test>]
    member this.``Parsing of a single entry .csv file with .srt extraction information``() = 
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

        Assert.AreEqual(1, entries |> Seq.length)
        Assert.AreEqual(expected, entries.[0])

    [<Test>]
    member this.``Parsing a single entry .csv file with an empty "SubtitleIdEnd" field, which should default to the "SubtitleIdStart" field``() = 
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

        Assert.AreEqual(1, entries |> Seq.length)
        Assert.AreEqual(expected, entries.[0])

    [<Test>]
    member this.``Parsing a .csv file with multiple .srt entries, including carry-over of previous values``() = 
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

        Assert.AreEqual(2, entries |> Seq.length)
        Assert.AreEqual(expected1, entries.[0])
        Assert.AreEqual(expected2, entries.[1])

