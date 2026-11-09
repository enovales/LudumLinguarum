module BalatroTests

open Balatro
open Expecto
open LLDatabase
open System
open System.IO
open System.IO.Compression
open System.Text

// Synthetic, representative localization content. Must not contain actual
// game text (see AGENTS.md); it mirrors the shape of the real files.
let private enLua = """return {
    descriptions={
        Joker={
            j_example={
                name="Example Joker",
                text={
                    "Gains {C:mult}+#1#{} Mult",
                    "",
                    "when a {C:attention}#2#{} is played",
                },
                unlock={
                    "Play {C:attention,E:1}#1#{} hands",
                    "{C:inactive}(#2#)",
                },
            },
        },
    },
    misc={
        dictionary={
            b_buy="BUY",
            ["$"]="$",
            collab_names={
                ["1"]="Default",
                ["2"]="Other Game",
            },
        },
        quips={
            wq_1={
                "You {C:attention}Aced{} it!",
            },
        },
        empty_group={
            blank="",
        },
    },
}
"""

let private frLua = """return {
    misc={
        dictionary={
            b_buy="ACHETER",
            poker={
                ["High Card"]="Carte haute",
                ["Pair"]="Paire",
            },
        },
        tutorial={
            t_1={
                "Jouez une {valores} main",
                "pour commencer",
            },
        },
    },
}
"""

let private expectedEnStrings =
    [|
        ("descriptions/Joker/j_example/name", "Example Joker")
        ("descriptions/Joker/j_example/text", "Gains +#1# Mult when a #2# is played")
        ("descriptions/Joker/j_example/unlock", "Play #1# hands (#2#)")
        ("misc/dictionary/$", "$")
        ("misc/dictionary/b_buy", "BUY")
        ("misc/dictionary/collab_names/1", "Default")
        ("misc/dictionary/collab_names/2", "Other Game")
        ("misc/quips/wq_1", "You Aced it!")
    |]

let private expectedFrStrings =
    [|
        ("misc/dictionary/b_buy", "ACHETER")
        ("misc/dictionary/poker/High Card", "Carte haute")
        ("misc/dictionary/poker/Pair", "Paire")
        ("misc/tutorial/t_1", "Jouez une {valores} main pour commencer")
    |]

let private mkCard(languageTag: string)(key: string)(text: string): CardRecord =
    {
        CardRecord.ID = 0
        LessonID = 0
        Text = text
        Gender = "masculine"
        Key = key
        GenderlessKey = key
        KeyHash = 0
        GenderlessKeyHash = 0
        SoundResource = ""
        LanguageTag = languageTag
        Reversible = true
    }

// Builds the raw bytes of a ZIP archive, exactly as it would appear embedded
// at the start of a file (central directory offsets relative to the ZIP start,
// as in a fused LÖVE2D executable).
let private makeZipBytes(entries: (string * string) array): byte array =
    use ms = new MemoryStream()
    do
        use zip = new ZipArchive(ms, ZipArchiveMode.Create, true)
        entries
        |> Array.iter (fun (name, contents) ->
            let entry = zip.CreateEntry(name)
            use es = entry.Open()
            let bytes = Encoding.UTF8.GetBytes(contents)
            es.Write(bytes, 0, bytes.Length))
    ms.ToArray()

let private makeArchiveBytes(prefix: byte array)(entries: (string * string) array): byte array =
    Array.append prefix (makeZipBytes entries)

