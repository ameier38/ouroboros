open System
open Expecto
open Ouroboros
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
        return
            [ DogCommand.Create (DateTime(2018, 8, 30, 12, 0, 0) |> EffectiveDate, benji')
              DogCommand.CallToEat (DateTime(2018, 8, 30, 13, 0, 0) |> EffectiveDate, benji'.Name)
              DogCommand.Play (DateTime(2018, 8, 30, 14, 0, 0) |> EffectiveDate)
              DogCommand.Sleep (DateTime(2018, 8, 30, 15, 0, 0) |> EffectiveDate) ]
    }

let minnieCommands =
    result {
        let! minnie' = minnie
        let! newName = Name.create "Raggles"
        return
            [ DogCommand.Create (DateTime(2018, 8, 30, 12, 0, 0) |> EffectiveDate, minnie')
              DogCommand.CallToEat (DateTime(2018, 8, 30, 13, 0, 0) |> EffectiveDate, minnie'.Name)
              // rename Benji to Raggles effective on birthday
              DogCommand.Rename (DateTime(2018, 8, 30, 12, 0, 0) |> EffectiveDate, newName)
              // the previous Eat command should now fail when trying to reconstitute state
              DogCommand.Play (DateTime(2018, 8, 30, 13, 0, 0) |> EffectiveDate) 
              DogCommand.Sleep (DateTime(2018, 8, 30, 13, 0, 0) |> EffectiveDate) ]
    }

let expectedBenjiEvents = 
    result {
        let! benji' = benji
        return
            [ DogEvent.Born benji'
              DogEvent.Ate benji'.Name
              DogEvent.Played
              DogEvent.Slept ]
    } |> Result.mapError DogError.Validation

let expectedMinnieEvents =
    result {
        let! minnie' = minnie
        let! newName = Name.create "Raggles"
        return
            [ DogEvent.Born minnie'
              DogEvent.Ate minnie'.Name
              DogEvent.Renamed newName ]
    } |> Result.mapError DogError.Validation

let executeCommand dogId command =
    asyncResult {
        let! handler = handlerResult |> AsyncResult.ofResult
        let handle = handler.handle dogId
        return! handle command
    }

let testOuroborosBenji =
    test "test Ouroboros Benji" {
        let executeCommand' = executeCommand benjiId
        result {
            let! benjiCommands' = 
                benjiCommands
                |> Result.mapError DogError.Validation
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
            let events = recordedEvents |> List.map (fun re -> re.Data)
            return Expect.equal events expectedBenjiEvents' "The events should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let asOfDate = DateTime.UtcNow
            let! currentName = 
                Projection.currentName repo benjiId asOfDate
                |> AsyncResult.map (fun n -> n.Name )
            Expect.equal currentName "Benji" "current name should be Benji"
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
            let! minnieCommands' = 
                minnieCommands 
                |> Result.mapError DogError.Validation
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
            let events = recordedEvents |> List.map (fun re -> re.Data)
            return Expect.equal events expectedMinnieEvents' "The events should equal"
        } 
        |> Async.RunSynchronously
        |> Expect.isOk 
        <| "should be ok"

        asyncResult {
            let! repo = repoResult |> AsyncResult.ofResult
            let asOfDate = DateTime.UtcNow
            let! currentName = 
                Projection.currentName repo minnieId asOfDate
                |> AsyncResult.map (fun n -> n.Name )
            Expect.equal currentName "Raggles" "current name should be Raggles"
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
