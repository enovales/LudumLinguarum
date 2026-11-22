module TtsProvidersTests

open AudioPipeline
open Expecto
open FsCheck
open MediaDatabase
open TtsProviders
open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks

type private MockHttpHandler(responder: HttpRequestMessage -> HttpResponseMessage) =
    inherit HttpMessageHandler()
    member val LastRequest: HttpRequestMessage option = None with get, set
    member val LastRequestBody: string = "" with get, set

    override this.Send(request: HttpRequestMessage, _cancellationToken: CancellationToken) =
        this.LastRequest <- Some request
        if not (isNull request.Content) then
            this.LastRequestBody <- request.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        responder request

    override this.SendAsync(request: HttpRequestMessage, cancellationToken: CancellationToken) =
        Task.FromResult(this.Send(request, cancellationToken))

type private CountingStubTtsProvider(providerName: string, voiceName: string, payloadBytes: byte array) =
    let mutable callCount = 0
    member _.CallCount = callCount
    interface ITtsProvider with
        member _.Name = providerName
        member _.ResolveVoice(_) = Some voiceName
        member _.Synthesize(lang, _text) =
            callCount <- callCount + 1
            Ok
                {
                    AudioBytes = payloadBytes
                    MimeType = "audio/mpeg"
                    FileExtension = ".mp3"
                    DurationMs = 1000
                    Provenance =
                        {
                            Provider = providerName
                            Voice = voiceName
                            Model = "stub-v1"
                            LanguageTag = lang
                            GeneratedAtUtc = "2026-11-21T12:00:00Z"
                        }
                }

