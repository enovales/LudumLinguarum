module RggEngineTests

open Expecto
open FsCheck
open LudumLinguarumPlugins
open RggEngine
open RggEngine.ParArchive
open RggEngine.RggFormats
open RggEngine.RggGames
open RggEngine.Sllz
open System
open System.IO
open System.Text

let private filterValidSyntheticStrings (items: string list) : string array =
    items
    |> List.map (fun s ->
        if isNull s then ""
        else
            s.Replace("\000", "")
             .Replace("\r", " ")
             .Replace("\n", " ")
             .Replace("<", "")
             .Replace(">", "")
             .Trim())
    |> List.map cleanText
    |> List.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
    |> List.distinct
    |> List.truncate 12
    |> List.toArray

[<Tests>]
let sllzTests =
    testList "RggEngine.Sllz tests" [
        testProperty "SLLZ v1 (Standard) little-endian compress and decompress round-trips arbitrary byte arrays" <|
            fun (rawBytes: byte array) ->
                let payload = if isNull rawBytes then [||] else rawBytes
                let compressed = compressStandard (payload, SllzEndianness.LittleEndian)
                Expect.isTrue (isSllz compressed) "compressed output should have SLLZ magic"
                use ms = new MemoryStream(compressed, false)
                let header = readHeader ms
                Expect.equal header.Version SllzVersion.Standard "expected v1 Standard"
                Expect.equal header.Endianness SllzEndianness.LittleEndian "expected LittleEndian"
                Expect.equal (int header.OriginalSize) payload.Length "decompressed size header should match"
                let decompressed = decompress compressed
                Expect.equal decompressed payload "decompressed payload should match original"

        testProperty "SLLZ v1 (Standard) big-endian compress and decompress round-trips arbitrary byte arrays" <|
            fun (rawBytes: byte array) ->
                let payload = if isNull rawBytes then [||] else rawBytes
                let compressed = compressStandard (payload, SllzEndianness.BigEndian)
                use ms = new MemoryStream(compressed, false)
                let header = readHeader ms
                Expect.equal header.Endianness SllzEndianness.BigEndian "expected BigEndian"
                let decompressed = decompress compressed
                Expect.equal decompressed payload "decompressed payload should match original"

        testProperty "SLLZ v2 (Zlib) little-endian compress and decompress round-trips non-empty byte arrays" <|
            fun (rawBytes: byte array) ->
                let payload =
                    if isNull rawBytes || rawBytes.Length = 0 then [| 0x41uy; 0x42uy; 0x43uy |]
                    else rawBytes
                let compressed = compressZlib (payload, SllzEndianness.LittleEndian)
                Expect.isTrue (isSllz compressed) "compressed output should have SLLZ magic"
                use ms = new MemoryStream(compressed, false)
                let header = readHeader ms
                Expect.equal header.Version SllzVersion.Zlib "expected v2 Zlib"
                let decompressed = decompress compressed
                Expect.equal decompressed payload "decompressed v2 payload should match original"

        testCase "SLLZ v1 compresses highly repetitive data and decompresses accurately" <|
            fun () ->
                let repetitive = Array.init 2048 (fun i -> byte (i % 7))
                let compressed = compressStandard (repetitive, SllzEndianness.LittleEndian)
                Expect.isLessThan compressed.Length repetitive.Length "repetitive data should compress smaller"
                let decompressed = decompress compressed
                Expect.equal decompressed repetitive "round-trip repetitive buffer"

        testCase "SLLZ v2 multi-chunk payload (> 64 KB) round-trips" <|
            fun () ->
                let largePayload = Array.init 75000 (fun i -> byte ((i * 31 + 7) % 251))
                let compressed = compressZlib (largePayload, SllzEndianness.BigEndian)
                let decompressed = decompress compressed
                Expect.equal decompressed largePayload "multi-chunk v2 payload should match original"

        testCase "decompressIfNeeded leaves non-SLLZ data untouched" <|
            fun () ->
                let plain = Encoding.UTF8.GetBytes("PLAIN_DATA_BUFFER")
                Expect.equal (decompressIfNeeded plain) plain "non-SLLZ data should be returned unchanged"
    ]

