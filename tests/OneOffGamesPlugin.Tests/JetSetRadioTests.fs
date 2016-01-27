module JetSetRadioTests

open NUnit.Framework
open JetSetRadio
open System
open System.IO
open System.Text

[<TestFixture>]
type JetSetRadioTests() =
    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestReadUntilTooManyNullsOneNull() = 
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0 |]))
        Assert.AreEqual(Some(byte 1, 0), JetSetRadio.readUntilTooManyNulls(br, 1, false)(0))
        Assert.AreEqual(None, JetSetRadio.readUntilTooManyNulls(br, 1, false)(0))

    [<Test>]
    member this.TestReadUntilTooManyNullsTwoNulls() = 
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0; byte 0; |]))
        Assert.AreEqual(Some(byte 1, 0), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(Some(byte 0, 1), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(None, JetSetRadio.readUntilTooManyNulls(br, 2, false)(1))

    [<Test>]
    member this.TestReadUntilTooManyNullsCountNulls() = 
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0; byte 2; byte 0; byte 0 |]))
        Assert.AreEqual(Some(byte 1, 0), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(Some(byte 0, 1), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(Some(byte 2, 0), JetSetRadio.readUntilTooManyNulls(br, 2, false)(1))
        Assert.AreEqual(Some(byte 0, 1), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(None, JetSetRadio.readUntilTooManyNulls(br, 2, false)(1))

    [<Test>]
    member this.TestReadSingleStringSet() = 
        let testData = Encoding.ASCII.GetBytes("Japanese\u0000\u0000English\u0000\u0000French\u0000\u0000German\u0000\u0000Spanish\u0000\u0000\u0000")
        use br = new BinaryReader(new MemoryStream(testData))
        let result = Array.unfold(JetSetRadio.readStringSet(br, 5))()
        Assert.AreEqual(1, result.Length)
        Assert.AreEqual([| "Japanese"; "English"; "French"; "German"; "Spanish" |], result.[0])

    [<Test>]
    member this.TestReadingMultipleStringSets() = 
        let string1 = "A\u0000\u0000B\u0000\u0000C\u0000\u0000D\u0000\u0000E\u0000\u0000"
        let string2 = "F\u0000\u0000G\u0000\u0000H\u0000\u0000I\u0000\u0000J\u0000\u0000"
        let testData = Encoding.ASCII.GetBytes(string1 + string2 + "\u0000\u0000\u0000")
        use br = new BinaryReader(new MemoryStream(testData))
        let result = Array.unfold(JetSetRadio.readStringSet(br, 5))()
        Assert.AreEqual(2, result.Length)
        Assert.AreEqual([| "A"; "B"; "C"; "D"; "E" |], result.[0])
        Assert.AreEqual([| "F"; "G"; "H"; "I"; "J" |], result.[1])

    [<Test>]
    member this.TestSrtParseSingle() = 
        let subtitleText = 
            """1
00:00:00,000 --> 00:00:01,000
Test Single Line Subtitle""".Split([| Environment.NewLine; "\r"; "\n" |], StringSplitOptions.None)
        let results = JetSetRadio.parseSrtSubtitles(subtitleText) |> Seq.toArray
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
        let results = JetSetRadio.parseSrtSubtitles(subtitleText) |> Seq.toArray
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
        let results = JetSetRadio.parseSrtSubtitles(subtitleText) |> Seq.toArray
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
        let results = JetSetRadio.parseSrtSubtitles(subtitleText) |> Seq.toArray
        let expected = [| 
            {
                SrtEntry.SubtitleId = "1"
                Timecodes = "00:00:00,000 --> 00:00:01,000"
                Subtitle = "Test Single Line Subtitle"
            }
        |]

        Assert.AreEqual(expected, results)
