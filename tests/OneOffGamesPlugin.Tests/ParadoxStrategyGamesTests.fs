module ParadoxStrategyGamesTests

open NUnit.Framework
open ParadoxStrategyGames

[<Test>]
let ``Regex for stripping numeric key annotations for EU4 YAMLs doesn't modify strings without annotations``() = 
    let s = "  key: value"
    let expected = s
    Assert.AreEqual(expected, eu4StripNumericKeyAnnotations(s))

[<Test>]
let ``Regex for stripping numeric key annotations for EU4 YAMLs removes single-digit annotations``() = 
    let s = "  key:1 value"
    let expected = "  key: value"
    Assert.AreEqual(expected, eu4StripNumericKeyAnnotations(s))

[<Test>]
let ``Regex for stripping numeric key annotations for EU4 YAMLs removes multiple-digit annotations``() = 
    let s = "  key:123 value"
    let expected = "  key: value"
    Assert.AreEqual(expected, eu4StripNumericKeyAnnotations(s))

[<Test>]
let ``Escaping quoted values for EU4 YAMLs doesn't modify strings without quoted values``() = 
    let s = "  key: value"
    let expected = s
    Assert.AreEqual(expected, eu4EscapeQuotedValues(s))

[<Test>]
let ``Escaping quoted values for EU4 YAMLs doesn't modify strings with quoted values without interior quotes``() = 
    let s = "  key: \"value\""
    let expected = s
    Assert.AreEqual(expected, eu4EscapeQuotedValues(s))

[<Test>]
let ``Escaping quoted values for EU4 YAMLs converts embedded double quotes to single quotes``() = 
    let s = "  key: \"here are some \"embedded\" quotes\""
    let expected = "  key: \"here are some 'embedded' quotes\""
    Assert.AreEqual(expected, eu4EscapeQuotedValues(s))

[<Test>]
let ``Escaping double sets of double quotes for EU4 YAMLs only converts the inner set of double quotes``() = 
    let s = "  key: \"\"friends\"\""
    let expected = "  key: \"'friends'\""
    Assert.AreEqual(expected, eu4EscapeQuotedValues(s))

[<Test>]
let ``Escaping quoted strings for EU4 YAMLs trims the interior``() = 
    let s = "  key: \" leading space\""
    let expected = "  key: \"leading space\""
    Assert.AreEqual(expected, eu4EscapeQuotedValues(s))
