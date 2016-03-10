module TwoDATests

open InfinityAuroraEnginePlugins.TwoDA
open NUnit.Framework
open System.IO

[<TestFixture>]
type TwoDATests() = 
    let sample2DAText = 
        """2DA V2.0
DEFAULT: foo
 COLUMN1 COLUMN2 COLUMN3
0 a b c
1 d e f
2 g h i
        """

    let sample2DATextWithEmpty = 
        """2DA V2.0
DEFAULT: foo
 COLUMN1 COLUMN2 COLUMN3
0 a **** c
        """

    let sample2DATextWithQuotes = 
        """2DA V2.0
DEFAULT: foo
 COLUMN1 COLUMN2 COLUMN3
0 "This has quotes" b c
1 d e "This has quotes too"
2 g **** i
        """

    let sample2DATextWithInts = 
        """2DA V2.0

 COLUMN1 COLUMN2 COLUMN3
0 1 2 3
1 4 5 6
2 7 8 9"""

    let sample2DATextWithFloats = 
        """2DA V2.0

 COLUMN1 COLUMN2 COLUMN3
0 1.0 2.0 3.0
1 4.0 5.0 6.0
2 7.0 8.0 9.0"""

    [<Test>]
    member this.TestLoadTwoDA(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Assert.AreEqual(3, twoDA.RowCount)
        Assert.AreEqual(3, twoDA.ColumnCount)
        ()

    [<Test>]
    member this.TestExtractByRowColumnIndices(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Assert.AreEqual("a", twoDA.Value(0, 0))
        Assert.AreEqual("b", twoDA.Value(0, 1))
        Assert.AreEqual("c", twoDA.Value(0, 2))
        Assert.AreEqual("d", twoDA.Value(1, 0))
        Assert.AreEqual("e", twoDA.Value(1, 1))
        Assert.AreEqual("f", twoDA.Value(1, 2))
        Assert.AreEqual("g", twoDA.Value(2, 0))
        Assert.AreEqual("h", twoDA.Value(2, 1))
        Assert.AreEqual("i", twoDA.Value(2, 2))

    [<Test>]
    member this.TestExtractByColumnName(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Assert.AreEqual("a", twoDA.Value(0, "COLUMN1"))
        Assert.AreEqual("b", twoDA.Value(0, "COLUMN2"))
        Assert.AreEqual("c", twoDA.Value(0, "COLUMN3"))
        Assert.AreEqual("d", twoDA.Value(1, "COLUMN1"))
        Assert.AreEqual("e", twoDA.Value(1, "COLUMN2"))
        Assert.AreEqual("f", twoDA.Value(1, "COLUMN3"))
        Assert.AreEqual("g", twoDA.Value(2, "COLUMN1"))
        Assert.AreEqual("h", twoDA.Value(2, "COLUMN2"))
        Assert.AreEqual("i", twoDA.Value(2, "COLUMN3"))

    [<Test>]
    member this.TestDefaultValue(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DAText)
        Assert.AreEqual("foo", twoDA.Value(900, 400))

    [<Test>]
    member this.TestEmptyValue(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DATextWithEmpty)
        Assert.AreEqual("", twoDA.Value(0, 1))

    [<Test>]
    member this.TestQuotedValue(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DATextWithQuotes)
        Assert.AreEqual("This has quotes", twoDA.Value(0, 0))
        Assert.AreEqual("This has quotes too", twoDA.Value(1, 2))

    [<Test>]
    member this.TestIntValue(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DATextWithInts)
        Assert.AreEqual(Some(5), twoDA.ValueInt(1, 1))

    [<Test>]
    member this.TestFloatValue(): Unit = 
        let twoDA = TwoDAFile.FromString(sample2DATextWithFloats)
        Assert.AreEqual(Some(5.0f), twoDA.ValueFloat(1, 1))

    // TODO: add binary 2DA read test, using test data
