module LLDatabase

open SQLite
open System.Collections.Generic

type LessonRecord = {
        ID: int;
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
        ID: int;
        LessonID: int;
        Text: string;
        Gender: string;
        Key: string;
        GenderlessKey: string;
        KeyHash: int;
        GenderlessKeyHash: int;
        SoundResource: string;
        LanguageTag: string;
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
    [<MaxLength(8192)>]
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
    [<MaxLength(8192)>]
    member val Key = "" with get, set

    [<MaxLength(8192)>]
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
    [<MaxLength(1024)>]
    member val SoundResource = "" with get, set

    /// <summary>
    /// An IETF language tag describing the text on this card.
    /// </summary>
    [<MaxLength(12)>]
    member val LanguageTag = "" with get, set

    /// <summary>
    /// Whether or not the card should be exported such that it generates
    /// a reversible version of the card. (i.e. a production version,
    /// as well as a recognition version)
    /// </summary>
    member val Reversible = true with get, set

    member this.ToCardRecord() = 
        {
            CardRecord.ID = this.ID; 
            LessonID = this.LessonID;
            Text = this.Text;
            Gender = this.Gender;
            Key = this.Key;
            GenderlessKey = this.GenderlessKey;
            KeyHash = this.KeyHash;
            GenderlessKeyHash = this.GenderlessKeyHash;
            SoundResource = this.SoundResource;
            LanguageTag = this.LanguageTag;
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

type LLDatabase(dbPath: string) = 
    let db = new SQLiteConnection(dbPath)
    let lessonTableID = db.CreateTable(typeof<LessonEntry>)
    let cardTableID = db.CreateTable(typeof<CardEntry>)

    let calculateKeyHash(key: string) = key.GetHashCode()

    member this.Lessons = 
        db.Query<LessonEntry>("select * from LessonEntry") |> Seq.map(fun t -> t.ToLessonRecord()) |> Array.ofSeq

    member this.Cards = 
        db.Query<CardEntry>("select * from CardEntry") |> Seq.map(fun t -> t.ToCardRecord()) |> Array.ofSeq

    member private this.Compact() = 
        db.Execute("VACUUM;") |> ignore

    member this.AddLesson(l: LessonRecord) = 
        db.Insert(LessonEntry.FromLessonRecord(l)) |> ignore
        (this.Lessons |> Array.find(fun t -> (t.Name = l.Name))).ID

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

        // find the ID of the just-inserted record
        let newRecordId = (db.Query<CardEntry>("select * from CardEntry where KeyHash = ? and LessonID = ? and LanguageTag = ?", cardWithHash.KeyHash, cardWithHash.LessonID, cardWithHash.LanguageTag) |> 
                            Array.ofSeq |> Array.head).ID
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
            db.Query<CardEntry>("select * from CardEntry where KeyHash = ? and LessonID = ?", calculateKeyHash(ce.Key), ce.LessonID) 
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

        let keyHashMap = this.Cards |> Array.groupBy(fun t -> t.KeyHash) |> Map.ofArray
        let checkLessonIDAndLanguage(key: CardRecord)(checking: CardRecord) = 
            (key.LessonID = checking.LessonID) && (key.LanguageTag = checking.LanguageTag)

        let hasExistingCard(card: CardRecord) = 
            match keyHashMap |> Map.containsKey(card.KeyHash) with
            | true when keyHashMap.Item(card.KeyHash) |> Array.exists(checkLessonIDAndLanguage(card)) -> true
            | _ -> false
                
        let idOfExistingCard(card: CardRecord) = 
            let cardList = keyHashMap.Item(card.KeyHash)

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