open System
open System.IO
open Expecto
open Ouroboros
open Ouroboros.EventStore
open Test.Config
open Test.Dog
open Test.Dog.Implementation

let benjiId = "2d94680171c64c86b136169551769831" |> Guid.Parse |> EntityId

let dog =
    { Name = "Benji"
      Breed = "Maltipoo" }

let commands =
    [ DogCommand.Create (DateTime(2018, 8, 30, 12, 0, 0), dog)
      DogCommand.Eat (DateTime(2018, 8, 30, 13, 0, 0))
      DogCommand.Play (DateTime(2018, 8, 30, 14, 0, 0))
      DogCommand.Sleep (DateTime(2018, 8, 30, 15, 0, 0)) ]

let expectedEvents = 
    [ DogEvent.Born dog
      DogEvent.Ate
      DogEvent.Played
      DogEvent.Slept ]

let getDogRepo () =
    result {
        printfn "getting dog repo"
        let! config = EventStoreConfig.load () |> Result.mapError DogError.IO
        let store = eventStore config.Uri
        let! entityType = EntityType.create "dog" |> Result.mapError DogError.Validation
        let mapError (EventStoreError e) = DogError.IO e
        let repo = Repository.create store mapError serializer entityType
        return repo
    }

let handler =
    printfn "getting handler"
    result {
        let! repo = getDogRepo () 
        return Aggregate.createHandler repo aggregate
    }

let executeCommand command =
    asyncResult {
        let! handler' = handler |> AsyncResult.ofResult
        let handle = handler' benjiId DateTime.UtcNow
        do! handle command
    }

let testOuroboros =
    test "test Ouroboros" {
        let onSuccess _ = printfn "Success!"
        let onError e = printfn "Error!: %A" e
        commands        
        |> List.map executeCommand
        |> AsyncResult.sequenceM
        |> Async.RunSynchronously
        |> Result.bimap onSuccess onError

        asyncResult {
            let! repo = getDogRepo () |> AsyncResult.ofResult
            let! recordedEvents = repo.load benjiId
            let events = recordedEvents |> List.map (fun re -> re.Data)
            return Expect.equal events expectedEvents "The events should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"
    }

[<EntryPoint>]
let main argv =
    runTests defaultConfig testOuroboros
