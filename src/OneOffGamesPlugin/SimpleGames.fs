module SimpleGames

open LLDatabase
open OneOffGamesUtils
open System
open System.IO

let ExtractSkullsOfTheShogun(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let lessonEntry = {
        LessonRecord.GameID = g.ID;
        ID = 0;
        Name = "Game Text"
    }
    let lessonEntryWithId = { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }
    OneOffGamesUtils.ExtractStringsFromAssemblies(path, Path.Combine(path, "SkullsOfTheShogun.exe"), "SkullsOfTheShogun.resources.dll", "TBS.Properties.AllResources", "gametext", lessonEntryWithId.ID)
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards
