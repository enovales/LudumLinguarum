module FmodTools

open System
open System.IO
open System.Text.RegularExpressions

/// <summary>
/// Locates a single voiced sample inside an FMOD FSB bank.
/// </summary>
type FmodBankEntry =
    {
        BankPath: string
        /// 1-based subsong index for vgmstream-cli (-s).
        SubsongIndex: int
        Speaker: string
    }

let private defineRegex =
    Regex(@"^\s*#define\s+(?<name>[A-Za-z0-9_]+)\s+(?<index>\d+)\s*$", RegexOptions.Compiled)

let private cuePrefixRegex =
    Regex(@"^(?<prefix>[A-Za-z][A-Za-z0-9]*?)_\d+", RegexOptions.Compiled)

/// <summary>
/// Normalizes a Supergiant/FMOD voice cue identifier (e.g. "/VO/Achilles_0005" -> "ACHILLES_0005").
/// </summary>
let normalizeCueId (rawCue: string) : string =
    if String.IsNullOrWhiteSpace(rawCue) then
        ""
    else
        let trimmed = rawCue.Trim()
        let withoutPrefix =
            if trimmed.StartsWith("/VO/", StringComparison.OrdinalIgnoreCase) then
                trimmed.Substring(4)
            elif trimmed.StartsWith("VO/", StringComparison.OrdinalIgnoreCase) then
                trimmed.Substring(3)
            elif trimmed.StartsWith("/", StringComparison.Ordinal) then
                trimmed.Substring(1)
            else
                trimmed
        withoutPrefix.Trim().ToUpperInvariant()

/// <summary>
/// Infers a speaker name from a cue identifier (e.g. "Achilles_0005" -> "Achilles").
/// </summary>
let inferSpeakerFromCue (rawCue: string) : string =
    if String.IsNullOrWhiteSpace(rawCue) then
        ""
    else
        let trimmed = rawCue.Trim()
        let cleaned =
            if trimmed.StartsWith("/VO/", StringComparison.OrdinalIgnoreCase) then
                trimmed.Substring(4)
            elif trimmed.StartsWith("VO/", StringComparison.OrdinalIgnoreCase) then
                trimmed.Substring(3)
            else
                trimmed
        let m = cuePrefixRegex.Match(cleaned)
        if m.Success then
            m.Groups.["prefix"].Value
        else
            ""

/// <summary>
/// Parses an FMOD C-style header index file containing `#define CUE_NAME <0-based-index>` lines.
/// Returns a map from uppercase cue name to 1-based vgmstream-cli subsong index.
/// </summary>
let parseFmodHeaderCueMap (headerLines: string seq) : Map<string, int> =
    headerLines
    |> Seq.choose (fun line ->
        if isNull line then
            None
        else
            let m = defineRegex.Match(line)
            if m.Success then
                let name = m.Groups.["name"].Value.ToUpperInvariant()
                match Int32.TryParse(m.Groups.["index"].Value) with
                | true, zeroBasedIndex when zeroBasedIndex >= 0 ->
                    Some (name, zeroBasedIndex + 1)
                | _ -> None
            else
                None
    )
    |> Map.ofSeq

/// <summary>
/// Loads a cue-to-bank map from a single paired FMOD `.fsb` bank and `.h`/`.txt` header index file.
/// </summary>
let loadSingleBankCueMap (fsbPath: string) (headerPath: string) (speakerOverride: string option) : Map<string, FmodBankEntry> =
    if not (File.Exists(fsbPath)) || not (File.Exists(headerPath)) then
        Map.empty
    else
        let cueIndices = File.ReadLines(headerPath) |> parseFmodHeaderCueMap
        cueIndices
        |> Map.map (fun cue subsongIdx ->
            let speaker =
                match speakerOverride with
                | Some s when not (String.IsNullOrWhiteSpace(s)) -> s
                | _ -> inferSpeakerFromCue cue
            {
                BankPath = fsbPath
                SubsongIndex = subsongIdx
                Speaker = speaker
            }
        )

/// <summary>
/// Loads a combined cue-to-bank map from a directory of paired `<Name>.fsb` and `<Name>.txt`/`.h` files
/// (such as Hades II's `Content/Audio/Desktop/VO/` directory).
/// </summary>
let loadDirectoryBankCueMap (voDirectory: string) : Map<string, FmodBankEntry> =
    if String.IsNullOrWhiteSpace(voDirectory) || not (Directory.Exists(voDirectory)) then
        Map.empty
    else
        let fsbFiles = Directory.GetFiles(voDirectory, "*.fsb", SearchOption.TopDirectoryOnly) |> Array.sort
        fsbFiles
        |> Array.fold (fun acc fsbPath ->
            let stem = Path.GetFileNameWithoutExtension(fsbPath)
            let dir = Path.GetDirectoryName(fsbPath)
            let txtPath = Path.Combine(dir, stem + ".txt")
            let hPath = Path.Combine(dir, stem + ".h")
            let headerPathOpt =
                if File.Exists(txtPath) then Some txtPath
                elif File.Exists(hPath) then Some hPath
                else None

            match headerPathOpt with
            | None -> acc
            | Some headerPath ->
                let bankMap = loadSingleBankCueMap fsbPath headerPath (Some stem)
                bankMap |> Map.fold (fun m k v -> m |> Map.add k v) acc
        ) Map.empty
