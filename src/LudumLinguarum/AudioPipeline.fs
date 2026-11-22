module AudioPipeline

open MediaDatabase
open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks

type ExternalToolConfig =
    {
        FfmpegPath: string option
        VgmstreamPath: string option
    }
    static member Default =
        {
            FfmpegPath = None
            VgmstreamPath = None
        }

type AudioIngestOptions =
    {
        TargetBitrateKbps: int
        SampleRateHz: int
        NormalizeLoudness: bool
        TrimSilence: bool
        PreferVgmstream: bool
    }
    static member Default =
        {
            TargetBitrateKbps = 64
            SampleRateHz = 24000
            NormalizeLoudness = true
            TrimSilence = false
            PreferVgmstream = false
        }

type TranscodeResult =
    {
        Mp3Bytes: byte array
        DurationMs: int
    }

let private isWindows = OperatingSystem.IsWindows()

let private splitPathEntries (pathVar: string) =
    if String.IsNullOrWhiteSpace(pathVar) then
        [||]
    else
        pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)

/// <summary>
/// Resolves an external executable from an explicit path (file or directory) or PATH.
/// </summary>
let resolveExecutable (explicitPath: string option) (candidateNames: string list) : string option =
    let withExtensions (name: string) =
        if isWindows && not (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) then
            [ name + ".exe"; name ]
        else
            [ name ]

    let expandedNames = candidateNames |> List.collect withExtensions

    let findInDirectory (dir: string) =
        if Directory.Exists(dir) then
            expandedNames
            |> List.tryPick (fun n ->
                let full = Path.Combine(dir, n)
                if File.Exists(full) then Some full else None
            )
        else
            None

    match explicitPath with
    | Some p when not (String.IsNullOrWhiteSpace(p)) ->
        let trimmed = p.Trim()
        if File.Exists(trimmed) then
            Some trimmed
        else
            findInDirectory trimmed
    | _ ->
        let pathEnv = Environment.GetEnvironmentVariable("PATH")
        splitPathEntries pathEnv
        |> Array.tryPick findInDirectory

let resolveFfmpeg (tools: ExternalToolConfig) : string option =
    resolveExecutable tools.FfmpegPath [ "ffmpeg" ]

let resolveVgmstream (tools: ExternalToolConfig) : string option =
    resolveExecutable tools.VgmstreamPath [ "vgmstream-cli"; "vgmstream_cli"; "test" ]

let ffmpegInstallHint =
    "ffmpeg was not found. Install it (e.g. 'winget install Gyan.FFmpeg' on Windows, 'brew install ffmpeg' on macOS, or 'apt install ffmpeg' on Linux) or pass --ffmpeg-path=<path>."

let vgmstreamInstallHint =
    "vgmstream-cli was not found. Download it from https://github.com/vgmstream/vgmstream/releases and add it to PATH or pass --vgmstream-path=<path>."

let private vgmstreamPreferredExtensions =
    set [ ".wem"; ".hca"; ".adx"; ".fsb"; ".acb"; ".awb"; ".xma"; ".bik"; ".xwb"; ".at3"; ".at9"; ".vag"; ".dsp"; ".brstm"; ".bcstm"; ".bfstm" ]

let requiresVgmstreamFirst (extension: string) : bool =
    let normalized =
        if String.IsNullOrWhiteSpace(extension) then ""
        elif extension.StartsWith(".") then extension.ToLowerInvariant()
        else "." + extension.ToLowerInvariant()
    vgmstreamPreferredExtensions.Contains(normalized)

