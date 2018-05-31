namespace LudumLinguarum.Tests

open Expecto

module RunTests =

    [<EntryPoint>]
    let main args =

        Tests.runTestsWithArgs defaultConfig args CardExportTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args CsvToolsTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args DebugToolsTests.rabinFingerprintHasherTests |> ignore
        Tests.runTestsWithArgs defaultConfig args DebugToolsTests.fixedLengthRabinKarpStringScannerTests |> ignore
        Tests.runTestsWithArgs defaultConfig args DebugToolsTests.rabinKarpStringScannerTests |> ignore
        Tests.runTestsWithArgs defaultConfig args DebugToolsTests.streamStringScannerTests |> ignore
        Tests.runTestsWithArgs defaultConfig args DebugToolsTests.textScannerTests |> ignore
        Tests.runTestsWithArgs defaultConfig args LLDatabaseTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args PluginManagerTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args SrtToolsTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args StringExtractorsTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args TrieTests.tests |> ignore

        0

