module Test.Dog

open System
open Expecto
open Expecto.Flip
open Dog
open Dog.Implementation
open Ouroboros

let benjiId = Guid.NewGuid() |> EntityId
let benji = ("Benji", "Maltipoo") ||> Dog.create
let minnieId = Guid.NewGuid() |> EntityId
let minnie = ("Minnie", "Shih Tzu") ||> Dog.create
let raggles = ("Raggles", "Mutt") ||> Dog.create

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
            |> List.map (spreadThree createCommand)
            |> Result.sequence
    }

let minnieCommands =
    result {
        let! minnie' = minnie
        let! eatEventNumber = 1L |> EventNumber.create |> Result.mapError DomainError
        return!
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create minnie')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat)
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Reverse eatEventNumber)
              ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Eat)
              ("test", DateTime(2018, 8, 30, 3, 0, 0), DogCommand.Play) 
              ]
            |> List.map (spreadThree createCommand)
            |> Result.sequence
    }

// let ragglesCommands =
//     result {
//         let! raggles' = raggles
//         let! eatEventNumber = 1L |> EventNumber.create |> Result.mapError DogError
//         return!
//             [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create raggles')
//               ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat) 
//               ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Reverse eatEventNumber) 
//               ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Play) ]
//             |> List.map (spreadThree createCommand)
//             |> Result.sequence
//     }

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
      "Reversed"
      "Ate"
      "Played" ]

// let expectedRagglesEventTypes =
//     [ "Born"
//       "Ate"
//       "Deleted" ]

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
        result {
            let! benjiCommands' = benjiCommands
            let executeCommand' = executeCommand benjiId
            return! 
                benjiCommands'
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        } 
        |> Expect.isOk "should be ok"

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
    ftest "test Minnie" {
        result {
            let! minnieCommands' = minnieCommands 
            let executeCommand' = executeCommand minnieId
            return!
                minnieCommands'
                |> List.map executeCommand'
                |> AsyncResult.sequenceM
                |> Async.RunSynchronously
                |> Result.map List.concat
        }
        |> Expect.isOk "should be ok"

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

// let testRaggles =
//     test "test Raggles" {
//         let onSuccess _ = "Success!"
//         let onError err = sprintf "%A" err
//         result {
//             let! ragglesCommands' = ragglesCommands 
//             let executeCommand' = executeCommand ragglesId
//             return!
//                 ragglesCommands'
//                 |> List.map executeCommand'
//                 |> AsyncResult.sequenceM
//                 |> Async.RunSynchronously
//                 |> Result.map List.concat
//         }
//         |> Result.bimap onSuccess onError
//         |> Expect.isMatch 
//         <| "Validation \"dog cannot play; dog is not bored\""
//         <| "should throw error"

//         asyncResult {
//             let! repo = repoResult |> AsyncResult.ofResult
//             let! recordedEvents = repo.loadAll ragglesId
//             let actualEventTypes =
//                 recordedEvents
//                 |> List.map (function
//                     | RecordedDomainEvent e -> e.Type |> DomainEventType.value
//                     | RecordedDeletedEvent _ -> "Deleted")
//             return Expect.equal actualEventTypes expectedRagglesEventTypes "The event types should equal"
//         } 
//         |> Async.RunSynchronously
//         |> Expect.isOk 
//         <| "should be ok"

//         asyncResult {
//             let! repo = repoResult |> AsyncResult.ofResult
//             let! mealCount = Projection.mealCount repo ragglesId
//             Expect.equal mealCount 0 "meal count shoud equal zero"
//         }
//         |> Async.RunSynchronously
//         |> Expect.isOk
//         <| "should be ok"
//     }

[<Tests>]
let testOuroboros =
    testList "test Ouroboros" [
        testBenji
        testMinnie
    ]
