module PillarsOfEternity

open ICSharpCode.SharpZipLib.Zip
open LLDatabase
open System
open System.IO
open System.Text
open System.Xml.Linq

/// <summary>
/// Generates a key-value pair from a Text XML element in a PQ2 localization
/// file, which will later be transformed into a card.
/// </summary>
/// <param name="el">the XML element</param>
let internal generateKVForTextElement(el: XElement) = 
    (el.Attribute(XName.Get("tag")).Value, el.Value)

let internal generateCardsForXElement(lessonID: int, language: string, keyRoot: string)(xel: XElement) = 
    // the element is the TextLibrary node
    xel.Descendants()
    |> Seq.filter(fun t -> t.Name.LocalName = "Text")
    |> Seq.map generateKVForTextElement
    |> Map.ofSeq
    |> AssemblyResourceTools.createCardRecordForStrings(lessonID, keyRoot, language)    

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
    let zipFile = new ZipFile(zipPath)

    let generateCardsForLessons(language: string)(lessonDir: string, lesson: LessonRecord) = 
        // open all XML files under the directory, and read the contents
        let isXmlFileInLessonDir(ze: ZipEntry) = 
            let isFile = ze.IsFile
            let fileDir = Path.GetDirectoryName(ze.Name.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)).Trim(Path.DirectorySeparatorChar)

            // unify path separators
            let lessonDirUnifiedPathSeps = lessonDir.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar)
            let isInLessonDir = fileDir = lessonDirUnifiedPathSeps

            let hasXmlExtension = Path.GetExtension(ze.Name).ToLowerInvariant() = ".xml"
            isFile && isInLessonDir && hasXmlExtension 

        let zipEntries = 
            seq { for i in 0..(int zipFile.Count - 1) do yield zipFile.EntryByIndex(i) }
            |> Seq.filter isXmlFileInLessonDir

        let getStreamForZipEntry(ze: ZipEntry) = 
            use zipStream = zipFile.GetInputStream(ze)
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

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let ExtractPillarsOfEternity(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)
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
        |> Array.map(fun (k, v) -> (k, configuredLessonCreator(v)))
        |> Map.ofArray

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key
    [|
        "Patch1.zip"
        "Assets.zip"
    |]
    |> Array.collect((fun p -> Path.Combine(path, p)) >> generateCardsForAssetZip(languageMap, lessonsMap))
    |> Array.distinctBy cardKeyAndLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

    ()