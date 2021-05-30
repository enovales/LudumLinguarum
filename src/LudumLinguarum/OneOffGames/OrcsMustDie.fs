module OrcsMustDie

open LLDatabase
open LLUtils
open System
open System.IO
open System.IO.Compression
open System.Xml.Linq

/// <summary>
/// Generates a key-value pair from a String XML element in a OMD localization
/// file, which will later be transformed into a card.
/// </summary>
/// <param name="el">the XML element</param>
let internal generateKVForTextElement(el: XElement) = 
  (el.Attribute(XName.Get("_locID")).Value, el.Value.Trim())

/// <summary>
/// Generates a set of cards for localized strings underneath a root XML element. A language
/// name to use can be passed in, or it can be determined from the 'name' attribute on the
/// Language node.
/// 
/// (Some games using this format have the language name correctly specified in the XML file.
/// Others do not, so this needs to be specified manually.)
/// </summary>
/// <param name="lessonID">the lesson ID to use for the generated cards</param>
/// <param name="languageOpt">the optional language to use for the cards -- if None, 
/// then the language will be autodetected.</param>
/// <param name="keyRoot">root name for the Key field of the generated cards</param>
/// <param name="xel">XML element to process</param>
let internal generateCardsForXElement(lessonID: int, languageOpt: string option, keyRoot: string)(xel: XElement) = 
  let language = 
    match languageOpt with
    | Some(l) -> 
        // use the language that was specified
        l
    | None ->
        // use the language name from the 'Language' node's 'name' attribute.
        let languageNode = 
          xel.Descendants()
          |> Seq.find(fun t -> t.Name.LocalName = "Language")
            
        let languageName = languageNode.Attribute(XName.Get("name")).Value
        match languageName with
        | "English" -> "en"
        | "French" -> "fr"
        | "German" -> "de"
        | "Spanish" -> "es"
        | "Italian" -> "it"
        | _ -> failwith "unrecognized language name in Language name attribute"

  // the element is the TextLibrary node
  xel.Descendants()
  |> Seq.filter(fun t -> t.Name.LocalName = "String")
  |> Seq.map generateKVForTextElement
  |> Seq.distinctBy(fun (_,v) -> v)
  |> Map.ofSeq
  |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "masculine")

let internal generateCardsForXmlStream(lessonID: int, languageOpt: string option, keyRoot: string)(stream: Stream) = 
  let xel = XElement.Load(stream)
  generateCardsForXElement(lessonID, languageOpt, keyRoot)(xel)

/// <summary>
/// Generates a set of cards for a single localization XML file.
/// </summary>
/// <param name="lessonID">lesson ID to use for generated cards</param>
/// <param name="language">the language of this content</param>
/// <param name="keyRoot">root to use for keys in the generated cards -- should correspond to the file name from which the content was pulled</param>
/// <param name="xmlContent">the XML content to parse</param>
let internal generateCardsForXml(lessonID: int, languageOpt: string option, keyRoot: string)(xmlContent: string) = 
  use stringReader = new StringReader(xmlContent)
  let xel = XElement.Load(stringReader)
  generateCardsForXElement(lessonID, languageOpt, keyRoot)(xel)

/// <summary>
/// Generates a set of cards for a single asset zip from OMD.
/// </summary>
/// <param name="languageMap">the map of directory names to language codes</param>
/// <param name="lesson">the lesson record for the game text</param>
/// <param name="zipPath">path to the zip file to process</param>
let internal generateCardsForAssetZip(languageMap: Map<string, string>, lesson: LessonRecord)(zipPath: string): CardRecord array = 
  use zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
  use zipFile = new ZipArchive(zipStream)

  let generateCardsForLessons(language: string)(lessonDir: string, lesson: LessonRecord) = 
    // open all XML files under the directory, and read the contents
    let isXmlFileInLessonDir(ze: ZipArchiveEntry) = 
      let fileDir = Path.GetDirectoryName(ze.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)).Trim(Path.DirectorySeparatorChar)

      // unify path separators
      let lessonDirUnifiedPathSeps = lessonDir.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar)
      let isInLessonDir = fileDir = lessonDirUnifiedPathSeps

      let hasXmlExtension = Path.GetExtension(ze.Name).ToLowerInvariant() = ".xml"
      isInLessonDir && hasXmlExtension 

    let zipEntries = 
      zipFile.Entries
      |> Seq.filter isXmlFileInLessonDir

    let getStreamForZipEntry(ze: ZipArchiveEntry) = 
      use zipStream = ze.Open()
      let memoryStream = new MemoryStream()
      zipStream.CopyTo(memoryStream)
      memoryStream.Seek(int64 0, SeekOrigin.Begin) |> ignore
      memoryStream

    zipEntries
    |> Seq.collect(getStreamForZipEntry >> generateCardsForXmlStream(lesson.ID, Some(language), lesson.Name))
    |> Array.ofSeq

  languageMap
  |> Map.toArray
  |> Array.collect(fun (dir: string, language: string) -> generateCardsForLessons(language)(dir, lesson))

let private extractOMDZips(assetZips: string array, l: LessonRecord, languageMap: Map<string, string>) = 
  let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key

  assetZips
  |> Array.collect(generateCardsForAssetZip(languageMap, l))
  |> Array.distinctBy cardKeyAndLanguage

// Both Orcs Must Die! games have the same set of localizations.
let private languageMap = 
  [|
    (FixPathSeps @"Localization\de", "de")
    (FixPathSeps @"Localization\default", "en")
    (FixPathSeps @"Localization\es", "es")
    (FixPathSeps @"Localization\fr", "fr")
    (FixPathSeps @"Localization\it", "it")
    (FixPathSeps @"Localization\ja", "ja")
    (FixPathSeps @"Localization\pl", "pl")
    (FixPathSeps @"Localization\pt", "pt")
    (FixPathSeps @"Localization\ru", "ru")
  |]
  |> Map.ofArray

let private extractOMDGame(assetZips: string array) =
  let lessonEntry = 
    {
      LessonRecord.ID = 0;
      Name = "Game Text"
    }

  let cards = extractOMDZips(assetZips, lessonEntry, languageMap)
  {
    LudumLinguarumPlugins.ExtractedContent.lessons = [| lessonEntry |]
    LudumLinguarumPlugins.ExtractedContent.cards = cards
  }

let ExtractOrcsMustDie(path: string) = 
  // load zips in reverse order, so the call to distinct will preserve the most recent ones
  let assetZips = 
    [|
      "data.zip"
      "datademo.zip"
    |]
    |> Array.map(fun p -> Path.Combine(path, p))

  extractOMDGame(assetZips)

let ExtractOrcsMustDie2(path: string) = 
  // load zips in reverse order, so the call to distinct will preserve the most recent ones
  let assetZips = 
    [|
      "data.zip"
    |]
    |> Array.map(fun p -> Path.Combine(path, p))

  extractOMDGame(assetZips)
