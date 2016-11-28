module SonicAdventureDXTests

open NUnit.Framework
open SonicAdventureDX
open System.IO
open System.Text

[<Test>]
let ``Calling readOffsetsFromSimpleBin with an empty offset block returns an empty array``() = 
    let testData = [| 0xffuy; 0xffuy; 0xffuy; 0xffuy |]
    let reader = new BinaryReader(new MemoryStream(testData))
    Assert.AreEqual([||], SonicAdventureDX.readOffsetsFromSimpleBin(reader))

[<Test>]
let ``Calling readOffsetsFromSimpleBin with an offset block returns the correct offsets``() = 
    let testData = [| 0x00uy; 0x00uy; 0x00uy; 0x09uy; 0xffuy; 0xffuy; 0xffuy; 0xffuy |]
    let reader = new BinaryReader(new MemoryStream(testData))
    Assert.AreEqual([| 9u |], SonicAdventureDX.readOffsetsFromSimpleBin(reader))

[<Test>]
let ``Calling calculateStringLengthsForSimpleBin with no offsets returns an empty array``() = 
    Assert.AreEqual([||], SonicAdventureDX.calculateStringLengthsForSimpleBin([||], 0u))

[<Test>]
let ``Calling calculateStringLengthsForSimpleBin with offsets returns the correct values``() =
    let testOffsets = [| 1u; 10u; 15u |]
    let streamLength = 25u;
    let expected = [| 9u; 5u; 10u |]
    Assert.AreEqual(expected, SonicAdventureDX.calculateStringLengthsForSimpleBin(testOffsets, streamLength))

[<Test>]
let ``Calling readStringsFromSimpleBin with a really simple stream returns expected strings``() = 
    let streamBytes = 
        [|
            0uy; 0uy; 0uy; 8uy; 0xFFuy; 0xFFuy; 0xFFuy; 0xFFuy; 65uy
        |]

    let stream = new MemoryStream(streamBytes)
    Assert.AreEqual([| "A" |], SonicAdventureDX.readStringsFromSimpleBin(stream, Encoding.ASCII))
    