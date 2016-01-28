namespace System
open System.Reflection
open System.Runtime.CompilerServices

[<assembly: AssemblyTitleAttribute("LudumLinguarumLib")>]
[<assembly: AssemblyProductAttribute("LudumLinguarum")>]
[<assembly: AssemblyDescriptionAttribute("Tools for extracting localized content from games, and turning them into language learning content.")>]
[<assembly: AssemblyVersionAttribute("0.0.1")>]
[<assembly: AssemblyFileVersionAttribute("0.0.1")>]
[<assembly: InternalsVisibleToAttribute("LudumLinguarumLib.Tests")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.1"
