module TtsProviders

open AudioPipeline
open MediaDatabase
open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Security
open System.Text
open System.Text.Json

type TtsSynthesisResult =
    {
        AudioBytes: byte array
        MimeType: string
        FileExtension: string
        DurationMs: int
        Provenance: TtsProvenance
    }

[<Interface>]
type ITtsProvider =
    abstract member Name: string
    abstract member ResolveVoice: languageTag: string -> string option
    abstract member Synthesize: languageTag: string * text: string -> Result<TtsSynthesisResult, string>

let private defaultLocaleMap =
    [|
        ("ar", "ar-SA")
        ("bg", "bg-BG")
        ("ca", "ca-ES")
        ("cs", "cs-CZ")
        ("cy", "cy-GB")
        ("da", "da-DK")
        ("de", "de-DE")
        ("el", "el-GR")
        ("en", "en-US")
        ("en-gb", "en-GB")
        ("en-us", "en-US")
        ("es", "es-ES")
        ("es-419", "es-MX")
        ("es-es", "es-ES")
        ("es-la", "es-MX")
        ("es-mx", "es-MX")
        ("fi", "fi-FI")
        ("fr", "fr-FR")
        ("fr-ca", "fr-CA")
        ("gl", "gl-ES")
        ("hu", "hu-HU")
        ("id", "id-ID")
        ("it", "it-IT")
        ("ja", "ja-JP")
        ("jp", "ja-JP")
        ("ko", "ko-KR")
        ("kr", "ko-KR")
        ("nl", "nl-NL")
        ("no", "nb-NO")
        ("pl", "pl-PL")
        ("pt", "pt-PT")
        ("pt-br", "pt-BR")
        ("pt-pt", "pt-PT")
        ("ro", "ro-RO")
        ("ru", "ru-RU")
        ("sk", "sk-SK")
        ("sv", "sv-SE")
        ("th", "th-TH")
        ("tr", "tr-TR")
        ("uk", "uk-UA")
        ("zh", "zh-CN")
        ("zh-cn", "zh-CN")
        ("zh-tw", "zh-TW")
    |]
    |> Map.ofArray

/// <summary>
/// Normalizes a short or regional LudumLinguarum language tag to a full BCP-47 locale (e.g. "de" -> "de-DE").
/// </summary>
let normalizeToBcp47Locale (languageTag: string) : string =
    if String.IsNullOrWhiteSpace(languageTag) then
        "en-US"
    else
        let cleaned = languageTag.Trim().Replace('_', '-')
        let lower = cleaned.ToLowerInvariant()
        match defaultLocaleMap |> Map.tryFind lower with
        | Some locale -> locale
        | None ->
            let parts = cleaned.Split('-')
            if parts.Length = 2 then
                parts.[0].ToLowerInvariant() + "-" + parts.[1].ToUpperInvariant()
            else
                match defaultLocaleMap |> Map.tryFind (parts.[0].ToLowerInvariant()) with
                | Some locale -> locale
                | None -> cleaned

let private defaultAzureVoiceByLocale =
    [|
        ("ar-SA", "ar-SA-ZariyahNeural")
        ("bg-BG", "bg-BG-KalinaNeural")
        ("ca-ES", "ca-ES-JoanaNeural")
        ("cs-CZ", "cs-CZ-VlastaNeural")
        ("cy-GB", "cy-GB-NiaNeural")
        ("da-DK", "da-DK-ChristelNeural")
        ("de-DE", "de-DE-KatjaNeural")
        ("de-AT", "de-AT-IngridNeural")
        ("de-CH", "de-CH-LeniNeural")
        ("el-GR", "el-GR-AthinaNeural")
        ("en-US", "en-US-AvaNeural")
        ("en-GB", "en-GB-SoniaNeural")
        ("es-ES", "es-ES-ElviraNeural")
        ("es-MX", "es-MX-DaliaNeural")
        ("fi-FI", "fi-FI-NooraNeural")
        ("fr-FR", "fr-FR-DeniseNeural")
        ("fr-CA", "fr-CA-SylvieNeural")
        ("hu-HU", "hu-HU-NoemiNeural")
        ("id-ID", "id-ID-GadisNeural")
        ("it-IT", "it-IT-ElsaNeural")
        ("ja-JP", "ja-JP-NanamiNeural")
        ("ko-KR", "ko-KR-SunHiNeural")
        ("nb-NO", "nb-NO-PernilleNeural")
        ("nl-NL", "nl-NL-ColetteNeural")
        ("pl-PL", "pl-PL-AgnieszkaNeural")
        ("pt-BR", "pt-BR-FranciscaNeural")
        ("pt-PT", "pt-PT-RaquelNeural")
        ("ro-RO", "ro-RO-AlinaNeural")
        ("ru-RU", "ru-RU-SvetlanaNeural")
        ("sk-SK", "sk-SK-ViktoriaNeural")
        ("sv-SE", "sv-SE-SofieNeural")
        ("th-TH", "th-TH-PremwadeeNeural")
        ("tr-TR", "tr-TR-EmelNeural")
        ("uk-UA", "uk-UA-PolinaNeural")
        ("zh-CN", "zh-CN-XiaoxiaoNeural")
        ("zh-TW", "zh-TW-HsiaoChenNeural")
    |]
    |> Map.ofArray

