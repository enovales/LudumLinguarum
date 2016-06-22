#I __SOURCE_DIRECTORY__
#load "load-project-debug.fsx"

open YamlDotNet.RepresentationModel;;
open System;;
open System.IO;;
open System.Text;;



let filePath = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis IV\localisation\achievements_l_english.yml"
let docText = File.ReadAllText(filePath, Encoding.UTF8)
let fixedUpDoc = docText.Replace(":0", ":")

let docReader = new StringReader(fixedUpDoc)
let yamlStream = new YamlStream()
yamlStream.Load(docReader)
let doc = yamlStream.Documents.[0]

let isScalarNode(n: YamlNode) = 
    match n with
    | :? YamlScalarNode as sn -> true
    | _ -> false

let isYamlNodeType<'T when 'T :> YamlNode>(n: YamlNode) = 
    match n with
    | :? 'T as nt -> true
    | _ -> false

let mapOnlyYamlType<'T, 'U when 'T :> YamlNode and 'U :> YamlNode>(s: 'U seq) = 
    let nodeFilter(n: YamlNode) =
        match n with
        | :? 'T as nt -> true
        | _ -> false

    s |> Seq.map (fun n -> n :> YamlNode) |> Seq.filter nodeFilter |> Seq.map (fun n -> n :?> 'T)

//doc.RootNode.AllNodes |> Seq.filter isYamlNodeType<YamlScalarNode> |> Seq.map (fun n -> n :?> YamlScalarNode) |> Seq.iter(fun n -> printfn "node tag [%s], value [%s]" n.Tag n.Value)
doc.RootNode.AllNodes |> mapOnlyYamlType<YamlSequenceNode, YamlNode> |> Seq.iter(fun n -> printfn "node tag [%s], # of children [%d]" n.Tag (n.Children.Count))


// strategy:
// find mapping node where first key is a scalar node with the value "l_<language name>"
// then, for the children of that node:
// key's scalar node value is the key, first child's scalar node is the value
