module AgeOfEmpiresGames

open LLDatabase
open System
open System.IO
open System.Text
open System.Text.RegularExpressions

let private formattingRegexes = 
    [|
        new Regex(@"\<.*\>")
    |]

let private stripFormattingTags(s: string) = 
    Array.fold (fun (a: string)(r: Regex) -> r.Replace(a, "")) s formattingRegexes

let private trimString(s: string) = s.Trim()
let private replaceEscapeChars(s: string) = s.Replace(@"\r", " ").Replace(@"\n", " ")
let private consolidateWhitespace(s: string) = Regex.Replace(s, @"\s+", " ")

let private sanitizePipeline =
    stripFormattingTags >> replaceEscapeChars >> consolidateWhitespace >> trimString

/////////////////////////////////////////////////////////////////////////////
// Age of Empires II HD

let private aoe2HDLanguages = [| "br"; "de"; "en"; "es"; "fr"; "it"; "jp"; "ko"; "nl"; "ru"; "zh" |]

let internal createLesson(i: int)(title: string): LessonRecord = 
    {
        LessonRecord.ID = i;
        Name = title
    }

let private aoe2MassageText(v: string) = 
    let removeStrings = [| "<b>"; "<B>"; "<i>"; "<I>"; "<GREY>"; "<BLUE>"; "<PURPLE>"; "<GREEN>"; "<AQUA>"; "<RED>"; "<YELLOW>"; "<ORANGE>" |]
    let trimmed = v.TrimStart([| '"' |]).TrimEnd([| '"' |])
    let removed = removeStrings |> Array.fold(fun (s: string)(toRemove: string) -> s.Replace(toRemove, "")) trimmed
    removed.Replace(@"\n", Environment.NewLine).Trim()


let private extractAOE2HDHistoryFile(fn: string, lid: int, lang: string) = 
    AssemblyResourceTools.createCardRecordForStrings(lid, "", lang, "masculine")([| ("card", aoe2MassageText(File.ReadAllText(fn, Encoding.UTF8))) |] |> Map.ofArray)

let private getHistoryLessonName(fn: string) = 
    Path.GetFileNameWithoutExtension(fn).Replace("-utf8", "")

