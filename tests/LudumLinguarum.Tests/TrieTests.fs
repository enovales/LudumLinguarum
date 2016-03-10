module TrieTests

open NUnit.Framework
open Trie

[<TestFixture>]
type TrieTests() = 
    // data for .Contains tests
    let testContainsTrie = 
        Trie.Root(
            [| Trie.Node('a', 
                [| Trie.Node('b', 
                    [| Trie.Node('c', [||], true) |], true)
            |], false)
        |])

    [<Test>]
    member this.``Build trie from empty array``() = 
        Assert.AreEqual(Trie.Root([||]), Trie.Build([||]))

    [<Test>]
    member this.``Build trie from empty string``() = 
        Assert.AreEqual(Trie.Root([||]), Trie.Build([|""|]))

    [<Test>]
    member this.``Build trie from single char``() = 
        Assert.AreEqual(Trie.Root([| Node('a', [||], true) |]), Trie.Build([| "a" |]))

    [<Test>]
    member this.``Build trie from two chars``() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true) |], false) |]), Trie.Build([| "ab" |]))

    [<Test>]
    member this.``Build trie from overlapping words``() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true) |], true) |]), Trie.Build([| "a"; "ab" |]))

    [<Test>]
    member this.``Build trie from non-overlapping words``() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true); Trie.Node('c', [||], true) |], false) |]), Trie.Build([| "ab"; "ac" |]))

    [<Test>]
    member this.``Build trie from disjoint words``() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [||], true); Trie.Node('b', [||], true) |]), Trie.Build([| "a"; "b" |]))

    [<Test>]
    member this.``Trie doesn't match empty string``() = 
        Assert.AreEqual(false, testContainsTrie.Contains(""))

    [<Test>]
    member this.``Empty trie doesn't contain a string``() = 
        Assert.AreEqual(false, Trie.Root([||]).Contains("anything"))

    [<Test>]
    member this.``Trie.Contains() returns false on non-matching string``() = 
        Assert.AreEqual(false, testContainsTrie.Contains("xyz"))

    [<Test>]
    member this.``Trie.Contains() returns false on non-terminal string``() = 
        Assert.AreEqual(false, testContainsTrie.Contains("a"))

    [<Test>]
    member this.``Trie.Contains() returns true on a matching string``() = 
        Assert.AreEqual(true, testContainsTrie.Contains("ab"))

    [<Test>]
    member this.``Trie.Contains() returns true on a matching string that reaches a terminal node``() = 
        Assert.AreEqual(true, testContainsTrie.Contains("abc"))

    [<Test>]
    member this.``Trie.Contains() returns false on a string that's too long``() = 
        Assert.AreEqual(false, testContainsTrie.Contains("abc1234567890"))

    [<Test>]
    member this.``NextNode() returns None for non-matching next character``() = 
        Assert.AreEqual(None, testContainsTrie.NextNode('1'))

    [<Test>]
    member this.``NextNode() returns Some for matching next character``() = 
        Assert.AreEqual(true, testContainsTrie.NextNode('a').IsSome)

    [<Test>]
    member this.``Matching is case-insensitive``() = 
        Assert.AreEqual(true, testContainsTrie.Contains("ABC"))