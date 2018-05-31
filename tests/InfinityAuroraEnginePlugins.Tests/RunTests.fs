namespace LudumLinguarum.Tests

open Expecto

module RunTests =

    [<EntryPoint>]
    let main args =

        Tests.runTestsWithArgs defaultConfig args ArchiveFilesTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args GFFFileTypesTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args TwoDATests.tests |> ignore

        0

