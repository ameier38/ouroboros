open System
open Expecto
open Ouroboros
open Ouroboros.Api
open Test.Dog
open Test.Dog.Implementation

let benjiId = "2d94680171c64c86b136169551769831" |> Guid.Parse |> EntityId

let minnieId = "ad114343-08c0-422d-a252-9cb58496972d" |> Guid.Parse |> EntityId

let benji =
    result {
        let! name = "Benji" |> Name.create
        let! breed = "Maltipoo" |> Breed.create
        return
            { Name = name
              Breed = breed }
    }

let minnie =
    result {
        let! name = "Minnie" |> Name.create
        let! breed = "Shih Tzu" |> Breed.create
        return
            { Name = name
              Breed = breed }
    }

let benjiCommands =
    result {
        let! benji' = benji
        return!
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create benji')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Play) ]
            |> List.map (fun (s, d, c) -> Command.createDomainCommand s d c)
            |> Result.sequence
    } |> Result.mapError DogError.Validation

let minnieCommands =
    result {
        let! minnie' = minnie
        return!
            [ ("test", DateTime(2018, 8, 30, 0, 0, 0), DogCommand.Create minnie')
              ("test", DateTime(2018, 8, 30, 1, 0, 0), DogCommand.Eat) ]
            |> List.map (fun (s, d, c) -> Command.createDomainCommand s d c)
            |> Result.sequence
    } |> Result.mapError DogError.Validation

let expectedBenjiEvents = 
    result {
        let! benji' = benji
        return
            [ DogEvent.Born benji'
              DogEvent.Ate ]
    } |> Result.mapError DogError.Validation

let expectedMinnieEvents = 
    result {
        let! minnie' = minnie
        return
            [ DogEvent.Born minnie'
              DogEvent.Ate ]
    } |> Result.mapError DogError.Validation

let executeCommand dogId command =
    asyncResult {
        let! handler = handlerResult |> AsyncResult.ofResult
        let handle = handler.handle dogId
        return! handle [ command ]
    }

let testOuroborosBenji =
    ftest "test Ouroboros Benji" {
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
            let! recordedEvents = repo.load benjiId
            let! expectedBenjiEvents' = expectedBenjiEvents |> AsyncResult.ofResult
            let events = 
                recordedEvents 
                |> List.choose (function
                    | RecordedDomainEvent e -> Some e
                    | _ -> None)
                |> List.map (fun re -> re.Data)
            return Expect.equal events expectedBenjiEvents' "The events should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let asOfDate = DateTime.UtcNow
            let! mealCount = Projection.mealCount repo benjiId asOfDate
            Expect.equal mealCount 1 "meal count should equal one"
        }
        |> Async.RunSynchronously
        |> Expect.isOk
        <| "should be ok"
    }

let testOuroborosMinnie =
    test "test Ouroboros Minnie" {
        let onSuccess r = sprintf "Success!"
        let onError e = sprintf "Error!: %A" e
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
        |> Result.bimap onSuccess onError
        |> Expect.isMatch 
        <| "Error!: Validation \"dog cannot eat; incorrect name\"" 
        <| "should throw error"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let! recordedEvents = repo.load minnieId
            let! expectedMinnieEvents' = expectedMinnieEvents |> AsyncResult.ofResult
            let events = 
                recordedEvents 
                |> List.choose (function
                    | RecordedDomainEvent e -> Some e
                    | _ -> None)
                |> List.map (fun re -> re.Data)
            return Expect.equal events expectedMinnieEvents' "The events should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let asOfDate = DateTime.UtcNow
            let! mealCount = Projection.mealCount repo minnieId asOfDate
            Expect.equal mealCount 1 "meal count shoud equal one"
        }
        |> Async.RunSynchronously
        |> Expect.isOk
        <| "should be ok"
    }

let testOuroboros =
    testList "test Ouroboros" [
        testOuroborosBenji
        testOuroborosMinnie
    ]

[<EntryPoint>]
let main argv =
    runTests defaultConfig testOuroboros
