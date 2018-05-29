module CardExportTests

open Expecto
open LLDatabase

let private sampleText = "Really long text"
let private cr = 
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

[<Tests>]
let tests = 
  testList "CardExportTests" [
    testCase "Length filter without a limit specified always allows the card" <|
      fun () -> Expect.isTrue (CardExport.mkLengthFilter(None)(cr)) "card should be allowed"

    testCase "Length filter allows cards up to the length limit" <|
      fun () -> Expect.isTrue (CardExport.mkLengthFilter(Some(sampleText.Length))(cr)) "card should be allowed"

    testCase "Length filter disallows cards beyond the length limit" <|
      fun () -> Expect.isFalse (CardExport.mkLengthFilter(Some(sampleText.Length - 1))(cr)) "card should be disallowed"

    testCase "Word count filter without a limit specified always allows the card" <|
      fun () -> Expect.isTrue (CardExport.mkWordCountFilter(None)(cr)) "card should be allowed"
    
    testCase "Word count filter allows cards up to the word count limit" <|
      fun () -> Expect.isTrue (CardExport.mkWordCountFilter(Some(3))(cr)) "card should be allowed"

    testCase "Word count filter disallows cards beyond the word count limit" <|
      fun () -> Expect.isFalse (CardExport.mkWordCountFilter(Some(2))(cr)) "card should be disallowed"
  ]
