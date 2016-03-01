module LLDatabase

open SQLite
open System.Collections.Generic

type GameRecord = {
        ID: int;
        Name: string
    }    

type GameEntry() = 
    [<PrimaryKey; AutoIncrement>]
    member val ID = 0 with get, set
    member val Name = "" with get, set

    member this.ToGameEntry() = 
        { GameRecord.ID = this.ID; Name = this.Name }

    static member FromGameEntry(ge: GameRecord) = 
        let dbge = new GameEntry()
        dbge.ID <- ge.ID
        dbge.Name <- ge.Name
        dbge

type LessonRecord = {
        ID: int;
        GameID: int;
        Name: string
    }

type LessonEntry() = 
    [<PrimaryKey; AutoIncrement>]
    member val ID = 0 with get, set
    member val GameID = 0 with get, set
    member val Name = "" with get, set

    member this.ToLessonEntry() = 
        { LessonRecord.ID = this.ID; GameID = this.GameID; Name = this.Name }

    static member FromLessonEntry(le: LessonRecord) = 
        let dble = new LessonEntry()
        dble.ID <- le.ID
        dble.GameID <- le.GameID
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

    member this.ToCardEntry() = 
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

    static member FromCardEntry(ce: CardRecord) = 
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
    let gameTableID = db.CreateTable(typeof<GameEntry>)
    let lessonTableID = db.CreateTable(typeof<LessonEntry>)
    let cardTableID = db.CreateTable(typeof<CardEntry>)

    let calculateKeyHash(key: string) = key.GetHashCode()

    member this.Games = 
        db.Query<GameEntry>("select * from GameEntry") |> Seq.map(fun t -> t.ToGameEntry()) |> Array.ofSeq

    member this.Lessons = 
        db.Query<LessonEntry>("select * from LessonEntry") |> Seq.map(fun t -> t.ToLessonEntry()) |> Array.ofSeq

    member this.Cards = 
        db.Query<CardEntry>("select * from CardEntry") |> Seq.map(fun t -> t.ToCardEntry()) |> Array.ofSeq

    member private this.Compact() = 
        db.Execute("VACUUM;") |> ignore

    member this.AddGame(g: GameRecord) = 
        db.Insert(GameEntry.FromGameEntry(g)) |> ignore
        (this.Games |> Array.find(fun t -> t.Name = g.Name)).ID

    member this.DeleteGame(g: GameRecord) = 
        db.BeginTransaction()
        this.DeleteGameInternal(g)
        db.Commit()
        this.Compact()

    member private this.DeleteGameInternal(g: GameRecord) =
        db.Delete(GameEntry.FromGameEntry(g)) |> ignore
        this.Lessons |> Array.filter(fun l -> l.GameID = g.ID) |> this.DeleteLessonsInternal

    member this.AddLesson(l: LessonRecord) = 
        db.Insert(LessonEntry.FromLessonEntry(l)) |> ignore
        (this.Lessons |> Array.find(fun t -> (t.Name = l.Name) && (t.GameID = l.GameID))).ID

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
        l |> Array.iter(LessonEntry.FromLessonEntry >> db.Delete >> ignore)

        // delete cards for this lesson
        this.Cards |> Array.filter(fun c -> lids.Contains(c.LessonID)) |> this.DeleteCardsInternal
        
    member private this.UpdateCardWithHash(c: CardRecord) = 
        { c with KeyHash = calculateKeyHash(c.Key); GenderlessKeyHash = calculateKeyHash(c.GenderlessKey) }

    member private this.AddCardInternal(c: CardRecord) = 
        let cardRecordWithHash = this.UpdateCardWithHash(c)
        let cardWithHash = CardEntry.FromCardEntry(cardRecordWithHash)
        db.Insert(cardWithHash) |> ignore

        // find the ID of the just-inserted record
        let newRecordId = (db.Query<CardEntry>("select * from CardEntry where KeyHash = ? and LessonID = ? and LanguageTag = ?", cardWithHash.KeyHash, cardWithHash.LessonID, cardWithHash.LanguageTag) |> 
                            Array.ofSeq |> Array.head).ID
        { cardRecordWithHash with ID = newRecordId }

    member this.AddCard(c: CardRecord) = 
        this.AddCardInternal(c).ID

    member this.AddCards(c: CardRecord seq) = 
        let cardsWithHashes = 
            c |> Seq.map(fun t -> this.UpdateCardWithHash >> CardEntry.FromCardEntry) 

        db.RunInTransaction(fun _ -> db.InsertAll(cardsWithHashes) |> ignore)

    member this.DeleteCard(c: CardRecord) = 
        this.DeleteCardInternal(c)
        this.Compact()

    member this.DeleteCards(cards: CardRecord seq) = 
        db.BeginTransaction()
        this.DeleteCardsInternal(cards)
        db.Commit()

    member private this.DeleteCardInternal(c: CardRecord) = 
        db.Delete(CardEntry.FromCardEntry(c)) |> ignore

    member private this.DeleteCardsInternal(cards: CardRecord seq) = 
        cards |> Seq.iter this.DeleteCardInternal

    member this.CreateOrUpdateGame(ge: GameRecord) = 
        let existingEntry = this.Games |> Array.tryFind(fun t -> t.Name = ge.Name)
        match existingEntry with
        | Some(ee) -> 
            let updatedEntry = { ge with ID = ee.ID }
            db.Update(GameEntry.FromGameEntry(updatedEntry)) |> ignore
            ee.ID
        | _ -> this.AddGame(ge)

    member this.CreateOrUpdateLesson(le: LessonRecord) = 
        let existingEntry = this.Lessons |> Array.tryFind(fun t -> (t.Name = le.Name) && (t.GameID = le.GameID))
        match existingEntry with
        | Some(ee) ->
            let updatedEntry = { le with ID = ee.ID }
            db.Update(LessonEntry.FromLessonEntry(updatedEntry)) |> ignore
            ee.ID
        | _ -> this.AddLesson(le)

    member this.CreateOrUpdateCard(ce: CardRecord) = 
        let existingEntry = db.Query<CardEntry>("select * from CardEntry where KeyHash = ? and LessonID = ?", calculateKeyHash(ce.Key), ce.LessonID) |> 
                            Seq.tryHead

        match existingEntry with
        | Some(ee) ->
            let updatedEntry = this.UpdateCardWithHash({ ce with ID = ee.ID })
            db.Update(CardEntry.FromCardEntry(updatedEntry)) |> ignore
            ee.ID
        | _ -> this.AddCard(ce)

    member this.CreateOrUpdateCards(c: CardRecord seq) = 
        // the cards passed in, but with the KeyHash computed
        let cardsWithHashes = c |> Seq.map this.UpdateCardWithHash |> Array.ofSeq
        let hasExistingCard(card: CardRecord) =
            db.Table<CardEntry>().Where(fun u -> 
                (u.LessonID = card.LessonID) && (u.KeyHash = card.KeyHash) && (u.LanguageTag = card.LanguageTag)) |> Array.ofSeq |> Array.isEmpty

        // cards with the same key hash and language tag that already exist in the db
        let (newCards, existingCards) = cardsWithHashes |> Array.partition hasExistingCard

        // the cards that passed in which had existing entries, with the IDs of the existing entries
        let idOfExistingCard(card: CardRecord) = 
            (db.Table<CardEntry>().Where(fun u -> (u.KeyHash = card.KeyHash) && (u.LanguageTag = card.LanguageTag)) |> Array.ofSeq |> Array.head).ID
            
        let cardsToUpdateWithIds = existingCards |> Array.map (fun c -> { c with ID = idOfExistingCard(c) })

        db.UpdateAll(cardsToUpdateWithIds |> Seq.map CardEntry.FromCardEntry) |> ignore
        db.InsertAll(newCards |> Seq.map CardEntry.FromCardEntry) |> ignore


    member this.CardsFromLesson(lid: int) = 
        db.Query<CardEntry>("select * from CardEntry where LessonID = ?", lid) |> Seq.map(fun t -> t.ToCardEntry()) |> Array.ofSeq

    member this.CardsFromLesson(le: LessonRecord) = 
        this.CardsFromLesson(le.ID)

    member this.LanguagesForLesson(lid: int) = 
        let result = db.Query<LanguageQueryResult>("select distinct LanguageTag from CardEntry where LessonID = ?", lid)
        result |> Seq.map (fun t -> t.LanguageTag) |> Seq.toList

    member this.LanguagesForLesson(le: LessonRecord) = 
        this.LanguagesForLesson(le.ID)

    member this.CardsFromLessonAndLanguageTag(lid: int, language: string) = 
        db.Query<CardEntry>("select * from CardEntry where LessonID = ? and LanguageTag = ?", lid, language) |> Seq.map(fun t -> t.ToCardEntry()) |> Array.ofSeq

    member this.CardsFromLessonAndLanguageTag(le: LessonRecord, language: string) = 
        this.CardsFromLessonAndLanguageTag(le.ID, language)