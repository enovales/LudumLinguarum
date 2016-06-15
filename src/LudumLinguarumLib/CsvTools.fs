module CsvTools

open System
open System.Collections
open System.Text.RegularExpressions

let private csvRegex(delimiter: string) = String.Format("""
(?:^{0})*
(?:
    (?# Double-quoted field)
    " (?# field's opening quote)
    ( (?> [^"]+ | "" )* )
    " (?# field's closing quote)
(?# ... or ...)
|
    (?# ... some non-quote/non-tab text ...)
    ( [^"{0}]+ )
)
""", delimiter
)

let internal lineRegex(delimiter: string) = new Regex(csvRegex(delimiter), RegexOptions.IgnorePatternWhitespace)
let internal quotesRegex = new Regex("\"\"")
let extractFieldsForLine(delimiter: string)(line: string): string array = 
    let matches = lineRegex(delimiter).Matches(line)
    let it = matches.GetEnumerator()
    let unfoldMatches(state: IEnumerator) = 
        if state.MoveNext() then
            Some(state.Current :?> Match, state)
        else
            None

    let getStringForMatch(m: Match) = 
        if (m.Groups.[1].Success) then
            m.Groups.[1].Value.Replace("\"\"", "\"")
        else
            m.Groups.[2].Value

    Seq.unfold unfoldMatches it
    |> Seq.map getStringForMatch
    |> Array.ofSeq

