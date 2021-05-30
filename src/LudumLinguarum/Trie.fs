module Trie

open System

type Trie = 
    | Root of Trie array
    | Node of char * Trie array * bool
    with
        static member IsLetter(t: Trie, c: char): bool =
            match t with
            | Node(n, _, _) when Char.ToLowerInvariant(c) = Char.ToLowerInvariant(n) -> true
            | _ -> false

        static member Build(strings: string array): Trie = 
            let rec buildInternal(c: char option, strings: string array): Trie = 
                let (empties, nextStrings) = strings |> Array.partition(fun t -> t.Length = 0)
                let byNextChar = nextStrings |> Array.groupBy(fun t -> t.[0])
                let nextNodes = byNextChar |> Array.map(fun (nc, ns) -> buildInternal(Some(nc), ns |> Array.map(fun t -> t.Substring(1))))

                match c with
                | Some(nc) -> Trie.Node(nc, nextNodes, empties.Length <> 0)
                | None when nextNodes.Length > 0 -> Trie.Root(nextNodes)
                | _ -> Trie.Root([||])

            buildInternal(None, strings)

        member this.Contains(s: string): bool = 
            match (s, this) with
            | (_, Root(nodes)) ->
                // at root node, search all subtrees
                nodes |> Array.exists(fun t -> t.Contains(s))
            | (s, Node(c, _, true)) when (s.Length = 1) && (Char.ToLowerInvariant(s.[0]) = Char.ToLowerInvariant(c)) -> 
                // character matches at terminal node, success
                true
            | (s, Node(c, nodes, _)) when (s.Length > 1) && (Char.ToLowerInvariant(s.[0]) = Char.ToLowerInvariant(c)) -> 
                // character matches at non-terminal node, continue searching subnodes
                nodes |> Array.exists(fun t -> t.Contains(s.Substring(1)))
            | _ -> false

        member this.NextNode(c: char): Trie option = 
            let isNodeWithChar(ch: char)(t: Trie) = 
                match t with
                | Node(c, _, _) when Char.ToLowerInvariant(c) = Char.ToLowerInvariant(ch) -> true
                | _ -> false
            match this with
            | Root(nodes) -> nodes |> Array.tryFind(isNodeWithChar(c))
            | Node(_, nodes, _) -> nodes |> Array.tryFind(isNodeWithChar(c))