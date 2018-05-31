module DebugToolsTests

open DebugTools
open Expecto
open System.IO
open System.Text

[<Tests>]
let rabinFingerprintHasherTests = 
  let hashBase = 101
  testList "Rabin fingerprint hasher tests" [
    testCase "Seeding with an empty array" <|
      fun () ->
        let expected = {
            RabinFingerprintHasher.ba = [||];
            startIndex = int64 0;
            endIndex = int64 0;
            h = int64 0;
            hashBase = hashBase
        }
        let computed = RabinFingerprintHasher.Seed([||], 0, hashBase) :?> RabinFingerprintHasher
        Expect.equal expected.ba computed.ba "unexpected byte array"
        Expect.equal expected.startIndex computed.startIndex "unexpected start index"
        Expect.equal expected.endIndex computed.endIndex "unexpected end index"
        Expect.equal expected.h computed.h "unexpected hash"
        Expect.equal expected.hashBase computed.hashBase "unexpected hash base"

    testCase "Seeding with a single byte" <|
      fun () ->
        let expected = {
            RabinFingerprintHasher.ba = Encoding.UTF8.GetBytes("A");
            startIndex = int64 0;
            endIndex = int64 1;
            h = int64 'A';
            hashBase = hashBase
        }
        let computed = RabinFingerprintHasher.Seed(Encoding.UTF8.GetBytes("A"), 1, hashBase) :?> RabinFingerprintHasher
        Expect.equal expected.ba computed.ba "unexpected byte array"
        Expect.equal expected.startIndex computed.startIndex "unexpected start index"
        Expect.equal expected.endIndex computed.endIndex "unexpected end index"
        Expect.equal expected.h computed.h "unexpected hash"
        Expect.equal expected.hashBase computed.hashBase "unexpected hash base"

    testCase "Seeding with two bytes" <|
      fun () ->
        let expected = {
            RabinFingerprintHasher.ba = Encoding.UTF8.GetBytes("AB");
            startIndex = int64 0;
            endIndex = int64 2;
            h = (int64 'A' * int64 hashBase) + (int64 'B');
            hashBase = hashBase
        }
        let computed = RabinFingerprintHasher.Seed(Encoding.UTF8.GetBytes("AB"), 2, hashBase) :?> RabinFingerprintHasher
        Expect.equal expected.ba computed.ba "unexpected byte array"
        Expect.equal expected.startIndex computed.startIndex "unexpected start index"
        Expect.equal expected.endIndex computed.endIndex "unexpected end index"
        Expect.equal expected.h computed.h "unexpected hash"
        Expect.equal expected.hashBase computed.hashBase "unexpected hash base"
  ]

[<Tests>]
let fixedLengthRabinKarpStringScannerTests = 
  let strings = 
      [|
          "abcd"
          "abab"
      |]
  let stringLength = strings.[0].Length
  let hashBase = 101

  testList "fixed length Rabin-Karp string scanner tests" [
    testCase "Scanning an empty string" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes(""), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = []
        let actual = rkss.GetStrings()
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string that's not long enough to match any of the strings in the dictionary" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("aaa"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = []
        let actual = rkss.GetStrings()
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string that's an exact match for one of the strings in the dictionary" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("abcd"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abcd" }]
        let actual = rkss.GetStrings()
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string that matches one of the strings in the dictionary, and has some excess" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("abcdefgh"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abcd" }]
        let actual = rkss.GetStrings()
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string where a match exists in the middle of the string" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("efghabcdijkl"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 4; s = "abcd" }]
        let actual = rkss.GetStrings()
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string where a match exists at the end of the string" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("efghabcd"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 4; s = "abcd" }]
        let actual = rkss.GetStrings()
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string with two non-overlapping matches" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("abcdabab"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abcd" }; { StringOffsetPair.o = int64 4; s = "abab" }] |> Set.ofList
        let actual = rkss.GetStrings() |> Set.ofList
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string with two overlapping matches" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("ababcd"), strings, stringLength, RabinFingerprintHasher.Seed([||], 0, hashBase))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abab" }; { StringOffsetPair.o = int64 2; s = "abcd" }] |> Set.ofList
        let actual = rkss.GetStrings() |> Set.ofList
        Expect.equal expected actual "unexpected result"

    testCase "Scanning a string with two matches, where their dictionary entries have the same hash" <|
      fun () ->
        let rkss = new FixedLengthRabinKarpStringScanner(Encoding.UTF8.GetBytes("ababcd"), strings, stringLength, new MapHasher([| ("abab", int64 1); ("abcd", int64 1) |]))
        let expected = [ { StringOffsetPair.o = int64 0; s = "abab" }; { StringOffsetPair.o = int64 2; s = "abcd" }] |> Set.ofList
        let actual = rkss.GetStrings() |> Set.ofList
        Expect.equal expected actual "unexpected result"
  ]