let private withTempGameDir(files: (string * byte array) array)(f: string -> unit): unit =
    let dir = Path.Combine(Path.GetTempPath(), "balatro-tests-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(dir) |> ignore
    try
        files |> Array.iter (fun (name, contents) -> File.WriteAllBytes(Path.Combine(dir, name), contents))
        f(dir)
    finally
        Directory.Delete(dir, true)

[<Tests>]
let sanitizeTextTests =
  testList "Balatro sanitizeText tests" [
    testCase "Styling tags and the empty reset tag are removed" <|
      fun () ->
        Expect.equal "+#1# Joker slot" (sanitizeText "{C:attention}+#1#{} Joker slot") ""

    testCase "Tags with multiple attributes are removed" <|
      fun () ->
        Expect.equal "x#1#" (sanitizeText "{X:mult,C:white}x#1#{s:0.85}") ""

    testCase "Substitution variables are kept" <|
      fun () ->
        Expect.equal "Earn $#2# per #1#" (sanitizeText "Earn {C:money}$#2#{} per {C:blue}#1#{}") ""

    testCase "Literal braced words without a tag colon are kept" <|
      fun () ->
        Expect.equal "une {valores} main" (sanitizeText "une {valores} main") ""

    testCase "Whitespace is collapsed and trimmed" <|
      fun () ->
        Expect.equal "a b c" (sanitizeText "  a  b\n\tc  ") ""

    testCase "Plain text is unchanged" <|
      fun () ->
        Expect.equal "Foo bar" (sanitizeText "Foo bar") ""
  ]

[<Tests>]
let parseTests =
  testList "Balatro parseLocalizationLua tests" [
    testCase "Nested tables, bracket keys, arrays, and blank entries are parsed" <|
      fun () ->
        let strings = parseLocalizationLua(enLua) |> collectStrings "" |> Array.ofList
        Expect.equal expectedEnStrings (strings |> Array.sortBy fst) ""

    testCase "A second language file parses independently" <|
      fun () ->
        let strings = parseLocalizationLua(frLua) |> collectStrings "" |> Array.ofList
        Expect.equal expectedFrStrings (strings |> Array.sortBy fst) ""

    testCase "Comments and escape sequences are handled" <|
      fun () ->
        let lua = "return { -- a comment\n    a=\"x\\\"y\\\\z\",\n}\n"
        match parseLocalizationLua(lua) with
        | LuaTable([| (Some("a"), LuaString(v)) |]) -> Expect.equal "x\"y\\z" v ""
        | other -> failwithf "unexpected parse result: %A" other

    testCase "Trailing content after the returned table is rejected" <|
      fun () ->
        Expect.throws (fun () -> parseLocalizationLua("return {} extra") |> ignore) ""

    testProperty "String values round-trip through the parser" <|
      fun (s: string) ->
        let safe = s.Replace("\"", "").Replace("\\", "")
        match parseLocalizationLua("return { a=\"" + safe + "\" }") with
        | LuaTable([| (Some("a"), LuaString(v)) |]) -> v = safe
        | _ -> false
  ]

[<Tests>]
let languageCodeToTagTests =
  testList "Balatro languageCodeToTag tests" [
    testCase "Known language codes map to IETF tags" <|
      fun () ->
        let cases =
            [|
                ("de", Some("de"))
                ("en-us", Some("en-US"))
                ("es_419", Some("es-419"))
                ("es_ES", Some("es-ES"))
                ("pt_BR", Some("pt-BR"))
                ("zh_CN", Some("zh-CN"))
                ("zh_TW", Some("zh-TW"))
            |]
        cases |> Array.iter (fun (code, expected) -> Expect.equal expected (languageCodeToTag code) code)

    testCase "Unknown codes fall back to generic IETF-style normalization" <|
      fun () ->
        Expect.equal (Some("xx-YY")) (languageCodeToTag "xx_yy") ""
        Expect.equal (Some("sv")) (languageCodeToTag "sv") ""
  ]

[<Tests>]
let zipOffsetTests =
  let locEntries =
    [|
        ("localization/en-us.lua", enLua)
        ("localization/fr.lua", frLua)
        ("game.lua", "return {}")
    |]

  testList "Balatro embedded ZIP archive tests" [
    testCase "A plain ZIP archive has offset zero" <|
      fun () ->
        use ms = new MemoryStream(makeZipBytes locEntries)
        Expect.equal 0L (findEmbeddedZipStartOffset ms) ""

    testCase "A fused executable skips the stub before the ZIP data" <|
      fun () ->
        let prefix = Encoding.ASCII.GetBytes("MZ fake executable stub")
        use ms = new MemoryStream(makeArchiveBytes prefix locEntries)
        Expect.equal (int64 prefix.Length) (findEmbeddedZipStartOffset ms) ""

    testCase "Localization entries are read from a fused executable, other entries are skipped" <|
      fun () ->
        let prefix = Encoding.ASCII.GetBytes("MZ fake executable stub")
        use ms = new MemoryStream(makeArchiveBytes prefix locEntries)
        let files = readLocalizationFilesFromArchive ms
        let tags = files |> Array.map fst |> Array.sort
        Expect.equal [| "en-US"; "fr" |] tags ""
        let enStrings = files |> Array.find (fun (tag, _) -> tag = "en-US") |> snd |> collectStrings "" |> Array.ofList
        Expect.equal expectedEnStrings (enStrings |> Array.sortBy fst) ""

    testCase "A non-archive stream is rejected with a useful error" <|
      fun () ->
        use ms = new MemoryStream(Encoding.ASCII.GetBytes("not a zip at all, just some bytes"))
        Expect.throws (fun () -> findEmbeddedZipStartOffset ms |> ignore) ""
  ]

[<Tests>]
let extractTests =
  testList "Balatro extraction tests" [
    testCase "Extraction from a fused Balatro.exe produces a single lesson and language-pairable cards" <|
      fun () ->
        let prefix = Encoding.ASCII.GetBytes("MZ fake executable stub")
        let exeBytes = makeArchiveBytes prefix [| ("localization/en-us.lua", enLua); ("localization/fr.lua", frLua) |]
        withTempGameDir [| ("Balatro.exe", exeBytes) |] (fun dir ->
            let result = ExtractBalatro(dir)
            Expect.equal [| { LessonRecord.ID = 0; Name = "Game Text" } |] result.lessons ""

            let expectedCards =
                [|
                    yield! expectedEnStrings |> Array.map (fun (k, t) -> mkCard "en-US" k t)
                    yield! expectedFrStrings |> Array.map (fun (k, t) -> mkCard "fr" k t)
                |] |> Array.sortBy (fun c -> (c.LanguageTag, c.Key))
            Expect.equal expectedCards (result.cards |> Array.sortBy (fun c -> (c.LanguageTag, c.Key))) "")

    testCase "A standalone .love archive is also supported" <|
      fun () ->
        let loveBytes = makeZipBytes [| ("localization/de.lua", frLua) |]
        withTempGameDir [| ("Balatro.love", loveBytes) |] (fun dir ->
            let result = ExtractBalatro(dir)
            Expect.equal 1 result.lessons.Length ""
            Expect.equal [| "de" |] (result.cards |> Array.map (fun c -> c.LanguageTag) |> Array.distinct) "")

    testCase "Extraction fails with a useful error when no game archive is present" <|
      fun () ->
        withTempGameDir [| ("readme.txt", Encoding.ASCII.GetBytes("nothing here")) |] (fun dir ->
            Expect.throws (fun () -> ExtractBalatro(dir) |> ignore) "")
  ]
