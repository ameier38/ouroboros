[<RequireQualifiedAccess>]
module Test.Dog.Projection

open Ouroboros
open Test.Dog.Implementation

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
