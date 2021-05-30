module JetSetRadioTests

open Expecto
open JetSetRadio
open System
open System.IO
open System.Text

[<Tests>]
let tests = 
  testList "Jet Set Radio tests" [
    testCase "Read until too many nulls, with one null" <|
      fun () ->
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0 |]))
        Expect.equal (Some(byte 1, 0)) (JetSetRadio.readUntilTooManyNulls(br, 1, false)(0)) ""
        Expect.equal (None) (JetSetRadio.readUntilTooManyNulls(br, 1, false)(0)) ""

    testCase "Read until too many nulls, with two nulls" <|
      fun () ->
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0; byte 0; |]))
        Expect.equal (Some(byte 1, 0)) (JetSetRadio.readUntilTooManyNulls(br, 2, false)(0)) ""
        Expect.equal (Some(byte 0, 1)) (JetSetRadio.readUntilTooManyNulls(br, 2, false)(0)) ""
        Expect.equal None (JetSetRadio.readUntilTooManyNulls(br, 2, false)(1)) ""

    testCase "Counting of nulls in readUntilTooManyNulls" <|
      fun () ->
        use br = new BinaryReader(new MemoryStream([| byte 1; byte 0; byte 2; byte 0; byte 0 |]))
        Expect.equal (Some(byte 1, 0)) (JetSetRadio.readUntilTooManyNulls(br, 2, false)(0)) ""
        Expect.equal (Some(byte 0, 1)) (JetSetRadio.readUntilTooManyNulls(br, 2, false)(0)) ""
        Expect.equal (Some(byte 2, 0)) (JetSetRadio.readUntilTooManyNulls(br, 2, false)(1)) ""
        Expect.equal (Some(byte 0, 1)) (JetSetRadio.readUntilTooManyNulls(br, 2, false)(0)) ""
        Expect.equal None (JetSetRadio.readUntilTooManyNulls(br, 2, false)(1)) ""

    testCase "Reading a single null-delimited string set" <|
      fun () -> 
        let testData = Encoding.ASCII.GetBytes("Japanese\u0000\u0000English\u0000\u0000French\u0000\u0000German\u0000\u0000Spanish\u0000\u0000\u0000")
        use br = new BinaryReader(new MemoryStream(testData))
        let result = Array.unfold(JetSetRadio.readStringSet(br, 5))()
        Expect.equal 1 result.Length ""
        Expect.equal [| "Japanese"; "English"; "French"; "German"; "Spanish" |] result.[0] ""

    testCase "Reading multiple null-delimited string sets" <|
      fun () ->
        let string1 = "A\u0000\u0000B\u0000\u0000C\u0000\u0000D\u0000\u0000E\u0000\u0000"
        let string2 = "F\u0000\u0000G\u0000\u0000H\u0000\u0000I\u0000\u0000J\u0000\u0000"
        let testData = Encoding.ASCII.GetBytes(string1 + string2 + "\u0000\u0000\u0000")
        use br = new BinaryReader(new MemoryStream(testData))
        let result = Array.unfold(JetSetRadio.readStringSet(br, 5))()
        Expect.equal 2 result.Length ""
        Expect.equal [| "A"; "B"; "C"; "D"; "E" |] result.[0] ""
        Expect.equal [| "F"; "G"; "H"; "I"; "J" |] result.[1] ""

    testCase "Getting a custom instructions string set with a single string" <|
      fun () ->
        let node = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "Foo"
                Children = []
            }

        Expect.equal [| "Foo" |] (JetSetRadio.getCustomInstructionsStrings node) ""

    testCase "Getting a custom instructions string set with multiple strings" <|
      fun () -> 
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

        Expect.equal [| "Foo"; "Bar" |] (JetSetRadio.getCustomInstructionsStrings node) ""

    testCase "Skipping embedded images in custom instructions strings" <|
      fun () ->
        let node = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Image
                Text = ""
                Children = []
            }

        Expect.equal [||] (JetSetRadio.getCustomInstructionsStrings node) ""

    testCase "Parsing custom instructions content with a single node" <|
      fun () ->
        let text = """[P0||||Test]"""
        use reader = new StringReader(text)
        let result = JetSetRadio.readCustomInstructions(reader)
        let expected = {
                CustomInstructionsNode.Type = CustomInstructionsNodeType.Paragraph
                Text = "Test"
                Children = []
            }
        Expect.equal expected result ""

    testCase "Parsing custom instructions content with an embedded node" <|
      fun () ->
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

        Expect.equal expected result ""

    testCase "Parsing custom instructions content with an image node" <|
      fun () ->
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

        Expect.equal expected result ""
  ]
