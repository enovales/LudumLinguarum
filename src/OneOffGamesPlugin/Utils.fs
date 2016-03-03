module OneOffGamesUtils

open System
open System.Globalization
open System.IO
open System.Reflection

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

    let generateLanguageAssemblyResourceTripleFromPath(p: string): string * Assembly * string = 
        let language = generateLanguagesFromPath(p)
        let resourceName = resourceNameRoot + "." + language + ".resources"
        (language, Assembly.ReflectionOnlyLoadFrom(p), resourceName)

    let generateCardsForLanguage(language: string, a: Assembly, resourcesName: string) = 
        AssemblyResourceTools.extractResourcesFromAssemblyViaResourceReader(a, CultureInfo.GetCultureInfo(language), resourcesName)
        |> AssemblyResourceTools.createCardRecordForStrings(lessonId, cardKeyRoot, language)

    // set up default assembly resolution during reflection-only loads.
    let provideAssembly(o: obj)(args: ResolveEventArgs): Assembly = 
        Assembly.ReflectionOnlyLoad(args.Name)
    let provideAssemblyHandler = new ResolveEventHandler(provideAssembly)
    AppDomain.CurrentDomain.add_ReflectionOnlyAssemblyResolve(provideAssemblyHandler)

    try
        let mainAssembly = Assembly.ReflectionOnlyLoadFrom(mainAssemblyPath)            
        let fallbackLanguageCards = generateCardsForLanguage("en", mainAssembly, resourceNameRoot + ".resources")
        let otherLanguageCards = (otherAssemblyPaths |> Array.map generateLanguageAssemblyResourceTripleFromPath) |> Array.collect generateCardsForLanguage

        // if there is a satellite assembly for English, ignore the fallback language (assuming it's English). Otherwise,
        // merge those cards in.
        if (otherLanguageCards |> Array.exists(fun t -> t.LanguageTag = "en")) then
            otherLanguageCards
        else
            Array.concat([| otherLanguageCards; fallbackLanguageCards |])
    finally
        AppDomain.CurrentDomain.remove_ReflectionOnlyAssemblyResolve(provideAssemblyHandler)            
