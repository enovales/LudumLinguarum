module SimpleGamesTests

open NUnit.Framework
open SimpleGames
open System
open System.IO
open System.Text

[<TestFixture>]
type MagicalDropVTests() =
    let testXml = """<?xml version="1.0" encoding="utf-8"?>
<golgoth>
  <content>
    <ID_UnitTest1>Testing</ID_UnitTest1>
    <ID_UnitTest2>Unescaped &</ID_UnitTest2>
    <ID_UnitTest3><string placeholder></ID_UnitTest3>
  </content>
</golgoth>"""

    [<Test>]
    member this.XmlAmpersandsEscapedBySanitization() = 
        let source = "<fakecontent>Foo & Bar</fakecontent>"
        let expected = "<fakecontent>Foo &amp; Bar</fakecontent>"
        Assert.AreEqual(expected, SimpleGames.sanitizeMagicalDropVXml(source))

    [<Test>]
    member this.XmlStringPlaceholdersRemovedBySanitization() = 
        let source = "<fakecontent><string placeholder></fakecontent>"
        let expected = "<fakecontent></fakecontent>"
        Assert.AreEqual(expected, SimpleGames.sanitizeMagicalDropVXml(source))

    [<Test>]
    member this.WellFormedXmlUnchangedBySanitization() = 
        let source = "<fakecontent>Foo and Bar</fakecontent>"
        Assert.AreEqual(source, SimpleGames.sanitizeMagicalDropVXml(source))

    [<Test>]
    member this.GeneratedStringMapFromXml() = 
        let expected = 
            [| 
                ("ID_UnitTest1", "Testing")
                ("ID_UnitTest2", "Unescaped &")
                ("ID_UnitTest3", "")
            |]
            |> Map.ofArray

        Assert.AreEqual(expected, SimpleGames.generateMagicalDropVStringMap(SimpleGames.sanitizeMagicalDropVXml(testXml)))
