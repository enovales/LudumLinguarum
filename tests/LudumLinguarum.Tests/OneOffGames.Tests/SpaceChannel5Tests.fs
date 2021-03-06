﻿module SpaceChannel5Tests

open Expecto
open SpaceChannel5
open System

[<Tests>]
let tests = 
  testList "Space Channel 5 tests" [
    testCase "Calling getNextDocTextBlock on a single non-comment line returns Some(line, empty seq)" <|
      fun () ->
        let sourceLine = "This is a test."
        let source = [| sourceLine |]
        let expected = Some((sourceLine, Seq.empty))
        match getNextDocTextBlock(source) with
        | Some((nextBlock, remainderSeq)) ->
            Expect.equal sourceLine nextBlock ""
            Expect.equal [||] (remainderSeq |> Seq.toArray) ""
        | _ -> failwith "expected block not found"

    testCase "Calling getNextDocTextBlock on a multi-line non-comment block returns that block, separated by newlines" <|
      fun () ->
        let sourceLine1 = "This is a test."
        let sourceLine2 = "This is also a test."
        let source = [| sourceLine1; sourceLine2 |]
        let expected = Some(((sourceLine1 + Environment.NewLine + sourceLine2), Seq.empty))
        match getNextDocTextBlock(source) with
        | Some((nextBlock, remainderSeq)) -> 
            Expect.equal (sourceLine1 + Environment.NewLine + sourceLine2) nextBlock ""
            Expect.equal [||] (remainderSeq |> Seq.toArray) ""
        | _ -> failwith "expected block not found"

    testCase "Calling getNextDocTextBlock on a block with two lines separated by a comment block, returns just the first block" <|
      fun () ->
        let sourceLine1 = "This is a test."
        let sourceLine2 = "# This is a comment."
        let sourceLine3 = "This is a second test."
        let source = [| sourceLine1; sourceLine2; sourceLine3 |]
        let expected = Some((sourceLine1, source |> Seq.skip(1)))
        match getNextDocTextBlock(source) with
        | Some((nextBlock, remainderSeq)) ->
            Expect.equal sourceLine1 nextBlock ""
            Expect.equal [| sourceLine2; sourceLine3 |] (remainderSeq |> Seq.toArray) ""
        | _ -> failwith "expected block not found"

    testCase "Calling getNextDocTextBlock skips over lines beginning with ^" <|
      fun () ->
        let sourceLine1 = "^should be skipped"
        let sourceLine2 = "This is a test."
        let source = [| sourceLine1; sourceLine2 |]
        let expected = Some((sourceLine2, Seq.empty))
        match getNextDocTextBlock(source) with
        | Some((nextBlock, remainderSeq)) ->
            Expect.equal sourceLine2 nextBlock ""
            Expect.equal [||] (remainderSeq |> Seq.toArray) ""
        | _ -> failwith "expected block not found"

    testCase "Using getNextDocTextBlock with Seq.unfold returns a single block when called with a set containing a single string" <|
      fun () -> 
        let sourceLine = "This is a test."
        let source = [| sourceLine |]
        let expected = [| sourceLine |]
        Expect.equal expected ((Seq.unfold getNextDocTextBlock (source |> Seq.ofArray)) |> Array.ofSeq) ""

    testCase "Using getNextDocTextBlock with Seq.unfold returns a single block when called with a set containing a comment block and a single string" <|
      fun () ->
        let sourceLine1 = "# This is a comment."
        let sourceLine2 = "This is a test."
        let source = [| sourceLine1; sourceLine2 |]
        let expected = [| sourceLine2 |]
        Expect.equal expected ((Seq.unfold getNextDocTextBlock (source |> Seq.ofArray)) |> Array.ofSeq) ""

    testCase "Using getNextDocTextBlock with Seq.unfold returns a single multi-line block when called with a set containing a comment block and a multi-line string" <|
      fun () ->
        let sourceLine1 = "# This is a comment."
        let sourceLine2 = "This is the first line."
        let sourceLine3 = "This is the second line."
        let source = [| sourceLine1; sourceLine2; sourceLine3 |]
        let expected = [| sourceLine2 + Environment.NewLine + sourceLine3 |]
        Expect.equal expected ((Seq.unfold getNextDocTextBlock (source |> Seq.ofArray)) |> Array.ofSeq) ""

    testCase "Using getNextDocTextBlock with Seq.unfold returns two blocks when called with a set containing two strings separated by a comment block" <|
      fun () ->
        let sourceLine1 = "This is the first block."
        let sourceLine2 = "# This is a comment."
        let sourceLine3 = "This is the second block."
        let source = [| sourceLine1; sourceLine2; sourceLine3 |]
        let expected = [| sourceLine1; sourceLine3 |]
        Expect.equal expected ((Seq.unfold getNextDocTextBlock (source |> Seq.ofArray)) |> Array.ofSeq) ""
  ]
