namespace LudumLinguarum.Tests

open Expecto

module RunTests =

    [<EntryPoint>]
    let main args =

        Tests.runTestsWithArgs defaultConfig args JetSetRadioTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args MadballsBaboInvasionTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args MagickaTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args OrcsMustDieTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args ParadoxStrategyGamesTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args PillarsOfEternityTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args PuzzleQuestGamesTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args SimpleGamesTests.magicalDropVTests |> ignore
        Tests.runTestsWithArgs defaultConfig args SimpleGamesTests.hatofulBoyfriendTests |> ignore
        Tests.runTestsWithArgs defaultConfig args SonicAdventureDXTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args SpaceChannel5Tests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args WormsArmageddonTests.tests |> ignore
        Tests.runTestsWithArgs defaultConfig args XUIGamesTests.tests |> ignore

        0