[<Tests>]
let parArchiveTests =
    testList "RggEngine.ParArchive tests" [
        testProperty "PARC archive build and extract round-trips synthetic file payloads" <|
            fun (p1: byte array) (p2: byte array) (bigEndian: bool) ->
                let b1 = if isNull p1 then [||] else p1
                let b2 = if isNull p2 then [||] else p2
                let archiveBytes =
                    buildParArchive (
                        [|
                            ("folder_a/alpha.bin", b1, false)
                            ("folder_b/sub/beta.bin", b2, true)
                        |],
                        bigEndian
                    )
                Expect.isTrue (isPar archiveBytes) "built archive should have PARC magic"
                use ms = new MemoryStream(archiveBytes, false)
                let extracted = extractAllFromStream (ms, false) |> Map.ofArray
                Expect.equal (extracted |> Map.find "folder_a/alpha.bin") b1 "first file payload should match"
                Expect.equal (extracted |> Map.find "folder_b/sub/beta.bin") b2 "second SLLZ-compressed file payload should match"

        testCase "PARC recursive extraction unpacks nested .par archives and SLLZ-compressed outer archives" <|
            fun () ->
                let innerPayload = Encoding.UTF8.GetBytes("Synthetic inner payload")
                let innerPar =
                    buildParArchive (
                        [| ("nested_dir/item.bin", innerPayload, true) |],
                        true
                    )
                let outerPar =
                    buildParArchive (
                        [| ("outer_dir/child.par", innerPar, true) |],
                        true
                    )
                let compressedOuterPar = compressZlib (outerPar, SllzEndianness.BigEndian)
                use ms = new MemoryStream(compressedOuterPar, false)
                let extracted = extractAllFromStream (ms, true) |> Map.ofArray
                Expect.isTrue (extracted.ContainsKey("outer_dir/child/nested_dir/item.bin")) "nested par entry should be expanded"
                Expect.equal extracted.["outer_dir/child/nested_dir/item.bin"] innerPayload "nested payload should match"
    ]

