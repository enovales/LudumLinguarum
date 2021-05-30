module CsvToolsTests

open CsvTools
open Expecto
open System

type ExtractorType = string -> string array
type TestFunction = string * string array -> unit

let private tsvExtractor = extractFieldsForLine("\t")
let private ssvExtractor = extractFieldsForLine(";")
let private csvExtractor = extractFieldsForLine(",")

let private extractorMap = [| (tsvExtractor, "\t"); (ssvExtractor, ";"); (csvExtractor, ",") |]
let private runExtractorTest(ts: string, testFunction: TestFunction)(extractor: ExtractorType, delimiter: string) = 
    testFunction(delimiter, extractor(String.Format(ts, delimiter)))

[<Tests>]
let tests = 
  testList "CsvToolsTests" [
    testCase "A line with non-quoted strings should be split appropriately" <|
      fun () ->
        let testString = "[some_id]{0}abc{0}def{0}ghi{0}jkl"
        let asserts(delimiter: string, fields: string array) = 
          Expect.equal 5 fields.Length "should be 5 fields"
          Expect.equal "[some_id]" fields.[0] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
          Expect.equal "abc" fields.[1] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
          Expect.equal "def" fields.[2] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
          Expect.equal "ghi" fields.[3] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
          Expect.equal "jkl" fields.[4] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())

        extractorMap |> Array.iter(runExtractorTest(testString, asserts))

    testCase "A line with a single quoted string should be split appropriately" <|
      fun () ->
        let testString = "[some_id]{0}\"abc\"{0}def{0}ghi{0}jkl"
        let asserts(delimiter: string, fields: string array) = 
            Expect.equal 5 fields.Length ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "[some_id]" fields.[0] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "abc" fields.[1] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "def" fields.[2] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "ghi" fields.[3] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "jkl" fields.[4] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())

        extractorMap |> Array.iter(runExtractorTest(testString, asserts))

    testCase "A line with a quoted string and embedded tab should be split appropriately" <|
      fun () ->
        let testString = "[some_id]{0}\"ab{0}c\"{0}def{0}ghi{0}jkl"
        let asserts(delimiter: string, fields: string array) = 
            Expect.equal 5 fields.Length ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "[some_id]" fields.[0] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal (String.Format("ab{0}c", delimiter)) fields.[1] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "def" fields.[2] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "ghi" fields.[3] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "jkl" fields.[4] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())

        extractorMap |> Array.iter(runExtractorTest(testString, asserts))

    testCase "A line with a triple-quoted string should be split appropriately" <|
      fun () ->
        let testString = "[some_id]{0}\"\"\"abc\"\"\"{0}def{0}ghi{0}jkl"
        let asserts(delimiter: string, fields: string array) = 
            Expect.equal 5 fields.Length ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "[some_id]" fields.[0] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "\"abc\"" fields.[1] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "def" fields.[2] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "ghi" fields.[3] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "jkl" fields.[4] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())

        extractorMap |> Array.iter(runExtractorTest(testString, asserts))

    testCase "A line with a triple-quoted string ended with a format token should be split appropriately" <|
      fun () ->
        let testString = "[some_id]{0}\"\"\"Someone's talking\"\"[p]\"{0}def{0}ghi{0}jkl"
        let asserts(delimiter: string, fields: string array) = 
            Expect.equal 5 fields.Length ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "[some_id]" fields.[0] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "\"Someone's talking\"[p]" fields.[1] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "def" fields.[2] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "ghi" fields.[3] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "jkl" fields.[4] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())

        extractorMap |> Array.iter(runExtractorTest(testString, asserts))

    testCase "A line with multiple delimiters in a row should return an empty string for each one" <|
      fun () ->
        let testString = "[some_id]{0}{0}{0}123"
        let asserts(delimiter: string, fields: string array) = 
            Expect.equal 4 fields.Length ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "[some_id]" fields.[0] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal String.Empty fields.[1] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal String.Empty fields.[2] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())
            Expect.equal "123" fields.[3] ("failed to parse [" + testString + "] with delimiter [" + delimiter + "], fields were: " + fields.ToString())

        extractorMap |> Array.iter(runExtractorTest(testString, asserts))
  ]
