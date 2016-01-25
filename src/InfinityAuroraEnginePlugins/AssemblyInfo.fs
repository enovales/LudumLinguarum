namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("InfinityAuroraEnginePlugins")>]
[<assembly: AssemblyProductAttribute("LudumLinguarum")>]
[<assembly: AssemblyDescriptionAttribute("Tools for extracting localized content from games, and turning them into language learning content.")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
