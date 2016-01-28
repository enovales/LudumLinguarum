module SrtToolsTests

open System

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

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    /// <summary>
    /// Test parsing of a single .srt entry.
    /// </summary>
    [<Test>]
    member this.TestSrtParseSingle() = 
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

    /// <summary>
    /// Test parsing of multiple .srt entries.
    /// </summary>
    [<Test>]
    member this.TestSrtParseMultipleEntries() = 
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

    /// <summary>
    /// Teset parsing an .srt entry that has more than one line in the subtitle.
    /// </summary>
    [<Test>]
    member this.TestSrtParseLongEntry() = 
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

    /// <summary>
    /// Test parsing of an incomplete .srt entry.
    /// </summary>
    [<Test>]
    member this.TestSrtParseMalformedEntry() = 
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000
Test Single Line Subtitle

2
00:00:01,000 --> 00:00:02,000""".Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = SrtTools.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = "Test Single Line Subtitle"
            }
        |]

        Assert.AreEqual(expected, results)

    /// <summary>
    /// Test extraction of a .csv file containing a single .srt entry to be extracted.
    /// </summary>
    [<Test>]
    member this.TestSrtBlockExtractSingle() = 
        let extractor = new SrtBlockExtractor([| srtExtractorEntry |], (fun _ -> [| srtEntry |]))
        let extracted = extractor.Extract()
        Assert.AreEqual(1, extracted |> Seq.length)

    /// <summary>
    /// Test parsing of a single entry .csv file with .srt extraction information.
    /// </summary>
    [<Test>]
    member this.TestSrtBlockParsing() = 
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

    /// <summary>
    /// Test parsing of a single entry .csv file, where the "SubtitleIdEnd" field is empty. It
    /// should be assumed to be equal to the "SubtitleIdStart" field.
    /// </summary>
    [<Test>]
    member this.TestSrtBlockParsingEmptySubtitleIdEnd() = 
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

