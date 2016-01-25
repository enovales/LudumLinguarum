module DebugToolsTests

open NUnit.Framework
open DebugTools
open System.IO
open System.Text

[<TestFixture>]
type RabinFingerprintHasherTests() = 
    let hashBase = 101

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestSeedEmptyString() = 
        let expected = {
            RabinFingerprintHasher.ba = [||];
            startIndex = int64 0;
            endIndex = int64 0;
            h = int64 0;
            hashBase = hashBase
        }
        let computed = RabinFingerprintHasher.Seed([||], 0, hashBase) :?> RabinFingerprintHasher
        Assert.AreEqual(expected.ba, computed.ba)
        Assert.AreEqual(expected.startIndex, computed.startIndex)
        Assert.AreEqual(expected.endIndex, computed.endIndex)
        Assert.AreEqual(expected.h, computed.h)
        Assert.AreEqual(expected.hashBase, computed.hashBase)

    [<Test>]
    member this.TestSeedSingleCharString() = 
        let expected = {
            RabinFingerprintHasher.ba = Encoding.UTF8.GetBytes("A");
            startIndex = int64 0;
            endIndex = int64 1;
            h = int64 'A';
            hashBase = hashBase
        }
        let computed = RabinFingerprintHasher.Seed(Encoding.UTF8.GetBytes("A"), 1, hashBase) :?> RabinFingerprintHasher
        Assert.AreEqual(expected.ba, computed.ba)
        Assert.AreEqual(expected.startIndex, computed.startIndex)
        Assert.AreEqual(expected.endIndex, computed.endIndex)
        Assert.AreEqual(expected.h, computed.h)
        Assert.AreEqual(expected.hashBase, computed.hashBase)

    [<Test>]
    member this.TestSeedTwoCharString() = 
        let expected = {
            RabinFingerprintHasher.ba = Encoding.UTF8.GetBytes("AB");
            startIndex = int64 0;
            endIndex = int64 2;
            h = (int64 'A' * int64 hashBase) + (int64 'B');
            hashBase = hashBase
        }
        let computed = RabinFingerprintHasher.Seed(Encoding.UTF8.GetBytes("AB"), 2, hashBase) :?> RabinFingerprintHasher
        Assert.AreEqual(expected.ba, computed.ba)
        Assert.AreEqual(expected.startIndex, computed.startIndex)
        Assert.AreEqual(expected.endIndex, computed.endIndex)
        Assert.AreEqual(expected.h, computed.h)
        Assert.AreEqual(expected.hashBase, computed.hashBase)


