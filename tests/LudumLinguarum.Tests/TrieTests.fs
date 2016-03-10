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
    member this.TestBuildTrieFromEmptyArray() = 
        Assert.AreEqual(Trie.Root([||]), Trie.Build([||]))

    [<Test>]
    member this.TestBuildTrieFromEmptyString() = 
        Assert.AreEqual(Trie.Root([||]), Trie.Build([|""|]))

    [<Test>]
    member this.TestBuildTrieFromSingleChar() = 
        Assert.AreEqual(Trie.Root([| Node('a', [||], true) |]), Trie.Build([| "a" |]))

    [<Test>]
    member this.TestBuildTrieFromTwoChars() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true) |], false) |]), Trie.Build([| "ab" |]))

    [<Test>]
    member this.TestBuildTrieFromOverlappingWords() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true) |], true) |]), Trie.Build([| "a"; "ab" |]))

    [<Test>]
    member this.TestBuildTrieFromNonOverlappingWords() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [| Trie.Node('b', [||], true); Trie.Node('c', [||], true) |], false) |]), Trie.Build([| "ab"; "ac" |]))

    [<Test>]
    member this.TestBuildTrieFromDisjointWords() = 
        Assert.AreEqual(Trie.Root([| Trie.Node('a', [||], true); Trie.Node('b', [||], true) |]), Trie.Build([| "a"; "b" |]))

    [<Test>]
    member this.TestContainsEmptyString() = 
        Assert.AreEqual(false, testContainsTrie.Contains(""))

    [<Test>]
    member this.TestContainsEmptyTrie() = 
        Assert.AreEqual(false, Trie.Root([||]).Contains("anything"))

    [<Test>]
    member this.TestContainsFalseOnNonMatchingString() = 
        Assert.AreEqual(false, testContainsTrie.Contains("xyz"))

    [<Test>]
    member this.TestContainsFalseOnNonTerminalString() = 
        Assert.AreEqual(false, testContainsTrie.Contains("a"))

    [<Test>]
    member this.TestContainsTrueOnTerminalString() = 
        Assert.AreEqual(true, testContainsTrie.Contains("ab"))

    [<Test>]
    member this.TestContainsPassesTerminalNodeCorrectly() = 
        Assert.AreEqual(true, testContainsTrie.Contains("abc"))

    [<Test>]
    member this.TestContainsFalseOnStringTooLong() = 
        Assert.AreEqual(false, testContainsTrie.Contains("abc1234567890"))

    [<Test>]
    member this.TestNextNodeNoneForNoMatch() = 
        Assert.AreEqual(None, testContainsTrie.NextNode('1'))

    [<Test>]
    member this.TestNextNodeSomeForMatch() = 
        Assert.AreEqual(true, testContainsTrie.NextNode('a').IsSome)

    [<Test>]
    member this.TestMatchingIsCaseInsensitive() = 
        Assert.AreEqual(true, testContainsTrie.Contains("ABC"))