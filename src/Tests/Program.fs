open System
open System.IO
open Expecto
open Ouroboros
open Test.Dog
open Test.Dog.Implementation

let benjiId = "2d94680171c64c86b136169551769831" |> Guid.Parse |> EntityId

let minnieId = "ad114343-08c0-422d-a252-9cb58496972d" |> Guid.Parse |> EntityId

let benji =
    { Name = "Benji"
      Breed = "Maltipoo" }

let minnie =
    { Name = "Minnie"
      Breed = "Shih Tzu" }

let validCommands =
    [ DogCommand.Create (DateTime(2018, 8, 30, 12, 0, 0), benji)
      DogCommand.Eat (DateTime(2018, 8, 30, 13, 0, 0))
      DogCommand.Play (DateTime(2018, 8, 30, 14, 0, 0))
      DogCommand.Sleep (DateTime(2018, 8, 30, 15, 0, 0)) ]

let invalidCommands =
    [ DogCommand.Create (DateTime(2018, 8, 30, 12, 0, 0), minnie)
      DogCommand.Play (DateTime(2018, 8, 30, 13, 0, 0)) ]

let expectedEvents = 
    [ DogEvent.Born benji
      DogEvent.Ate
      DogEvent.Played
      DogEvent.Slept ]

let executeCommand dogId command =
    asyncResult {
        let! handler = handlerResult |> AsyncResult.ofResult
        let handle = handler.handle dogId
        return! handle command
    }

let testOuroborosSuccess =
    test "test Ouroboros Success" {
        let onSuccess _ = printfn "Success!"
        let onError e = printfn "Error!: %A" e
        let executeCommand' = executeCommand benjiId
        validCommands        
        |> List.map executeCommand'
        |> AsyncResult.sequenceM
        |> Async.RunSynchronously
        |> Result.bimap onSuccess onError

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.load benjiId
            let events = recordedEvents |> List.map (fun re -> re.Data)
            return Expect.equal events expectedEvents "The events should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"
    }

let testOuroborosError =
    test "test Ouroboros Error" {
        let onSuccess _ = sprintf "Success!"
        let onError e = sprintf "Error!: %A" e
        let executeCommand' = executeCommand minnieId
        let result =
            invalidCommands        
            |> List.map executeCommand'
            |> AsyncResult.sequenceM
            |> Async.RunSynchronously
            |> Result.bimap onSuccess onError
        Expect.isMatch result "Error!: Validation \"invalid command Play (?:8/30/2018 1:00:00 PM|08/30/2018 13:00:00) on state Hungry\"" "should throw error"
    }

let testOuroboros =
    testList "test Ouroboros" [
        testOuroborosSuccess
        testOuroborosError
    ]

[<EntryPoint>]
let main argv =
    runTests defaultConfig testOuroboros
