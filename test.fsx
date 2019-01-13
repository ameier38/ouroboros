#r "netstandard"
#r @"packages\Expecto\lib\netstandard2.0\Expecto.dll"
#r @"src\Dog\out\Dog.dll"
#r @"src\Dog\out\Ouroboros.dll"

open System
open Expecto
open Ouroboros
open Dog
open Dog.Implementation
let benjiId = Guid.NewGuid() |> EntityId

let minnieId = Guid.NewGuid() |> EntityId

let ragglesId = Guid.NewGuid() |> EntityId

let benji = ("Benji", "Maltipoo") ||> Dog.create
let minnie = ("Minnie", "Shih Tzu") ||> Dog.create
let raggles = ("Raggles", "Mutt") ||> Dog.create

let spreadTwo f tup = tup ||> f
let spreadThree f tup = tup |||> f

let createCommand commandSource effectiveDate (command:DogCommand) = 
    result {
        let! commandSource' = 
            commandSource 
            |> Source.create
            |> Result.mapError DogError
        let effectiveDate' = effectiveDate |> EffectiveDate
        let dogCommandMeta = { CommandSource = commandSource' }
        let meta = { EffectiveDate = effectiveDate'; DomainCommandMeta = dogCommandMeta }
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
        let! eatEventNumber = 1L |> EventNumber.create |> Result.mapError DogError
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

let ragglesCommands =
    result {
        let! raggles' = raggles
        let! eatEventNumber = 1L |> EventNumber.create |> Result.mapError DogError
        return!
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create raggles')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat) 
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Reverse eatEventNumber) 
              ("test", DateTime(2018, 8, 30, 2, 0, 0), DogCommand.Play) ]
            |> List.map (spreadThree createCommand)
            |> Result.sequence
    }

// let expectedBenjiEventTypes = 
//     [ "Born"
//       "Ate"
//       "Played"
//       "Slept"
//       "Woke"
//       "Ate" ]

// let expectedMinnieEventTypes = 
//     [ "Born"
//       "Ate"
//       "Deleted"
//       "Ate"
//       "Played" ]

// let expectedRagglesEventTypes =
//     [ "Born"
//       "Ate"
//       "Deleted" ]

// let executeCommand dogId =
//     fun command ->
//         asyncResult {
//             let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
//             let handle = commandHandler.handle dogId
//             let commandList = command |> List.singleton
//             let! newEvents = handle commandList
//             do! AsyncResult.sleep 500
//             return newEvents
//         }

// let testBenji =
//     test "test Benji" {
//         result {
//             let! benjiCommands' = benjiCommands
//             let executeCommand' = executeCommand benjiId
//             return! 
//                 benjiCommands'
//                 |> List.map executeCommand'
//                 |> AsyncResult.sequenceM
//                 |> Async.RunSynchronously
//                 |> Result.map List.concat
//         } 
//         |> Expect.isOk
//         <| "should be ok"

//         asyncResult {
//             let! repo = repoResult |> AsyncResult.ofResult
//             let! recordedEvents = repo.loadAll benjiId
//             let actualEventTypes =
//                 recordedEvents
//                 |> List.map (function
//                     | RecordedDomainEvent e -> e.Type |> DomainEventType.value
//                     | RecordedDeletedEvent _ -> "Deleted")
//             return Expect.equal actualEventTypes expectedBenjiEventTypes "The event types should equal"
//         } 
//         |> Async.RunSynchronously
//         |> Expect.isOk 
//         <| "should be ok"

//         asyncResult {
//             let! repo = repoResult |> AsyncResult.ofResult
//             let! mealCount = Projection.mealCount repo benjiId
//             Expect.equal mealCount 2 "meal count should equal two"
//         }
//         |> Async.RunSynchronously
//         |> Expect.isOk
//         <| "should be ok"
//     }

// let testMinnie =
//     ftest "test Minnie" {
//         result {
//             let! minnieCommands' = minnieCommands 
//             let executeCommand' = executeCommand minnieId
//             return!
//                 minnieCommands'
//                 |> List.map executeCommand'
//                 |> AsyncResult.sequenceM
//                 |> Async.RunSynchronously
//                 |> Result.map List.concat
//         }
//         |> Expect.isOk 
//         <| "should be ok"

//         asyncResult {
//             let! repo = repoResult |> AsyncResult.ofResult
//             let! recordedEvents = repo.loadAll minnieId
//             let actualEventTypes =
//                 recordedEvents
//                 |> List.map (function
//                     | RecordedDomainEvent e -> e.Type |> DomainEventType.value
//                     | RecordedDeletedEvent _ -> "Deleted")
//             return Expect.equal actualEventTypes expectedMinnieEventTypes "The event types should equal"
//         } 
//         |> Async.RunSynchronously
//         |> Expect.isOk 
//         <| "should be ok"

//         asyncResult {
//             let! repo = repoResult |> AsyncResult.ofResult
//             let! mealCount = Projection.mealCount repo minnieId
//             Expect.equal mealCount 1 "meal count shoud equal one"
//         }
//         |> Async.RunSynchronously
//         |> Expect.isOk
//         <| "should be ok"
//     }

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

// let testOuroboros =
//     testList "test Ouroboros" [
//         testBenji
//         testMinnie
//         testRaggles
//     ]