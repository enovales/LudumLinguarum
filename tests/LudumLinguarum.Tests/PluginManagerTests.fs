module PluginManagerTests

open Expecto
open LudumLinguarumPlugins
open System.IO

[<Tests>]
let tests = 
  testList "PluginManager tests" [
      test "Plugins declare supported audio languages for audio-capable games" {
          let pm = new PluginManager() :> IPluginManager
          pm.Instantiate(TextWriter.Null, typeof<OneOffGames.OneOffGamesPlugin>, [||])
          pm.Instantiate(TextWriter.Null, typeof<InfinityAuroraEngine.AuroraPlugin>, [||])
          pm.Instantiate(TextWriter.Null, typeof<RggEngine.RggPlugin>, [||])

          let byName =
              pm.SupportedGames
              |> Array.map (fun g -> (g.name, g))
              |> Map.ofArray

          let civ4 = byName.["Sid Meier's Civilization IV"]
          Expect.isTrue civ4.SupportsAudio "Civ IV should declare audio support"
          Expect.isTrue (civ4.supportedAudioLanguages |> Array.contains "de") "Civ IV should declare German audio"
          Expect.isTrue (civ4.supportedAudioLanguages |> Array.contains "en") "Civ IV should declare English audio"

          let kotor2 = byName.["Star Wars: Knights of the Old Republic II"]
          Expect.isTrue kotor2.SupportsAudio "KOTOR II should declare audio support"
          Expect.isTrue (kotor2.supportedAudioLanguages |> Array.contains "de") "KOTOR II should declare German audio"

          let balatro = byName.["Balatro"]
          Expect.isFalse balatro.SupportsAudio "Balatro should declare no native audio extraction"
          Expect.isFalse balatro.SupportsImages "Balatro should declare no native image extraction"
      }
  ]
