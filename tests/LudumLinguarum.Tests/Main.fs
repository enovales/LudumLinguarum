namespace LudumLinguarum.Tests

open System.Text

module ExpectoTemplate =

    open Expecto

    [<EntryPoint>]
    let main argv =
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

        Tests.runTestsInAssemblyWithCLIArgs [||] argv
