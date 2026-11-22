module AgeOfEmpiresGamesTests

open Expecto
open System
open System.IO
open System.Xml.Linq

[<Tests>]
let tests =
    testList "Age of Empires III audio mapping tests" [
        test "parseAoe3StringTableXml extracts language, speaker, and soundfilename metadata" {
            let xml = """<?xml version="1.0" encoding="utf-8"?>
<StringTable version="0">
  <Language name="German">
    <String _locID="1001" gamecharacter="Captain" soundfilename="CAPT0001">Vorwärts, Männer!</String>
    <String _locID="1002">Einstellungen</String>
  </Language>
</StringTable>"""

            let (lang, entries) = AgeOfEmpiresGames.parseAoe3StringTableXml (XElement.Parse(xml))
            Expect.equal lang "de" "German language tag should be detected"
            Expect.equal entries.Length 2 "should parse 2 entries"

            let byId = entries |> Array.map (fun e -> (e.LocId, e)) |> Map.ofArray
            Expect.equal byId.["1001"].Text "Vorwärts, Männer!" "voiced text"
            Expect.equal byId.["1001"].Speaker "Captain" "speaker from gamecharacter"
            Expect.equal byId.["1001"].SoundFileName (Some "CAPT0001") "soundfilename attribute"

            Expect.equal byId.["1002"].Speaker "" "unvoiced entry has empty speaker"
            Expect.equal byId.["1002"].SoundFileName None "unvoiced entry has no soundfilename"
        }

        test "indexAoe3SoundFiles indexes dialog/taunts/cinematics MP3s and prioritizes dialog" {
            let root = Path.Combine(Path.GetTempPath(), "ll-aoe3-sound-" + Guid.NewGuid().ToString("N"))
            try
                let dialogDir = Path.Combine(root, "bin", "Sound", "dialog")
                let otherDir = Path.Combine(root, "bin", "Sound", "misc")
                let tauntsDir = Path.Combine(root, "bin", "Sound", "taunts")
                Directory.CreateDirectory(dialogDir) |> ignore
                Directory.CreateDirectory(otherDir) |> ignore
                Directory.CreateDirectory(tauntsDir) |> ignore

                let dialogFile = Path.Combine(dialogDir, "CAPT0001.mp3")
                let miscDup = Path.Combine(otherDir, "CAPT0001.mp3")
                let tauntFile = Path.Combine(tauntsDir, "001 Yes.mp3")
                File.WriteAllBytes(dialogFile, [| 1uy |])
                File.WriteAllBytes(miscDup, [| 2uy |])
                File.WriteAllBytes(tauntFile, [| 3uy |])

                let index = AgeOfEmpiresGames.indexAoe3SoundFiles root
                Expect.equal index.["capt0001"] dialogFile "dialog folder should take precedence over misc"
                Expect.equal index.["001 yes"] tauntFile "taunts should also be indexed"
            finally
                if Directory.Exists(root) then Directory.Delete(root, true)
        }
    ]
