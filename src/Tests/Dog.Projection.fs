[<RequireQualifiedAccess>]
module Test.Dog.Projection

open Ouroboros
open Test.Dog.Implementation

type CurrentName =
    { Name: string }

let currentNameFolder acc event =
    match event with
    | { RecordedEvent.Data = (DogEvent.Born dog) } -> { acc with CurrentName.Name = dog.Name |> Name.value }
    | { RecordedEvent.Data = (DogEvent.Renamed name) } -> { acc with CurrentName.Name = name |> Name.value }
    | _ -> acc

let currentName
    (repo:Repository<DogEvent, DogError>) =
    fun entityId asOfDate ->
        asyncResult {
            let initialCurrentName = { Name = "" }
            let! events = repo.load entityId
            let filteredEvents =
                events
                |> List.filter (fun { CreatedDate = (CreatedDate createdDate)} -> createdDate <= asOfDate)
            return
                filteredEvents
                |> List.fold currentNameFolder initialCurrentName
        }