[<Tests>]
let rggFormatsTests =
    testList "RggEngine.RggFormats tests" [
        testProperty "cleanText is idempotent and strips control tags" <|
            fun (raw: string) ->
                let input = if isNull raw then "" else raw
                let once = cleanText input
                let twice = cleanText once
                Expect.equal twice once "cleanText should be idempotent"
                let tagged = $"<Color:Red>{once}<Sign:01>"
                Expect.equal (cleanText tagged) once "cleanText should strip angle-bracket tags"

        testProperty "StringTbl build and parse round-trips synthetic string lists" <|
            fun (rawStrings: string list) ->
                let valid = filterValidSyntheticStrings rawStrings
                if valid.Length > 0 then
                    let bytes = StringTbl.build ([| valid |], Encoding.UTF8)
                    let parsed = StringTbl.parse (bytes, Encoding.UTF8) |> Array.map snd
                    Expect.equal parsed valid "StringTbl parsed strings should match input"

        testProperty "Epmb build and parse round-trips synthetic popup messages" <|
            fun (rawStrings: string list) ->
                let valid =
                    filterValidSyntheticStrings rawStrings
                    |> Array.filter (fun s -> Encoding.UTF8.GetByteCount(s) < 60)
                if valid.Length > 0 then
                    let bytes = Epmb.build (valid, Encoding.UTF8)
                    let parsed = Epmb.parse (bytes, Encoding.UTF8) |> Array.map snd
                    Expect.equal parsed valid "Epmb parsed entries should match input"

        testProperty "OffsetTableBin build and parse round-trips synthetic string lists" <|
            fun (rawStrings: string list) ->
                let valid = filterValidSyntheticStrings rawStrings
                if valid.Length > 0 then
                    let bytes = OffsetTableBin.build (valid, 8L, Encoding.UTF8)
                    let parsed = OffsetTableBin.parse (bytes, 8L, Encoding.UTF8) |> Array.map snd
                    Expect.equal parsed valid "OffsetTableBin parsed strings should match input"

        testCase "Armp Dragon Engine table builder and parser extract synthetic entries accurately" <|
            fun () ->
                let columns = [| "name"; "description" |]
                let sampleRows =
                    [|
                        ("entry_01", [| "Synthetic Title Alpha"; "Synthetic Description Alpha" |])
                        ("entry_02", [| "Synthetic Title Beta"; "Synthetic Description Beta" |])
                    |]

                let armpBytes = Armp.buildDragonEngineArmp (columns, sampleRows)
                Expect.isTrue (Armp.isArmp armpBytes) "built bytes should have armp magic"
                let parsed = Armp.parse armpBytes |> Map.ofArray
                Expect.equal parsed.["Main/entry_01/name"] "Synthetic Title Alpha" "row 0 col name"
                Expect.equal parsed.["Main/entry_01/description"] "Synthetic Description Alpha" "row 0 col description"
                Expect.equal parsed.["Main/entry_02/name"] "Synthetic Title Beta" "row 1 col name"
                Expect.equal parsed.["Main/entry_02/description"] "Synthetic Description Beta" "row 1 col description"

        testCase "Bin20070319 builds and parses String columns accurately" <|
            fun () ->
                let bytes =
                    Bin20070319.build (
                        [| "title"; "explanation" |],
                        [|
                            [| "Synthetic Strike"; "A neutral test technique." |]
                            [| "Synthetic Guard"; "A neutral defensive stance." |]
                        |],
                        Encoding.UTF8
                    )
                Expect.isTrue (Bin20070319.isBin20070319 bytes) "should recognize 20070319 magic"
                let parsed = Bin20070319.parse (bytes, Encoding.UTF8) |> Map.ofArray
                Expect.equal parsed.["0/title"] "Synthetic Strike" "row 0 title"
                Expect.equal parsed.["0/explanation"] "A neutral test technique." "row 0 explanation"
                Expect.equal parsed.["1/title"] "Synthetic Guard" "row 1 title"
                Expect.equal parsed.["1/explanation"] "A neutral defensive stance." "row 1 explanation"

        testCase "Msg build and parse extracts dialogue lines and talker names" <|
            fun () ->
                let groups =
                    [|
                        [| "First line of dialogue."; "Second line of dialogue." |]
                        [| "Response from the second speaker." |]
                    |]
                let talkers = [| "Speaker Alpha"; "Speaker Beta" |]
                let msgBytes = Msg.build (groups, talkers, Encoding.UTF8)
                let parsed = Msg.parse (msgBytes, Encoding.UTF8) |> Map.ofArray
                Expect.equal parsed.["line/0/0"] "First line of dialogue." "group 0 line 0"
                Expect.equal parsed.["line/0/1"] "Second line of dialogue." "group 0 line 1"
                Expect.equal parsed.["line/1/0"] "Response from the second speaker." "group 1 line 0"
                Expect.equal parsed.["talker/0"] "Speaker Alpha" "talker 0"
                Expect.equal parsed.["talker/1"] "Speaker Beta" "talker 1"

        testCase "CmnBin build and parse extracts Japanese and localized cutscene subtitles after marker" <|
            fun () ->
                let jaSubs = [| "字幕テスト一"; "字幕テスト二" |]
                let enSubs = [| "Subtitle line one."; "Subtitle line two." |]
                let cmnBytes = CmnBin.build (jaSubs, enSubs, Encoding.UTF8)
                let parsedJa = CmnBin.parse (cmnBytes, Encoding.UTF8, false)
                let parsedEn = CmnBin.parse (cmnBytes, Encoding.UTF8, true)
                Expect.equal parsedJa [| ("sub/0/0", "字幕テスト一"); ("sub/0/1", "字幕テスト二") |] "Japanese CmnBin subtitles should match"
                Expect.equal parsedEn [| ("sub/0/0", "Subtitle line one."); ("sub/0/1", "Subtitle line two.") |] "English CmnBin subtitles should match"
    ]

