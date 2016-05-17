module MadballsBaboInvasion

open LLDatabase
open SQLite
open System
open System.IO
open System.Text.RegularExpressions

type Key() =
    [<PrimaryKey; AutoIncrement>]
    member val IndexID: int = 0 with get, set
    member val CatID: int = 0 with get, set
    member val ConstID: string = "" with get, set
    member val Context: string = "" with get, set
    member val Value: string = "" with get, set

type Value() = 
    [<PrimaryKey; AutoIncrement>]
    member val IndexID: int = 0 with get, set
    member val Value: string = "" with get, set

type Category() = 
    [<PrimaryKey; AutoIncrement>]
    member val CatID: int = 0 with get, set
    member val Name: string = "" with get, set

let private formattingRegexes = 
    [|
        new Regex("\^[0-9]{3,}")
        new Regex("\{[0-9]+\}")
    |]

let internal stripFormattingTags(s: string) = 
    Array.fold (fun (a: string)(r: Regex) -> r.Replace(a, "")) s formattingRegexes

// hardcoded categories based on contents of "Category" table in each DB
let private categories =
    [|
        new Category(CatID = 1, Name = "arena")
        new Category(CatID = 2, Name = "babos")
        new Category(CatID = 3, Name = "challenge")
        new Category(CatID = 4, Name = "glossary")
        new Category(CatID = 5, Name = "menus")
        new Category(CatID = 6, Name = "errors")
    |]

let private createLessonForCategory(db: LLDatabase, gameID: int)(cat: Category) = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = cat.Name
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let private createCardsForDatabase(dbPath: string, language: string, lessons: LessonRecord array) = 
    use db = new SQLiteConnection(dbPath)
    let keyTableID = db.CreateTable(typeof<Key>)
    let valueTableID = db.CreateTable(typeof<Value>)
    let categoryTableID = db.CreateTable(typeof<Category>)

    let keys = db.Query<Key>("select * from Key") |> Array.ofSeq
    let valueMap = db.Query<Value>("select * from Value") |> Seq.map (fun v -> (v.IndexID, v.Value)) |> Map.ofSeq

    let makeCardForKey(k: Key) = 
        {
            CardRecord.ID = 0
            LessonID = lessons.[k.CatID - 1].ID
            Text = valueMap.[k.IndexID]
            Gender = "masculine"
            Key = k.ConstID
            GenderlessKey = k.ConstID
            KeyHash = 0
            GenderlessKeyHash = 0
            SoundResource = ""
            LanguageTag = language
            Reversible = true
        }

    keys
    |> Array.map makeCardForKey

let ExtractMadballsBaboInvasion(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    // each language file corresponds to a .db in path\main\db
    let languages = 
        [|
            "de"
            "en"
            "es"
            "fr"
            "it"
            "ja"
            "ko"
            "pt"
            "ru"
            "zh"
        |]

    // create a lesson for each category
    let lessons = 
        categories
        |> Array.map(createLessonForCategory(db, g.ID))

    let generateCardsForLanguage(l: string) = 
        let dbPath = Path.Combine(path, @"main\db\" + l + ".db")
        createCardsForDatabase(dbPath, l, lessons)

    // read in each database, and make cards for it
    languages
    |> Array.collect generateCardsForLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
