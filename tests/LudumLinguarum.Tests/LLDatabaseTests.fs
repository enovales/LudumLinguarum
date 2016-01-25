﻿module LLDatabaseTests

open LLDatabase
open NUnit.Framework

type LLDatabaseTestData = 
    {
        TestGame: GameRecord;
        TestLesson: LessonRecord;
        TestCardEn: CardRecord;
        TestCardDe: CardRecord
    }

[<TestFixture>]
type LLDatabaseTests() = 
    let mutable db: LLDatabase option = None

    let setupTestData(db: LLDatabase): LLDatabaseTestData = 
        let game = {
            GameRecord.Name = "Test game";
            ID = 0
        }
        let gameWithId = { game with ID = db.AddGame(game) }

        let lesson = {
            LessonRecord.Name = "Test lesson";
            GameID = gameWithId.ID;
            ID = 0
        }
        let lessonWithId = { lesson with ID = db.AddLesson(lesson) }

        let cardEn = {
            CardRecord.LessonID = lessonWithId.ID;
            Gender = "";
            GenderlessKey = "";
            GenderlessKeyHash = 0;
            Key = "";
            KeyHash = 0;
            Reversible = true;
            SoundResource = "";
            Text = "Test card";
            LanguageTag = "en";
            ID = 0
        }
        let cardEnWithId = { cardEn with ID = db.AddCard(cardEn) }

        let cardDe = {
            CardRecord.LessonID = lessonWithId.ID;
            Gender = "";
            GenderlessKey = "";
            GenderlessKeyHash = 0;
            Key = "";
            KeyHash = 0;
            Reversible = true;
            SoundResource = "";
            Text = "Test card";
            LanguageTag = "de";
            ID = 0
        }
        let cardDeWithId = { cardDe with ID = db.AddCard(cardDe) }

        {
            LLDatabaseTestData.TestGame = gameWithId;
            TestLesson = lessonWithId;
            TestCardEn = cardEnWithId;
            TestCardDe = cardDeWithId
        }

    [<TestFixtureSetUp>]
    member this.SetUpTestFixture() = ()

    [<TestFixtureTearDown>]
    member this.TearDownTestFixture() = ()

    [<SetUp>]
    member this.SetUpTest() = 
        System.Console.WriteLine(System.IO.Directory.GetCurrentDirectory())
        db <- Some(new LLDatabase(":memory:"))
        ()

    [<TearDown>]
    member this.TearDownTest() = ()

    [<Test>]
    member this.TestAddGame() =
        let testGameEntry = {
            GameRecord.Name = "Test game";
            ID = 0
        }
        let gid = db.Value.AddGame(testGameEntry)

        Assert.IsNotEmpty(db.Value.Games)
        Assert.AreEqual(gid, db.Value.Games.[0].ID)
        ()

    [<Test>]
    member this.TestAddLesson() = 
        let le = {
            LessonRecord.Name = "Test entry";
            GameID = 0;
            ID = 0
        }

        let lid = db.Value.AddLesson(le)
        Assert.IsNotEmpty(db.Value.Lessons)
        Assert.AreEqual(lid, db.Value.Lessons.[0].ID)

    [<Test>]
    member this.TestAddCard() = 
        let ce = {
            CardRecord.LessonID = 0;
            Gender = "";
            GenderlessKey = "";
            GenderlessKeyHash = 0;
            Key = "";
            KeyHash = 0;
            LanguageTag = "en";
            ID = 0;
            Reversible = true;
            Text = "Test card";
            SoundResource = ""
        }

        let cid = db.Value.AddCard(ce)
        Assert.IsNotEmpty(db.Value.Cards)
        Assert.AreEqual(cid, db.Value.Cards.[0].ID)

    [<Test>]
    member this.TestGetCardsByLesson() = 
        let testData = setupTestData(db.Value)
        let cardsForLesson = db.Value.CardsFromLesson(testData.TestLesson.ID)
        Assert.IsNotEmpty(cardsForLesson)
        Assert.AreEqual(testData.TestCardEn.ID, cardsForLesson.[0].ID)
        Assert.AreEqual("Test card", cardsForLesson.[0].Text)
        ()

    [<Test>]
    member this.TestGetLanguagesForLesson() = 
        let testData = setupTestData(db.Value)
        let languagesForLesson = db.Value.LanguagesForLesson(testData.TestLesson.ID)
        let expectedLanguages = ["en"; "de"]
        expectedLanguages |> List.iter (fun t -> Assert.IsTrue(languagesForLesson |> List.contains(t)))
        ()

    [<Test>]
    member this.TestGetCardsByLessonAndLanguage() = 
        let testData = setupTestData(db.Value)
        let results = db.Value.CardsFromLessonAndLanguageTag(testData.TestLesson, "en")
        Assert.IsNotEmpty(results)
        Assert.AreEqual(testData.TestCardEn.ID, results.[0].ID)
        Assert.AreEqual(testData.TestCardEn.LanguageTag, results.[0].LanguageTag)
        ()

    [<Test>]
    member this.TestCreateOrUpdateGame() = 
        Assert.IsEmpty(db.Value.Games)

        let testGameEntry = {
            GameRecord.Name = "Test game";
            ID = 0
        }
        let gid = db.Value.CreateOrUpdateGame(testGameEntry)

        Assert.IsNotEmpty(db.Value.Games)
        Assert.AreEqual(gid, db.Value.Games.[0].ID)
        ()

    [<Test>]
    member this.TestCreateOrUpdateLesson() = 
        Assert.IsEmpty(db.Value.Lessons)
        let le = {
            LessonRecord.Name = "Test entry";
            GameID = 0;
            ID = 0;
        }

        let lid = db.Value.CreateOrUpdateLesson(le)
        Assert.IsNotEmpty(db.Value.Lessons)
        Assert.AreEqual(lid, db.Value.Lessons.[0].ID)

    [<Test>]
    member this.TestCreateOrUpdateCard() = 
        Assert.IsEmpty(db.Value.Cards)
        let ce = {
            CardRecord.LessonID = 0
            ID = 0;
            Gender = "";
            GenderlessKey = "";
            GenderlessKeyHash = 0;
            Key = "";
            KeyHash = 0;
            LanguageTag = "en";
            Reversible = true;
            Text = "Test card"
            SoundResource = ""
        }

        let cid = db.Value.CreateOrUpdateCard(ce)
        Assert.IsNotEmpty(db.Value.Cards)
        Assert.AreEqual(cid, db.Value.Cards.[0].ID)
