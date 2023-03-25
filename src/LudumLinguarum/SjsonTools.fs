module SjsonTools

open FParsec

open System.IO
open System.Text.Json
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

// Trace operator, for debugging parsers. https://www.quanttec.com/fparsec/users-guide/debugging-a-parser.html#tracing-a-parser
let private (<!>) (p: Parser<_,_>) label : Parser<_,_> =
    fun stream ->
        printfn "%A: Entering %s" stream.Position label
        let reply = p stream
        match box reply.Result with
        | null ->
            printfn "%A: Leaving %s (%A)" stream.Position label reply.Status
        | _ ->
            printfn "%A: Leaving %s (%A) (%A)" stream.Position label reply.Status reply.Result

        reply

// Module for the parser which is used to replace multi-line and single-line comments with a single space.
module SjsonCommentStrippingGrammar =
    type internal CommentPreprocessorNode =
        | NonCommentBlock of string
        | Comment

    let private multiLineComment: Parser<_, Unit> =
        attempt (skipChar '/' .>> skipChar '*' .>> manyCharsTill anyChar (attempt (skipChar '*' .>> skipChar '/')))
    let private lineComment: Parser<_, Unit> = attempt (skipChar '/' .>> skipChar '/' .>> skipRestOfLine false)
    let private comment =
        choice [
            multiLineComment
            lineComment
        ] >>% Comment

    let private nonComment =
        // Note that we have to use `lookAhead` for the comment/end-of-file check, because
        // we don't want to actually consume those characters right now. (We want to
        // parse the comment block on its own, so we can accurately replace it with a single
        // space.)
        manyCharsTill anyChar (choice [lookAhead comment >>% (); eof]) |>> NonCommentBlock

    let internal commentPreprocessorParser: Parser<_, Unit> =
        manyTill (choice [comment; nonComment]) eof

module SjsonGrammar =
    let private sjsonNull: Parser<_, Unit> = stringReturn "null" SjsonNull .>> spaces
    let private sjsonBoolTrue: Parser<_, Unit> = stringReturn "true" <| SjsonBool true .>> spaces
    let private sjsonBoolFalse: Parser<_, Unit> = stringReturn "false" <| SjsonBool false .>> spaces
    let private sjsonBool: Parser<_, Unit> = sjsonBoolTrue <|> sjsonBoolFalse
    let private sjsonNumber: Parser<_, Unit> = pfloat .>> spaces |>> SjsonNumber

    let internal tripleQuotedStringLiteral =
        // Check for triple quotes at the end of the file, or triple quotes followed by something that is not a quote.
        let terminalTripleQuotes =
            (pstring "\"\"\"") .>> (choice [eof; (notFollowedByL (pstring "\"") "terminal quote") >>% ()])

        // Normalize any non-standard escaped characters inside triple quotes, by using the
        // \u syntax.
        let anyCharAndFixupInvalidEscaped =
            choice [
                attempt (pchar '\\' >>. notFollowedBy (anyOf "\\'\"nrt[]") >>. anyChar)
                anyChar
            ]
        between (pstring "\"\"\"") (pstring "\"\"\"") (manyCharsTill anyCharAndFixupInvalidEscaped (choice [eof; lookAhead (attempt terminalTripleQuotes >>% ())]))

    // Parses any string between triple, double or single quotes, with escaping support
    let internal quotedStringLiteral: Parser<_, Unit> =
        let str s = pstring s
        let normalCharSnippetForDoubleQuoted = manySatisfy (fun c -> c <> '\\' && c <> '"')
        let normalCharSnippetForSingleQuoted = manySatisfy (fun c -> c <> '\\' && c <> '\'')

        let escapedChar =
            pstring "\\" >>.
