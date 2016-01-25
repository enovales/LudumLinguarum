module OneOffGamesData

open System.IO
open System.Reflection

let DataAssembly = Assembly.LoadFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "OneOffGamesData.dll"))


