module CodemastersGames

open LLDatabase
open LLUtils
open StreamTools
open System
open System.Text
open System.IO
open StreamTools

type LngtChunk = 
  {
    size: uint32
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
      {
        LngtChunk.size = rw.ReadUInt32()
      }

type HshsChunk = 
  {
    size: uint32
    buckets: uint32
    seed: uint32
    multiplier: uint32
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
     {
       HshsChunk.size = rw.ReadUInt32()
       buckets = rw.ReadUInt32()
       seed = rw.ReadUInt32()
       multiplier = rw.ReadUInt32()
     }

type HshtChunk = 
  {
    size: uint32
    HashEntries: (uint32 * uint32) array
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
      let n = rw.ReadUInt32()

      {
        HshtChunk.size = n
        HashEntries = seq { for _ in uint32 1..n / 8u do yield (rw.ReadUInt32(), rw.ReadUInt32()) } |> Array.ofSeq
      }

type SidaChunk = 
  {
    size: uint32
    KeyValueOffsets: (uint32 * uint32) array
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
      let chunkSize = rw.ReadUInt32()
      let recordCount = rw.ReadUInt32()

      {
        SidaChunk.size = chunkSize
        KeyValueOffsets = seq { for _ in uint32 1..recordCount do yield (rw.ReadUInt32(), rw.ReadUInt32()) } |> Array.ofSeq
      }

type SidbChunk = 
  {
    size: uint32
    data: byte array
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
      let size = rw.ReadUInt32()
      {
        SidbChunk.size = size
        data = rw.ReadBytes(int size)
      }

type LngbChunk = 
  {
    size: uint32
    data: byte array
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
      let size = rw.ReadUInt32()
      {
        LngbChunk.size = size
        data = rw.ReadBytes(int size)
      }

type LngFile = 
  {
    lngt: LngtChunk option
    hshs: HshsChunk option
    hsht: HshtChunk option
    sida: SidaChunk option
    sidb: SidbChunk option
    lngb: LngbChunk option
  }
  with
    static member FromReader(rw: EndianReaderWrapper) = 
      let mutable r = {
          LngFile.lngt = None
          hshs = None
          hsht = None
          sida = None
          sidb = None
          lngb = None
        }

      while (r.lngt.IsNone || r.hshs.IsNone || r.hsht.IsNone || r.sida.IsNone || r.sidb.IsNone || r.lngb.IsNone) do
        r <- r.ReadNextChunk(rw)

      r

    member private this.ReadNextChunk(rw: EndianReaderWrapper) = 
      match Encoding.ASCII.GetString(rw.ReadBytes(4)) with
      | "LNGT" ->
        {
          this with lngt = Some(LngtChunk.FromReader(rw))
        }
      | "HSHS" ->
        {
          this with hshs = Some(HshsChunk.FromReader(rw))
        }
      | "HSHT" ->
        {
          this with hsht = Some(HshtChunk.FromReader(rw))
        }
      | "SIDA" ->
        {
          this with sida = Some(SidaChunk.FromReader(rw))
        }
      | "SIDB" ->
        {
          this with sidb = Some(SidbChunk.FromReader(rw))
        }
      | "LNGB" ->
        {
          this with lngb = Some(LngbChunk.FromReader(rw))
        }
      | s -> failwith("unrecognized chunk type [" + s + "]")

// read null-terminated strings
let readNextString(bytes: byte array, startPosition: int, encoding: Encoding) = 
  let mutable i = startPosition
  let mutable readMore = true
  let mutable endPosition = i
  while readMore && (i < bytes.Length) do
    if (bytes.[i] = 0uy) then
      endPosition <- i
      readMore <- false

    i <- i + 1
  
  encoding.GetString(bytes, startPosition, endPosition - startPosition)

let getStringsForLngFile(lf: LngFile, encoding: Encoding) = 
  let stringPairForKVPair(keyOffset: uint32, valueOffset: uint32) = 
    (
      readNextString(lf.sidb.Value.data, int keyOffset, encoding),
      readNextString(lf.lngb.Value.data, int valueOffset, encoding)
    )

  lf.sida
  |> Option.map(fun sida -> sida.KeyValueOffsets |> Array.map stringPairForKVPair |> Map.ofArray)

let internal readStringsFromReader(br: BinaryReader, encoding: Encoding) = 
    let wrapper = BigEndianReaderWrapper(br)
    let lngFile = LngFile.FromReader(wrapper)
    match getStringsForLngFile(lngFile, encoding) with
    | Some(strings) -> strings
    | _ -> Map.empty

let internal cardsForLanguage(lid: int)(fpl: string * (string * Encoding)) = 
    let (filePath, (language, encoding)) = fpl
    if File.Exists(filePath) then
      use br = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      readStringsFromReader(br, encoding)
      |> AssemblyResourceTools.createCardRecordForStrings(lid, "", language, "masculine")
    else
      [||]

let internal extractCodemastersEgoGame(filePathsLanguagesAndEncodings: (string * (string * Encoding)) array) = 
    let lessonEntry = {
        LessonRecord.ID = 0;
        Name = "Game Text"
    }

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
        LudumLinguarumPlugins.ExtractedContent.cards = 
            filePathsLanguagesAndEncodings
            |> Array.collect(cardsForLanguage(lessonEntry.ID))
    }
let ExtractF1RaceStars(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"data_win\localisation\language_bra.lng"
            FixPathSeps @"data_win\localisation\language_eng.lng"
            FixPathSeps @"data_win\localisation\language_fre.lng"
            FixPathSeps @"data_win\localisation\language_ger.lng"
            FixPathSeps @"data_win\localisation\language_ita.lng"
            FixPathSeps @"data_win\localisation\language_jap.lng"
            FixPathSeps @"data_win\localisation\language_pol.lng"
            FixPathSeps @"data_win\localisation\language_spa.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("es", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractF12011(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_bra.lng"
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_jpn.lng"
            FixPathSeps @"language\language_pol.lng"
            FixPathSeps @"language\language_por.lng"
            FixPathSeps @"language\language_rus.lng"
            FixPathSeps @"language\language_spa.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("pt-PT", Encoding.UTF8)
            ("ru", Encoding.UTF8)
            ("es", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractF12012(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_bra.lng"
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_jpn.lng"
            FixPathSeps @"language\language_pol.lng"
            FixPathSeps @"language\language_rus.lng"
            FixPathSeps @"language\language_spa.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("ru", Encoding.UTF8)
            ("es", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractF12014(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_bra.lng"
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_jpn.lng"
            FixPathSeps @"language\language_pol.lng"
            FixPathSeps @"language\language_spa.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("es", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractF12015(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"localisation\language_bra.lng"
            FixPathSeps @"localisation\language_eng.lng"
            FixPathSeps @"localisation\language_fre.lng"
            FixPathSeps @"localisation\language_ger.lng"
            FixPathSeps @"localisation\language_ita.lng"
            FixPathSeps @"localisation\language_jap.lng"
            FixPathSeps @"localisation\language_pol.lng"
            FixPathSeps @"localisation\language_rus.lng"
            FixPathSeps @"localisation\language_spa.lng"
            FixPathSeps @"localisation\language_zhs.lng"
            FixPathSeps @"localisation\language_zht.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("ru", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("zh-CN", Encoding.UTF8)
            ("zh-TW", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractF12016(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"localisation\language_bra.lng"
            FixPathSeps @"localisation\language_eng.lng"
            FixPathSeps @"localisation\language_fre.lng"
            FixPathSeps @"localisation\language_ger.lng"
            FixPathSeps @"localisation\language_ita.lng"
            FixPathSeps @"localisation\language_jap.lng"
            FixPathSeps @"localisation\language_pol.lng"
            FixPathSeps @"localisation\language_rus.lng"
            FixPathSeps @"localisation\language_spa.lng"
            FixPathSeps @"localisation\language_zhs.lng"
            FixPathSeps @"localisation\language_zht.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("ru", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("zh-CN", Encoding.UTF8)
            ("zh-TW", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractDirt(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\languagedata_english.lng"
            FixPathSeps @"language\languagedata_french.lng"
            FixPathSeps @"language\languagedata_german.lng"
            FixPathSeps @"language\languagedata_italian.lng"
            FixPathSeps @"language\languagedata_spanish.lng"
            FixPathSeps @"language\languagedata_us.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("en-GB", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("en-US", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractDirt2(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_jpn.lng"
            FixPathSeps @"language\language_pol.lng"
            FixPathSeps @"language\language_rus.lng"
            FixPathSeps @"language\language_spa.lng"
            FixPathSeps @"language\language_use.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("en-GB", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("ru", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("en-US", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractGrid(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_eng_dlc_0.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_fre_dlc_0.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ger_dlc_0.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_ita_dlc_0.lng"
            FixPathSeps @"language\language_jpn_dlc_0.lng"
            FixPathSeps @"language\language_spa.lng"
            FixPathSeps @"language\language_spa_dlc_0.lng"
            FixPathSeps @"language\language_use.lng"
            FixPathSeps @"language\language_use_dlc_0.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("en-GB", Encoding.UTF8)
            ("en-GB", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("en-US", Encoding.UTF8)
            ("en-US", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractGrid2(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_bra.lng"
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_jpn.lng"
            FixPathSeps @"language\language_pol.lng"
            FixPathSeps @"language\language_spa.lng"
            FixPathSeps @"language\language_use.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en-GB", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("en-US", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)

let ExtractGridAutosport(path: string) = 
    let filePaths = 
        [|
            FixPathSeps @"language\language_bra.lng"
            FixPathSeps @"language\language_eng.lng"
            FixPathSeps @"language\language_fre.lng"
            FixPathSeps @"language\language_ger.lng"
            FixPathSeps @"language\language_ita.lng"
            FixPathSeps @"language\language_jpn.lng"
            FixPathSeps @"language\language_pol.lng"
            FixPathSeps @"language\language_rus.lng"
            FixPathSeps @"language\language_spa.lng"
            FixPathSeps @"language\language_use.lng"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    let languagesAndEncodings = 
        [|
            ("pt-BR", Encoding.UTF8)
            ("en-GB", Encoding.UTF8)
            ("fr", Encoding.UTF8)
            ("de", Encoding.UTF8)
            ("it", Encoding.UTF8)
            ("ja", Encoding.UTF8)
            ("pl", Encoding.UTF8)
            ("ru", Encoding.UTF8)
            ("es", Encoding.UTF8)
            ("en-US", Encoding.UTF8)
        |]

    extractCodemastersEgoGame(Array.zip filePaths languagesAndEncodings)
