module Test.Dog

open System
open Expecto
open Expecto.Flip
open Dog
open Dog.Implementation
open Ouroboros

let spreadThree f (a, b, c) = f a b c

let createCommand source effectiveDate (command:DogCommand) = 
    result {
        let! source' = 
            source 
            |> Source.create
            |> Result.mapError DomainError
        let effectiveDate' = effectiveDate |> EffectiveDate
        let meta = 
            { EffectiveDate = effectiveDate'
              Source = source' }
        return
            { Command.Data = command
              Meta = meta }
    }

let executeCommand dogId =
    fun command ->
        asyncResult {
            let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
            let execute = commandHandler.execute dogId
            let! newEvents = execute command
            do! AsyncResult.sleep 500
            return newEvents
        }

let testBenji =
    test "test Benji" {
        let benjiId = Guid.NewGuid() |> EntityId
        result {
            let! benji = ("Benji", "Maltipoo") ||> Dog.create
            let! benjiCommands =
                [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create benji)
                  ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat)
                  ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Play) 
                  ("test", DateTime(2018, 8, 30, 3, 0, 0), DogCommand.Sleep) 
                  ("test", DateTime(2018, 8, 30, 4, 0, 0), DogCommand.Wake) 
                  ("test", DateTime(2018, 8, 30, 5, 0, 0), DogCommand.Eat) ]
                |> List.map (spreadThree createCommand)
                |> Result.sequence
            let executeCommand' = executeCommand benjiId
            return! 
                benjiCommands
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        } 
        |> Expect.isOk "should be ok"

        let expectedBenjiEventTypes = 
            [ "Born"
              "Ate"
              "Played"
              "Slept"
              "Woke"
              "Ate" ]

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.load benjiId
            recordedEvents
            |> List.map (fun event -> event.Type |> EventType.value)
            |> Expect.equal "events should equal" expectedBenjiEventTypes
        } 
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok" 

        asyncResult {
            let! queryHandler = queryHandlerResult |> AsyncResult.ofResult
            let! mealCount = Projection.mealCount queryHandler benjiId Latest
            Expect.equal "meal count should equal two" mealCount 2
        }
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok"
    }

let testMinnie =
    test "test Minnie" {
        let minnieId = Guid.NewGuid() |> EntityId
        result {
            let! minnie = ("Minnie", "Shih Tzu") ||> Dog.create
            let! eatEventNumber = 1L |> EventNumber.create |> Result.mapError DomainError
            let! minnieCommands =
                [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create minnie)
                  ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat)
                  ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Reverse eatEventNumber)
                  ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Eat)
                  ("test", DateTime(2018, 8, 30, 3, 0, 0), DogCommand.Play) 
                  ]
                |> List.map (spreadThree createCommand)
                |> Result.sequence

            let executeCommand' = executeCommand minnieId
            return!
                minnieCommands
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        }
        |> Expect.isOk "should be ok"

        let expectedMinnieEventTypes = 
            [ "Born"
              "Ate"
              "Reversed"
              "Ate"
              "Played" ]

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.load minnieId
            recordedEvents
            |> List.map (fun event -> event.Type |> EventType.value)
            |> Expect.equal "event types should equal" expectedMinnieEventTypes
        } 
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok"

        asyncResult {
            let! queryHandler = queryHandlerResult |> AsyncResult.ofResult
            let! mealCount = Projection.mealCount queryHandler minnieId Latest
            Expect.equal "meal count shoud equal one" 1 mealCount
        }
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok"
    }

let testRaggles =
    test "test Raggles" {
        let ragglesId = Guid.NewGuid() |> EntityId

        let onSuccess _ = "Success!"
        let onError err = sprintf "%A" err
        result {
            let executeCommand' = executeCommand ragglesId
            let! raggles = ("Raggles", "Mutt") ||> Dog.create
            let! eatEventNumber = 1L |> EventNumber.create |> Result.mapError DomainError
            let! ragglesCommands =
                [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create raggles)
                  ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat) 
                  ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Reverse eatEventNumber) 
                  ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Play) ]
                |> List.map (spreadThree createCommand)
                |> Result.sequence

            return!
                ragglesCommands
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        }
        |> Result.bimap onSuccess onError
        |> Expect.equal 
            "should throw error"
            "DomainError \"dog cannot play; dog is Hungry\""

        let expectedRagglesEventTypes =
            [ "Born"
              "Ate"
              "Reversed" ]

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.load ragglesId
            recordedEvents
            |> List.map (fun event -> event.Type |> EventType.value)
            |> Expect.equal "event types should equal" expectedRagglesEventTypes
        } 
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok"

        asyncResult {
            let! queryHandler = queryHandlerResult |> AsyncResult.ofResult
            let! mealCount = Projection.mealCount queryHandler ragglesId Latest
            Expect.equal "meal count shoud equal zero" 0 mealCount
        }
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok"
    }

[<Tests>]
let testOuroboros =
    testList "test Ouroboros" [
        testBenji
        testMinnie
        testRaggles
    ]
