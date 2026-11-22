module FmodToolsTests

open Expecto
open FmodTools
open FsCheck
open System
open System.IO

[<Tests>]
let tests =
    testList "FmodTools tests" [
        test "normalizeCueId strips /VO/ or VO/ prefixes and uppercases" {
            Expect.equal (normalizeCueId "/VO/SpeakerA_0005") "SPEAKERA_0005" "strips /VO/ prefix"
            Expect.equal (normalizeCueId "VO/SpeakerB_0010") "SPEAKERB_0010" "strips VO/ prefix"
            Expect.equal (normalizeCueId "  SpeakerC_0042  ") "SPEAKERC_0042" "trims and uppercases"
            Expect.equal (normalizeCueId "") "" "empty string stays empty"
        }

        test "inferSpeakerFromCue extracts speaker prefix before numeric suffix" {
            Expect.equal (inferSpeakerFromCue "/VO/Guide_0001") "Guide" "extracts speaker after /VO/"
            Expect.equal (inferSpeakerFromCue "TravelerField_0123") "TravelerField" "extracts multi-word camel prefix"
            Expect.equal (inferSpeakerFromCue "MiscCueWithoutNumber") "" "returns empty when no numeric suffix"
        }

        test "parseFmodHeaderCueMap maps 0-based #define indices to 1-based vgmstream subsong indices" {
            let headerLines =
                [|
                    "#ifndef _VO_H"
                    "#define _VO_H"
                    ""
                    "#define ALPHA_0001 0"
                    "#define ALPHA_0002 1"
                    "#define BETA_0010 42"
                    "#endif"
                |]
            let map = parseFmodHeaderCueMap headerLines
            Expect.equal map.Count 3 "should parse 3 numeric #define entries"
            Expect.equal map.["ALPHA_0001"] 1 "0-based index 0 becomes 1-based 1"
            Expect.equal map.["ALPHA_0002"] 2 "0-based index 1 becomes 1-based 2"
            Expect.equal map.["BETA_0010"] 43 "0-based index 42 becomes 1-based 43"
        }

        test "loadDirectoryBankCueMap pairs .fsb and .txt/.h files and uses bank stem as speaker" {
            let dir = Path.Combine(Path.GetTempPath(), "ll-fmod-dir-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(dir) |> ignore
            try
                let bank1 = Path.Combine(dir, "Mentor.fsb")
                let txt1 = Path.Combine(dir, "Mentor.txt")
                let bank2 = Path.Combine(dir, "Scout.fsb")
                let h2 = Path.Combine(dir, "Scout.h")
                File.WriteAllBytes(bank1, [| 0uy |])
                File.WriteAllText(txt1, "#define MENTOR_0001 0\n#define MENTOR_0002 1\n")
                File.WriteAllBytes(bank2, [| 0uy |])
                File.WriteAllText(h2, "#define SCOUT_0005 3\n")

                let map = loadDirectoryBankCueMap dir
                Expect.equal map.Count 3 "should load all cues across both banks"
                Expect.equal map.["MENTOR_0001"].BankPath bank1 "Mentor bank path"
                Expect.equal map.["MENTOR_0001"].SubsongIndex 1 "Mentor subsong 1"
                Expect.equal map.["MENTOR_0001"].Speaker "Mentor" "Mentor speaker from filename"
                Expect.equal map.["SCOUT_0005"].BankPath bank2 "Scout bank path"
                Expect.equal map.["SCOUT_0005"].SubsongIndex 4 "Scout subsong 4"
                Expect.equal map.["SCOUT_0005"].Speaker "Scout" "Scout speaker from filename"
            finally
                Directory.Delete(dir, true)
        }

        testProperty "parseFmodHeaderCueMap converts arbitrary non-negative 0-based index to index + 1" <|
            fun (NonNegativeInt idx) ->
                let bounded = idx % 100000
                let line = sprintf "#define SYNTH_CUE_%d %d" bounded bounded
                let map = parseFmodHeaderCueMap [| line |]
                let expectedKey = sprintf "SYNTH_CUE_%d" bounded
                map |> Map.tryFind expectedKey = Some (bounded + 1)
    ]
