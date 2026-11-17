module LLDatabase

open SQLite
open System
open System.Collections.Generic
open System.Security.Cryptography
open System.Text

// one-time initialization of provider
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3())

type LessonRecord = {
        ID: int
        Name: string
    }

type LessonEntry() = 
    [<PrimaryKey; AutoIncrement>]
    member val ID = 0 with get, set
    member val Name = "" with get, set

    member this.ToLessonRecord() = 
        { LessonRecord.ID = this.ID; Name = this.Name }

    static member FromLessonRecord(le: LessonRecord) = 
        let dble = new LessonEntry()
        dble.ID <- le.ID
        dble.Name <- le.Name
        dble

type CardRecord = {
        ID: int
        LessonID: int
        Text: string
        Gender: string
        Key: string
        GenderlessKey: string
        KeyHash: int
        GenderlessKeyHash: int
        SoundResource: string
        LanguageTag: string
        Reversible: bool
    }
    with
        override this.ToString() = 
            "CardRecord("
            + "ID = " + this.ID.ToString() + ", "
            + "LessonID = " + this.LessonID.ToString() + ", "
            + "Text = " + this.Text.ToString() + ", "
            + "Gender = " + this.Gender.ToString() + ", "
            + "Key = " + this.Key.ToString() + ", "
            + "GenderlessKey = " + this.GenderlessKey.ToString () + ", "
            + "KeyHash = " + this.KeyHash.ToString() + ", "
            + "GenderlessKeyHash = " + this.GenderlessKeyHash.ToString() + ", "
            + "SoundResource = " + this.SoundResource.ToString() + ", "
            + "LanguageTag = " + this.LanguageTag.ToString() + ", "
            + "Reversible = " + this.Reversible.ToString() + ", "
            + ")"


type CardEntry() = 
    [<PrimaryKey; AutoIncrement>]
    member val ID = 0 with get, set

    /// <summary>
    /// The ID of the lesson to which this card belongs.
    /// </summary>
    [<Indexed>]
    member val LessonID = 0 with get, set

    /// <summary>
    /// The text of this card.
    /// </summary>
    member val Text = "" with get, set

    /// <summary>
    /// Used to disambiguate between cards that may refer to the same concept,
    /// but gendered differently.
    /// </summary>
    member val Gender = "" with get, set

    /// <summary>
    /// A free-form description of where this card originated. Intended for debugging
    /// exported data.
    /// </summary>
    member val Key = "" with get, set

    member val GenderlessKey = "" with get, set

    /// <summary>
    /// Hashed version of the key. Used as an index.
    /// </summary>
    [<Indexed>]
    member val KeyHash = 0 with get, set

    [<Indexed>]
    member val GenderlessKeyHash = 0 with get, set

    /// <summary>
    /// The path to an optional sound resource associated with this.
    /// </summary>
    member val SoundResource = "" with get, set

    /// <summary>
    /// An IETF language tag describing the text on this card.
    /// </summary>
    member val LanguageTag = "" with get, set

    /// <summary>
    /// Whether or not the card should be exported such that it generates
    /// a reversible version of the card. (i.e. a production version,
    /// as well as a recognition version)
    /// </summary>
    member val Reversible = true with get, set

    member this.ToCardRecord() = 
        {
            CardRecord.ID = this.ID
            LessonID = this.LessonID
            Text = this.Text
            Gender = this.Gender
            Key = this.Key
            GenderlessKey = this.GenderlessKey
            KeyHash = this.KeyHash
            GenderlessKeyHash = this.GenderlessKeyHash
            SoundResource = this.SoundResource
            LanguageTag = this.LanguageTag
            Reversible = this.Reversible
        }

    static member FromCardRecord(ce: CardRecord) = 
        let dbce = new CardEntry()
        dbce.ID <- ce.ID
        dbce.LessonID <- ce.LessonID
        dbce.Text <- ce.Text
        dbce.Gender <- ce.Gender
        dbce.Key <- ce.Key
        dbce.GenderlessKey <- ce.GenderlessKey
        dbce.KeyHash <- ce.KeyHash
        dbce.GenderlessKeyHash <- ce.GenderlessKeyHash
        dbce.SoundResource <- ce.SoundResource
        dbce.LanguageTag <- ce.LanguageTag
        dbce.Reversible <- ce.Reversible
        dbce

