module OneOffGamesUtils

open System.Globalization
open System.IO

let ExtractStringsFromAssemblies
    (
        rootPath: string, 
        mainAssemblyPath: string, 
        satelliteName: string, 
        resourceNameRoot: string,
        cardKeyRoot: string,
        lessonId: int
    ) = 
    let otherAssemblyPaths = Directory.GetFiles(rootPath, satelliteName, SearchOption.AllDirectories)
    let generateLanguagesFromPath(p: string): string = 
        let parentDirectory = Path.GetDirectoryName(p)
        let parentOfParentDirectory = Path.GetDirectoryName(parentDirectory)
        parentDirectory.Substring(parentOfParentDirectory.Length).Trim([| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |])

    let generateLanguageFileResourceTripleFromPath(p: string): string * string * string = 
        let language = generateLanguagesFromPath(p)
        let resourceName = resourceNameRoot + "." + language + ".resources"
        (language, p, resourceName)

    let generateCardsForLanguageByFile(language: string, filename: string, resourcesName: string) = 
        AssemblyResourceTools.extractResourcesFromAssemblyViaCecil(filename, CultureInfo.GetCultureInfo(language), resourcesName)
        |> AssemblyResourceTools.createCardRecordForStrings(lessonId, cardKeyRoot, language, "masculine")

    let fallbackLanguageCards = generateCardsForLanguageByFile("en", mainAssemblyPath, resourceNameRoot + ".resources")
    let otherLanguageCards = (otherAssemblyPaths |> Array.map generateLanguageFileResourceTripleFromPath) |> Array.collect generateCardsForLanguageByFile

    // if there is a satellite assembly for English, ignore the fallback language (assuming it's English). Otherwise,
    // merge those cards in.
    if (otherLanguageCards |> Array.exists(fun t -> t.LanguageTag = "en")) then
        otherLanguageCards
    else
        Array.concat([| otherLanguageCards; fallbackLanguageCards |])
