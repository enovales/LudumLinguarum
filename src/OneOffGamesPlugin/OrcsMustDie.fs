module OrcsMustDie

open ICSharpCode.SharpZipLib.Zip
open LLDatabase
open System
open System.IO
open System.Xml.Linq

/// <summary>
/// Generates a key-value pair from a String XML element in a OMD localization
/// file, which will later be transformed into a card.
/// </summary>
/// <param name="el">the XML element</param>
let internal generateKVForTextElement(el: XElement) = 
    (el.Attribute(XName.Get("_locID")).Value, el.Value)

let internal generateCardsForXElement(lessonID: int, language: string, keyRoot: string)(xel: XElement) = 
    // the element is the TextLibrary node
    xel.Descendants()
    |> Seq.filter(fun t -> t.Name.LocalName = "String")
    |> Seq.map generateKVForTextElement
    |> Seq.distinctBy(fun (_,v) -> v)
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
/// Generates a set of cards for a single asset zip from OMD.
/// </summary>
/// <param name="languageMap">the map of directory names to language codes</param>
/// <param name="lesson">the lesson record for the game text</param>
/// <param name="zipPath">path to the zip file to process</param>
let internal generateCardsForAssetZip(languageMap: Map<string, string>, lesson: LessonRecord)(zipPath: string): CardRecord array = 
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

    languageMap
    |> Map.toArray
    |> Array.collect(fun (dir: string, language: string) -> generateCardsForLessons(language)(dir, lesson))

let internal createLesson(gameID: int, db: LLDatabase)(title: string): LessonRecord = 
    let lessonEntry = {
        LessonRecord.GameID = gameID;
        ID = 0;
        Name = title
    }
    { lessonEntry with ID = db.CreateOrUpdateLesson(lessonEntry) }

let private extractOMDZips(assetZips: string array, db: LLDatabase, l: LessonRecord, languageMap: Map<string, string>) = 
    let cardKeyAndLanguage(c: CardRecord) = c.LanguageTag + c.Key

    assetZips
    |> Array.collect(generateCardsForAssetZip(languageMap, l))
    |> Array.distinctBy cardKeyAndLanguage
    |> Array.filter(fun t -> not(String.IsNullOrWhiteSpace(t.Text)))
    |> db.CreateOrUpdateCards

// Both Orcs Must Die! games have the same set of localizations.
let private languageMap = 
    [|
        (@"Localization\de", "de")
        (@"Localization\default", "en")
        (@"Localization\es", "es")
        (@"Localization\fr", "fr")
        (@"Localization\it", "it")
        (@"Localization\ja", "ja")
        (@"Localization\pl", "pl")
        (@"Localization\pt", "pt")
        (@"Localization\ru", "ru")
    |]
    |> Map.ofArray

let ExtractOrcsMustDie(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)
    let lessonEntry = configuredLessonCreator("Game Text")

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let assetZips = 
        [|
            "data.zip"
            "datademo.zip"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    extractOMDZips(assetZips, db, lessonEntry, languageMap)

let ExtractOrcsMustDie2(path: string, db: LLDatabase, g: GameRecord, args: string array) = 
    let configuredLessonCreator = createLesson(g.ID, db)
    let lessonEntry = configuredLessonCreator("Game Text")

    // load zips in reverse order, so the call to distinct will preserve the most recent ones
    let assetZips = 
        [|
            "data.zip"
        |]
        |> Array.map(fun p -> Path.Combine(path, p))

    extractOMDZips(assetZips, db, lessonEntry, languageMap)
