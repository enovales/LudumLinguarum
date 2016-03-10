module StringExtractorsTests

open System.IO
open System.Text

open NUnit.Framework
open StringExtractors

[<TestFixture>]
type StringExtractorsTests() =
    let failStreamGeneration(e: CSVExtractorEntry): Stream = raise(exn("failure"))
    let stringStreamGenerator(s: string)(e: CSVExtractorEntry): Stream = new MemoryStream(Encoding.UTF8.GetBytes(s)) :> Stream
    let stringMapStreamGenerator(sm: Map<string, string>)(e: CSVExtractorEntry): Stream = 
        new MemoryStream(Encoding.UTF8.GetBytes(sm.Item(e.RelativePath))) :> Stream

    [<Test>]
    member this.``Extracting without any extraction entries``() = 
        let extractor = new CSVExtractor([||] |> Seq.ofArray, failStreamGeneration)
        Assert.IsEmpty(extractor.Extract())

    [<Test>]
    member this.``Extracting with a single entry``() = 
        let sampleStream = stringStreamGenerator("single")
        let sampleEntry = {
                CSVExtractorEntry.Id = int64 0
                Language = "en"
                Length = int64 3
                Offset = int64 1
                RelativePath = "foo"
                Gender = "masculine"
                SoundResource = ""
                Reversible = true
            }
        let extractor = new CSVExtractor([| sampleEntry |], sampleStream)
        let expected = [| { CSVExtractedEntry.Entry = sampleEntry; Text = "ing" } |] |> Set.ofArray
        let actual = extractor.Extract() |> Set.ofSeq
        Assert.AreEqual(expected, actual)

    [<Test>]
    member this.``Extracting with multiple entries``() = 
        let sampleStreamMap = stringMapStreamGenerator(Map([| ("abc", "def"); ("ghi", "jkl") |]))
        let sampleEntry1 = {
                CSVExtractorEntry.Id = int64 0
                Language = "en"
                Length = int64 3
                Offset = int64 0
                RelativePath = "abc"
                Gender = "masculine"
                SoundResource = ""
                Reversible = true
            }

        let sampleEntry2 = { sampleEntry1 with RelativePath = "ghi" }
        let extractor = new CSVExtractor([| sampleEntry1; sampleEntry2 |], sampleStreamMap)
        let expected = [| { CSVExtractedEntry.Entry = sampleEntry1; Text = "def" }; { CSVExtractedEntry.Entry = sampleEntry2; Text = "jkl" } |] |> Set.ofArray
        let actual = extractor.Extract() |> Set.ofSeq
        Assert.AreEqual(expected, actual)
