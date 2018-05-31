module MadballsBaboInvasion

open LLDatabase
open LLUtils
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
        new Regex(@"\^[0-9]+")
        new Regex(@"\{[0-9]+?\}")
        new Regex(@"\[.+?\]")
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

let private createLessonForCategory(i: int)(cat: Category) = 
    {
        LessonRecord.ID = i;
        Name = cat.Name
    }

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
            Text = stripFormattingTags(valueMap.[k.IndexID])
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

let ExtractMadballsBaboInvasion(path: string) = 
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
        |> Array.mapi(createLessonForCategory)

    let generateCardsForLanguage(l: string) = 
        let dbPath = Path.Combine(path, FixPathSeps(@"main\db\" + l + ".db"))
        createCardsForDatabase(dbPath, l, lessons)

    let cards = languages |> Array.collect generateCardsForLanguage

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
