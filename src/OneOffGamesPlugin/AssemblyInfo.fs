namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("OneOffGamesPlugin")>]
[<assembly: AssemblyProductAttribute("LudumLinguarum")>]
[<assembly: AssemblyDescriptionAttribute("Tools for extracting localized content from games, and turning them into language learning content.")>]
[<assembly: AssemblyVersionAttribute("0.10.0")>]
[<assembly: AssemblyFileVersionAttribute("0.10.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.10.0"
    let [<Literal>] InformationalVersion = "0.10.0"