let private durationRegex = Regex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled)
let private timeRegex = Regex(@"time=(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled)

/// <summary>
/// Parses a duration in milliseconds from ffmpeg stderr output (prefers encoded output time=,
/// falling back to input Duration:).
/// </summary>
let parseDurationMsFromFfmpegOutput (stderr: string) : int =
    if String.IsNullOrWhiteSpace(stderr) then
        0
    else
        let parseMatch (m: Match) =
            let hours = Double.Parse(m.Groups.[1].Value, CultureInfo.InvariantCulture)
            let mins = Double.Parse(m.Groups.[2].Value, CultureInfo.InvariantCulture)
            let secs = Double.Parse(m.Groups.[3].Value, CultureInfo.InvariantCulture)
            let totalMs = (hours * 3600.0 + mins * 60.0 + secs) * 1000.0
            if totalMs < 0.0 || Double.IsNaN(totalMs) || Double.IsInfinity(totalMs) then 0
            else int (Math.Round(totalMs))

        let timeMatches = timeRegex.Matches(stderr)
        if timeMatches.Count > 0 then
            parseMatch timeMatches.[timeMatches.Count - 1]
        else
            let durMatches = durationRegex.Matches(stderr)
            if durMatches.Count > 0 then
                parseMatch durMatches.[0]
            else
                0

/// <summary>
/// Builds the ffmpeg audio filter chain string for the given ingest options.
/// </summary>
let buildAudioFilterChain (opts: AudioIngestOptions) : string option =
    let filters = ResizeArray<string>()
    if opts.TrimSilence then
        filters.Add("silenceremove=start_periods=1:start_silence=0.05:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_silence=0.05:start_threshold=-45dB,areverse")
    if opts.NormalizeLoudness then
        filters.Add("loudnorm=I=-16:TP=-1.5:LRA=11")
    if filters.Count = 0 then None
    else Some (String.Join(",", filters))

let private runProcess (exePath: string) (args: string list) (stdinBytes: byte array option) (timeoutMs: int) : Result<string * string, string> =
    try
        let psi = ProcessStartInfo()
        psi.FileName <- exePath
        for a in args do
            psi.ArgumentList.Add(a)
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.RedirectStandardInput <- stdinBytes.IsSome

        use proc = new Process()
        proc.StartInfo <- psi
        if not (proc.Start()) then
            Error ("Failed to start process: " + exePath)
        else
            let stdoutTask = proc.StandardOutput.ReadToEndAsync()
            let stderrTask = proc.StandardError.ReadToEndAsync()

            match stdinBytes with
            | Some bytes ->
                proc.StandardInput.BaseStream.Write(bytes, 0, bytes.Length)
                proc.StandardInput.BaseStream.Flush()
                proc.StandardInput.Close()
            | None -> ()

            if not (proc.WaitForExit(timeoutMs)) then
                try proc.Kill(true) with _ -> ()
                Error (sprintf "Process '%s' timed out after %d ms." exePath timeoutMs)
            else
                let stdout = stdoutTask.GetAwaiter().GetResult()
                let stderr = stderrTask.GetAwaiter().GetResult()
                if proc.ExitCode = 0 then
                    Ok (stdout, stderr)
                else
                    Error (sprintf "Process '%s' exited with code %d: %s" (Path.GetFileName(exePath)) proc.ExitCode (stderr.Trim()))
    with ex ->
        Error (sprintf "Error running '%s': %s" exePath ex.Message)

let private runFfmpegToMp3 (ffmpegExe: string) (opts: AudioIngestOptions) (inputFile: string) (outputFile: string) : Result<TranscodeResult, string> =
    let args = ResizeArray<string>()
    args.AddRange([| "-y"; "-hide_banner"; "-loglevel"; "info"; "-i"; inputFile; "-vn"; "-ac"; "1"; "-ar"; opts.SampleRateHz.ToString(CultureInfo.InvariantCulture); "-b:a"; sprintf "%dk" opts.TargetBitrateKbps |])
    match buildAudioFilterChain opts with
    | Some af ->
        args.Add("-af")
        args.Add(af)
    | None -> ()
    args.AddRange([| "-f"; "mp3"; outputFile |])

    match runProcess ffmpegExe (List.ofSeq args) None 60000 with
    | Error err -> Error err
    | Ok (_, stderr) ->
        if not (File.Exists(outputFile)) then
            Error "ffmpeg completed without producing an output MP3 file."
        else
            let mp3Bytes = File.ReadAllBytes(outputFile)
            if mp3Bytes.Length = 0 then
                Error "ffmpeg produced an empty MP3 file."
            else
                let durationMs = parseDurationMsFromFfmpegOutput stderr
                Ok { Mp3Bytes = mp3Bytes; DurationMs = durationMs }

let private runVgmstreamToWav (vgmstreamExe: string) (inputFile: string) (outputWavFile: string) (subsongIndex: int option) : Result<unit, string> =
    let args = ResizeArray<string>()
    match subsongIndex with
    | Some idx ->
        args.Add("-s")
        args.Add(idx.ToString(CultureInfo.InvariantCulture))
    | None -> ()
    args.Add("-o")
    args.Add(outputWavFile)
    args.Add(inputFile)

    match runProcess vgmstreamExe (List.ofSeq args) None 60000 with
    | Error err -> Error err
    | Ok _ ->
        if File.Exists(outputWavFile) && (FileInfo(outputWavFile).Length > 0L) then
            Ok ()
        else
            Error "vgmstream-cli completed without producing an output WAV file."

/// <summary>
/// Transcodes an audio file on disk to normalized mono MP3 using ffmpeg (and vgmstream-cli
/// when required or as a fallback for proprietary/wrapped game audio formats such as BioWare WAVs).
/// </summary>
let transcodeFileToMp3
    (tools: ExternalToolConfig)
    (opts: AudioIngestOptions)
    (inputFilePath: string)
    (subsongIndex: int option)
    : Result<TranscodeResult, string> =
    if not (File.Exists(inputFilePath)) then
        Error ("Audio file does not exist: " + inputFilePath)
    else
        match resolveFfmpeg tools with
        | None -> Error ffmpegInstallHint
        | Some ffmpegExe ->
            let tempDir = Path.Combine(Path.GetTempPath(), "ll-audio-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(tempDir) |> ignore
            try
                let outputMp3 = Path.Combine(tempDir, "out.mp3")
                let decodedWav = Path.Combine(tempDir, "decoded.wav")
                let ext = Path.GetExtension(inputFilePath)
                let useVgmFirst =
                    subsongIndex.IsSome
                    || requiresVgmstreamFirst ext
                    || (opts.PreferVgmstream && (resolveVgmstream tools).IsSome)

                let decodeViaVgmstreamThenFfmpeg () =
                    match resolveVgmstream tools with
                    | None -> Error vgmstreamInstallHint
                    | Some vgmExe ->
                        match runVgmstreamToWav vgmExe inputFilePath decodedWav subsongIndex with
                        | Error e -> Error e
                        | Ok () -> runFfmpegToMp3 ffmpegExe opts decodedWav outputMp3

                if useVgmFirst then
                    decodeViaVgmstreamThenFfmpeg ()
                else
                    match runFfmpegToMp3 ffmpegExe opts inputFilePath outputMp3 with
                    | Ok res -> Ok res
                    | Error ffmpegErr ->
                        // Fallback to vgmstream-cli for quirky game audio (e.g., fake-WAV MP3 headers)
                        match resolveVgmstream tools with
                        | Some _ -> decodeViaVgmstreamThenFfmpeg ()
                        | None -> Error ffmpegErr
            finally
                try Directory.Delete(tempDir, true) with _ -> ()

/// <summary>
/// Transcodes in-memory audio bytes to normalized mono MP3.
/// </summary>
let transcodeBytesToMp3
    (tools: ExternalToolConfig)
    (opts: AudioIngestOptions)
    (inputBytes: byte array)
    (sourceExtension: string)
    (subsongIndex: int option)
    : Result<TranscodeResult, string> =
    let safeExt =
        if String.IsNullOrWhiteSpace(sourceExtension) then ".bin"
        elif sourceExtension.StartsWith(".") then sourceExtension
        else "." + sourceExtension

    let tempDir = Path.Combine(Path.GetTempPath(), "ll-audio-in-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(tempDir) |> ignore
    try
        let inputPath = Path.Combine(tempDir, "input" + safeExt)
        File.WriteAllBytes(inputPath, inputBytes)
        transcodeFileToMp3 tools opts inputPath subsongIndex
    finally
        try Directory.Delete(tempDir, true) with _ -> ()

/// <summary>
/// Ingests an extracted audio file from disk into the companion IMediaStore as MP3 and returns its SHA-256 hash.
/// </summary>
let ingestExtractedAudioFile
    (store: IMediaStore)
    (tools: ExternalToolConfig)
    (opts: AudioIngestOptions)
    (languageTag: string)
    (sourceDescription: string)
    (inputFilePath: string)
    (subsongIndex: int option)
    : Result<string, string> =
    match transcodeFileToMp3 tools opts inputFilePath subsongIndex with
    | Error err -> Error err
    | Ok transcoded ->
        let record =
            {
                MediaRecord.Hash = ""
                Kind = MediaKind.Audio
                MimeType = "audio/mpeg"
                FileExtension = ".mp3"
                LanguageTag = languageTag
                Source = MediaSource.Extracted
                SourceDescription = sourceDescription
                TtsProvider = ""
                TtsVoice = ""
                TtsModel = ""
                GeneratedAtUtc = DateTime.UtcNow.ToString("O")
                DurationMs = transcoded.DurationMs
                Width = 0
                Height = 0
                ByteLength = transcoded.Mp3Bytes.Length
                Data = transcoded.Mp3Bytes
            }
        Ok (store.PutMedia(record))

/// <summary>
/// Ingests extracted audio bytes (e.g. read from a game archive) into IMediaStore as MP3 and returns its SHA-256 hash.
/// </summary>
let ingestExtractedAudioBytes
    (store: IMediaStore)
    (tools: ExternalToolConfig)
    (opts: AudioIngestOptions)
    (languageTag: string)
    (sourceDescription: string)
    (inputBytes: byte array)
    (sourceExtension: string)
    (subsongIndex: int option)
    : Result<string, string> =
    match transcodeBytesToMp3 tools opts inputBytes sourceExtension subsongIndex with
    | Error err -> Error err
    | Ok transcoded ->
        let record =
            {
                MediaRecord.Hash = ""
                Kind = MediaKind.Audio
                MimeType = "audio/mpeg"
                FileExtension = ".mp3"
                LanguageTag = languageTag
                Source = MediaSource.Extracted
                SourceDescription = sourceDescription
                TtsProvider = ""
                TtsVoice = ""
                TtsModel = ""
                GeneratedAtUtc = DateTime.UtcNow.ToString("O")
                DurationMs = transcoded.DurationMs
                Width = 0
                Height = 0
                ByteLength = transcoded.Mp3Bytes.Length
                Data = transcoded.Mp3Bytes
            }
        Ok (store.PutMedia(record))

/// <summary>
/// Describes a single extracted audio file (or subsong inside an audio bank) to ingest in a batch.
/// </summary>
type AudioIngestWorkItem =
    {
        Key: string
        LanguageTag: string
        SourceDescription: string
        InputFilePath: string
        SubsongIndex: int option
    }

/// <summary>
/// Transcodes and ingests a batch of extracted audio files in parallel (bounded concurrency)
/// into IMediaStore, returning a map from work-item Key to stored SHA-256 hash.
/// </summary>
let ingestExtractedAudioFileBatch
    (store: IMediaStore)
    (tools: ExternalToolConfig)
    (opts: AudioIngestOptions)
    (log: string -> unit)
    (items: AudioIngestWorkItem array)
    : Map<string, string> * int * int =
    if items.Length = 0 then
        (Map.empty, 0, 0)
    else
        let results = ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let mutable completed = 0
        let mutable succeeded = 0
        let mutable errors = 0
        let logLock = obj()

        let parallelOptions = ParallelOptions()
        parallelOptions.MaxDegreeOfParallelism <- Math.Clamp(Environment.ProcessorCount, 1, 6)

        Parallel.ForEach(
            items,
            parallelOptions,
            fun item ->
                match ingestExtractedAudioFile store tools opts item.LanguageTag item.SourceDescription item.InputFilePath item.SubsongIndex with
                | Ok hash ->
                    results.[item.Key] <- hash
                    Interlocked.Increment(&succeeded) |> ignore
                | Error err ->
                    let errNum = Interlocked.Increment(&errors)
                    if errNum <= 10 then
                        lock logLock (fun () -> log (sprintf "Warning: failed to ingest audio '%s': %s" item.SourceDescription err))

                let doneCount = Interlocked.Increment(&completed)
                if items.Length >= 50 && (doneCount % 100 = 0 || doneCount = items.Length) then
                    lock logLock (fun () ->
                        log (sprintf "Audio ingest progress: %d/%d (%d succeeded, %d errors)" doneCount items.Length succeeded errors)
                    )
        )
        |> ignore

        let resultMap =
            results
            |> Seq.map (fun kv -> (kv.Key, kv.Value))
            |> Map.ofSeq
        (resultMap, succeeded, errors)

/// <summary>
/// Extracts PNG dimensions (width, height) from raw PNG header bytes if valid.
/// </summary>
let tryReadPngDimensions (bytes: byte array) : (int * int) option =
    if bytes.Length >= 24
       && bytes.[0] = 0x89uy && bytes.[1] = 0x50uy && bytes.[2] = 0x4Euy && bytes.[3] = 0x47uy
       && bytes.[4] = 0x0Duy && bytes.[5] = 0x0Auy && bytes.[6] = 0x1Auy && bytes.[7] = 0x0Auy then
        let width =
            (int bytes.[16] <<< 24) ||| (int bytes.[17] <<< 16) ||| (int bytes.[18] <<< 8) ||| int bytes.[19]
        let height =
            (int bytes.[20] <<< 24) ||| (int bytes.[21] <<< 16) ||| (int bytes.[22] <<< 8) ||| int bytes.[23]
        Some (width, height)
    else
        None

/// <summary>
/// Ingests an extracted visual asset (PNG, JPEG, WebP) into IMediaStore and returns its SHA-256 hash.
/// </summary>
let ingestExtractedImageBytes
    (store: IMediaStore)
    (languageTag: string)
    (sourceDescription: string)
    (imageBytes: byte array)
    (extension: string)
    : Result<string, string> =
    if isNull imageBytes || imageBytes.Length = 0 then
        Error "Image payload is empty."
    else
        let normalizedExt =
            if extension.StartsWith(".") then extension.ToLowerInvariant()
            else "." + extension.ToLowerInvariant()

        let mimeType =
            match normalizedExt with
            | ".png" -> Ok "image/png"
            | ".jpg" | ".jpeg" -> Ok "image/jpeg"
            | ".webp" -> Ok "image/webp"
            | ".gif" -> Ok "image/gif"
            | other -> Error ("Unsupported image format: " + other)

        match mimeType with
        | Error e -> Error e
        | Ok mime ->
            let (width, height) =
                if normalizedExt = ".png" then
                    tryReadPngDimensions imageBytes |> Option.defaultValue (0, 0)
                else
                    (0, 0)

            let record =
                {
                    MediaRecord.Hash = ""
                    Kind = MediaKind.Image
                    MimeType = mime
                    FileExtension = normalizedExt
                    LanguageTag = languageTag
                    Source = MediaSource.Extracted
                    SourceDescription = sourceDescription
                    TtsProvider = ""
                    TtsVoice = ""
                    TtsModel = ""
                    GeneratedAtUtc = DateTime.UtcNow.ToString("O")
                    DurationMs = 0
                    Width = width
                    Height = height
                    ByteLength = imageBytes.Length
                    Data = imageBytes
                }
            Ok (store.PutMedia(record))
