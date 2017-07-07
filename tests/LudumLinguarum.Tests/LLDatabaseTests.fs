module LLDatabaseTests

open LLDatabase
open NUnit.Framework

type LLDatabaseTestData = 
    {
        TestLesson: LessonRecord
        TestCardEn: CardRecord
        TestCardDe: CardRecord
    }

[<TestFixture>]
type LLDatabaseTests() = 
    let mutable db: LLDatabase option = None

    let setupTestData(db: LLDatabase, count: int): LLDatabaseTestData = 
        let lesson = {
            LessonRecord.Name = "Test lesson " + count.ToString()
            ID = 0
        }
        let lessonWithId = { lesson with ID = db.AddLesson(lesson) }

        let cardEn = {
            CardRecord.LessonID = lessonWithId.ID
            Gender = ""
            GenderlessKey = ""
            GenderlessKeyHash = 0
            Key = count.ToString()
            KeyHash = 0
            Reversible = true
            SoundResource = ""
            Text = "Test card"
            LanguageTag = "en"
            ID = 0
        }
        let cardEnWithId = { cardEn with ID = db.AddCard(cardEn) }

        let cardDe = {
            CardRecord.LessonID = lessonWithId.ID
            Gender = ""
            GenderlessKey = ""
            GenderlessKeyHash = 0
            Key = count.ToString()
            KeyHash = 0
            Reversible = true
            SoundResource = ""
            Text = "Test card"
            LanguageTag = "de"
            ID = 0
        }
        let cardDeWithId = { cardDe with ID = db.AddCard(cardDe) }

        {
            LLDatabaseTestData.TestLesson = lessonWithId
            TestCardEn = cardEnWithId
            TestCardDe = cardDeWithId
        }

    [<SetUp>]
    member this.SetUpTest() = 
        System.Console.WriteLine(System.IO.Directory.GetCurrentDirectory())
        db <- Some(new LLDatabase(":memory:"))
        ()

    [<Test>]
    member this.``Adding a lesson``() = 
        let le = {
            LessonRecord.Name = "Test entry"
            ID = 0
        }

        let lid = db.Value.AddLesson(le)
        Assert.IsNotEmpty(db.Value.Lessons)
        Assert.AreEqual(lid, db.Value.Lessons.[0].ID)

    [<Test>]
    member this.``Adding a card``() = 
        let ce = {
            CardRecord.LessonID = 0
            Gender = ""
            GenderlessKey = ""
            GenderlessKeyHash = 0
            Key = ""
            KeyHash = 0
            LanguageTag = "en"
            ID = 0
            Reversible = true
            Text = "Test card"
            SoundResource = ""
        }

        let cid = db.Value.AddCard(ce)
        Assert.IsNotEmpty(db.Value.Cards)
        Assert.AreEqual(cid, db.Value.Cards.[0].ID)

    [<Test>]
    member this.``Getting cards by lesson``() = 
        let testData = setupTestData(db.Value, 1)
        let cardsForLesson = db.Value.CardsFromLesson(testData.TestLesson.ID)
        Assert.IsNotEmpty(cardsForLesson)
        Assert.AreEqual(testData.TestCardEn.ID, cardsForLesson.[0].ID)
        Assert.AreEqual("Test card", cardsForLesson.[0].Text)
        ()

    [<Test>]
    member this.``Getting available languages for a lesson``() = 
        let testData = setupTestData(db.Value, 1)
        let languagesForLesson = db.Value.LanguagesForLesson(testData.TestLesson.ID)
        let expectedLanguages = ["en"; "de"]
        expectedLanguages |> List.iter (fun t -> Assert.IsTrue(languagesForLesson |> List.contains(t)))
        ()

    [<Test>]
    member this.``Getting cards by lesson and language``() = 
        let testData = setupTestData(db.Value, 1)
        let results = db.Value.CardsFromLessonAndLanguageTag(testData.TestLesson, "en")
        Assert.IsNotEmpty(results)
        Assert.AreEqual(testData.TestCardEn.ID, results.[0].ID)
        Assert.AreEqual(testData.TestCardEn.LanguageTag, results.[0].LanguageTag)
        ()

    [<Test>]
    member this.``CreateOrUpdateLesson``() = 
        Assert.IsEmpty(db.Value.Lessons)
        let le = {
            LessonRecord.Name = "Test entry"
            ID = 0
        }

        let lid = db.Value.CreateOrUpdateLesson(le)
        Assert.IsNotEmpty(db.Value.Lessons)
        Assert.AreEqual(lid, db.Value.Lessons.[0].ID)

    [<Test>]
    member this.``CreateOrUpdateCard``() = 
        Assert.IsEmpty(db.Value.Cards)
        let ce = {
            CardRecord.LessonID = 0
            ID = 0
            Gender = ""
            GenderlessKey = ""
            GenderlessKeyHash = 0
            Key = ""
            KeyHash = 0
            LanguageTag = "en"
            Reversible = true
            Text = "Test card"
            SoundResource = ""
        }

        let cid = db.Value.CreateOrUpdateCard(ce)
        Assert.IsNotEmpty(db.Value.Cards)
        Assert.AreEqual(cid, db.Value.Cards.[0].ID)

    [<Test>]
    member this.``Deleting a lesson``() = 
        let le = {
            LessonRecord.Name = "Test entry"
            ID = 0
        }

        let lid = db.Value.AddLesson(le)
        Assert.IsNotEmpty(db.Value.Lessons)
        Assert.AreEqual(lid, db.Value.Lessons.[0].ID)

        db.Value.DeleteLesson({ le with ID = lid })
        Assert.IsEmpty(db.Value.Lessons)

    [<Test>]
    member this.``Deleting a card``() = 
        let ce = {
            CardRecord.LessonID = 0
            Gender = ""
            GenderlessKey = ""
            GenderlessKeyHash = 0
            Key = ""
            KeyHash = 0
            LanguageTag = "en"
            ID = 0
            Reversible = true
            Text = "Test card"
            SoundResource = ""
        }

        let cid = db.Value.AddCard(ce)
        Assert.IsNotEmpty(db.Value.Cards)
        Assert.AreEqual(cid, db.Value.Cards.[0].ID)

        db.Value.DeleteCard({ ce with ID = cid})
        Assert.IsEmpty(db.Value.Cards)

    [<Test>]
    member this.``Deleting a lesson deletes all cards associated with it``() = 
        let testData = setupTestData(db.Value, 1)

        // set up additional test data
        let additionalData = setupTestData(db.Value, 2)
        Assert.AreNotEqual(testData.TestLesson.ID, additionalData.TestLesson.ID)
        Assert.AreEqual(testData.TestLesson.ID, testData.TestCardDe.LessonID, "lesson ID mismatch in testData.TestCardDe")
        Assert.AreEqual(testData.TestLesson.ID, testData.TestCardEn.LessonID, "lesson ID mismatch in testData.TestCardEn")

        Assert.AreEqual(additionalData.TestLesson.ID, additionalData.TestCardDe.LessonID, "lesson ID mismatch in additionalData.TestCardDe")
        Assert.AreEqual(additionalData.TestLesson.ID, additionalData.TestCardEn.LessonID, "lesson ID mismatch in additionalData.TestCardEn")

        Assert.AreNotEqual(testData.TestCardDe.ID, additionalData.TestCardDe.ID)
        Assert.AreNotEqual(testData.TestCardEn.ID, additionalData.TestCardEn.ID)

        db.Value.DeleteLesson(testData.TestLesson)

        Assert.AreEqual(None, db.Value.Lessons |> Array.tryFind(fun t -> t = testData.TestLesson))
        Assert.AreEqual(None, db.Value.Cards |> Array.tryFind(fun t -> t = testData.TestCardEn))
        Assert.AreEqual(None, db.Value.Cards |> Array.tryFind(fun t -> t = testData.TestCardDe))
        Assert.IsNotEmpty(db.Value.Lessons)
        Assert.IsNotEmpty(db.Value.Cards)