type LanguageQueryResult() = 
    member val LanguageTag = "" with get, set

// string.GetHashCode() is randomized per process, which made re-imported
// cards fail to match their existing rows and get duplicated. Hash the
// UTF-8 bytes of the key with SHA-256 instead, so the value is stable
// across processes and platforms.
let internal calculateKeyHash(key: string) =
    use sha = SHA256.Create()
    BitConverter.ToInt32(sha.ComputeHash(Encoding.UTF8.GetBytes(key)), 0)

type LLDatabase(dbPath: string) = 
    let db = new SQLiteConnection(dbPath)
    let lessonTableID = db.CreateTable(typeof<LessonEntry>)
    let cardTableID = db.CreateTable(typeof<CardEntry>)

    member this.Lessons = 
        db.Query<LessonEntry>("select * from LessonEntry") |> Seq.map(fun t -> t.ToLessonRecord()) |> Array.ofSeq

    member this.Cards = 
        db.Query<CardEntry>("select * from CardEntry") |> Seq.map(fun t -> t.ToCardRecord()) |> Array.ofSeq

    member private this.Compact() = 
        db.Execute("VACUUM;") |> ignore

    member this.AddLesson(l: LessonRecord) = 
        db.Insert(LessonEntry.FromLessonRecord(l)) |> ignore
        (this.Lessons |> Seq.find(fun t -> t.Name = l.Name)).ID

    member this.DeleteLesson(l: LessonRecord) = 
        db.BeginTransaction()
        this.DeleteLessonsInternal([| l |])
        db.Commit()
        this.Compact()

    member this.DeleteLessons(l: LessonRecord array) = 
        db.BeginTransaction()
        this.DeleteLessonsInternal(l)
        db.Commit()
        this.Compact()

    member private this.DeleteLessonsInternal(l: LessonRecord array) = 
        let lids = new SortedSet<int>(l |> Array.map(fun t -> t.ID))
        l |> Array.iter(LessonEntry.FromLessonRecord >> db.Delete >> ignore)

        // delete cards for this lesson
        this.Cards |> Array.filter(fun c -> lids.Contains(c.LessonID)) |> this.DeleteCardsInternal
        
    member private this.UpdateCardWithHash(c: CardRecord) = 
        { c with KeyHash = calculateKeyHash(c.Key); GenderlessKeyHash = calculateKeyHash(c.GenderlessKey) }

    member private this.AddCardInternal(c: CardRecord) = 
        let cardRecordWithHash = this.UpdateCardWithHash(c)
        let cardWithHash = CardEntry.FromCardRecord(cardRecordWithHash)
        db.Insert(cardWithHash) |> ignore
        // Match on the key text rather than the stored hash, so rows written
        // by older versions (with per-process randomized hashes) are found too.
        let newRecordId = (db.Query<CardEntry>("select * from CardEntry where \"Key\" = ? and LessonID = ? and LanguageTag = ?", cardWithHash.Key, cardWithHash.LessonID, cardWithHash.LanguageTag) |> Array.ofSeq |> Array.head).ID
        { cardRecordWithHash with ID = newRecordId }

    member this.AddCard(c: CardRecord) = 
        this.AddCardInternal(c).ID

    member this.AddCards(c: CardRecord seq) = 
        let cardsWithHashes = 
            c |> Seq.map(fun t -> this.UpdateCardWithHash >> CardEntry.FromCardRecord) 

        db.RunInTransaction(fun _ -> db.InsertAll(cardsWithHashes) |> ignore)

    member this.DeleteCard(c: CardRecord) = 
        this.DeleteCardInternal(c)
        this.Compact()

    member this.DeleteCards(cards: CardRecord seq) = 
        db.BeginTransaction()
        this.DeleteCardsInternal(cards)
        db.Commit()

    member private this.DeleteCardInternal(c: CardRecord) = 
        db.Delete(CardEntry.FromCardRecord(c)) |> ignore

    member private this.DeleteCardsInternal(cards: CardRecord seq) = 
        cards |> Seq.iter this.DeleteCardInternal

    member this.CreateOrUpdateLesson(le: LessonRecord) = 
        let existingEntry = 
            db.Query<LessonEntry>("select * from LessonEntry where Name = ?", le.Name)
            |> Seq.tryHead

        match existingEntry with
        | Some(ee) ->
            let updatedEntry = { le with ID = ee.ID }
            db.Update(LessonEntry.FromLessonRecord(updatedEntry)) |> ignore
            ee.ID
        | _ -> this.AddLesson(le)

    member this.CreateOrUpdateCard(ce: CardRecord) = 
        let existingEntry = 
            db.Query<CardEntry>("select * from CardEntry where \"Key\" = ? and LessonID = ?", ce.Key, ce.LessonID) 
            |> Seq.tryHead

        match existingEntry with
        | Some(ee) ->
            let updatedEntry = this.UpdateCardWithHash({ ce with ID = ee.ID })
            db.Update(CardEntry.FromCardRecord(updatedEntry)) |> ignore
            ee.ID
        | _ -> this.AddCard(ce)

    member this.CreateOrUpdateCards(c: CardRecord seq) = 
        // the cards passed in, but with the KeyHash computed
        let cardsWithHashes = c |> Seq.map this.UpdateCardWithHash |> Array.ofSeq

        // Group by the key text rather than the stored hash, so rows written
        // by older versions (with per-process randomized hashes) still match.
        let keyMap = this.Cards |> Array.groupBy(fun t -> t.Key) |> Map.ofArray
        let checkLessonIDAndLanguage(key: CardRecord)(checking: CardRecord) = 
            (key.LessonID = checking.LessonID) && (key.LanguageTag = checking.LanguageTag)

        let hasExistingCard(card: CardRecord) = 
            match keyMap |> Map.containsKey(card.Key) with
            | true when keyMap.Item(card.Key) |> Array.exists(checkLessonIDAndLanguage(card)) -> true
            | _ -> false
                
        let idOfExistingCard(card: CardRecord) = 
            let cardList = keyMap.Item(card.Key)

            match cardList |> Array.tryFind(checkLessonIDAndLanguage(card)) with
            | Some(found) -> found.ID
            | _ -> failwith "shouldn't happen"

        // cards with the same key hash and language tag that already exist in the db
        let (existingCards, newCards) = cardsWithHashes |> Array.partition hasExistingCard

        let cardsToUpdateWithIds = existingCards |> Array.map (fun c -> { c with ID = idOfExistingCard(c) })

        db.UpdateAll(cardsToUpdateWithIds |> Seq.map CardEntry.FromCardRecord) |> ignore
        db.InsertAll(newCards |> Seq.map CardEntry.FromCardRecord) |> ignore


    member this.CardsFromLesson(lid: int) = 
        db.Query<CardEntry>("select * from CardEntry where LessonID = ?", lid) |> Seq.map(fun t -> t.ToCardRecord()) |> Array.ofSeq

    member this.CardsFromLesson(le: LessonRecord) = 
        this.CardsFromLesson(le.ID)

    member this.LanguagesForLesson(lid: int) = 
        let result = db.Query<LanguageQueryResult>("select distinct LanguageTag from CardEntry where LessonID = ?", lid)
        result |> Seq.map (fun t -> t.LanguageTag) |> Seq.toList

    member this.LanguagesForLesson(le: LessonRecord) = 
        this.LanguagesForLesson(le.ID)

    member this.CardsFromLessonAndLanguageTag(lid: int, language: string) = 
        db.Query<CardEntry>("select * from CardEntry where LessonID = ? and LanguageTag = ?", lid, language) |> Seq.map(fun t -> t.ToCardRecord()) |> Array.ofSeq

    member this.CardsFromLessonAndLanguageTag(le: LessonRecord, language: string) = 
        this.CardsFromLessonAndLanguageTag(le.ID, language)

    interface IDisposable with
        member _.Dispose() = db.Dispose()
