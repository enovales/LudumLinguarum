module MadballsBaboInvasionTests

open MadballsBaboInvasion
open NUnit.Framework

type MadballsBaboInvasionTests() = 
    [<Test>]
    member this.``Calling stripFormattingTags doesn't affect strings without formatting tags``() = 
        let input = "This is a test string with no formatting tags."
        Assert.AreEqual(input, stripFormattingTags(input))

    [<Test>]
    member this.``Calling stripFormattingTags removes tags of the format ^###``() = 
        let input1 = "Here's a caret tag: ^123 Isn't it great?"
        let expected = "Here's a caret tag:  Isn't it great?"
        let input2 = "Here's a caret tag: ^123456789 Isn't it great?"
        Assert.AreEqual(expected, stripFormattingTags(input1))
        Assert.AreEqual(expected, stripFormattingTags(input2))

    [<Test>]
    member this.``Calling stripFormattingTags doesn't remove caret tags that are less than 3 digits``() = 
        let input = "Here's a caret tag with two digits: ^12"
        Assert.AreEqual(input, stripFormattingTags(input))

    [<Test>]
    member this.``Calling stripFormattingTags removes substitutions of the format {#}``() = 
        let input = "Here's a substitution tag: {0} Isn't it great?"
        let expected = "Here's a substitution tag:  Isn't it great?"
        Assert.AreEqual(expected, stripFormattingTags(input))

    [<Test>]
    member this.``Calling stripFormattingTags removes all tags and substitutions``() = 
        let input = "Here's a string with both ^456 and {9} Isn't it great?"
        let expected = "Here's a string with both  and  Isn't it great?"
        Assert.AreEqual(expected, stripFormattingTags(input))
