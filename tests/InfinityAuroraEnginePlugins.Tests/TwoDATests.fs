module TwoDATests

open Expecto
open InfinityAuroraEnginePlugins.TwoDA
open System.IO

let private sample2DAText = 
  """2DA V2.0
DEFAULT: foo
 COLUMN1 COLUMN2 COLUMN3
0 a b c
1 d e f
2 g h i
  """

let private sample2DATextWithEmpty = 
  """2DA V2.0
DEFAULT: foo
 COLUMN1 COLUMN2 COLUMN3
0 a **** c
  """

let private sample2DATextWithQuotes = 
  """2DA V2.0
DEFAULT: foo
 COLUMN1 COLUMN2 COLUMN3
0 "This has quotes" b c
1 d e "This has quotes too"
2 g **** i
  """

let private sample2DATextWithInts = 
  """2DA V2.0

 COLUMN1 COLUMN2 COLUMN3
0 1 2 3
1 4 5 6
2 7 8 9"""

let private sample2DATextWithFloats = 
  """2DA V2.0

 COLUMN1 COLUMN2 COLUMN3
0 1.0 2.0 3.0
1 4.0 5.0 6.0
2 7.0 8.0 9.0"""

[<Tests>]
let tests = 
  testList "2DA tests" [
    testCase "Loading a simple 2DA" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Expect.equal 3 twoDA.RowCount "expected 3 rows in file"
        Expect.equal 3 twoDA.ColumnCount "expected 3 columns in file"

    testCase "Extracting by row and column indices" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Expect.equal "a" (twoDA.Value(0, 0)) "unexpected cell value"
        Expect.equal "b" (twoDA.Value(0, 1)) "unexpected cell value"
        Expect.equal "c" (twoDA.Value(0, 2)) "unexpected cell value"
        Expect.equal "d" (twoDA.Value(1, 0)) "unexpected cell value"
        Expect.equal "e" (twoDA.Value(1, 1)) "unexpected cell value"
        Expect.equal "f" (twoDA.Value(1, 2)) "unexpected cell value"
        Expect.equal "g" (twoDA.Value(2, 0)) "unexpected cell value"
        Expect.equal "h" (twoDA.Value(2, 1)) "unexpected cell value"
        Expect.equal "i" (twoDA.Value(2, 2)) "unexpected cell value"

    testCase "Extracting by column name" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Expect.equal "a" (twoDA.Value(0, "COLUMN1")) "unexpected cell value"
        Expect.equal "b" (twoDA.Value(0, "COLUMN2")) "unexpected cell value"
        Expect.equal "c" (twoDA.Value(0, "COLUMN3")) "unexpected cell value"
        Expect.equal "d" (twoDA.Value(1, "COLUMN1")) "unexpected cell value"
        Expect.equal "e" (twoDA.Value(1, "COLUMN2")) "unexpected cell value"
        Expect.equal "f" (twoDA.Value(1, "COLUMN3")) "unexpected cell value"
        Expect.equal "g" (twoDA.Value(2, "COLUMN1")) "unexpected cell value"
        Expect.equal "h" (twoDA.Value(2, "COLUMN2")) "unexpected cell value"
        Expect.equal "i" (twoDA.Value(2, "COLUMN3")) "unexpected cell value"

    testCase "Default value is used for indices outside the bounds of the 2DA" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Expect.equal "foo" (twoDA.Value(900, 400)) "default value not applied"

    testCase "Empty value is returned for ***" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DATextWithEmpty)
        Expect.equal "" (twoDA.Value(0, 1)) "empty value not applied for ***"

    testCase "Quoted cell values in 2DAs" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DATextWithQuotes)
        Expect.equal "This has quotes" (twoDA.Value(0, 0)) "Quotes not handled correctly"
        Expect.equal "This has quotes too" (twoDA.Value(1, 2)) "Quotes not handled correctly"

    testCase "Extracting integer values" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DATextWithInts)
        Expect.equal (Some 5) (twoDA.ValueInt(1, 1)) "Integer value not handled correctly"

    testCase "Extracting float values" <|
      fun () ->
        let twoDA = TwoDAFile.FromString(sample2DATextWithFloats)
        Expect.equal (Some 5.0f) (twoDA.ValueFloat(1, 1)) "Float value not handled correctly"

    // TODO: add binary 2DA read test, using test data
  ]