[<Tests>]
let rggGamesAndPluginTests =
    testList "RggEngine.RggGames and RggPlugin tests" [
        testCase "Language code mappers distinguish Old Engine and Dragon Engine conventions" <|
            fun () ->
                // In Old Engine (Yakuza 0 / Kiwami), 'c' is English while in Dragon Engine (Yakuza 6) 'c' is Traditional Chinese
                Expect.equal (tryMapOldEngineLanguage "c") (Some "en") "Old Engine 'c' should map to 'en'"
                Expect.equal (tryMapOldEngineLanguage "j") (Some "ja") "Old Engine 'j' should map to 'ja'"
                Expect.equal (tryMapDragonEngineLanguage "c") (Some "zh-TW") "Dragon Engine 'c' should map to 'zh-TW'"
                Expect.equal (tryMapDragonEngineLanguage "e") (Some "en") "Dragon Engine 'e' should map to 'en'"
                Expect.equal (tryMapDragonEngineLanguage "j") (Some "ja") "Dragon Engine 'j' should map to 'ja'"
                Expect.equal (tryMapDragonEngineLanguage "zhs") (Some "zh-CN") "Dragon Engine 'zhs' should map to 'zh-CN'"
                Expect.equal (tryMapDragonEngineLanguage "zht") (Some "zh-TW") "Dragon Engine 'zht' should map to 'zh-TW'"

        testCase "Old Engine game extraction aligns English and Japanese keys across boot.par, wdr_par, and auth_w64" <|
            fun () ->
                let tempDir = Path.Combine(Path.GetTempPath(), "LL_RggOldEngineTest_" + Guid.NewGuid().ToString("N"))
                Directory.CreateDirectory(tempDir) |> ignore
                try
                    let dataDir = Path.Combine(tempDir, "media", "data")
                    Directory.CreateDirectory(dataDir) |> ignore

                    // 1. boot.par containing ability.bin (Japanese) and ability.bin_c (English)
                    let jaBootBin =
                        Bin20070319.build (
                            [| "name" |],
                            [| [| "合成スキル一" |]; [| "合成スキル二" |] |],
                            Encoding.UTF8
                        )
                    let enBootBin =
                        Bin20070319.build (
                            [| "name" |],
                            [| [| "Synthetic Skill One" |]; [| "Synthetic Skill Two" |] |],
                            Encoding.UTF8
                        )
                    let bootParBytes =
                        buildParArchive (
                            [|
                                ("ability.bin", jaBootBin, true)
                                ("ability.bin_c", enBootBin, true)
                            |],
                            true
                        )
                    File.WriteAllBytes(Path.Combine(dataDir, "boot.par"), bootParBytes)

                    // 2. wdr_par_j/wdr.par and wdr_par_c/wdr.par containing scenario.msg
                    let wdrJaDir = Path.Combine(dataDir, "wdr_par_j")
                    let wdrEnDir = Path.Combine(dataDir, "wdr_par_c")
                    Directory.CreateDirectory(wdrJaDir) |> ignore
                    Directory.CreateDirectory(wdrEnDir) |> ignore

                    let jaMsg = Msg.build ([| [| "こんにちは、合成世界。" |] |], [| "話者A" |], Encoding.UTF8)
                    let enMsg = Msg.build ([| [| "Hello, synthetic world." |] |], [| "Speaker A" |], Encoding.UTF8)

                    File.WriteAllBytes(
                        Path.Combine(wdrJaDir, "wdr.par"),
                        buildParArchive ([| ("sub/scenario.msg", jaMsg, false) |], true)
                    )
                    File.WriteAllBytes(
                        Path.Combine(wdrEnDir, "wdr.par"),
                        buildParArchive ([| ("sub/scenario.msg", enMsg, false) |], true)
                    )

                    // 3. auth_w64_j/a01.par and auth_w64_e/a01.par containing cmn.bin
                    let authJaDir = Path.Combine(dataDir, "auth_w64_j")
                    let authEnDir = Path.Combine(dataDir, "auth_w64_e")
                    Directory.CreateDirectory(authJaDir) |> ignore
                    Directory.CreateDirectory(authEnDir) |> ignore

                    let jaCmn = CmnBin.build ([| "字幕テスト一" |], [||], Encoding.UTF8)
                    let enCmn = CmnBin.build ([| "字幕テスト一" |], [| "Subtitle test one" |], Encoding.UTF8)

                    File.WriteAllBytes(
                        Path.Combine(authJaDir, "a01.par"),
                        buildParArchive ([| ("cmn.bin", jaCmn, false) |], true)
                    )
                    File.WriteAllBytes(
                        Path.Combine(authEnDir, "a01.par"),
                        buildParArchive ([| ("cmn.bin", enCmn, false) |], true)
                    )

                    let extracted = extractOldEngineGame "Yakuza 0" tempDir
                    Expect.equal extracted.lessons.Length 1 "should produce 1 lesson"

                    let enCards =
                        extracted.cards
                        |> Array.filter (fun c -> c.LanguageTag = "en")
                        |> Array.map (fun c -> (c.Key, c.Text))
                        |> Map.ofArray
                    let jaCards =
                        extracted.cards
                        |> Array.filter (fun c -> c.LanguageTag = "ja")
                        |> Array.map (fun c -> (c.Key, c.Text))
                        |> Array.map id
                        |> Map.ofArray

                    Expect.equal (enCards |> Map.keys |> Seq.toArray) (jaCards |> Map.keys |> Seq.toArray) "English and Japanese card keys must align identically"
                    Expect.equal enCards.["boot/ability:0/name"] "Synthetic Skill One" "English boot skill"
                    Expect.equal jaCards.["boot/ability:0/name"] "合成スキル一" "Japanese boot skill"
                    Expect.equal enCards.["wdr/sub/scenario:line/0/0"] "Hello, synthetic world." "English wdr msg"
                    Expect.equal jaCards.["wdr/sub/scenario:line/0/0"] "こんにちは、合成世界。" "Japanese wdr msg"
                    Expect.equal enCards.["auth/a01/cmn:sub/0/0"] "Subtitle test one" "English cutscene subtitle"
                    Expect.equal jaCards.["auth/a01/cmn:sub/0/0"] "字幕テスト一" "Japanese cutscene subtitle"
                finally
                    if Directory.Exists(tempDir) then
                        Directory.Delete(tempDir, true)

        testCase "Dragon Engine game extraction aligns keys across codename archives and loose db folders" <|
            fun () ->
                let tempDir = Path.Combine(Path.GetTempPath(), "LL_RggDragonEngineTest_" + Guid.NewGuid().ToString("N"))
                Directory.CreateDirectory(tempDir) |> ignore
                try
                    let dataDir = Path.Combine(tempDir, "runtime", "media", "data")
                    Directory.CreateDirectory(dataDir) |> ignore

                    let enArmp =
                        Armp.buildDragonEngineArmp (
                            [| "name"; "help" |],
                            [| ("item_01", [| "Synthetic Beverage"; "Restores synthetic vitality." |]) |]
                        )
                    let jaArmp =
                        Armp.buildDragonEngineArmp (
                            [| "name"; "help" |],
                            [| ("item_01", [| "合成飲料"; "合成体力を回復する。" |]) |]
                        )

                    let enPar =
                        buildParArchive (
                            [| ("db.yazawa/en/item.bin", enArmp, true) |],
                            false
                        )
                    let jaPar =
                        buildParArchive (
                            [| ("db.yazawa/ja/item.bin", jaArmp, true) |],
                            false
                        )

                    File.WriteAllBytes(Path.Combine(dataDir, "db.yazawa.en.par"), enPar)
                    File.WriteAllBytes(Path.Combine(dataDir, "db.yazawa.ja.par"), jaPar)

                    let plugin = new RggPlugin() :> IGameExtractorPlugin
                    let extracted = plugin.ExtractAll("Yakuza: Like a Dragon", tempDir, [||])

                    let enCards =
                        extracted.cards
                        |> Array.filter (fun c -> c.LanguageTag = "en")
                        |> Array.map (fun c -> (c.Key, c.Text))
                        |> Map.ofArray
                    let jaCards =
                        extracted.cards
                        |> Array.filter (fun c -> c.LanguageTag = "ja")
                        |> Array.map (fun c -> (c.Key, c.Text))
                        |> Map.ofArray

                    Expect.equal (enCards |> Map.keys |> Seq.toArray) (jaCards |> Map.keys |> Seq.toArray) "Dragon Engine English and Japanese keys must match"
                    Expect.equal enCards.["db/item:Main/item_01/name"] "Synthetic Beverage" "English item name"
                    Expect.equal jaCards.["db/item:Main/item_01/name"] "合成飲料" "Japanese item name"
                    Expect.equal enCards.["db/item:Main/item_01/help"] "Restores synthetic vitality." "English item description"
                    Expect.equal jaCards.["db/item:Main/item_01/help"] "合成体力を回復する。" "Japanese item description"
                finally
                    if Directory.Exists(tempDir) then
                        Directory.Delete(tempDir, true)

        testCase "Yakuza Kiwami 3 & Dark Ties (codename bis) extracts db.bis, ui.bis, and talk_bis archives across languages" <|
            fun () ->
                let tempDir = Path.Combine(Path.GetTempPath(), "LL_RggYK3Test_" + Guid.NewGuid().ToString("N"))
                Directory.CreateDirectory(tempDir) |> ignore
                try
                    let dataDir = Path.Combine(tempDir, "runtime", "media", "data")
                    Directory.CreateDirectory(dataDir) |> ignore

                    let enDbArmp =
                        Armp.buildDragonEngineArmp (
                            [| "title" |],
                            [| ("chapter_01", [| "Synthetic Chapter One" |]) |]
                        )
                    let eslDbArmp =
                        Armp.buildDragonEngineArmp (
                            [| "title" |],
                            [| ("chapter_01", [| "Capítulo Sintético Uno" |]) |]
                        )
                    let enUiArmp =
                        Armp.buildDragonEngineArmp (
                            [| "label" |],
                            [| ("menu_start", [| "Start Synthetic Mode" |]) |]
                        )
                    let eslUiArmp =
                        Armp.buildDragonEngineArmp (
                            [| "label" |],
                            [| ("menu_start", [| "Iniciar Modo Sintético" |]) |]
                        )

                    File.WriteAllBytes(
                        Path.Combine(dataDir, "db.bis.en.par"),
                        buildParArchive ([| ("db.bis/en/scenario.bin", enDbArmp, true) |], false)
                    )
                    File.WriteAllBytes(
                        Path.Combine(dataDir, "db.bis.esl.par"),
                        buildParArchive ([| ("db.bis/esl/scenario.bin", eslDbArmp, true) |], false)
                    )
                    File.WriteAllBytes(
                        Path.Combine(dataDir, "ui.bis.en.par"),
                        buildParArchive ([| ("ui.bis/en/menu.bin", enUiArmp, true) |], false)
                    )
                    File.WriteAllBytes(
                        Path.Combine(dataDir, "ui.bis.esl.par"),
                        buildParArchive ([| ("ui.bis/esl/menu.bin", eslUiArmp, true) |], false)
                    )

                    let plugin = new RggPlugin() :> IGameExtractorPlugin
                    let extracted = plugin.ExtractAll("Yakuza Kiwami 3 & Dark Ties", tempDir, [||])

                    let enCards =
                        extracted.cards
                        |> Array.filter (fun c -> c.LanguageTag = "en")
                        |> Array.map (fun c -> (c.Key, c.Text))
                        |> Map.ofArray
                    let es419Cards =
                        extracted.cards
                        |> Array.filter (fun c -> c.LanguageTag = "es-419")
                        |> Array.map (fun c -> (c.Key, c.Text))
                        |> Map.ofArray

                    Expect.equal (enCards |> Map.keys |> Seq.toArray) (es419Cards |> Map.keys |> Seq.toArray) "English and Latin American Spanish keys must match"
                    Expect.equal enCards.["db/scenario:Main/chapter_01/title"] "Synthetic Chapter One" "English db entry"
                    Expect.equal es419Cards.["db/scenario:Main/chapter_01/title"] "Capítulo Sintético Uno" "es-419 db entry"
                    Expect.equal enCards.["ui/menu:Main/menu_start/label"] "Start Synthetic Mode" "English ui entry"
                    Expect.equal es419Cards.["ui/menu:Main/menu_start/label"] "Iniciar Modo Sintético" "es-419 ui entry"
                finally
                    if Directory.Exists(tempDir) then
                        Directory.Delete(tempDir, true)

        testCase "RggPlugin is discoverable by PluginManager and exposes all 14 RGG Studio games" <|
            fun () ->
                let pm = new PluginManager() :> IPluginManager
                let discovered = pm.Discover(typeof<RggPlugin>.Assembly)
                Expect.isTrue (discovered |> List.contains typeof<RggPlugin>) "PluginManager.Discover should find RggPlugin"

                pm.Instantiate(TextWriter.Null, typeof<RggPlugin>, [||])
                Expect.equal pm.SupportedGames.Length 14 "RggPlugin should expose 14 supported games"
                for spec in supportedGames do
                    Expect.isSome (pm.GetPluginForGame(spec.Name)) $"GetPluginForGame should resolve '{spec.Name}'"
    ]
