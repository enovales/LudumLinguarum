module SjsonTools

open FParsec

open System
open System.IO
open System.Text
open System.Text.RegularExpressions

// Tools to deal with "simplified JSON" and related files, as used by Autodesk Stingray and Bitsquid.
//
// https://help.autodesk.com/cloudhelp/ENU/Stingray-Help/stingray_help/managing_content/sjson.html
// http://bitsquid.blogspot.com/2009/10/simplified-json-notation.html
//
// Our general strategy is to convert simplified JSON files into standard JSON files, so that we can use
// standard tools on their contents.

type SjsonNode =
    | SjsonNull
    | SjsonBool of bool
    | SjsonNumber of float
    | SjsonString of string
    | SjsonArray of List<SjsonNode>
    | SjsonObject of Map<string, SjsonNode>

module SjsonGrammar =
    open FParsec

    let (<!>) (p: Parser<_,_>) label : Parser<_,_> =
        fun stream ->
            printfn "%A: Entering %s" stream.Position label
            let reply = p stream
            match box reply.Result with
            | null ->
                printfn "%A: Leaving %s (%A)" stream.Position label reply.Status
            | _ ->
                printfn "%A: Leaving %s (%A) (%A)" stream.Position label reply.Status reply.Result

            reply

    let sjsonNull: Parser<_, Unit> = stringReturn "null" SjsonNull .>> spaces <!> "sjsonNull"
    let sjsonBoolTrue: Parser<_, Unit> = stringReturn "true" <| SjsonBool true .>> spaces <!> "sjsonBoolTrue"
    let sjsonBoolFalse: Parser<_, Unit> = stringReturn "false" <| SjsonBool false .>> spaces <!> "sjsonBoolFalse"
    let sjsonBool: Parser<_, Unit> = sjsonBoolTrue <|> sjsonBoolFalse <!> "sjsonBool"
    let sjsonNumber: Parser<_, Unit> = pfloat .>> spaces |>> SjsonNumber <!> "sjsonNumber"

    // Applies popen, then pchar repeatedly until pclose succeeds,
    // returns the string in the middle
    let manyCharsBetween popen pclose pchar = popen >>? manyCharsTill pchar pclose

    // Parses any string between popen and pclose
    let anyStringBetween popen pclose = manyCharsBetween popen pclose anyChar

    // Parses any string between double quotes
    let quotedString: Parser<_, Unit> = skipChar '"' |> anyStringBetween <| skipChar '"'
    // is equivalent to: anyStringBetween (skipChar '"') (skipChar '"')

    let sjsonString: Parser<_, Unit> = quotedString .>> spaces |>> SjsonString <!> "sjsonString"

    let sjsonLiteral: Parser<_, Unit> =
        choice [
            sjsonNull
            sjsonBool
            sjsonNumber
            sjsonString
        ] <!> "sjsonLiteral"

    let sjsonNode, sjsonNodeRef = createParserForwardedToRef<SjsonNode, Unit>()

    let manyContained popen pclose psep p = between popen pclose <| sepBy p psep

    let optionalCommaSeparator: Parser<_, Unit> =
        choice [
            spaces >>. skipChar ',' .>> spaces
            spaces >>. lookAhead(skipNoneOf ['}'; ']'])
        ]

    let sjsonArray =
        sjsonNode                       // parse JSON nodes...
        |> manyContained               // contained within...
            (skipChar '[' .>> spaces)  // opening square bracket...
            (skipChar ']' .>> spaces)  // and closing square bracket...
            optionalCommaSeparator
        |>> SjsonArray <!> "sjsonArray"

    let quotedStringLiteral: Parser<_, Unit> =
        let normalChar = satisfy (fun c -> c <> '\\' && c <> '"')
        let unescape c = match c with
                         | 'n' -> '\n'
                         | 'r' -> '\r'
                         | 't' -> '\t'
                         | c   -> c
        let escapedChar = pstring "\\" >>. (anyOf "\\nrt\"" |>> unescape)
        between (pstring "\"") (pstring "\"")
                (manyChars (normalChar <|> escapedChar))

    let unquotedStringLiteral: Parser<_, Unit> =
        many1Satisfy (function ' ' | '\t' | '\n' | '}' | ']' -> false | _ -> true)

    let sjsonKeyString: Parser<_, Unit> =
        choice [
            quotedStringLiteral <!> "sjsonKeyString.quotedStringLiteral"
            unquotedStringLiteral <!> "sjsonKeyString.unquotedStringLiteral"
        ]

    let sjsonProperty =
        sjsonKeyString .>> spaces .>> skipChar '=' .>> spaces .>>. sjsonNode .>> spaces <!> "sjsonProperty"

    let sjsonObject =
        sjsonProperty
        |> manyContained
            (skipChar '{' .>> spaces)
            (skipChar '}' .>> spaces)
            optionalCommaSeparator
        |>> Map.ofList
        |>> SjsonObject <!> "sjsonObject"

    do sjsonNodeRef.Value <- choice [ sjsonObject; sjsonArray; sjsonLiteral ]

    let sjsonFileScopeContainer =
        spaces >>. manyTill (sjsonProperty .>> spaces) eof |>> Map.ofList |>> SjsonObject <!> "sjsonFileScopeContainer"

    let sjsonTestString = quotedString .>> spaces .>> skipChar '=' .>> spaces .>>. sjsonNode .>> spaces <!> "sjsonTestString"
    let sjsonTestParser =
        spaces >>. manyTill (sjsonProperty .>> spaces) eof |>> Map.ofList |>> SjsonObject <!> "sjsonTestParser"

    let sjsonTestParser2 =
        spaces >>. many (sjsonKeyString .>> spaces) <!> "sjsonTestParser2"

    let rec printJson(node: SjsonNode, writer: TextWriter) =
        match node with
        | SjsonNull -> writer.Write("null")
        | SjsonBool b -> writer.Write(b.ToString())
        | SjsonNumber n -> writer.Write(n.ToString())
        | SjsonString s -> writer.Write("\"" + s + "\"")
        | SjsonArray a ->
            writer.WriteLine("[")
            a |> List.iteri(fun i v ->
                printJson(v, writer)
                if i < (a.Length - 1) then
                    writer.WriteLine(",")
            )
            writer.WriteLine()
            writer.WriteLine("]")
        | SjsonObject o ->
            writer.WriteLine("{")
            o |> Map.toList |> List.iteri (fun i (k, v) ->
                writer.Write("\"" + k + "\": ")
                printJson(v, writer)
                if i < (o.Count - 1) then
                    writer.WriteLine(",")
            )
            writer.WriteLine()
            writer.WriteLine("}")



