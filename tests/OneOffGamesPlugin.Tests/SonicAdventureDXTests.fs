module SonicAdventureDXTests

open Expecto
open SonicAdventureDX
open System.IO
open System.Text

[<Tests>]
let tests = 
  testList "Sonic Adventure DX tests" [
    testCase "Calling readOffsetsFromSimpleBin with an empty offset block returns an empty array" <|
      fun () ->
        let testData = [| 0xffuy; 0xffuy; 0xffuy; 0xffuy |]
        let reader = new BinaryReader(new MemoryStream(testData))
        Expect.equal [||] (SonicAdventureDX.readOffsetsFromSimpleBin reader) ""

    testCase "Calling readOffsetsFromSimpleBin with an offset block returns the correct offsets" <|
      fun () ->
        let testData = [| 0x00uy; 0x00uy; 0x00uy; 0x09uy; 0xffuy; 0xffuy; 0xffuy; 0xffuy |]
        let reader = new BinaryReader(new MemoryStream(testData))
        Expect.equal [| 9u |] (SonicAdventureDX.readOffsetsFromSimpleBin reader) ""

    testCase "Calling calculateStringLengthsForSimpleBin with no offsets returns an empty array" <|
      fun () -> Expect.equal [||] (SonicAdventureDX.calculateStringLengthsForSimpleBin([||], 0u)) ""

    testCase "Calling calculateStringLengthsForSimpleBin with offsets returns the correct values" <|
      fun () -> 
        let testOffsets = [| 1u; 10u; 15u |]
        let streamLength = 25u;
        let expected = [| 9u; 5u; 10u |]
        Expect.equal expected (SonicAdventureDX.calculateStringLengthsForSimpleBin(testOffsets, streamLength)) ""

    testCase "Calling readStringsFromSimpleBin with a really simple stream returns expected strings" <|
      fun () ->
        let streamBytes = 
            [|
                0uy; 0uy; 0uy; 8uy; 0xFFuy; 0xFFuy; 0xFFuy; 0xFFuy; 65uy
            |]

        let stream = new MemoryStream(streamBytes)
        Expect.equal [| "A" |] (SonicAdventureDX.readStringsFromSimpleBin(stream, Encoding.ASCII)) ""
  ]
