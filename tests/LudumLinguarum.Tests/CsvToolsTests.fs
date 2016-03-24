module CsvToolsTests

open CsvTools
open NUnit.Framework

[<TestFixture>]
type CsvToolsTests() = 
    [<Test>]
    member this.``A line with non-quoted strings should be split appropriately``() = 
        let testString = "{some_id}\tabc\tdef\tghi\tjkl"
        let fields = extractFieldsForLine(testString)
        Assert.AreEqual(5, fields.Length)
        Assert.AreEqual("{some_id}", fields.[0])
        Assert.AreEqual("abc", fields.[1])
        Assert.AreEqual("def", fields.[2])
        Assert.AreEqual("ghi", fields.[3])
        Assert.AreEqual("jkl", fields.[4])

    [<Test>]
    member this.``A line with a single quoted string should be split appropriately``() = 
        let testString = "{some_id}\t\"abc\"\tdef\tghi\tjkl"
        let fields = extractFieldsForLine(testString)
        Assert.AreEqual(5, fields.Length)
        Assert.AreEqual("{some_id}", fields.[0])
        Assert.AreEqual("abc", fields.[1])
        Assert.AreEqual("def", fields.[2])
        Assert.AreEqual("ghi", fields.[3])
        Assert.AreEqual("jkl", fields.[4])

    [<Test>]
    member this.``A line with a quoted string and embedded tab should be split appropriately``() = 
        let testString = "{some_id}\t\"ab\tc\"\tdef\tghi\tjkl"
        let fields = extractFieldsForLine(testString)
        Assert.AreEqual(5, fields.Length)
        Assert.AreEqual("{some_id}", fields.[0])
        Assert.AreEqual("ab\tc", fields.[1])
        Assert.AreEqual("def", fields.[2])
        Assert.AreEqual("ghi", fields.[3])
        Assert.AreEqual("jkl", fields.[4])

    [<Test>]
    member this.``A line with a triple-quoted string should be split appropriately``() = 
        let testString = "{some_id}\t\"\"\"abc\"\"\"\tdef\tghi\tjkl"
        let fields = extractFieldsForLine(testString)
        Assert.AreEqual(5, fields.Length)
        Assert.AreEqual("{some_id}", fields.[0])
        Assert.AreEqual("\"abc\"", fields.[1])
        Assert.AreEqual("def", fields.[2])
        Assert.AreEqual("ghi", fields.[3])
        Assert.AreEqual("jkl", fields.[4])

    [<Test>]
    member this.``A line with a triple-quoted string ended with a format token should be split appropriately``() = 
        let testString = "{some_id}\t\"\"\"Someone's talking\"\"[p]\"\tdef\tghi\tjkl"
        let fields = extractFieldsForLine(testString)
        Assert.AreEqual(5, fields.Length)
        Assert.AreEqual("{some_id}", fields.[0])
        Assert.AreEqual("\"Someone's talking\"[p]", fields.[1])
        Assert.AreEqual("def", fields.[2])
        Assert.AreEqual("ghi", fields.[3])
        Assert.AreEqual("jkl", fields.[4])
        ()