[<Tests>]
let tests =
    testList "TtsProviders tests" [
        test "normalizeToBcp47Locale maps short and regional language tags" {
            Expect.equal (normalizeToBcp47Locale "de") "de-DE" "German"
            Expect.equal (normalizeToBcp47Locale "en") "en-US" "English"
            Expect.equal (normalizeToBcp47Locale "es-419") "es-MX" "Latin American Spanish"
            Expect.equal (normalizeToBcp47Locale "pt-br") "pt-BR" "Brazilian Portuguese"
            Expect.equal (normalizeToBcp47Locale "de_AT") "de-AT" "Austrian German"
        }

        test "parseLanguageMappingSpec handles single default and per-language maps" {
            let (single, map1) = parseLanguageMappingSpec (Some "de-DE-ConradNeural")
            Expect.equal single (Some "de-DE-ConradNeural") "single voice should be default"
            Expect.isTrue map1.IsEmpty "map should be empty"

            let (def2, map2) = parseLanguageMappingSpec (Some "de=de-DE-ConradNeural;en=en-US-AndrewNeural")
            Expect.isNone def2 "mapped spec should not set single default"
            Expect.equal (map2 |> Map.tryFind "de") (Some "de-DE-ConradNeural") "German voice mapped"
            Expect.equal (map2 |> Map.tryFind "en") (Some "en-US-AndrewNeural") "English voice mapped"
        }

        test "buildAzureSsml escapes XML special characters while preserving Unicode" {
            let ssml = buildAzureSsml "de-DE" "de-DE-KatjaNeural" "Ärger & <Spaß> \"hier\"!"
            Expect.isTrue (ssml.Contains("Ärger &amp; &lt;Spaß&gt; &quot;hier&quot;!")) "XML characters must be escaped"
            Expect.isTrue (ssml.Contains("name='de-DE-KatjaNeural'")) "voice name must be included"
        }

        test "AzureTtsProvider sends expected REST headers, SSML, and records provenance" {
            let fakeMp3 = [| 0x49uy; 0x44uy; 0x33uy; 0xAAuy; 0xBBuy |]
            use handler =
                new MockHttpHandler(fun _ ->
                    let resp = new HttpResponseMessage(HttpStatusCode.OK)
                    resp.Content <- new ByteArrayContent(fakeMp3)
                    resp
                )

            use provider =
                new AzureTtsProvider(
                    {
                        SubscriptionKey = "synthetic-key"
                        Region = "westeurope"
                        VoiceSpec = Some "de=de-DE-ConradNeural"
                        OutputFormat = AzureTtsConfig.DefaultOutputFormat
                    },
                    handler
                )

            match (provider :> ITtsProvider).Synthesize("de", "Hallo Welt!") with
            | Error e -> failtest ("unexpected synthesis error: " + e)
            | Ok res ->
                Expect.equal res.AudioBytes fakeMp3 "should return response audio bytes"
                Expect.equal res.Provenance.Provider "azure" "provider should be azure"
                Expect.equal res.Provenance.Voice "de-DE-ConradNeural" "voice override should be used"
                Expect.equal res.Provenance.Model AzureTtsConfig.DefaultOutputFormat "model should capture output format"

                let req = handler.LastRequest.Value
                Expect.equal (req.RequestUri.ToString()) "https://westeurope.tts.speech.microsoft.com/cognitiveservices/v1" "endpoint URI"
                Expect.isTrue (req.Headers.Contains("Ocp-Apim-Subscription-Key")) "subscription header present"
                Expect.isTrue (handler.LastRequestBody.Contains("Hallo Welt!")) "SSML body should contain text"
        }

        test "parseCommandInvocation handles executable paths and Python module invocations" {
            Expect.equal (parseCommandInvocation "py -3.14 -m piper") (Some ("py", [ "-3.14"; "-m"; "piper" ])) "python module invocation"
            Expect.equal (parseCommandInvocation "\"C:\\Program Files\\piper\\piper.exe\" --cuda") (Some ("C:\\Program Files\\piper\\piper.exe", [ "--cuda" ])) "quoted path with spaces"
        }

        test "tryExtractPythonPiperCommand detects Python module invocations and inline --cuda" {
            Expect.equal (tryExtractPythonPiperCommand "py" [ "-3.14"; "-m"; "piper" ]) (Some ("py", [ "-3.14" ], false)) "py -3.14 -m piper"
            Expect.equal (tryExtractPythonPiperCommand "py" [ "-3.14"; "-m"; "piper"; "--cuda" ]) (Some ("py", [ "-3.14" ], true)) "py -3.14 -m piper --cuda"
            Expect.equal (tryExtractPythonPiperCommand "python" [ "-m"; "piper" ]) (Some ("python", [], false)) "python -m piper"
            Expect.equal (tryExtractPythonPiperCommand "piper.exe" [ "--cuda" ]) None "standalone piper binary is not python module"
        }

        test "tryFindPiperModelInDirectory matches exact locale before base language" {
            let dir = Path.Combine(Path.GetTempPath(), "ll-piper-models-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(dir) |> ignore
            try
                let deDeLow = Path.Combine(dir, "de_DE-alpha-x_low.onnx")
                let deDeHigh = Path.Combine(dir, "de_DE-thorsten-high.onnx")
                let deAt = Path.Combine(dir, "de_AT-sample-medium.onnx")
                let enUs = Path.Combine(dir, "en_US-lessac-medium.onnx")
                File.WriteAllBytes(deDeLow, [| 0uy |])
                File.WriteAllBytes(deDeHigh, [| 0uy |])
                File.WriteAllBytes(deAt, [| 0uy |])
                File.WriteAllBytes(enUs, [| 0uy |])

                Expect.equal (tryFindPiperModelInDirectory dir "de") (Some deDeHigh) "should prefer high quality de_DE model over x_low"
                Expect.equal (tryFindPiperModelInDirectory dir "de-AT") (Some deAt) "should match de_AT for de-AT"
                Expect.equal (tryFindPiperModelInDirectory dir "en") (Some enUs) "should match en_US for en"
                Expect.equal (tryFindPiperModelInDirectory dir "ja") None "missing language returns None"
            finally
                Directory.Delete(dir, true)
        }

        test "resolveCardAudio prefers native Extracted audio over TTS and respects caching + regeneration" {
            use db = new MediaDatabase(":memory:")
            let store = db :> IMediaStore

            // 1. Native extracted audio must win over TTS without invoking provider
            let nativeBytes = [| 1uy; 2uy; 3uy; 4uy |]
            let nativeHash =
                store.PutMedia
                    {
                        Hash = ""
                        Kind = MediaKind.Audio
                        MimeType = "audio/mpeg"
                        FileExtension = ".mp3"
                        LanguageTag = "de"
                        Source = MediaSource.Extracted
                        SourceDescription = "game/audio.mp3"
                        TtsProvider = ""
                        TtsVoice = ""
                        TtsModel = ""
                        GeneratedAtUtc = "2026-11-21T00:00:00Z"
                        DurationMs = 1000
                        Width = 0
                        Height = 0
                        ByteLength = nativeBytes.Length
                        Data = nativeBytes
                    }

            let stub1 = CountingStubTtsProvider("piper", "v1", [| 9uy; 9uy; 9uy |])
            let resNative = resolveCardAudio nativeHash "de" "Hallo" (Some store) (Some (stub1 :> ITtsProvider)) true true
            Expect.isOk resNative "should succeed"
            Expect.equal (Result.defaultValue None resNative).Value.Data nativeBytes "native audio must win"
            Expect.equal stub1.CallCount 0 "TTS must not be called when native audio exists"

            // 2. Missing native audio uses TTS and caches it
            let resTts1 = resolveCardAudio "" "de" "Guten Tag" (Some store) (Some (stub1 :> ITtsProvider)) true false
            Expect.isOk resTts1 "first TTS synthesis should succeed"
            Expect.equal stub1.CallCount 1 "TTS should be called once"

            // 3. Second export hits cache without invoking TTS again
            let resTts2 = resolveCardAudio "" "de" "Guten Tag" (Some store) (Some (stub1 :> ITtsProvider)) true false
            Expect.isOk resTts2 "cached lookup should succeed"
            Expect.equal stub1.CallCount 1 "cache hit should not call TTS again"

            // 4. regenerateTts = true invokes new provider and updates stored provenance
            let stub2 = CountingStubTtsProvider("azure", "de-DE-KatjaNeural", [| 8uy; 8uy; 8uy; 8uy |])
            let resTts3 = resolveCardAudio "" "de" "Guten Tag" (Some store) (Some (stub2 :> ITtsProvider)) true true
            Expect.isOk resTts3 "regeneration should succeed"
            Expect.equal stub2.CallCount 1 "new provider should be called"
            let (cachedMedia, cachedProv) = store.TryGetTts("de", "Guten Tag").Value
            Expect.equal cachedMedia.Data [| 8uy; 8uy; 8uy; 8uy |] "cached audio should be updated"
            Expect.equal cachedProv.Provider "azure" "cached provenance should be updated to azure"
            Expect.equal cachedProv.Voice "de-DE-KatjaNeural" "cached voice should be updated"
        }

        testProperty "buildAzureSsml always produces well-formed XML for arbitrary non-null text without control chars" <|
            fun (NonEmptyString raw) ->
                let cleaned =
                    raw.ToCharArray()
                    |> Array.filter (fun c -> c >= ' ' || c = '\t' || c = '\n' || c = '\r')
                    |> fun chars -> new string(chars)
                let ssml = buildAzureSsml "de-DE" "de-DE-KatjaNeural" cleaned
                try
                    System.Xml.Linq.XElement.Parse(ssml) |> ignore
                    true
                with _ ->
                    false
    ]