let private extractAOE2HDHistoryFiles(path: string) = 
    let languagesAndPathsByLessonNames = 
        aoe2HDLanguages
        |> Array.collect (fun l -> Directory.GetFiles(Path.Combine(path, @"resources\" + l + @"\strings\history")) |> Array.map (fun p -> (l, p)))
        |> Array.groupBy (fun (_, p) -> getHistoryLessonName(p))

    let (lessonNames, languagesAndPaths) = languagesAndPathsByLessonNames |> Array.unzip
    let lessons = 
        lessonNames
        |> Array.mapi(fun i n -> { LessonRecord.ID = i; Name = n })
        
    let languagesAndPathsByLessons = 
        languagesAndPaths
        |> Array.zip lessons

    languagesAndPathsByLessons
    |> Array.map (fun (lesson, lps) -> (lesson, lps |> Array.map(fun (lang, path) -> extractAOE2HDHistoryFile(path, lesson.ID, lang)) |> Array.collect id))

let private aoe2HDCampaignStringRegex = new Regex(@"^(\S+)\s+(.+)$")
let private extractAOE2HDCampaignStrings(path: string) = 
    let lesson = { LessonRecord.ID = 0; Name = "Game Text" }
    let trimLineComments(s: string) = 
        match s.IndexOf("//") with
        | i when i >= 0 -> s.Substring(0, i).Trim()
        | _ -> s

    let cardsForLanguage(lang: string) = 
        let p = Path.Combine(path, @"resources\" + lang + @"\strings\key-value\key-value-strings-utf8.txt")
        let lines = 
            File.ReadAllLines(p, Encoding.UTF8)
            |> Array.map(fun l -> l.Trim())
            |> Array.filter(fun l -> not(l.StartsWith("//")) && not(String.IsNullOrWhiteSpace(l)))
            |> Array.map trimLineComments

        let kvPairForLine(l: string) = 
            let m = aoe2HDCampaignStringRegex.Match(l)
            if m.Success then
                [| (m.Groups.[1].Value, aoe2MassageText(m.Groups.[2].Value)) |]
            else
                [||]

        lines
        |> Array.collect kvPairForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, "", lang, "masculine")


    [| (lesson, aoe2HDLanguages |> Array.collect cardsForLanguage) |]

let private aoe2HDLauncherStringRegex = new Regex(@"^([^=]+)=(.+)$")
let private extractAOE2HDLauncherStrings(path: string) = 
    let lesson = { LessonRecord.ID = 0; Name = "Launcher Text" }
    let cardsForLocaleIni(lang: string) = 
        let p = Path.Combine(path, @"launcher_res\" + lang + @"\locale.ini")
        let lines =
            File.ReadAllLines(p)
            |> Array.map (fun l -> l.Trim())
            |> Array.filter(fun l -> not(l.StartsWith("[")) && not (String.IsNullOrWhiteSpace(l)))
        let kvPairForLine(l: string) = 
            let m = aoe2HDLauncherStringRegex.Match(l)
            if m.Success then
                [| (m.Groups.[1].Value, m.Groups.[2].Value.TrimStart([| '"' |]).TrimEnd([| '"' |])) |]
            else
                [||]

        lines
        |> Array.collect kvPairForLine
        |> Map.ofArray
        |> AssemblyResourceTools.createCardRecordForStrings(lesson.ID, "", lang, "masculine")

    [| (lesson, aoe2HDLanguages |> Array.collect cardsForLocaleIni) |]

let ExtractAOE2HD(path: string) = 
    // Extract strings from the "history" files, each of which is just a single long passage.
    let allLessonGroupsAndCards = 
        [|
            extractAOE2HDHistoryFiles(path)
            extractAOE2HDCampaignStrings(path)
            extractAOE2HDLauncherStrings(path)
        |]

    // Get all lessons, and renumber them. Then apply that remapping to all of the cards.
    let originalLessonsToRemappedLessons = 
        allLessonGroupsAndCards
        |> Array.collect(fun lgc -> lgc |> Array.map(fst))
        |> Array.mapi(fun i l -> (l, { l with ID = i }))

    let originalLessonIDsToRemappedLessonIDs = 
        originalLessonsToRemappedLessons
        |> Array.map(fun (original, remapped) -> (original.ID, remapped.ID))
        |> Map.ofArray

    let remapLessonForCardGroup(mapping: Map<int, int>)(lcg: LessonRecord * CardRecord array) = 
        let (lesson, cards) = lcg
        let newLessonID = mapping |> Map.find(lesson.ID)
        cards |> Array.map(fun c -> { c with LessonID = newLessonID })

    let allCards = 
        allLessonGroupsAndCards
        |> Array.collect(fun lgc -> lgc |> Array.map(remapLessonForCardGroup(originalLessonIDsToRemappedLessonIDs)))
        |> Array.collect id

    {
        LudumLinguarumPlugins.ExtractedContent.lessons = originalLessonsToRemappedLessons |> Array.map snd
        LudumLinguarumPlugins.ExtractedContent.cards = allCards
    }

/////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////
// Age of Empires III

let ExtractAOE3(path: string) = 
    let stringSources = 
        [|
            (@"bin\data\stringtable.xml", "Base Game")
            (@"bin\data\stringtablex.xml", "Warchiefs Expansion")
            (@"bin\data\stringtabley.xml", "Asian Dynasties Expansion")
            (@"bin\data\unithelpstrings.xml", "Base Game Unit Help")
            (@"bin\data\unithelpstringsx.xml", "Warchiefs Expansion Unit Help")
            (@"bin\data\unithelpstringsy.xml", "Asian Dynasties Expansion Unit Help")
        |]

    let generateCardsForXml(xmlPath: string, lesson: LessonRecord) = 
        if (File.Exists(xmlPath)) then
            use fs = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            OrcsMustDie.generateCardsForXmlStream(lesson.ID, None, "")(fs)
            |> Array.map (fun c -> { c with Text = sanitizePipeline(c.Text) })
        else
            [||]

    let pathsToLessons = 
        stringSources
        |> Array.mapi (fun i (p, lessonName) -> (Path.Combine(path, p), { LessonRecord.ID = i; Name = lessonName }))

    let lessonsAndCardGroups = 
        pathsToLessons
        |> Array.map(fun ptl -> (snd(ptl), generateCardsForXml(ptl)))

    let lessons = lessonsAndCardGroups |> Array.map fst
    let cards = lessonsAndCardGroups |> Array.map(snd) |> Array.collect(id)

    // TODO: extract launcher strings from resource DLLs?
    {
        LudumLinguarumPlugins.ExtractedContent.lessons = lessons
        LudumLinguarumPlugins.ExtractedContent.cards = cards
    }
