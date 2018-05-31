// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docsrc/content' directory
// (the generated documentation is stored in the 'docsrc/output' directory)
// --------------------------------------------------------------------------------------
let referenceBinaries = []
// Web site location for the generated documentation
let website = "/LudumLinguarum"

let githubLink = "https://github.com/enovales/LudumLinguarum"

// Specify more information about your project
let info =
  [ "project-name", "LudumLinguarum"
    "project-author", "Erik Novales"
    "project-summary", "Tools for extracting localized content from games, and turning them into language learning content."
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/LudumLinguarum" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#I "../../packages/FAKE/tools/"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper

#load "../../packages/FSharp.Formatting/FSharp.Formatting.fsx"

open FSharp.Literate
open FSharp.MetadataFormat
open FSharp.Formatting.Razor

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../../docs")
#endif

System.IO.Directory.SetCurrentDirectory (__SOURCE_DIRECTORY__)

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content"
let output     = __SOURCE_DIRECTORY__ @@ "../../docs"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let templates  = __SOURCE_DIRECTORY__ @@ "templates"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting/"
let docTemplate = "docpage.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting @@ "templates"
                          formatting @@ "templates/reference" ])
subDirectories (directoryInfo templates)
|> Seq.iter (fun d ->
                let name = d.Name
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates @@ name
                                   formatting @@ "templates"
                                   formatting @@ "templates/reference" ]))

let fsiEvaluator = lazy (Some (FsiEvaluator() :> IFsiEvaluator))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Log "Copying styles and scripts: "

let binaries =
    let manuallyAdded = 
        referenceBinaries 
        |> List.map (fun b -> bin @@ b)

    let findFrameworkDirectory(d: DirectoryInfo) = 
      // From https://docs.microsoft.com/en-us/dotnet/standard/frameworks
      // Entries are included in increasing preference order.
      let frameworkNames = 
        [|
          // Windows Phone      
          "wp"
          "wp7"
          "wp75"
          "wp8"
          "wp81"
          "wpa81"

          // Silverlight
          "sl4"
          "sl5"

          // .NET Micro Framework
          "netmf"

          // Windows Store
          "netcore"
          "netcore45"
          "netcore45"
          "win"
          "win8"
          "netcore451"
          "win81"

          // Universal Windows Platform
          "uap"
          "uap10.0"
          "uap10.0"
          "win10"
          "netcore50"

          // .NET Framework
          "net11"
          "net20"
          "net35"
          "net40"
          "net403"
          "net45"
          "net451"
          "net452"
          "net46"
          "net461"
          "net462"
          "net47"
          "net471"
          "net472"

          // .NET Core
          "netcoreapp1.0"
          "netcoreapp1.1"
          "netcoreapp2.0"
          "netcoreapp2.1"

          // .NET Standard
          "netstandard1.0"
          "netstandard1.1"
          "netstandard1.2"
          "netstandard1.3"
          "netstandard1.4"
          "netstandard1.5"
          "netstandard1.6"
          "netstandard2.0"
        |] 
        |> Array.rev

      subDirectories d
      |> Seq.collect(fun sd -> frameworkNames |> Seq.tryFind(fun x -> sd.FullName.ToLower().Contains(x)) |> Option.map (fun _ -> sd) |> Option.toArray)
      |> Seq.head
    
    let conventionBased = 
        directoryInfo bin 
        |> subDirectories
        |> Array.map (fun d -> d.Name, findFrameworkDirectory d )
        |> Array.map (fun (name, d) -> 
            d.GetFiles()
            |> Array.filter (fun x -> 
                x.Name.ToLower() = (sprintf "%s.dll" name).ToLower())
            |> Array.map (fun x -> x.FullName) 
            )
        |> Array.concat
        |> List.ofArray

    conventionBased @ manuallyAdded

let libDirs =
    let conventionBasedbinDirs =
        directoryInfo bin 
        |> subDirectories
        |> Array.map (fun d -> d.FullName)
        |> List.ofArray

    conventionBasedbinDirs @ [bin]

// Build API reference from XML comments
let buildReference () =
  CleanDir (output @@ "reference")
  RazorMetadataFormat.Generate
    ( binaries, output @@ "reference", layoutRootsAll.["en"],
      parameters = ("root", root)::info,
      sourceRepo = githubLink @@ "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
      publicOnly = true,libDirs = libDirs )

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs =
    [ content, docTemplate; ]
  for dir, template in subdirs do
    let sub = "." // Everything goes into the same output directory here
    let langSpecificPath(lang, path:string) =
        path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.exists(fun i -> i = lang)
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    RazorLiterate.ProcessDirectory
      ( dir, template, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,
        generateAnchors = true,
        processRecursive = false,
        includeSource = true, // Only needed for 'side-by-side' pages, but does not hurt others
        ?fsiEvaluator = fsiEvaluator.Value ) // Currently we don't need it but it's a good stress test to have it here.

// Generate
copyFiles()
#if HELP
buildDocumentation()
#endif
#if REFERENCE
buildReference()
#endif
