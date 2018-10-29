[<RequireQualifiedAccess>]
module Dog.Projection

open Ouroboros
open Dog.Implementation

let mealsFolder acc event =
    match event with
    | DogEvent.Ate -> acc + 1
    | _ -> acc

let mealCount
    (repo:Repository<DogEvent, DogError>) =
    fun dogId ->
        asyncResult {
            let initialMealCount = 0
            let! domainEvents = repo.load dogId
            return
                domainEvents
                |> List.map (fun e -> e.Data)
                |> List.fold mealsFolder initialMealCount
        }

let stateFolder acc event =
    match event with
    | DogEvent.Created dog -> 
        { state = ""}

let dogState
    (repo:Repository<DogEvent, DogError>) =
    fun dogId asOfDate ->
        asyncResult {
            let! recordedDomainEvents = repo.load dogId
            let filteredDomainEvents =
                recordedDomainEvents
                |> List.filter ()
        }