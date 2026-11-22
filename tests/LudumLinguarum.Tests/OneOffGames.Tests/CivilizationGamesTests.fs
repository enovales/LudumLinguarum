module CivilizationGamesTests

open Expecto
open System
open System.IO
open System.Xml.Linq

[<Tests>]
let tests =
    testList "Civilization IV audio mapping tests" [
        test "XML parsers resolve tech quote tags through Audio2DScripts and AudioDefines to audio files" {
            let tempRoot = Path.Combine(Path.GetTempPath(), "ll-civ4-synth-" + Guid.NewGuid().ToString("N"))
            try
                let baseAssets = Path.Combine(tempRoot, "Assets")
                let expAssets = Path.Combine(tempRoot, "Expansion", "Assets")

                Directory.CreateDirectory(Path.Combine(baseAssets, "XML", "Technologies")) |> ignore
                Directory.CreateDirectory(Path.Combine(baseAssets, "XML", "Audio")) |> ignore
                Directory.CreateDirectory(Path.Combine(baseAssets, "Sounds", "Tech")) |> ignore
                Directory.CreateDirectory(Path.Combine(expAssets, "XML", "Technologies")) |> ignore
                Directory.CreateDirectory(Path.Combine(expAssets, "Sounds", "Tech")) |> ignore

                let techXml = """<?xml version="1.0"?>
<Civ4TechInfos xmlns="x-schema:CIV4TechnologiesSchema.xml">
  <TechInfos>
    <TechInfo>
      <Type>TECH_SYNTH_ALPHA</Type>
      <Quote>TXT_KEY_TECH_SYNTH_ALPHA_QUOTE</Quote>
      <Sound>AS2D_TECH_SYNTH_ALPHA</Sound>
    </TechInfo>
    <TechInfo>
      <Type>TECH_SYNTH_BETA</Type>
      <Quote>TXT_KEY_TECH_SYNTH_BETA_QUOTE</Quote>
      <Sound>AS2D_TECH_SYNTH_BETA</Sound>
    </TechInfo>
  </TechInfos>
</Civ4TechInfos>"""

                let scriptsXml = """<?xml version="1.0"?>
<Script2DSounds>
  <Script2DSound>
    <ScriptID>AS2D_TECH_SYNTH_ALPHA</ScriptID>
    <SoundID>SND_TECH_SYNTH_ALPHA</SoundID>
  </Script2DSound>
  <Script2DSound>
    <ScriptID>AS2D_TECH_SYNTH_BETA</ScriptID>
    <SoundID>SND_TECH_SYNTH_BETA</SoundID>
  </Script2DSound>
</Script2DSounds>"""

                let definesXml = """<?xml version="1.0"?>
<AudioDefines>
  <SoundDatas>
    <SoundData>
      <SoundID>SND_TECH_SYNTH_ALPHA</SoundID>
      <Filename>Sounds/Tech/Tech_SynthAlpha</Filename>
    </SoundData>
    <SoundData>
      <SoundID>SND_TECH_SYNTH_BETA</SoundID>
      <Filename>Sounds/Tech/Tech_SynthBeta</Filename>
    </SoundData>
  </SoundDatas>
</AudioDefines>"""

                File.WriteAllText(Path.Combine(baseAssets, "XML", "Technologies", "CIV4TechInfos.xml"), techXml)
                File.WriteAllText(Path.Combine(baseAssets, "XML", "Audio", "Audio2DScripts.xml"), scriptsXml)
                File.WriteAllText(Path.Combine(baseAssets, "XML", "Audio", "AudioDefines.xml"), definesXml)

                let baseAlphaAudio = Path.Combine(baseAssets, "Sounds", "Tech", "Tech_SynthAlpha.mp3")
                let expBetaAudio = Path.Combine(expAssets, "Sounds", "Tech", "Tech_SynthBeta.mp3")
                File.WriteAllBytes(baseAlphaAudio, [| 1uy; 2uy; 3uy |])
                File.WriteAllBytes(expBetaAudio, [| 4uy; 5uy; 6uy |])

                // Also test expansion override when both exist
                let baseBetaAudio = Path.Combine(baseAssets, "Sounds", "Tech", "Tech_SynthBeta.mp3")
                File.WriteAllBytes(baseBetaAudio, [| 9uy; 9uy; 9uy |])

                let resolved = CivilizationGames.resolveCiv4QuoteAudioFiles [| baseAssets; expAssets |]
                Expect.equal resolved.Count 2 "both synthetic tech quotes should resolve"
                Expect.equal resolved.["TXT_KEY_TECH_SYNTH_ALPHA_QUOTE"] baseAlphaAudio "base quote should resolve to base audio"
                Expect.equal resolved.["TXT_KEY_TECH_SYNTH_BETA_QUOTE"] expBetaAudio "expansion audio should override base audio"
            finally
                if Directory.Exists(tempRoot) then Directory.Delete(tempRoot, true)
        }
    ]