//                (anyOf "\\'\"nrt[]" |>> function
                (anyChar |>> function
                         | 'n' -> @"\n"
                         | 'r' -> @"\r"
                         | 't' -> @"\t"
                         | '"' -> "\""
                         | '\'' -> "'"
                         | c   -> "\\u" + (int(c)).ToString("X4")
                )


        choice [
            tripleQuotedStringLiteral
            between (str "'") (str "'") (stringsSepBy normalCharSnippetForSingleQuoted escapedChar)
            between (str "\"") (str "\"")
                    (stringsSepBy normalCharSnippetForDoubleQuoted escapedChar)
        ]
        

    let private sjsonString: Parser<_, Unit> = quotedStringLiteral .>> spaces |>> SjsonString

    let private sjsonLiteral: Parser<_, Unit> =
        choice [
            sjsonNull
            sjsonBool
            sjsonNumber
            sjsonString
        ]

    let private sjsonNode, private sjsonNodeRef = createParserForwardedToRef<SjsonNode, Unit>()

    let private manyContained popen pclose psep p = between popen pclose <| sepEndBy p psep

    let private optionalCommaSeparator: Parser<_, Unit> =
        choice [
            spaces >>. skipChar ',' .>> spaces
            spaces >>. lookAhead(skipNoneOf ['}'; ']'])
        ]

    let private sjsonArray =
        sjsonNode                       // parse JSON nodes...
        |> manyContained               // contained within...
            (skipChar '[' .>> spaces)  // opening square bracket...
            (skipChar ']' .>> spaces)  // and closing square bracket...
            optionalCommaSeparator
        |>> SjsonArray

    let private unquotedStringLiteral: Parser<_, Unit> =
        many1Satisfy (function ' ' | '\t' | '\n' | '}' | ']' -> false | _ -> true)

    let private sjsonKeyString: Parser<_, Unit> =
        choice [
            quotedStringLiteral
            unquotedStringLiteral
        ]

    let private sjsonProperty =
        sjsonKeyString .>> spaces .>> skipChar '=' .>> spaces .>>. sjsonNode .>> spaces

    let private sjsonObject =
        sjsonProperty
        |> manyContained
            (skipChar '{' .>> spaces)
            (skipChar '}' .>> spaces)
            optionalCommaSeparator
        |>> Map.ofList
        |>> SjsonObject

    do sjsonNodeRef.Value <- choice [ sjsonObject; sjsonArray; sjsonLiteral ]

    let internal sjsonFileScopeContainer =
        spaces >>. manyTill (sjsonProperty .>> spaces) eof |>> Map.ofList |>> SjsonObject

    let private unescapedDoubleQuotesRegex = new Regex("([^\\\\])\"|^\"")

    // Used to output standard JSON, from the SJSON domain model.
    let rec internal printUnnamedJsonNodeInternal(node: SjsonNode, writer: Utf8JsonWriter) =
        match node with
        | SjsonObject o ->
            writer.WriteStartObject()
            o
            |> Seq.iter (fun kv -> printNamedJsonNodeInternal(kv.Value, kv.Key, writer))
            writer.WriteEndObject()
        | SjsonArray a ->
            writer.WriteStartArray()
            a
            |> Seq.iter (fun v -> printUnnamedJsonNodeInternal(v, writer))
            writer.WriteEndArray()
        | SjsonNull -> writer.WriteNullValue()
        | SjsonBool b -> writer.WriteBooleanValue(b)
        | SjsonNumber n -> writer.WriteNumberValue(n)
        | SjsonString s -> writer.WriteStringValue(s)
    and internal printNamedJsonNodeInternal(node: SjsonNode, name: string, writer: Utf8JsonWriter) =
        match node with
        | SjsonObject o ->
            writer.WriteStartObject(name)
            o
            |> Seq.iter (fun kv -> printNamedJsonNodeInternal(kv.Value, kv.Key, writer))
            writer.WriteEndObject()
        | SjsonArray a ->
            writer.WriteStartArray(name)
            a
            |> Seq.iter (fun v -> printUnnamedJsonNodeInternal(v, writer))
            writer.WriteEndArray()
        | SjsonNull -> writer.WriteNull(name)
        | SjsonBool b -> writer.WriteBoolean(name, b)
        | SjsonNumber n -> writer.WriteNumber(name, n)
        | SjsonString s -> writer.WriteString(name, s)

    let internal printJson(node: SjsonNode) =
        use ms = new MemoryStream()
        let writer = new Utf8JsonWriter(ms)
        printUnnamedJsonNodeInternal(node, writer)
        writer.Flush() |> ignore

        ms.Seek(0, SeekOrigin.Begin) |> ignore
        use sr = new StreamReader(ms)
        let result = sr.ReadToEnd()
        result
        

    let rec internal printJsonOld(node: SjsonNode, writer: TextWriter) =
        match node with
        | SjsonNull -> writer.Write("null")
        | SjsonBool b -> if b then writer.Write("true") else writer.Write("false")
        | SjsonNumber n -> writer.Write(n.ToString())
        | SjsonString s ->
            // Escape any unescaped quotes that might be in the string, and replace any embedded newlines with an escaped newline.
            let sFixed =
                unescapedDoubleQuotesRegex.Replace(s, new MatchEvaluator(fun m -> m.Groups.Item(1).Value + "\\\""))
                    .Replace("\r", @"\n")
                    .Replace("\n", @"\n")
                    .Replace("\\", @"\\")

            writer.Write("\"" + sFixed + "\"")
        | SjsonArray a ->
            writer.WriteLine("[")
            a |> List.iteri(fun i v ->
                printJsonOld(v, writer)
                if i < (a.Length - 1) then
                    writer.WriteLine(",")
            )
            writer.WriteLine()
            writer.WriteLine("]")
        | SjsonObject o ->
            writer.WriteLine("{")
            o |> Map.toList |> List.iteri (fun i (k, v) ->
                writer.Write("\"" + k + "\": ")
                printJsonOld(v, writer)
                if i < (o.Count - 1) then
                    writer.WriteLine(",")
            )
            writer.WriteLine()
            writer.WriteLine("}")

let stripComments(sjson: string): string =
    let parsedSjson = run SjsonCommentStrippingGrammar.commentPreprocessorParser sjson
    match parsedSjson with
    | ParserResult.Failure (s, e, us) -> failwith "failed to remove comments from SJSON successfully"
    | ParserResult.Success (result, _, _) ->
        result
        |> List.map (fun b ->
            match b with
            | SjsonCommentStrippingGrammar.NonCommentBlock s -> s
            | SjsonCommentStrippingGrammar.Comment -> " "
        )
        |> String.concat ""


let sjsonToJSON(sjson: string): string =
    let commentStrippedJson = stripComments sjson
    let parsedSjson = run SjsonGrammar.sjsonFileScopeContainer commentStrippedJson
    match parsedSjson with
    | ParserResult.Failure _ ->
        failwith "failed to parse SJSON successfully for JSON transformation"
    | ParserResult.Success (result, _, _) ->
        SjsonGrammar.printJson(result)