[<TestFixture>]
type FixedLengthRabinKarpStringScannerTests() = 
    let strings = [|
            "abcd";
            "abab"
        |]
    let stringLength = strings.[0].Length
    let hashBase = 101

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestScanEmptyString() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes(""), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = []
        let actual = rkss.GetStrings()
        Assert.AreEqual(expected, actual)
        
    [<Test>]
    member this.TestScanNotLongEnoughString() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("aaa"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = []
        let actual = rkss.GetStrings()
        Assert.AreEqual(expected, actual)
        
    [<Test>]
    member this.TestScanExactString() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("abcd"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abcd" }]
        let actual = rkss.GetStrings()
        Assert.AreEqual(expected, actual)
        
    [<Test>]
    member this.TestScanLongerString() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("abcdefgh"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abcd" }]
        let actual = rkss.GetStrings()
        Assert.AreEqual(expected, actual)

    [<Test>]
    member this.TestScanStringInMiddle() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("efghabcdijkl"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 4; s = "abcd" }]
        let actual = rkss.GetStrings()
        Assert.AreEqual(expected, actual)

    [<Test>]
    member this.TestScanStringAtEnd() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("efghabcd"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 4; s = "abcd" }]
        let actual = rkss.GetStrings()
        Assert.AreEqual(expected, actual)
        
    [<Test>]
    member this.TestScanTwoStrings() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("abcdabab"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abcd" }; { StringOffsetPair.o = int64 4; s = "abab" }] |> Set.ofList
        let actual = rkss.GetStrings() |> Set.ofList
        Assert.AreEqual(expected, actual)
    
    [<Test>]
    member this.TestScanOverlappingStrings() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("ababcd"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abab" }; { StringOffsetPair.o = int64 2; s = "abcd" }] |> Set.ofList
        let actual = rkss.GetStrings() |> Set.ofList
        Assert.AreEqual(expected, actual)
        
    [<Test>]
    member this.TestScanStringsWithSameHash() = 
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("ababcd"), strings, stringLength, new MapHasher([| ("abab", int64 1); ("abcd", int64 1) |]))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abab" }; { StringOffsetPair.o = int64 2; s = "abcd" }] |> Set.ofList
        let actual = rkss.GetStrings() |> Set.ofList
        Assert.AreEqual(expected, actual)

[<TestFixture>]
type RabinKarpStringScannerTests() = 
    let hashBase = 101
    let stringsToMatch = [|
            "abcd";
            "ab";
            "cdef";
            "ghi"
        |]
    let hasher = RabinFingerprintHasher.Seed([||], 0, hashBase)
    let scanner(d: byte array) = new RabinKarpStringScanner(d, stringsToMatch, hasher)

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestScanEmptyString() = 
        Assert.IsEmpty(scanner(Encoding.UTF8.GetBytes("")).GetStrings())

    [<Test>]
    member this.TestScanStringWithMinimalMatch() = 
        Assert.AreEqual([| { StringOffsetPair.o = int64 0; s = "ab" } |], scanner(Encoding.UTF8.GetBytes("ab")).GetStrings())

    [<Test>]
    member this.TestScanStringWithContainedSpans() = 
        Assert.AreEqual([| { StringOffsetPair.o = int64 0; s = "abcd" } |], scanner(Encoding.UTF8.GetBytes("abcd")).GetStrings())

    [<Test>]
    member this.TestScanStringWithTwoMatches() = 
        Assert.AreEqual([| { StringOffsetPair.o = int64 0; s = "ab" }; { StringOffsetPair.o = int64 2; s = "ghi" } |] |> Set.ofArray, scanner(Encoding.UTF8.GetBytes("abghi")).GetStrings() |> Set.ofArray)

    [<Test>]
    member this.TestScanStringWithOverlappingMatches() = 
        Assert.AreEqual([| { StringOffsetPair.o = int64 0; s = "abcd" }; { StringOffsetPair.o = int64 2; s = "cdef" } |] |> Set.ofArray, scanner(Encoding.UTF8.GetBytes("abcdef")).GetStrings() |> Set.ofArray)

[<TestFixture>]
type StreamStringScannerTests() = 
    let testContainsTrie = 
        Trie.Root(
            [| Trie.Node('a', 
                [| Trie.Node('b', 
                    [| Trie.Node('c', [||], true) |], true)
            |], false)
        |])

    let readNextChar(sr: StringReader)() = 
        match sr.Read() with
        | -1 -> None
        | c -> Some(char c)

    let emptyStream() = None

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestScanEmptyString() = 
        Assert.AreEqual(None, (new StreamStringScanner(emptyStream, testContainsTrie)).GetString())

    [<Test>]
    member this.TestScanFoundString() = 
        Assert.AreEqual(Some("ab"), (new StreamStringScanner(readNextChar(new StringReader("ab")), testContainsTrie)).GetString())

    [<Test>]
    member this.TestScanFoundStringWithoutSuffix() = 
        Assert.AreEqual(Some("ab"), (new StreamStringScanner(readNextChar(new StringReader("aba")), testContainsTrie)).GetString())

    [<Test>]
    member this.TestScanFoundCaseInsensitiveStrings() = 
        Assert.AreEqual(Some("AB"), (new StreamStringScanner(readNextChar(new StringReader("ABA")), testContainsTrie)).GetString())

    [<Test>]
    member this.TestScanNotFoundString() = 
        Assert.AreEqual(None, (new StreamStringScanner(readNextChar(new StringReader("a")), testContainsTrie)).GetString())

[<TestFixture>]
type TextScannerTests() = 
    let minimalStringConfig = new TextScannerConfiguration()
    let tooLongStringConfig = new TextScannerConfiguration()
    let testContainsTrie = 
        Trie.Root(
            [| Trie.Node('a', 
                [| Trie.Node('b', 
                    [| Trie.Node('c', [||], true) |], true)
            |], false)
        |])

    let minimalScanner = new NaiveTextScanner(minimalStringConfig, testContainsTrie)
    let tooLongScanner = new NaiveTextScanner(tooLongStringConfig, testContainsTrie)

    do
        minimalStringConfig.MinimumLength <- 1
        tooLongStringConfig.MinimumLength <- 10

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestGetNoStringsFromEmptyFile() = 
        Assert.IsEmpty(minimalScanner.ScanBytes([||]))

    [<Test>]
    member this.TestGetNoStringsFromValidFile() = 
        Assert.IsEmpty(minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("12345")))

    [<Test>]
    member this.TestGetSingleString() = 
        Assert.AreEqual([| { FoundString.offset = int64 0; value = "ab" } |], minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("ab")))

    [<Test>]
    member this.TestGetCaseInsensitiveString() = 
        Assert.AreEqual([| { FoundString.offset = int64 0; value = "AB" } |], minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("AB")))

    [<Test>]
    member this.TestGetMultipleStrings() = 
        let expected = [|
                { FoundString.offset = int64 0; value = "ab" };
                { FoundString.offset = int64 2; value = "ab" }
            |]
        Assert.AreEqual(expected, minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("abab")))

    [<Test>]
    member this.TestGetLongestString() = 
        Assert.AreEqual([| { FoundString.offset = int64 0; value = "abc" } |], minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("abc")))

    [<Test>]
    member this.TestSkipShortStrings() = 
        Assert.AreEqual([||], tooLongScanner.ScanBytes(Encoding.UTF8.GetBytes("ab")))