let sjsonToJSON(sjson: string): string =
    let parsedSjson = run SjsonGrammar.sjsonFileScopeContainer sjson
    match parsedSjson with
    | ParserResult.Failure _ ->
        failwith "failed to parse SJSON successfully"
    | ParserResult.Success (result, _, _) ->
        use sw = new StringWriter()
        SjsonGrammar.printJson(result, sw)
        sw.ToString()
    
    (*
    let testString = """
    "foo" = "bar"
    "baz" = "boo"
    bonk = [1.0 2.0 3.0
            4.0, 5.0]
    goo = {
        blargh = 3.0,
        yoink = false
        beep = "meep"
    }
    """
    let parsedSjson = run SjsonGrammar.sjsonFileScopeContainer testString
    printfn "%O" parsedSjson

    //let testString2 = """foo bar baz blah"""
    //let parsedSjson2 = run SjsonGrammar.sjsonTestParser2 testString2
    //printfn "%O" parsedSjson2
    *)

(*
open System.Text.RegularExpressions

let rec parseSJSON (text: string) (result: System.Text.StringBuilder) (idx: int) =
    let appendCommaIfNeeded (idx: int) =
        if idx > 0 && result.Length > 0 && result.[result.Length - 1] <> '{' && result.[result.Length - 1] <> '[' &&
           result.[result.Length - 1] <> ',' && result.[result.Length - 1] <> ':' then
            result.Append(',') |> ignore
        ()
    
    let parseKey (idx: int) =
        let rec loop (idx: int) =
            if idx >= text.Length || Char.IsWhiteSpace(text.[idx]) || text.[idx] = '=' then
                idx
            else
                result.Append(text.[idx]) |> ignore
                loop (idx + 1)
        
        result.Append('"') |> ignore
        let nextIdx = loop idx
        result.Append('"') |> ignore
        nextIdx

    if idx >= text.Length then
        ()
    else
        match text.[idx] with
        | '{' ->
            appendCommaIfNeeded idx
            result.Append('{') |> ignore
            parseSJSON text result (idx + 1)
        | '}' ->
            result.Append('}') |> ignore
            parseSJSON text result (idx + 1)
        | '[' ->
            appendCommaIfNeeded idx
            result.Append('[') |> ignore
            parseSJSON text result (idx + 1)
        | ']' ->
            result.Append(']') |> ignore
            parseSJSON text result (idx + 1)
        | '=' ->
            result.Append(':') |> ignore
            parseSJSON text result (idx + 1)
        | '/' when (idx + 1 < text.Length) && (text.[idx + 1] = '/') ->
            let nextIdx = text.IndexOf('\n', idx)
            if nextIdx = -1 then () else parseSJSON text result (nextIdx + 1)
        | '/' when (idx + 1 < text.Length) && (text.[idx + 1] = '*') ->
            let nextIdx = text.IndexOf("*/", idx)
            if nextIdx = -1 then () else parseSJSON text result (nextIdx + 2)
        | '"' ->
            appendCommaIfNeeded idx
            result.Append('"') |> ignore
            let rec parseString (idx: int) =
                if idx >= text.Length || text.[idx] = '"' then
                    result.Append('"') |> ignore
                else
                    result.Append(text.[idx]) |> ignore
                    parseString (idx + 1)
            parseString (idx + 1)
            parseSJSON text result (idx + 2)
        | c when Char.IsWhiteSpace(c) ->
            parseSJSON text result (idx + 1)
        | c ->
            appendCommaIfNeeded idx
            let nextIdx = parseKey idx
            parseSJSON text result nextIdx

let sjsonToJSON (sjson: string) =
    let result = System.Text.StringBuilder()
    parseSJSON sjson result 0
    result.ToString()

*)

// Attempt to convert a file containing Lua data declarations into a simplified JSON file, which can then
// be converted into standard JSON.
