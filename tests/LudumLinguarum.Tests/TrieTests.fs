module TrieTests

open Expecto
open Trie

// data for .Contains tests
let private testContainsTrie = 
    Trie.Root(
        [| Trie.Node('a', 
            [| Trie.Node('b', 
                [| Trie.Node('c', [||], true) |], true)
        |], false)
    |])

[<Tests>]
let tests = 
  testList "Trie tests" [
    testCase "Build trie from empty array" <|
      fun () -> Expect.equal (Trie.Root [||]) (Trie.Build [||]) "should just be an empty node"

    testCase "Build trie from empty string" <|
      fun () -> Expect.equal (Trie.Root [||]) (Trie.Build [|""|]) "should just be an empty node"

    testCase "Build trie from single char" <|
      fun () -> Expect.equal (Trie.Root [| Node('a', [||], true) |]) (Trie.Build [| "a" |]) "should be a single node"

    testCase "Build trie from two chars" <|
      fun () -> 
        let expected = Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true) |], false) |])
        let built = Trie.Build([| "ab" |])
        Expect.equal expected built "should be two nested nodes"

    testCase "Build trie from overlapping words" <|
      fun () ->
        let expected = Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true) |], true) |])
        let built = Trie.Build([| "a"; "ab" |])
        Expect.equal expected built "should be two nested nodes"

    testCase "Build trie from non-overlapping words" <|
      fun () ->
        let expected = Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true); Trie.Node('c', [||], true) |], false) |])
        let built = Trie.Build([| "ab"; "ac" |])
        Expect.equal expected built "should be three nodes"

    testCase "Build trie from disjoint words" <|
      fun () ->
        let expected = Trie.Root([| Trie.Node('a', [||], true); Trie.Node('b', [||], true) |])
        let built = Trie.Build([| "a"; "b" |])
        Expect.equal expected built "should be two disjoint nodes"

    testCase "Trie doesn't match empty string" <|
      fun () -> Expect.isFalse (testContainsTrie.Contains("")) "should not match empty string"

    testCase "Empty trie doesn't contain a string" <|
      fun () -> Expect.isFalse (Trie.Root([||]).Contains("anything")) "should not contain anything"

    testCase "Trie.Contains() returns false on non-matching string" <|
      fun () -> Expect.isFalse (testContainsTrie.Contains("xyz")) "should return false for non-matching string"

    testCase "Trie.Contains() returns false on non-terminal string" <|
      fun () -> Expect.isFalse (testContainsTrie.Contains("a")) "should return false for non-terminal string"

    testCase "Trie.Contains() returns true on a matching string" <|
      fun () -> Expect.isTrue (testContainsTrie.Contains("ab")) "should return true for matching string"

    testCase "Trie.Contains() returns true on a matching string that reaches a terminal node" <|
      fun () -> Expect.isTrue (testContainsTrie.Contains("abc")) "should return true when reaching terminal node"

    testCase "Trie.Contains() returns false on a string that's too long" <|
      fun () -> Expect.isFalse (testContainsTrie.Contains("abc1234567890")) "should return false on too-long string"

    testCase "NextNode() returns None for non-matching next character" <|
      fun () -> Expect.isNone (testContainsTrie.NextNode('1')) "should return None for non-matching next character"

    testCase "NextNode() returns Some for matching next character" <|
      fun () -> Expect.isTrue (testContainsTrie.NextNode('a').IsSome) "should return Some for matching next character"

    testCase "Matching is case-insensitive" <|
      fun () -> Expect.isTrue (testContainsTrie.Contains("ABC")) "matching should be case-insensitive"
  ]
