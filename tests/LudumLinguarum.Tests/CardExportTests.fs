module CardExportTests

open LLDatabase
open NUnit.Framework

[<TestFixture>]
type CardExportTests() = 
    let sampleText = "Really long text"
    let cr = 
        {
            CardRecord.ID = 0
            LessonID = 0
            Text = sampleText
            Gender = ""
            Key = ""
            GenderlessKey = ""
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = ""
            Reversible = true
        }

    [<Test>]
    member this.``Length filter without a limit specified always allows the card``() = 
        let f = CardExport.mkLengthFilter(None)
        Assert.IsTrue(f(cr))

    [<Test>]
    member this.``Length filter allows cards up to the length limit``() = 
        let f = CardExport.mkLengthFilter(Some(sampleText.Length))
        Assert.IsTrue(f(cr))

    [<Test>]
    member this.``Length filter disallows cards beyond the length limit``() = 
        let f = CardExport.mkLengthFilter(Some(sampleText.Length - 1))
        Assert.IsFalse(f(cr))

    [<Test>]
    member this.``Word count filter without a limit specified always allows the card``() = 
        let f = CardExport.mkWordCountFilter(None)
        Assert.IsTrue(f(cr))
        
    [<Test>]
    member this.``Word count filter allows cards up to the word count limit``() = 
        let f = CardExport.mkWordCountFilter(Some(3))
        Assert.IsTrue(f(cr))

    [<Test>]
    member this.``Word count filter disallows cards beyond the word count limit``() =
        let f = CardExport.mkWordCountFilter(Some(2))
        Assert.IsFalse(f(cr))
