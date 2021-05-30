module PuzzleQuestGames

open LLDatabase
open System
open System.IO
open System.IO.Compression
open System.Text
open System.Xml.Linq

/// <summary>
/// Generates a key-value pair from a Text XML element in a Puzzle Quest-engine localization
/// file, which will later be transformed into a card.
/// </summary>
/// <param name="el">the XML element</param>
let internal generateKVForTextElement(el: XElement) = 
  (el.Attribute(XName.Get("tag")).Value, el.Value)

/// <summary>
/// Generates a key-value pair from a CutScene Text XML element, from Puzzle Kingdoms.
/// </summary>
/// <param name="el">the text element</param>
let internal generateKVForCutsceneTextElement(el: XElement) = 
  (el.Attribute(XName.Get("time")).Value, el.Value)

/// <summary>
/// Generates cards for the top-level node in a Puzzle Quest-engine localization file.
/// </summary>
/// <param name="lessonID">lesson ID to use</param>
/// <param name="language">language to use</param>
/// <param name="keyRoot">key root for generating cards</param>
/// <param name="xel">the XML root element</param>
let internal generateCardsForXElement(lessonID: int, language: string, keyRoot: string)(xel: XElement) = 
  let cutsceneName = XName.Get("CutScene")
  let elementExtractor = 
    match xel.Name with
    | n when n = cutsceneName -> generateKVForCutsceneTextElement
    | _ -> generateKVForTextElement

  // the element is the TextLibrary node
  xel.Descendants()
  |> Seq.filter(fun t -> t.Name.LocalName = "Text")
  |> Seq.map elementExtractor
  |> Map.ofSeq
  |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language, "masculine")

let internal generateCardsForXmlStream(lessonID: int, language: string, keyRoot: string)(stream: Stream) = 
  let xel = XElement.Load(stream)
  generateCardsForXElement(lessonID, language, keyRoot)(xel)

/// <summary>
/// Generates a set of cards for a single localization XML file.
/// </summary>
/// <param name="lessonID">lesson ID to use for generated cards</param>
/// <param name="language">the language of this content</param>
/// <param name="keyRoot">root to use for keys in the generated cards -- should correspond to the file name from which the content was pulled</param>
/// <param name="xmlContent">the XML content to parse</param>
let internal generateCardsForXml(lessonID: int, language: string, keyRoot: string)(xmlContent: string) = 
  use stringReader = new StringReader(xmlContent)
  let xel = XElement.Load(stringReader)
  generateCardsForXElement(lessonID, language, keyRoot)(xel)

/// <summary>
/// Generates a set of cards for a single asset zip from PQ2.
/// </summary>
/// <param name="languageMap">the map of directory names to language codes</param>
/// <param name="lessonsMap">the map of directory paths (inside the zip) to lesson records</param>
/// <param name="zipPath">path to the zip file to process</param>
let internal generateCardsForAssetZip(languageMap: Map<string, string>, lessonsMap: Map<string, LessonRecord>)(zipPath: string): CardRecord array = 
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
    |> Seq.collect(getStreamForZipEntry >> generateCardsForXmlStream(lesson.ID, language, lesson.Name))
    |> Array.ofSeq

  let generateCardsForLanguage(dir: string, language: string): CardRecord array = 
    lessonsMap
    |> Map.toArray
    |> Array.map (fun (lessonDir, lesson) -> (Path.Combine(dir, lessonDir), lesson))
    |> Array.collect(generateCardsForLessons(language))

  languageMap
  |> Map.toArray
  |> Array.collect generateCardsForLanguage

let internal createLesson(i: int)(title: string): LessonRecord = 
  {
    LessonRecord.ID = i;
    Name = title
  }

let ExtractPuzzleQuest2(path: string) = 
  let languageMap = 
    [|
      ("English", "en")
      ("French", "fr")
      ("German", "de")
      ("Italian", "it")
      ("Spanish", "es")
    |]
    |> Map.ofArray

  // create lessons for each of the subdirectories in the asset zips
  let lessonsMap = 
    [|
      ("", "Game Text")
      ("Conversations", "Conversations")
      ("Levels", "Levels")
      ("NIS", "NIS")
      ("pc", "PC")
      ("Tutorials", "Tutorials")
    |]
    |> Array.mapi(fun i (k, v) -> (k, createLesson(i)(v)))
    |> Map.ofArray

  // load zips in reverse order, so the call to distinct will preserve the most recent ones
  let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key

  let cards = 
    [|
      "Patch1.zip"
      "Assets.zip"
    |]
    |> Array.collect((fun p -> Path.Combine(path, p)) >> generateCardsForAssetZip(languageMap, lessonsMap))
    |> Array.distinctBy cardKeyAndLanguage

  let (_, lessons) = lessonsMap |> Map.toArray |> Array.unzip
  {
    LudumLinguarumPlugins.ExtractedContent.lessons = lessons
    LudumLinguarumPlugins.ExtractedContent.cards = cards
  }

let ExtractPuzzleChronicles(path: string) = 
  let languageMap = 
    [|
      ("English_eu", "en-gb")
      ("English_us", "en")
      ("French_eu", "fr")
      ("French_us", "fr-ca")
      ("German_eu", "de")
      ("Italian_eu", "it")
      ("Spanish_eu", "es")
      ("Spanish_us", "es-mx")
    |]
    |> Map.ofArray

  // create lessons for each of the subdirectories in the asset zips
  let lessonsMap = 
    [|
      ("", "Game Text")
      ("NIS", "NIS")
      ("Quests", "Quests")
      ("Tutorials", "Tutorials")
    |]
    |> Array.mapi(fun i (k, v) -> (k, createLesson(i)(v)))
    |> Map.ofArray

  // load zips in reverse order, so the call to distinct will preserve the most recent ones
  let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key
  let cards = 
    [|
      "Assets.zip"
    |]
    |> Array.collect((fun p -> Path.Combine(path, p)) >> generateCardsForAssetZip(languageMap, lessonsMap))
    |> Array.distinctBy cardKeyAndLanguage

  let (_, lessons) = lessonsMap |> Map.toArray |> Array.unzip
  {
    LudumLinguarumPlugins.ExtractedContent.lessons = lessons
    LudumLinguarumPlugins.ExtractedContent.cards = cards
  }

let ExtractPuzzleKingdoms(path: string) = 
  let languageMap = 
    [|
      ("English", "en")
      ("French", "fr")
      ("German", "de")
      ("Italian", "it")
      ("Spanish", "es")
    |]
    |> Map.ofArray

  // create lessons for each of the subdirectories in the asset zips
  let lessonsMap = 
    [|
      ("", "Game Text")
      ("Conversations", "Conversations")
      ("CutScenes", "Cutscenes")
      ("pc", "pc")
      ("Tutorials", "Tutorials")
    |]
    |> Array.mapi(fun i (k, v) -> (k, createLesson(i)(v)))
    |> Map.ofArray

  // load zips in reverse order, so the call to distinct will preserve the most recent ones
  let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key

  let cards = 
    [|
      "Assets.zip"
    |]
    |> Array.collect((fun p -> Path.Combine(path, p)) >> generateCardsForAssetZip(languageMap, lessonsMap))
    |> Array.distinctBy cardKeyAndLanguage

  let (_, lessons) = lessonsMap |> Map.toArray |> Array.unzip
  {
    LudumLinguarumPlugins.ExtractedContent.lessons = lessons
    LudumLinguarumPlugins.ExtractedContent.cards = cards
  }
