module YamlTools

open YamlDotNet.RepresentationModel

/// <summary>
/// Type test for YAML node types. Useful for filtering collections.
/// </summary>
/// <param name="n">the YAML node to test</param>
let isYamlNodeType<'T when 'T :> YamlNode>(n: YamlNode) = 
    match n with
    | :? 'T as nt -> true
    | _ -> false

/// <summary>
/// Filters a YAML node sequence on a particular type, and casts to that type.
/// </summary>
/// <param name="s">the sequence to filter and cast</param>
let mapOnlyYamlType<'T, 'U when 'T :> YamlNode and 'U :> YamlNode>(s: 'U seq) = 
    let nodeFilter(n: YamlNode) =
        match n with
        | :? 'T as nt -> true
        | _ -> false

    s |> Seq.map (fun n -> n :> YamlNode) |> Seq.filter nodeFilter |> Seq.map (fun n -> n :?> 'T)

let mappingNodeHasKey(k: string)(n: YamlMappingNode) = 
    n.Children.Keys
    |> mapOnlyYamlType<YamlScalarNode, YamlNode>
    |> Seq.exists(fun sn -> sn.Value = k)

let findMappingNodesWithName(name: string)(n: YamlNode) = 
    n.AllNodes 
    |> mapOnlyYamlType<YamlMappingNode, YamlNode>
    |> Seq.filter(mappingNodeHasKey(name))
    |> Seq.map (fun mn -> mn.Children.[new YamlScalarNode(name)])
    |> mapOnlyYamlType<YamlMappingNode, YamlNode>

/// <summary>
/// Converts a mapping node consisting of scalar keys on each side into a sequence of string-to-string pairs.
/// </summary>
/// <param name="n">mapping node containing only scalar keys and scalar values</param>
let convertMappingNodeToStringPairs(n: YamlMappingNode): (string * string) seq = 
    let kvPairsForScalarNodeKey(sn: YamlScalarNode) = 
        [| n.Children.[sn] |] 
        |> mapOnlyYamlType<YamlScalarNode, YamlNode> 
        |> Seq.map (fun vn -> (sn.Value, vn.Value))

    n.Children.Keys 
    |> mapOnlyYamlType<YamlScalarNode, YamlNode> 
    |> Seq.filter (fun sn -> isYamlNodeType<YamlScalarNode>(n.Children.[sn]))
    |> Seq.collect kvPairsForScalarNodeKey
