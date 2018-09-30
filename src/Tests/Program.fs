open System
open Expecto
open Ouroboros
open Ouroboros.Constants
open Ouroboros.Api
open Test.Dog
open Test.Dog.Implementation

let benjiId = "2d94680171c64c86b136169551769831" |> Guid.Parse |> EntityId

let minnieId = "ad114343-08c0-422d-a252-9cb58496972d" |> Guid.Parse |> EntityId

let ragglesId = "0e5c0d15-9cfc-4220-99e5-bdcce47ac70c" |> Guid.Parse |> EntityId

let benji = ("Benji", "Maltipoo") ||> Dog.create
let minnie = ("Minnie", "Shih Tzu") ||> Dog.create
let raggles = ("Raggles", "Mutt") ||> Dog.create

let spreadTwo f tup = tup ||> f
let spreadThree f tup = tup |||> f

let benjiCommands =
    result {
        let! benji' = benji
        return!
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create benji')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat)
              ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Play) 
              ("test", DateTime(2018, 8, 30, 3, 0, 0), DogCommand.Sleep) 
              ("test", DateTime(2018, 8, 30, 4, 0, 0), DogCommand.Wake) 
              ("test", DateTime(2018, 8, 30, 5, 0, 0), DogCommand.Eat) ]
            |> List.map (spreadThree Command.createDomainCommand)
            |> Result.sequence
    } |> Result.mapError DogError.Validation

let minnieCommands =
    result {
        let! minnie' = minnie
        let! domainCommands =
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create minnie')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat) ]
            |> List.map (spreadThree Command.createDomainCommand)
            |> Result.sequence
        let! deleteEvent1 = (1L, "mistake") ||> Deletion.create
        let! deleteCommands =
            [ ("test", deleteEvent1) ]
            |> List.map (spreadTwo Command.createDeleteCommand)
            |> Result.sequence
        let! newDomainCommands =
            [ ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Eat)
              ("test", DateTime(2018, 8, 30, 3, 0, 0), DogCommand.Play) ]
            |> List.map (spreadThree Command.createDomainCommand)
            |> Result.sequence
        return domainCommands @ deleteCommands @ newDomainCommands
    } |> Result.mapError DogError.Validation

let ragglesCommands =
    result {
        let! raggles' = raggles
        let! domainCommands =
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create raggles')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat) ]
            |> List.map (spreadThree Command.createDomainCommand)
            |> Result.sequence
        let! deleteEvent1 = (1L, "mistake") ||> Deletion.create
        let! deleteCommands =
            [ ("test", deleteEvent1) ]
            |> List.map (spreadTwo Command.createDeleteCommand)
            |> Result.sequence
        let! newDomainCommands =
            [ ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Play) ]
            |> List.map (spreadThree Command.createDomainCommand)
            |> Result.sequence
        return domainCommands @ deleteCommands @ newDomainCommands
    } |> Result.mapError DogError.Validation

let expectedBenjiEventTypes = 
    [ "Born"
      "Ate"
      "Played"
      "Slept"
      "Woke"
      "Ate" ]

let expectedMinnieEventTypes = 
    [ "Born"
      "Ate"
      "Deleted"
      "Ate"
      "Played" ]

let expectedRagglesEventTypes =
    [ "Born"
      "Ate"
      "Deleted" ]

let executeCommand dogId command =
    asyncResult {
        let! handler = handlerResult |> AsyncResult.ofResult
        let handle = handler.handle dogId
        return! handle [ command ]
    }

let testBenji =
    test "test Benji" {
        let executeCommand' = executeCommand benjiId
        result {
            let! benjiCommands' = benjiCommands
            return! 
                benjiCommands'
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        } 
        |> Expect.isOk
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.loadAll benjiId
            let actualEventTypes =
                recordedEvents
                |> List.map (function
                    | RecordedDomainEvent e -> e.Type |> DomainEventType.value
                    | RecordedDeletedEvent _ -> DeletedEventTypeValue)
            return Expect.equal actualEventTypes expectedBenjiEventTypes "The event types should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! mealCount = Projection.mealCount repo benjiId
            Expect.equal mealCount 2 "meal count should equal two"
        }
        |> Async.RunSynchronously
        |> Expect.isOk
        <| "should be ok"
    }

let testMinnie =
    test "test Minnie" {
        let executeCommand' = executeCommand minnieId
        result {
            let! minnieCommands' = minnieCommands 
            return!
                minnieCommands'
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        }
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.loadAll minnieId
            let actualEventTypes =
                recordedEvents
                |> List.map (function
                    | RecordedDomainEvent e -> e.Type |> DomainEventType.value
                    | RecordedDeletedEvent _ -> DeletedEventTypeValue)
            return Expect.equal actualEventTypes expectedMinnieEventTypes "The event types should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! mealCount = Projection.mealCount repo minnieId
            Expect.equal mealCount 1 "meal count shoud equal one"
        }
        |> Async.RunSynchronously
        |> Expect.isOk
        <| "should be ok"
    }

let testRaggles =
    test "test Raggles" {
        let onSuccess _ = "Success!"
        let onError err = sprintf "%A" err
        let executeCommand' = executeCommand ragglesId
        result {
            let! ragglesCommands' = ragglesCommands 
            return!
                ragglesCommands'
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        }
        |> Result.bimap onSuccess onError
        |> Expect.isMatch 
        <| "Validation \"dog cannot play; dog is not bored\""
        <| "should throw error"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.loadAll ragglesId
            let actualEventTypes =
                recordedEvents
                |> List.map (function
                    | RecordedDomainEvent e -> e.Type |> DomainEventType.value
                    | RecordedDeletedEvent _ -> DeletedEventTypeValue)
            return Expect.equal actualEventTypes expectedRagglesEventTypes "The event types should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! mealCount = Projection.mealCount repo ragglesId
            Expect.equal mealCount 0 "meal count shoud equal zero"
        }
        |> Async.RunSynchronously
        |> Expect.isOk
        <| "should be ok"
    }

let testOuroboros =
    testList "test Ouroboros" [
        testBenji
        testMinnie
        testRaggles
    ]

[<EntryPoint>]
let main argv =
    runTests defaultConfig testOuroboros
