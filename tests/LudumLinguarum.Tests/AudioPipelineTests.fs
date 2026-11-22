module AudioPipelineTests

open AudioPipeline
open Expecto
open FsCheck
open MediaDatabase
open System
open System.IO

/// Generates a tiny valid mono 16-bit PCM WAV file containing a short synthetic sine wave.
let private generateSyntheticWavBytes (sampleRate: int) (durationSeconds: float) : byte array =
    let sampleCount = int (float sampleRate * durationSeconds)
    let dataSize = sampleCount * 2
    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms)
    // RIFF header
    bw.Write(Text.Encoding.ASCII.GetBytes("RIFF"))
    bw.Write(36 + dataSize)
    bw.Write(Text.Encoding.ASCII.GetBytes("WAVE"))
    // fmt chunk
    bw.Write(Text.Encoding.ASCII.GetBytes("fmt "))
    bw.Write(16)
    bw.Write(1s) // PCM
    bw.Write(1s) // mono
    bw.Write(sampleRate)
    bw.Write(sampleRate * 2)
    bw.Write(2s)
    bw.Write(16s)
    // data chunk
    bw.Write(Text.Encoding.ASCII.GetBytes("data"))
    bw.Write(dataSize)
    for i in 0 .. sampleCount - 1 do
        let sample = int16 (12000.0 * Math.Sin(2.0 * Math.PI * 440.0 * float i / float sampleRate))
        bw.Write(sample)
    bw.Flush()
    ms.ToArray()

[<Tests>]
let tests =
    testList "AudioPipeline tests" [
        test "requiresVgmstreamFirst classifies game-specific vs standard audio extensions" {
            Expect.isTrue (requiresVgmstreamFirst ".wem") ".wem should use vgmstream first"
            Expect.isTrue (requiresVgmstreamFirst ".hca") ".hca should use vgmstream first"
            Expect.isTrue (requiresVgmstreamFirst "FSB") "case-insensitive without dot should match"
            Expect.isFalse (requiresVgmstreamFirst ".mp3") ".mp3 should go directly to ffmpeg"
            Expect.isFalse (requiresVgmstreamFirst ".wav") ".wav should try ffmpeg first"
            Expect.isFalse (requiresVgmstreamFirst ".ogg") ".ogg should go directly to ffmpeg"
        }

        test "parseDurationMsFromFfmpegOutput prefers final time= and falls back to Duration:" {
            let withTime = "Duration: 00:00:05.50, start: 0.0\nsize= 24kB time=00:00:03.25 bitrate=64kbits/s"
            Expect.equal (parseDurationMsFromFfmpegOutput withTime) 3250 "should prefer encoded output time="

            let durationOnly = "  Duration: 00:01:02.40, start: 0.000000, bitrate: 128 kb/s"
            Expect.equal (parseDurationMsFromFfmpegOutput durationOnly) 62400 "should parse Duration: fallback"

            Expect.equal (parseDurationMsFromFfmpegOutput "") 0 "empty output should return 0"
        }

        testProperty "parseDurationMsFromFfmpegOutput accurately parses arbitrary valid HH:MM:SS.cc timestamps" <|
            fun (NonNegativeInt hRaw, NonNegativeInt mRaw, NonNegativeInt sRaw, NonNegativeInt csRaw) ->
                let h = hRaw % 10
                let m = mRaw % 60
                let s = sRaw % 60
                let cs = csRaw % 100
                let formatted = sprintf "size= 12kB time=%02d:%02d:%02d.%02d bitrate=64.0kbits/s" h m s cs
                let expectedMs = ((h * 3600 + m * 60 + s) * 1000) + (cs * 10)
                parseDurationMsFromFfmpegOutput formatted = expectedMs

        test "buildAudioFilterChain includes loudnorm and optional silenceremove" {
            let defaultFilter = buildAudioFilterChain AudioIngestOptions.Default
            Expect.isSome defaultFilter "default should include loudnorm"
            Expect.isTrue (defaultFilter.Value.Contains("loudnorm=")) "should contain loudnorm"
            Expect.isFalse (defaultFilter.Value.Contains("silenceremove=")) "should not trim silence by default"

            let trimmed = buildAudioFilterChain { AudioIngestOptions.Default with TrimSilence = true }
            Expect.isTrue (trimmed.Value.Contains("silenceremove=")) "should include silenceremove when enabled"
            Expect.isTrue (trimmed.Value.Contains("loudnorm=")) "should keep loudnorm"

            let disabled = buildAudioFilterChain { AudioIngestOptions.Default with NormalizeLoudness = false; TrimSilence = false }
            Expect.isNone disabled "no filters when both disabled"
        }

        test "tryReadPngDimensions parses valid PNG IHDR header and rejects non-PNG" {
            let pngHeader =
                [|
                    0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0x0Duy; 0x0Auy; 0x1Auy; 0x0Auy
                    0x00uy; 0x00uy; 0x00uy; 0x0Duy // chunk length
                    0x49uy; 0x48uy; 0x44uy; 0x52uy // IHDR
                    0x00uy; 0x00uy; 0x01uy; 0x40uy // width = 320
                    0x00uy; 0x00uy; 0x00uy; 0xF0uy // height = 240
                |]
            Expect.equal (tryReadPngDimensions pngHeader) (Some (320, 240)) "should parse 320x240"
            Expect.equal (tryReadPngDimensions [| 1uy; 2uy; 3uy |]) None "short/invalid payload should return None"
        }

        test "ingestExtractedImageBytes stores image with parsed dimensions" {
            use db = new MediaDatabase(":memory:")
            let pngBytes =
                [|
                    0x89uy; 0x50uy; 0x4Euy; 0x47uy; 0x0Duy; 0x0Auy; 0x1Auy; 0x0Auy
                    0x00uy; 0x00uy; 0x00uy; 0x0Duy
                    0x49uy; 0x48uy; 0x44uy; 0x52uy
                    0x00uy; 0x00uy; 0x00uy; 0x80uy // width = 128
                    0x00uy; 0x00uy; 0x00uy; 0x40uy // height = 64
                |]
            match ingestExtractedImageBytes (db :> IMediaStore) "de" "ui/icon.png" pngBytes ".png" with
            | Error e -> failtest ("unexpected image ingest error: " + e)
            | Ok hash ->
                let meta = (db :> IMediaStore).TryGetMetadata(hash).Value
                Expect.equal meta.Kind MediaKind.Image "should be Image"
                Expect.equal meta.MimeType "image/png" "mime should be image/png"
                Expect.equal meta.Width 128 "width should be 128"
                Expect.equal meta.Height 64 "height should be 64"
        }

        test "transcodeBytesToMp3 encodes synthetic WAV to MP3 when ffmpeg is available" {
            match resolveFfmpeg ExternalToolConfig.Default with
            | None -> () // CI environments without ffmpeg skip external binary check cleanly
            | Some _ ->
                let wav = generateSyntheticWavBytes 16000 0.5
                match transcodeBytesToMp3 ExternalToolConfig.Default AudioIngestOptions.Default wav ".wav" None with
                | Error e -> failtest ("ffmpeg transcode failed: " + e)
                | Ok res ->
                    Expect.isGreaterThan res.Mp3Bytes.Length 100 "MP3 output should be non-trivial"
                    Expect.isGreaterThan res.DurationMs 200 "duration should be parsed as > 200 ms"
        }
    ]
