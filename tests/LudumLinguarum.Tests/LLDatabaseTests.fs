module LLDatabaseTests

open Expecto
open LLDatabase
open FsCheck

type LLDatabaseTestData = 
    {
        TestLesson: LessonRecord
        TestCardEn: CardRecord
        TestCardDe: CardRecord
    }

[<Tests>]
let tests = 
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

  let testsWithSetup setup = 
    [
      test "Adding a lesson" {
        setup (fun (db: LLDatabase) ->
          let le = {
            LessonRecord.Name = "Test entry"
            ID = 0
          }

          let lid = db.AddLesson(le)
          Expect.isNonEmpty db.Lessons "lessons should not be empty"
          Expect.equal lid db.Lessons.[0].ID "lesson should have expected ID"
        )
      }

      test "Adding a card" {
        setup (fun (db: LLDatabase) ->
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

          let cid = db.AddCard(ce)
          Expect.isNonEmpty db.Cards "should have a card"
          Expect.equal cid db.Cards.[0].ID "should have expected card ID"
        )
      }

      test "Getting cards by lesson" {
        setup (fun (db: LLDatabase) ->
          let testData = setupTestData(db, 1)
          let cardsForLesson = db.CardsFromLesson(testData.TestLesson.ID)
          Expect.isNonEmpty cardsForLesson "should have a card in the lesson"
          Expect.equal testData.TestCardEn.ID cardsForLesson.[0].ID "lesson card should have expected ID"
          Expect.equal "Test card" cardsForLesson.[0].Text "card text should be expected value"
        )
      }

      test "Getting available languages for a lesson" {
        setup (fun (db: LLDatabase) ->
          let testData = setupTestData(db, 1)
          let languagesForLesson = db.LanguagesForLesson(testData.TestLesson.ID)
          let expectedLanguages = ["en"; "de"]
          expectedLanguages |> List.iter (fun t -> Expect.isTrue (languagesForLesson |> List.contains(t)) "should contain expected language")
        )
      }

      test "Getting cards by lesson and language" {
        setup (fun (db: LLDatabase) -> 
          let testData = setupTestData(db, 1)
          let results = db.CardsFromLessonAndLanguageTag(testData.TestLesson, "en")
          Expect.isNonEmpty results "should return some results for this search"
          Expect.equal testData.TestCardEn.ID results.[0].ID "result should have expected card ID"
          Expect.equal testData.TestCardEn.LanguageTag results.[0].LanguageTag "result should have expected language tag"
        )
      }

      test "CreateOrUpdateLesson" {
        setup (fun (db: LLDatabase) ->
          Expect.isEmpty db.Lessons "lessons should start empty"
          let le = {
              LessonRecord.Name = "Test entry"
              ID = 0
          }

          let lid = db.CreateOrUpdateLesson(le)
          Expect.isNonEmpty db.Lessons "lessons should not be empty after add"
          Expect.equal lid db.Lessons.[0].ID "added lesson should have expected ID"
        )
      }

      test "CreateOrUpdateCard" {
        setup (fun (db: LLDatabase) ->
          Expect.isEmpty db.Cards "cards should start empty"
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

          let cid = db.CreateOrUpdateCard(ce)
          Expect.isNonEmpty db.Cards "cards should not be empty after add"
          Expect.equal cid db.Cards.[0].ID "added card should have expected ID"
        )
      }

      test "Deleting a lesson" {
        setup (fun (db: LLDatabase) ->
          let le = {
              LessonRecord.Name = "Test entry"
              ID = 0
          }

          let lid = db.AddLesson(le)
          Expect.isNonEmpty db.Lessons "lessons should not be empty after add"
          Expect.equal lid db.Lessons.[0].ID "added lesson should have expected ID"

          db.DeleteLesson({ le with ID = lid })
          Expect.isEmpty db.Lessons "lessons should be empty after delete"
        )
      }

      test "Deleting a card" {
        setup (fun (db: LLDatabase) ->
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

          let cid = db.AddCard(ce)
          Expect.isNonEmpty db.Cards "cards should not be empty after add"
          Expect.equal cid db.Cards.[0].ID "added card should have expected ID"

          db.DeleteCard({ ce with ID = cid})
          Expect.isEmpty db.Cards "cards should be empty after delete"
        )
      }

      test "Deleting a lesson deletes all cards associated with it" {
        setup (fun (db: LLDatabase) ->
          let testData = setupTestData(db, 1)

          // set up additional test data
          let additionalData = setupTestData(db, 2)
          Expect.notEqual testData.TestLesson.ID additionalData.TestLesson.ID "lesson ID mismatch"
          Expect.equal testData.TestLesson.ID testData.TestCardDe.LessonID "lesson ID mismatch in testData.TestCardDe"
          Expect.equal testData.TestLesson.ID testData.TestCardEn.LessonID "lesson ID mismatch in testData.TestCardEn"

          Expect.equal additionalData.TestLesson.ID additionalData.TestCardDe.LessonID "lesson ID mismatch in additionalData.TestCardDe"
          Expect.equal additionalData.TestLesson.ID additionalData.TestCardEn.LessonID "lesson ID mismatch in additionalData.TestCardEn"

          Expect.notEqual testData.TestCardDe.ID additionalData.TestCardDe.ID "additional data card ID should not match"
          Expect.notEqual testData.TestCardEn.ID additionalData.TestCardEn.ID "additional data card ID should not match"

          db.DeleteLesson(testData.TestLesson)

          Expect.equal None (db.Lessons |> Array.tryFind(fun t -> t = testData.TestLesson)) "no lesson should remain"
          Expect.equal None (db.Cards |> Array.tryFind(fun t -> t = testData.TestCardEn)) "no cards should remain from deleted lesson"
          Expect.equal None (db.Cards |> Array.tryFind(fun t -> t = testData.TestCardDe)) "no cards should remain from deleted lesson"
          Expect.isNonEmpty db.Lessons "lessons should not be empty"
          Expect.isNonEmpty db.Cards "cards should not be empty"
        )
      }
    ]

  testsWithSetup (fun test ->
    let db = new LLDatabase(":memory:")
    try
      test db
    finally
      ()
  )
  |> testList "LLDatabase tests"
