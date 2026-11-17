module UnrealEngine.UnrealLocRes

open System
open System.Collections.Generic
open System.IO
open System.Text
open UnrealEngine.UnrealPak

/// 16-byte GUID signature at the beginning of compact/optimized Unreal Engine .locres files (v1+).
let LocResMagic: byte array =
    [|
        0x0Euy; 0x14uy; 0x74uy; 0x75uy
        0x67uy; 0x4Auy; 0x03uy; 0xFCuy
        0x4Auy; 0x15uy; 0x90uy; 0x9Duy
        0xC3uy; 0x37uy; 0x7Fuy; 0x1Buy
    |]

/// Checks whether a byte array starts with the 16-byte Unreal Engine LocRes magic GUID.
let isLocRes (data: byte array) : bool =
    if isNull data || data.Length < 25 then
        false
    else
        let mutable matched = true
        for i = 0 to 15 do
            if data.[i] <> LocResMagic.[i] then
                matched <- false
        matched

/// Maps an Unreal Engine culture/localization folder code (e.g. "en", "ja", "zh-Hans", "zh-Hant") to a normalized IETF language tag.
let tryMapUnrealLanguage (code: string) : string option =
    if String.IsNullOrWhiteSpace(code) then
        None
    else
        match code.Trim().ToLowerInvariant() with
        | "ar" -> Some "ar"
        | "de" | "de-de" -> Some "de"
        | "en" | "en-us" | "en-gb" -> Some "en"
        | "es" | "es-es" -> Some "es"
        | "es-419" | "es-la" | "es-mx" -> Some "es-419"
        | "fr" | "fr-fr" -> Some "fr"
        | "it" | "it-it" -> Some "it"
        | "ja" | "ja-jp" | "jp" -> Some "ja"
        | "ko" | "ko-kr" | "kr" -> Some "ko"
        | "pl" | "pl-pl" -> Some "pl"
        | "pt" | "pt-br" -> Some "pt-BR"
        | "pt-pt" -> Some "pt-PT"
        | "ru" | "ru-ru" -> Some "ru"
        | "zh-hans" | "zh-cn" | "zh-sg" | "zhs" | "chs" -> Some "zh-CN"
        | "zh-hant" | "zh-tw" | "zh-hk" | "zh" | "zht" | "cht" -> Some "zh-TW"
        | _ -> None

/// Parses an Unreal Engine .locres binary file (versions 1..3) and returns ("{namespace}/{key}", value) pairs.
let parse (data: byte array) : (string * string) array =
    if not (isLocRes data) then
        raise (FormatException("Input does not have the Unreal Engine .locres magic header."))

    use ms = new MemoryStream(data, false)
    use br = new BinaryReader(ms, Encoding.UTF8, true)
    let _magic = br.ReadBytes(16)
    let version = br.ReadByte()
    if version = 0uy || version > 3uy then
        raise (FormatException($"Unsupported Unreal .locres version {version}."))

    let stringArrayOffset = br.ReadInt64()
    let afterHeaderPos = ms.Position

    if stringArrayOffset <= 0L || stringArrayOffset >= ms.Length then
        [||]
    else
        ms.Seek(stringArrayOffset, SeekOrigin.Begin) |> ignore
        let stringCount = br.ReadInt32()
        if stringCount < 0 then
            [||]
        else
            let stringPool =
                Array.init stringCount (fun _ ->
                    let s = readFString br
                    if version >= 2uy then
                        let _refCount = br.ReadInt32()
                        ()
                    s)

            ms.Seek(afterHeaderPos, SeekOrigin.Begin) |> ignore
            if version >= 2uy then
                let _entriesCount = br.ReadUInt32()
                ()

            let namespaceCount = int (br.ReadUInt32())
            let results = ResizeArray<string * string>()

            for _ = 0 to namespaceCount - 1 do
                if version >= 2uy then
                    let _nsHash = br.ReadUInt32()
                    ()
                let namespaceName = readFString br
                let keyCount = int (br.ReadUInt32())
                for _ = 0 to keyCount - 1 do
                    if version >= 2uy then
                        let _keyHash = br.ReadUInt32()
                        ()
                    let keyName = readFString br
                    let _sourceStringHash = br.ReadUInt32()
                    let stringIndex = br.ReadInt32()
                    if stringIndex >= 0 && stringIndex < stringPool.Length then
                        let text = stringPool.[stringIndex]
                        if not (String.IsNullOrWhiteSpace(text)) then
                            let fullKey =
                                if String.IsNullOrEmpty(namespaceName) then keyName
                                else $"{namespaceName}/{keyName}"
                            results.Add((fullKey, text))

            results.ToArray()

/// Builds a binary Unreal Engine v3 .locres resource in memory from an array of (namespaceName, (keyName, text) array) groups.
let build (namespaces: (string * (string * string) array) array) : byte array =
    let stringPool = ResizeArray<string * int>()
    let stringIndexMap = Dictionary<string, int>(StringComparer.Ordinal)

    let indexedNamespaces =
        namespaces
        |> Array.map (fun (nsName, entries) ->
            let indexedEntries =
                entries
                |> Array.map (fun (keyName, text) ->
                    let cleanVal = if isNull text then "" else text
                    let idx =
                        match stringIndexMap.TryGetValue(cleanVal) with
                        | true, existingIdx ->
                            let (s, rc) = stringPool.[existingIdx]
                            stringPool.[existingIdx] <- (s, rc + 1)
                            existingIdx
                        | false, _ ->
                            let newIdx = stringPool.Count
                            stringPool.Add((cleanVal, 1))
                            stringIndexMap.[cleanVal] <- newIdx
                            newIdx
                    (keyName, idx))
            (nsName, indexedEntries))

    let totalEntries =
        indexedNamespaces
        |> Array.sumBy (fun (_, entries) -> uint32 entries.Length)

    use ms = new MemoryStream()
    use bw = new BinaryWriter(ms, Encoding.UTF8, true)

    // 16-byte magic + 1-byte version (3) + 8-byte stringArrayOffset placeholder
    bw.Write(LocResMagic)
    bw.Write(3uy)
    let offsetPlaceholderPos = ms.Position
    bw.Write(0L)

    bw.Write(totalEntries)
    bw.Write(uint32 indexedNamespaces.Length)

    for (nsName, entries) in indexedNamespaces do
        bw.Write(0u) // Namespace hash
        writeFString (bw, nsName)
        bw.Write(uint32 entries.Length)
        for (keyName, strIdx) in entries do
            bw.Write(0u) // Key hash
            writeFString (bw, keyName)
            bw.Write(0u) // Source string hash
            bw.Write(strIdx)

    let stringArrayOffset = ms.Position
    bw.Write(stringPool.Count)
    for (s, refCount) in stringPool do
        writeFString (bw, s)
        bw.Write(refCount)

    ms.Seek(offsetPlaceholderPos, SeekOrigin.Begin) |> ignore
    bw.Write(stringArrayOffset)

    ms.ToArray()
