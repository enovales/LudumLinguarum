module JetSetRadioTests

open NUnit.Framework
open JetSetRadio
open System
open System.IO
open System.Text

[<TestFixture>]
type JetSetRadioTests() =
    [<Test>]
    member this.``Read until too many nulls, with one null``() = 
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0 |]))
        Assert.AreEqual(Some(byte 1, 0), JetSetRadio.readUntilTooManyNulls(br, 1, false)(0))
        Assert.AreEqual(None, JetSetRadio.readUntilTooManyNulls(br, 1, false)(0))

    [<Test>]
    member this.``Read until too many nulls, with two nulls``() = 
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0; byte 0; |]))
        Assert.AreEqual(Some(byte 1, 0), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(Some(byte 0, 1), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(None, JetSetRadio.readUntilTooManyNulls(br, 2, false)(1))

    [<Test>]
    member this.``Counting of nulls in readUntilTooManyNulls``() = 
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0; byte 2; byte 0; byte 0 |]))
        Assert.AreEqual(Some(byte 1, 0), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(Some(byte 0, 1), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(Some(byte 2, 0), JetSetRadio.readUntilTooManyNulls(br, 2, false)(1))
        Assert.AreEqual(Some(byte 0, 1), JetSetRadio.readUntilTooManyNulls(br, 2, false)(0))
        Assert.AreEqual(None, JetSetRadio.readUntilTooManyNulls(br, 2, false)(1))

    [<Test>]
    member this.``Reading a single null-delimited string set``() = 
        let testData = Encoding.ASCII.GetBytes("Japanese\u0000\u0000English\u0000\u0000French\u0000\u0000German\u0000\u0000Spanish\u0000\u0000\u0000")
        use br = new BinaryReader(new MemoryStream(testData))
        let result = Array.unfold(JetSetRadio.readStringSet(br, 5))()
        Assert.AreEqual(1, result.Length)
        Assert.AreEqual([| "Japanese"; "English"; "French"; "German"; "Spanish" |], result.[0])

    [<Test>]
    member this.``Reading multiple null-delimited string sets``() = 
        let string1 = "A\u0000\u0000B\u0000\u0000C\u0000\u0000D\u0000\u0000E\u0000\u0000"
        let string2 = "F\u0000\u0000G\u0000\u0000H\u0000\u0000I\u0000\u0000J\u0000\u0000"
        let testData = Encoding.ASCII.GetBytes(string1 + string2 + "\u0000\u0000\u0000")
        use br = new BinaryReader(new MemoryStream(testData))
        let result = Array.unfold(JetSetRadio.readStringSet(br, 5))()
        Assert.AreEqual(2, result.Length)
        Assert.AreEqual([| "A"; "B"; "C"; "D"; "E" |], result.[0])
        Assert.AreEqual([| "F"; "G"; "H"; "I"; "J" |], result.[1])

    [<Test>]
    member this.``Getting a custom instructions string set with a single string``() = 
        let node = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "Foo"
                Children = []
            }

        Assert.AreEqual([| "Foo" |], JetSetRadio.getCustomInstructionsStrings(node))

    [<Test>]
    member this.``Getting a custom instructions string set with multiple strings``() = 
        let node = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "Foo"
                Children = 
                    [{
                        CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                        Text = "Bar"
                        Children = []
                    }]
            }

        Assert.AreEqual([| "Foo"; "Bar" |], JetSetRadio.getCustomInstructionsStrings(node))

    [<Test>]
    member this.``Skipping embedded images in custom instructions strings``() = 
        let node = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Image
                Text = ""
                Children = []
            }

        Assert.AreEqual([||], JetSetRadio.getCustomInstructionsStrings(node))

    [<Test>]
    member this.``Parsing custom instructions content with a single node``() = 
        let text = """[P0||||Test]"""
        use reader = new StringReader(text)
        let result = JetSetRadio.readCustomInstructions(reader)
        let expected = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "Test"
                Children = []
            }
        Assert.AreEqual(expected, result)

    [<Test>]
    member this.``Parsing custom instructions content with an embedded node``() = 
        let text = """[P0||||[P1||||Test]String]"""
        use reader = new StringReader(text)
        let result = JetSetRadio.readCustomInstructions(reader)
        let expected = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "String"
                Children = 
                    [{
                        CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                        Text = "Test"
                        Children = []
                    }]
            }

        Assert.AreEqual(expected, result)

    [<Test>]
    member this.``Parsing custom instructions content with an image node``() = 
        let text = """[P0||||[I1||||Test.png]String]"""
        use reader = new StringReader(text)
        let result = JetSetRadio.readCustomInstructions(reader)
        let expected = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "String"
                Children = 
                    [{
                        CustomInstructionsNode.Type = CustomInstructionsNodeType.Image
                        Text = ""
                        Children = []
                    }]
            }

        Assert.AreEqual(expected, result)
