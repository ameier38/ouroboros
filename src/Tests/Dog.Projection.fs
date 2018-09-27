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
    fun entityId asOfDate ->
        asyncResult {
            let initialMealCount = 0
            let! recordedEvents = repo.load entityId
            let filteredEvents =
                recordedEvents
                |> List.choose (function
                    | RecordedDomainEvent e -> Some e
                    | _ -> None)
                |> List.filter (fun { CreatedDate = (CreatedDate createdDate)} -> 
                    createdDate <= asOfDate)
                |> List.map (fun e -> e.Data)
            return
                filteredEvents
                |> List.fold mealsFolder initialMealCount
        }
