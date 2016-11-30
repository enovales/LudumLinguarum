namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("LudumLinguarumConsole")>]
[<assembly: AssemblyProductAttribute("LudumLinguarum")>]
[<assembly: AssemblyDescriptionAttribute("Tools for extracting localized content from games, and turning them into language learning content.")>]
[<assembly: AssemblyVersionAttribute("0.13.0")>]
[<assembly: AssemblyFileVersionAttribute("0.13.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.13.0"
    let [<Literal>] InformationalVersion = "0.13.0"