/// <summary>
/// Parses a voice or model specification string supporting either a single default value
/// (e.g. "de-DE-ConradNeural") or per-language mappings (e.g. "de=de-DE-ConradNeural;en=en-US-AndrewNeural").
/// </summary>
let parseLanguageMappingSpec (specOpt: string option) : (string option * Map<string, string>) =
    match specOpt with
    | None -> (None, Map.empty)
    | Some raw when String.IsNullOrWhiteSpace(raw) -> (None, Map.empty)
    | Some raw ->
        let trimmed = raw.Trim()
        if not (trimmed.Contains("=")) then
            (Some trimmed, Map.empty)
        else
            let pairs =
                trimmed.Split([| ';'; ',' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.choose (fun entry ->
                    let idx = entry.IndexOf('=')
                    if idx > 0 && idx < entry.Length - 1 then
                        let lang = entry.Substring(0, idx).Trim().ToLowerInvariant()
                        let value = entry.Substring(idx + 1).Trim()
                        if String.IsNullOrWhiteSpace(lang) || String.IsNullOrWhiteSpace(value) then None
                        else Some (lang, value)
                    else
                        None
                )
                |> Map.ofArray
            (None, pairs)

let private lookupLanguageMapping (defaultValue: string option) (langMap: Map<string, string>) (languageTag: string) : string option =
    let normalized = languageTag.Trim().Replace('_', '-').ToLowerInvariant()
    let localeLower = (normalizeToBcp47Locale languageTag).ToLowerInvariant()
    let baseLang = normalized.Split('-').[0]
    match langMap |> Map.tryFind normalized with
    | Some v -> Some v
    | None ->
        match langMap |> Map.tryFind localeLower with
        | Some v -> Some v
        | None ->
            match langMap |> Map.tryFind baseLang with
            | Some v -> Some v
            | None -> defaultValue

/// <summary>
/// Builds a valid SSML document for Azure AI Speech TTS REST synthesis.
/// </summary>
let buildAzureSsml (locale: string) (voiceName: string) (text: string) : string =
    let escapedText = SecurityElement.Escape(text)
    let escapedLocale = SecurityElement.Escape(locale)
    let escapedVoice = SecurityElement.Escape(voiceName)
    sprintf "<speak version='1.0' xml:lang='%s'><voice xml:lang='%s' name='%s'>%s</voice></speak>" escapedLocale escapedLocale escapedVoice escapedText

type AzureTtsConfig =
    {
        SubscriptionKey: string
        Region: string
        VoiceSpec: string option
        OutputFormat: string
    }
    static member DefaultOutputFormat = "audio-24khz-48kbitrate-mono-mp3"

/// <summary>
/// Azure AI Speech TTS provider using the lightweight REST API (no native SDK dependencies).
/// Returns MP3 directly in the requested output format (default: audio-24khz-48kbitrate-mono-mp3).
/// </summary>
type AzureTtsProvider(config: AzureTtsConfig, ?httpMessageHandler: HttpMessageHandler) =
    let (defaultVoiceOverride, voiceMap) = parseLanguageMappingSpec config.VoiceSpec
    let outputFormat =
        if String.IsNullOrWhiteSpace(config.OutputFormat) then
            AzureTtsConfig.DefaultOutputFormat
        else
            config.OutputFormat.Trim()

    let httpClient =
        match httpMessageHandler with
        | Some handler -> new HttpClient(handler, disposeHandler = false)
        | None -> new HttpClient()

    let resolveVoiceForLanguage (languageTag: string) =
        match lookupLanguageMapping defaultVoiceOverride voiceMap languageTag with
        | Some v -> Some v
        | None ->
            let locale = normalizeToBcp47Locale languageTag
            defaultAzureVoiceByLocale |> Map.tryFind locale

    interface ITtsProvider with
        member _.Name = "azure"

        member _.ResolveVoice(languageTag: string) =
            resolveVoiceForLanguage languageTag

        member _.Synthesize(languageTag: string, text: string) =
            if String.IsNullOrWhiteSpace(config.SubscriptionKey) then
                Error "Azure Speech subscription key is missing. Set AZURE_SPEECH_KEY or pass --azure-speech-key=<key>."
            elif String.IsNullOrWhiteSpace(config.Region) then
                Error "Azure Speech region is missing. Set AZURE_SPEECH_REGION or pass --azure-speech-region=<region>."
            elif String.IsNullOrWhiteSpace(text) then
                Error "Cannot synthesize empty text."
            else
                match resolveVoiceForLanguage languageTag with
                | None ->
                    Error (sprintf "No default Azure TTS voice is mapped for language '%s'. Specify one with --tts-voice=\"%s=<VoiceName>\"." languageTag languageTag)
                | Some voiceName ->
                    try
                        let locale = normalizeToBcp47Locale languageTag
                        let ssml = buildAzureSsml locale voiceName text
                        let endpoint = sprintf "https://%s.tts.speech.microsoft.com/cognitiveservices/v1" (config.Region.Trim())

                        use req = new HttpRequestMessage(HttpMethod.Post, endpoint)
                        req.Headers.Add("Ocp-Apim-Subscription-Key", config.SubscriptionKey.Trim())
                        req.Headers.Add("X-Microsoft-OutputFormat", outputFormat)
                        req.Headers.Add("User-Agent", "LudumLinguarum")
                        req.Content <- new StringContent(ssml, Encoding.UTF8, "application/ssml+xml")

                        use resp = httpClient.Send(req)
                        let bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()

                        if not resp.IsSuccessStatusCode then
                            let bodyText =
                                try Encoding.UTF8.GetString(bytes)
                                with _ -> ""
                            Error (sprintf "Azure TTS request failed (%d %s): %s" (int resp.StatusCode) (resp.ReasonPhrase) bodyText)
                        elif bytes.Length = 0 then
                            Error "Azure TTS returned an empty audio payload."
                        else
                            let provenance =
                                {
                                    Provider = "azure"
                                    Voice = voiceName
                                    Model = outputFormat
                                    LanguageTag = languageTag
                                    GeneratedAtUtc = DateTime.UtcNow.ToString("O")
                                }
                            Ok
                                {
                                    AudioBytes = bytes
                                    MimeType = "audio/mpeg"
                                    FileExtension = ".mp3"
                                    DurationMs = 0
                                    Provenance = provenance
                                }
                    with ex ->
                        Error (sprintf "Azure TTS request failed: %s" ex.Message)

    interface IDisposable with
        member _.Dispose() = httpClient.Dispose()

type PiperTtsConfig =
    {
        /// Optional command to invoke Piper: either a direct executable path (e.g. "piper.exe")
        /// or a command with prefix arguments (e.g. "py -3.14 -m piper"). If None, auto-discovers.
        PiperCommand: string option
        /// Optional model path or per-language mapping (e.g. "de=C:\models\de_DE-thorsten-high.onnx;en=C:\models\en_US-lessac-medium.onnx").
        ModelSpec: string option
        /// Optional directory containing downloaded Piper .onnx voice models for automatic language matching.
        ModelsDirectory: string option
        /// When true, instructs Piper / ONNX Runtime to run inference on CUDA (requires onnxruntime-gpu).
        UseCuda: bool
        /// External audio tool config (ffmpeg) used to normalize and encode Piper's WAV output to MP3.
        AudioTools: ExternalToolConfig
        AudioOptions: AudioIngestOptions
    }

/// <summary>
/// Splits a command invocation string (supporting quoted paths with spaces) into (executable, prefixArgs).
/// </summary>
let parseCommandInvocation (command: string) : (string * string list) option =
    if String.IsNullOrWhiteSpace(command) then
        None
    else
        let trimmed = command.Trim()
        // If the entire string points to an existing executable file (e.g. path containing spaces), don't split it.
        if File.Exists(trimmed) then
            Some (trimmed, [])
        else
            let tokens = ResizeArray<string>()
            let current = StringBuilder()
            let mutable inQuotes = false
            for ch in trimmed do
                if ch = '"' then
                    inQuotes <- not inQuotes
                elif Char.IsWhiteSpace(ch) && not inQuotes then
                    if current.Length > 0 then
                        tokens.Add(current.ToString())
                        current.Clear() |> ignore
                else
                    current.Append(ch) |> ignore
            if current.Length > 0 then
                tokens.Add(current.ToString())

            if tokens.Count = 0 then None
            else Some (tokens.[0], tokens |> Seq.skip 1 |> List.ofSeq)

let private piperModelQualityRank (path: string) : int =
    let lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant()
    if lower.EndsWith("-high", StringComparison.Ordinal) || lower.EndsWith("_high", StringComparison.Ordinal) then 0
    elif lower.EndsWith("-medium", StringComparison.Ordinal) || lower.EndsWith("_medium", StringComparison.Ordinal) then 1
    elif lower.EndsWith("-x_low", StringComparison.Ordinal) || lower.EndsWith("_x_low", StringComparison.Ordinal) then 3
    elif lower.EndsWith("-low", StringComparison.Ordinal) || lower.EndsWith("_low", StringComparison.Ordinal) then 2
    else 4

/// <summary>
/// Auto-discovers a matching Piper .onnx model in a directory for a given language tag
/// (prefers exact locale match like "de_DE-*.onnx", then base language match like "de_*.onnx",
/// and within matches prefers higher quality tiers: high > medium > low > x_low).
/// </summary>
let tryFindPiperModelInDirectory (modelsDir: string) (languageTag: string) : string option =
    if String.IsNullOrWhiteSpace(modelsDir) || not (Directory.Exists(modelsDir)) then
        None
    else
        let onnxFiles =
            Directory.GetFiles(modelsDir, "*.onnx", SearchOption.AllDirectories)
            |> Array.sortBy (fun p -> (piperModelQualityRank p, Path.GetFileName(p).ToLowerInvariant()))

        if onnxFiles.Length = 0 then
            None
        else
            let localeUnderscore = (normalizeToBcp47Locale languageTag).Replace('-', '_').ToLowerInvariant()
            let baseLang = languageTag.Trim().Replace('_', '-').Split('-').[0].ToLowerInvariant()

            let exactLocaleMatch =
                onnxFiles
                |> Array.tryFind (fun p ->
                    let name = Path.GetFileName(p).ToLowerInvariant()
                    name.StartsWith(localeUnderscore + "-", StringComparison.Ordinal)
                    || name.StartsWith(localeUnderscore + "_", StringComparison.Ordinal)
                )

            match exactLocaleMatch with
            | Some m -> Some m
            | None ->
                onnxFiles
                |> Array.tryFind (fun p ->
                    let name = Path.GetFileName(p).ToLowerInvariant()
                    name.StartsWith(baseLang + "_", StringComparison.Ordinal)
                    || name.StartsWith(baseLang + "-", StringComparison.Ordinal)
                )

let private tryProbeCommand (exe: string) (prefixArgs: string list) : bool =
    try
        let psi = ProcessStartInfo()
        psi.FileName <- exe
        for a in prefixArgs do psi.ArgumentList.Add(a)
        psi.ArgumentList.Add("--help")
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        use proc = new Process()
        proc.StartInfo <- psi
        if not (proc.Start()) then false
        else
            if not (proc.WaitForExit(5000)) then
                try proc.Kill(true) with _ -> ()
                false
            else
                proc.ExitCode = 0
    with _ ->
        false

let resolvePiperInvocation (explicitCommand: string option) : (string * string list) option =
    match explicitCommand |> Option.bind parseCommandInvocation with
    | Some (exe, args) -> Some (exe, args)
    | None ->
        match resolveExecutable None [ "piper" ] with
        | Some exe -> Some (exe, [])
        | None ->
            // Fall back to Python module invocation (e.g. `py -3.14 -m piper` or `python -m piper`).
            let candidates =
                [
                    ("py", [ "-3.14"; "-m"; "piper" ])
                    ("py", [ "-m"; "piper" ])
                    ("python", [ "-m"; "piper" ])
                    ("python3", [ "-m"; "piper" ])
                ]
            candidates |> List.tryFind (fun (exe, args) -> tryProbeCommand exe args)

let piperInstallHint =
    "Piper TTS was not found. Install 'piper-tts' (e.g. 'py -3.14 -m pip install piper-tts') or download the Piper CLI binary, and pass --piper-command=\"py -3.14 -m piper\" if it is not on PATH."

/// <summary>
/// Inspects a parsed Piper command invocation to determine whether it invokes the Python
/// `piper` module (e.g. `py -3.14 -m piper [--cuda]`). If so, returns the Python executable,
/// Python interpreter prefix args (e.g. `["-3.14"]`), and whether `--cuda` was included in the command.
/// </summary>
let tryExtractPythonPiperCommand (exe: string) (prefixArgs: string list) : (string * string list * bool) option =
    let rec findModulePiper (before: string list) (rest: string list) =
        match rest with
        | "-m" :: "piper" :: tail ->
            let hasCudaInArgs =
                tail |> List.exists (fun a -> String.Equals(a, "--cuda", StringComparison.OrdinalIgnoreCase))
            Some (exe, List.rev before, hasCudaInArgs)
        | head :: tail -> findModulePiper (head :: before) tail
        | [] -> None
    findModulePiper [] prefixArgs

let private persistentPiperWorkerScript = """import glob
import json
import os
import sys
import wave

model_path = sys.argv[1]
use_cuda = sys.argv[2].lower() == "true"

if use_cuda and os.name == "nt":
    candidate_dirs = []
    cuda_env = os.environ.get("CUDA_PATH")
    if cuda_env:
        candidate_dirs.extend([os.path.join(cuda_env, "bin", "x64"), os.path.join(cuda_env, "bin")])
    toolkit_root = r"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA"
    if os.path.isdir(toolkit_root):
        for ver_dir in sorted(glob.glob(os.path.join(toolkit_root, "v*")), reverse=True):
            candidate_dirs.extend([os.path.join(ver_dir, "bin", "x64"), os.path.join(ver_dir, "bin")])
    cudnn_root = r"C:\Program Files\NVIDIA\CUDNN"
    if os.path.isdir(cudnn_root):
        for bin_dir in sorted(glob.glob(os.path.join(cudnn_root, "**", "bin", "*"), recursive=True), reverse=True):
            if os.path.isdir(bin_dir):
                candidate_dirs.append(bin_dir)
    import site
    site_dirs = list(site.getsitepackages())
    try:
        user_site = site.getusersitepackages()
        if user_site:
            site_dirs.append(user_site)
    except Exception:
        pass
    for sp in site_dirs:
        nvidia_root = os.path.join(sp, "nvidia")
        if os.path.isdir(nvidia_root):
            for bin_dir in glob.glob(os.path.join(nvidia_root, "*", "bin")):
                if os.path.isdir(bin_dir):
                    candidate_dirs.append(bin_dir)
    added = []
    for d in candidate_dirs:
        if os.path.isdir(d) and d not in added:
            try:
                os.add_dll_directory(d)
                added.append(d)
            except OSError:
                pass
    if added:
        os.environ["PATH"] = os.pathsep.join(added) + os.pathsep + os.environ.get("PATH", "")

if use_cuda:
    try:
        import onnxruntime as ort
        if hasattr(ort, "preload_dlls"):
            ort.preload_dlls()
    except Exception:
        pass

from piper.voice import PiperVoice

voice = PiperVoice.load(model_path, use_cuda=use_cuda)
active_providers = []
try:
    active_providers = list(voice.session.get_providers())
except Exception:
    pass
print("READY:" + json.dumps({"providers": active_providers}), flush=True)

for raw_line in sys.stdin:
    line = raw_line.strip()
    if not line:
        continue
    req = json.loads(line)
    if req.get("cmd") == "quit":
        break
    try:
        out_path = req["out"]
        text = req["text"]
        if hasattr(voice, "synthesize_wav"):
            chunks = list(voice.synthesize(text))
            if not chunks:
                print(json.dumps({"ok": False, "error": "Piper produced no audio for text."}), flush=True)
                continue
            with wave.open(out_path, "wb") as wf:
                first = chunks[0]
                wf.setframerate(first.sample_rate)
                wf.setsampwidth(first.sample_width)
                wf.setnchannels(first.sample_channels)
                for chunk in chunks:
                    wf.writeframes(chunk.audio_int16_bytes)
        else:
            with wave.open(out_path, "wb") as wf:
                wf.setnchannels(1)
                wf.setsampwidth(2)
                wf.setframerate(voice.config.sample_rate)
                voice.synthesize(text, wf)
        print(json.dumps({"ok": True}), flush=True)
    except Exception as ex:
        print(json.dumps({"ok": False, "error": str(ex)}), flush=True)
"""

/// <summary>
/// Long-lived Python PiperVoice worker process that loads an ONNX model once and serves
/// synthesis requests over UTF-8 JSON lines, avoiding per-card Python/ONNX startup overhead.
/// </summary>
type private PersistentPiperWorker(pythonExe: string, pythonPrefixArgs: string list, modelPath: string, useCuda: bool) =
    let scriptPath = Path.Combine(Path.GetTempPath(), "ll-piper-worker-" + Guid.NewGuid().ToString("N") + ".py")
    let stderrBuffer = StringBuilder()
    let stderrLock = obj()
    let mutable disposed = false

    let proc =
        File.WriteAllText(scriptPath, persistentPiperWorkerScript, UTF8Encoding(false))
        let psi = ProcessStartInfo()
        psi.FileName <- pythonExe
        for a in pythonPrefixArgs do psi.ArgumentList.Add(a)
        psi.ArgumentList.Add("-u")
        psi.ArgumentList.Add(scriptPath)
        psi.ArgumentList.Add(modelPath)
        psi.ArgumentList.Add(if useCuda then "true" else "false")
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.StandardInputEncoding <- UTF8Encoding(false)
        psi.StandardOutputEncoding <- UTF8Encoding(false)
        psi.StandardErrorEncoding <- UTF8Encoding(false)
        psi.Environment.["PYTHONIOENCODING"] <- "utf-8"

        let p = new Process()
        p.StartInfo <- psi
        p.ErrorDataReceived.Add(fun ev ->
            if not (isNull ev.Data) then
                lock stderrLock (fun () -> stderrBuffer.AppendLine(ev.Data) |> ignore)
        )
        if not (p.Start()) then
            p.Dispose()
            try File.Delete(scriptPath) with _ -> ()
            failwith ("Failed to start persistent Piper worker: " + pythonExe)
        p.BeginErrorReadLine()
        p

    let getStderr () =
        lock stderrLock (fun () -> stderrBuffer.ToString().Trim())

    let waitUntilReady () =
        let rec readLoop () =
            let line = proc.StandardOutput.ReadLine()
            if isNull line then
                Error (sprintf "Persistent Piper worker exited before becoming ready: %s" (getStderr ()))
            else
                let trimmed = line.Trim()
                if trimmed = "READY" then
                    Ok [||]
                elif trimmed.StartsWith("READY:", StringComparison.Ordinal) then
                    let jsonPart = trimmed.Substring("READY:".Length)
                    try
                        use doc = JsonDocument.Parse(jsonPart)
                        let mutable prop = Unchecked.defaultof<JsonElement>
                        if doc.RootElement.TryGetProperty("providers", &prop) && prop.ValueKind = JsonValueKind.Array then
                            let providers =
                                [| for el in prop.EnumerateArray() -> el.GetString() |]
                            Ok providers
                        else
                            Ok [||]
                    with _ ->
                        Ok [||]
                else
                    readLoop ()
        readLoop ()

    let readyResult = waitUntilReady ()

    do
        match readyResult with
        | Ok providers when useCuda && providers.Length > 0 && not (providers |> Array.exists (fun p -> p.Contains("CUDA", StringComparison.OrdinalIgnoreCase))) ->
            Console.Error.WriteLine(
                sprintf "Warning: --piper-cuda was requested for '%s', but ONNX Runtime fell back to [%s]. %s"
                    (Path.GetFileName(modelPath))
                    (String.Join(", ", providers))
                    (getStderr ())
            )
        | Ok providers when providers.Length > 0 ->
            Console.Out.WriteLine(
                sprintf "Piper worker ready for '%s' (providers: %s)"
                    (Path.GetFileName(modelPath))
                    (String.Join(", ", providers))
            )
        | _ -> ()

    member _.IsReady =
        match readyResult with
        | Ok _ -> not disposed && not proc.HasExited
        | Error _ -> false

    member _.StartupError =
        match readyResult with
        | Ok _ -> None
        | Error e -> Some e

    member _.SynthesizeToWav(text: string, outputWavPath: string) : Result<unit, string> =
        match readyResult with
        | Error e -> Error e
        | Ok _ ->
            if disposed || proc.HasExited then
                Error ("Persistent Piper worker is no longer running: " + getStderr ())
            else
                try
                    let reqJson = JsonSerializer.Serialize(dict [ "text", box text; "out", box outputWavPath ])
                    proc.StandardInput.WriteLine(reqJson)
                    proc.StandardInput.Flush()

                    let respLine = proc.StandardOutput.ReadLine()
                    if isNull respLine then
                        Error ("Persistent Piper worker closed unexpectedly: " + getStderr ())
                    else
                        use doc = JsonDocument.Parse(respLine)
                        let root = doc.RootElement
                        let mutable okProp = Unchecked.defaultof<JsonElement>
                        if root.TryGetProperty("ok", &okProp) && okProp.GetBoolean() then
                            if File.Exists(outputWavPath) && (FileInfo(outputWavPath).Length > 0L) then
                                Ok ()
                            else
                                Error "Persistent Piper worker succeeded without producing a WAV file."
                        else
                            let mutable errProp = Unchecked.defaultof<JsonElement>
                            let errMsg =
                                if root.TryGetProperty("error", &errProp) then errProp.GetString()
                                else "Unknown Piper worker error"
                            Error ("Piper synthesis error: " + errMsg)
                with ex ->
                    Error ("Failed communicating with persistent Piper worker: " + ex.Message)

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true
                try
                    if not proc.HasExited then
                        try
                            proc.StandardInput.WriteLine("{\"cmd\":\"quit\"}")
                            proc.StandardInput.Flush()
                            proc.StandardInput.Close()
                        with _ -> ()
                        if not (proc.WaitForExit(3000)) then
                            try proc.Kill(true) with _ -> ()
                finally
                    proc.Dispose()
                    try File.Delete(scriptPath) with _ -> ()

/// <summary>
/// Local offline TTS provider powered by Piper TTS (persistent Python `PiperVoice` worker
/// when invoked via Python, or standalone CLI binary), encoding output WAV to normalized MP3 via ffmpeg.
/// </summary>
type PiperTtsProvider(config: PiperTtsConfig) =
    let (defaultModelOverride, modelMap) = parseLanguageMappingSpec config.ModelSpec
    let resolvedInvocation = lazy (resolvePiperInvocation config.PiperCommand)
    let workers = Dictionary<string, PersistentPiperWorker>(StringComparer.OrdinalIgnoreCase)
    let mutable disposed = false

    let resolveModelPath (languageTag: string) : string option =
        match lookupLanguageMapping defaultModelOverride modelMap languageTag with
        | Some explicitPath when File.Exists(explicitPath) -> Some explicitPath
        | Some explicitPath ->
            match config.ModelsDirectory with
            | Some dir ->
                let combined = Path.Combine(dir, explicitPath)
                if File.Exists(combined) then Some combined
                elif File.Exists(combined + ".onnx") then Some (combined + ".onnx")
                else Some explicitPath
            | None -> Some explicitPath
        | None ->
            config.ModelsDirectory
            |> Option.bind (fun dir -> tryFindPiperModelInDirectory dir languageTag)

    let synthesizeViaPersistentPythonWorker (pythonExe: string) (pythonPrefixArgs: string list) (useCuda: bool) (modelPath: string) (text: string) (outputWav: string) : Result<unit, string> =
        let worker =
            match workers.TryGetValue(modelPath) with
            | true, existing when existing.IsReady -> existing
            | true, stale ->
                (stale :> IDisposable).Dispose()
                workers.Remove(modelPath) |> ignore
                let fresh = new PersistentPiperWorker(pythonExe, pythonPrefixArgs, modelPath, useCuda)
                workers.[modelPath] <- fresh
                fresh
            | false, _ ->
                let fresh = new PersistentPiperWorker(pythonExe, pythonPrefixArgs, modelPath, useCuda)
                workers.[modelPath] <- fresh
                fresh

        match worker.StartupError with
        | Some err -> Error err
        | None -> worker.SynthesizeToWav(text, outputWav)

    let synthesizeViaCliSubprocess (exe: string) (prefixArgs: string list) (useCuda: bool) (modelPath: string) (text: string) (tempDir: string) (outputWav: string) : Result<unit, string> =
        let inputTxt = Path.Combine(tempDir, "input.txt")
        File.WriteAllText(inputTxt, text, UTF8Encoding(false))

        let psi = ProcessStartInfo()
        psi.FileName <- exe
        for a in prefixArgs do psi.ArgumentList.Add(a)
        let hasCudaAlready = prefixArgs |> List.exists (fun a -> String.Equals(a, "--cuda", StringComparison.OrdinalIgnoreCase))
        if useCuda && not hasCudaAlready then
            psi.ArgumentList.Add("--cuda")
        psi.ArgumentList.Add("-m")
        psi.ArgumentList.Add(modelPath)
        psi.ArgumentList.Add("-i")
        psi.ArgumentList.Add(inputTxt)
        psi.ArgumentList.Add("-f")
        psi.ArgumentList.Add(outputWav)
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true

        use proc = new Process()
        proc.StartInfo <- psi
        if not (proc.Start()) then
            Error ("Failed to launch Piper: " + exe)
        else
            let stderrTask = proc.StandardError.ReadToEndAsync()
            if not (proc.WaitForExit(60000)) then
                try proc.Kill(true) with _ -> ()
                Error "Piper TTS timed out after 60 seconds."
            elif proc.ExitCode <> 0 then
                let stderr = stderrTask.GetAwaiter().GetResult()
                Error (sprintf "Piper TTS exited with code %d: %s" proc.ExitCode (stderr.Trim()))
            elif not (File.Exists(outputWav)) || (FileInfo(outputWav).Length = 0L) then
                Error "Piper TTS did not produce an output WAV file."
            else
                Ok ()

    interface ITtsProvider with
        member _.Name = "piper"

        member _.ResolveVoice(languageTag: string) =
            resolveModelPath languageTag
            |> Option.map Path.GetFileNameWithoutExtension

        member _.Synthesize(languageTag: string, text: string) =
            if String.IsNullOrWhiteSpace(text) then
                Error "Cannot synthesize empty text."
            else
                match resolvedInvocation.Value with
                | None -> Error piperInstallHint
                | Some (exe, prefixArgs) ->
                    match resolveModelPath languageTag with
                    | None ->
                        Error (sprintf "No Piper .onnx voice model found for language '%s'. Pass --piper-model=\"%s=<path.onnx>\" or --piper-models-dir=<dir>." languageTag languageTag)
                    | Some modelPath when not (File.Exists(modelPath)) ->
                        Error (sprintf "Piper model file does not exist: %s" modelPath)
                    | Some modelPath ->
                        let tempDir = Path.Combine(Path.GetTempPath(), "ll-piper-" + Guid.NewGuid().ToString("N"))
                        Directory.CreateDirectory(tempDir) |> ignore
                        try
                            let outputWav = Path.Combine(tempDir, "output.wav")
                            let wavResult =
                                match tryExtractPythonPiperCommand exe prefixArgs with
                                | Some (pyExe, pyArgs, cudaInArgs) ->
                                    let effectiveCuda = config.UseCuda || cudaInArgs
                                    synthesizeViaPersistentPythonWorker pyExe pyArgs effectiveCuda modelPath text outputWav
                                | None ->
                                    synthesizeViaCliSubprocess exe prefixArgs config.UseCuda modelPath text tempDir outputWav

                            match wavResult with
                            | Error err -> Error err
                            | Ok () ->
                                match transcodeFileToMp3 config.AudioTools config.AudioOptions outputWav None with
                                | Error err -> Error ("Failed to encode Piper WAV output to MP3: " + err)
                                | Ok mp3 ->
                                    let voiceName = Path.GetFileNameWithoutExtension(modelPath)
                                    let provenance =
                                        {
                                            Provider = "piper"
                                            Voice = voiceName
                                            Model = Path.GetFileName(modelPath)
                                            LanguageTag = languageTag
                                            GeneratedAtUtc = DateTime.UtcNow.ToString("O")
                                        }
                                    Ok
                                        {
                                            AudioBytes = mp3.Mp3Bytes
                                            MimeType = "audio/mpeg"
                                            FileExtension = ".mp3"
                                            DurationMs = mp3.DurationMs
                                            Provenance = provenance
                                        }
                        finally
                            try Directory.Delete(tempDir, true) with _ -> ()

    interface IDisposable with
        member _.Dispose() =
            if not disposed then
                disposed <- true
                for kv in workers do
                    (kv.Value :> IDisposable).Dispose()
                workers.Clear()

type ResolvedCardAudio =
    | NoAudio
    | NativeExtracted of MediaRecord
    | CachedTts of MediaRecord
    | NewlySynthesizedTts of MediaRecord

/// <summary>
/// Resolves audio for a card during export, returning whether the audio came from native
/// extracted media, the TTS cache, or fresh TTS synthesis.
/// </summary>
let resolveCardAudioDetailed
    (soundResourceHash: string)
    (languageTag: string)
    (text: string)
    (mediaStoreOpt: IMediaStore option)
    (ttsProviderOpt: ITtsProvider option)
    (allowTts: bool)
    (regenerateTts: bool)
    : Result<ResolvedCardAudio, string> =
    // 1. Check native extracted audio first
    let extractedOpt =
        if String.IsNullOrWhiteSpace(soundResourceHash) then
            None
        else
            mediaStoreOpt
            |> Option.bind (fun store -> store.TryGetMedia(soundResourceHash))

    match extractedOpt with
    | Some media -> Ok (NativeExtracted media)
    | None ->
        if not allowTts || String.IsNullOrWhiteSpace(text) then
            Ok NoAudio
        else
            match ttsProviderOpt with
            | None -> Ok NoAudio
            | Some provider ->
                // 2. Check cached TTS (unless regeneration was requested)
                let cachedOpt =
                    if regenerateTts then
                        None
                    else
                        mediaStoreOpt
                        |> Option.bind (fun store -> store.TryGetTts(languageTag, text))
                        |> Option.map fst

                match cachedOpt with
                | Some cached -> Ok (CachedTts cached)
                | None ->
                    // 3. Synthesize and persist with full provenance
                    match provider.Synthesize(languageTag, text) with
                    | Error err -> Error err
                    | Ok synth ->
                        let record =
                            {
                                MediaRecord.Hash = computeSha256Hex synth.AudioBytes
                                Kind = MediaKind.Audio
                                MimeType = synth.MimeType
                                FileExtension = synth.FileExtension
                                LanguageTag = languageTag
                                Source = MediaSource.Tts
                                SourceDescription = sprintf "%s:%s" synth.Provenance.Provider synth.Provenance.Voice
                                TtsProvider = synth.Provenance.Provider
                                TtsVoice = synth.Provenance.Voice
                                TtsModel = synth.Provenance.Model
                                GeneratedAtUtc = synth.Provenance.GeneratedAtUtc
                                DurationMs = synth.DurationMs
                                Width = 0
                                Height = 0
                                ByteLength = synth.AudioBytes.Length
                                Data = synth.AudioBytes
                            }
                        match mediaStoreOpt with
                        | Some store ->
                            let hash = store.PutTts(languageTag, text, record)
                            Ok (NewlySynthesizedTts { record with Hash = hash })
                        | None ->
                            Ok (NewlySynthesizedTts record)

/// <summary>
/// Resolves audio for a card during export:
/// 1. Prefers native Extracted audio stored in the companion IMediaStore (never overwritten by TTS).
/// 2. If missing and TTS is enabled for this card's side, checks the TTS cache unless regenerateTts is true.
/// 3. Synthesizes missing or invalidated TTS audio via the provider and caches it with full provenance.
/// </summary>
let resolveCardAudio
    (soundResourceHash: string)
    (languageTag: string)
    (text: string)
    (mediaStoreOpt: IMediaStore option)
    (ttsProviderOpt: ITtsProvider option)
    (allowTts: bool)
    (regenerateTts: bool)
    : Result<MediaRecord option, string> =
    match resolveCardAudioDetailed soundResourceHash languageTag text mediaStoreOpt ttsProviderOpt allowTts regenerateTts with
    | Error err -> Error err
    | Ok NoAudio -> Ok None
    | Ok (NativeExtracted m)
    | Ok (CachedTts m)
    | Ok (NewlySynthesizedTts m) -> Ok (Some m)
