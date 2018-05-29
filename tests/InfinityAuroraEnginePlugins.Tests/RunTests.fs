namespace LudumLinguarum.Tests

open Expecto

module RunTests =

    [<EntryPoint>]
    let main args =

        Tests.runTestsWithArgs defaultConfig args ArchiveFilesTests.tests |> ignore

        0