[<Tests>]
let rabinKarpStringScannerTests = 
  let hashBase = 101
  let stringsToMatch = [|
          "abcd";
          "ab";
          "cdef";
          "ghi"
      |]
  let hasher = RabinFingerprintHasher.Seed([||], 0, hashBase)
  let scanner(d: byte array) = new RabinKarpStringScanner(d, stringsToMatch, hasher)

  testList "Rabin-Karp string scanner tests" [
    testCase "Scanning an empty string" <|
      fun () -> Expect.isEmpty (scanner(Encoding.UTF8.GetBytes("")).GetStrings()) "scanner should return no strings"

    testCase "Scanning a string with a minimal match" <|
      fun () -> Expect.equal [| { StringOffsetPair.o = int64 0; s = "ab" } |] (scanner(Encoding.UTF8.GetBytes("ab")).GetStrings()) "should have single string"

    testCase "Scanning a string with contained spans, and matching the longest one" <|
      fun () -> Expect.equal [| { StringOffsetPair.o = int64 0; s = "abcd" } |] (scanner(Encoding.UTF8.GetBytes("abcd")).GetStrings()) "should match longest string"

    testCase "Scanning a string with two matches" <|
      fun () -> 
        let expected = [| { StringOffsetPair.o = int64 0; s = "ab" }; { StringOffsetPair.o = int64 2; s = "ghi" } |] |> Set.ofArray
        let found = scanner(Encoding.UTF8.GetBytes("abghi")).GetStrings() |> Set.ofArray
        Expect.equal expected found "should find two matches"

    testCase "Scanning a string with overlapping matches" <|
      fun () -> 
        let expected = [| { StringOffsetPair.o = int64 0; s = "abcd" }; { StringOffsetPair.o = int64 2; s = "cdef" } |] |> Set.ofArray
        let found = scanner(Encoding.UTF8.GetBytes("abcdef")).GetStrings() |> Set.ofArray
        Expect.equal expected found "should find two overlapping matches"
  ]

[<Tests>]
let streamStringScannerTests = 
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

  testList "Stream string scanner tests" [
    testCase "Scanning an empty string" <|
      fun () -> Expect.equal None ((new StreamStringScanner(emptyStream, testContainsTrie)).GetString()) "should be None"

    testCase "Scanning a string with a single match" <|
      fun () -> Expect.equal (Some "ab") ((new StreamStringScanner(readNextChar(new StringReader("ab")), testContainsTrie)).GetString()) "should be 'ab'"

    testCase "Scanning a string with a match, and some extra characters beyond the match" <|
      fun () -> Expect.equal (Some "ab") ((new StreamStringScanner(readNextChar(new StringReader("aba")), testContainsTrie)).GetString()) "should be 'ab'"

    testCase "Case insensitivity of matching" <|
      fun () -> Expect.equal (Some "AB") ((new StreamStringScanner(readNextChar(new StringReader("ABA")), testContainsTrie)).GetString()) "should be 'ab'"

    testCase "Scanning a string with no matches" <|
      fun () -> Expect.equal None ((new StreamStringScanner(readNextChar(new StringReader("a")), testContainsTrie)).GetString()) "should be None"
  ]

[<Tests>]
let textScannerTests = 
  let minimalStringConfig = { TextScannerConfiguration.Empty with MinimumLength = 1 }
  let tooLongStringConfig = { TextScannerConfiguration.Empty with MinimumLength = 10 }
  let testContainsTrie = 
      Trie.Root(
          [| Trie.Node('a', 
              [| Trie.Node('b', 
                  [| Trie.Node('c', [||], true) |], true)
          |], false)
      |])

  let minimalScanner = new NaiveTextScanner(minimalStringConfig, testContainsTrie)
  let tooLongScanner = new NaiveTextScanner(tooLongStringConfig, testContainsTrie)

  testList "Text scanner tests" [
    testCase "Scanning an empty file returns no strings" <|
      fun () -> Expect.isEmpty (minimalScanner.ScanBytes([||])) "should be empty"

    testCase "Matching no strings from a valid file" <|
      fun () -> Expect.isEmpty (minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("12345"))) "should be empty when no strings matched"

    testCase "Matching a single string" <|
      fun () -> 
        let expected = [| { FoundString.offset = int64 0; value = "ab" } |]
        let found = minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("ab"))
        Expect.equal expected found "should match a single string"

    testCase "Matching is case insensitive" <|
      fun () ->
        let expected = [| { FoundString.offset = int64 0; value = "AB" } |]
        let found = minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("AB"))
        Expect.equal expected found "should match a single string"

    testCase "Matching multiple strings" <|
      fun () ->
        let expected = [|
            { FoundString.offset = int64 0; value = "ab" };
            { FoundString.offset = int64 2; value = "ab" }
          |]
        let found = minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("abab"))
        Expect.equal expected found "should match multiple strings"

    testCase "Returning the longest possible match" <|
      fun () ->
        let expected = [| { FoundString.offset = int64 0; value = "abc" } |]
        let found = minimalScanner.ScanBytes(Encoding.UTF8.GetBytes("abc"))
        Expect.equal expected found "should return the longest possible match"

    testCase "Skip strings that are shorter than the configured minimum" <|
      fun () -> Expect.equal [||] (tooLongScanner.ScanBytes(Encoding.UTF8.GetBytes("ab"))) "should return no strings"
  ]
