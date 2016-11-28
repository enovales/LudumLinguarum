namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("LudumLinguarumLib")>]
[<assembly: AssemblyProductAttribute("LudumLinguarum")>]
[<assembly: AssemblyDescriptionAttribute("Tools for extracting localized content from games, and turning them into language learning content.")>]
[<assembly: AssemblyVersionAttribute("0.12.0")>]
[<assembly: AssemblyFileVersionAttribute("0.12.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.12.0"
    let [<Literal>] InformationalVersion = "0.12.0"
