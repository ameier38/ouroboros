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
    fun entityId ->
        asyncResult {
            let initialMealCount = 0
            let! domainEvents = repo.load entityId
            return
                domainEvents
                |> List.map (fun e -> e.Data)
                |> List.fold mealsFolder initialMealCount
        }

let dogState
    (repo:Repository<DogEvent, DogError>) =
    fun entityId ->
        asyncResult {
            let! domainEvents = repo.load entityId
            return!
                domainEvents
                |> List.fold aggregate.apply aggregate.zero
                |> AsyncResult.ofResult
        }
