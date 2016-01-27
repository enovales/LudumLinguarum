module SrtToolsTests

open System

open NUnit.Framework
open SrtTools

[<TestFixture>]
type SrtToolsTests() =
    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

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
