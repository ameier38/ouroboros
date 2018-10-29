[<RequireQualifiedAccess>]
module Dog.Projection

open Ouroboros
open Ouroboros.Api
open Dog
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

let dogState
    (queryHandler:QueryHandler<DogState, DogEvent, DogError>) =
    fun dogId asOfDate ->
        asyncResult {
            let asOf = asOfDate |> AsOf.Specific
            let! domainEvents =
                (dogId, asOf)
                ||> queryHandler.replay 
                |> AsyncResult.map (List.map RecordedDomainEvent.toDomainEvent)
            let! currentState = 
                queryHandler.reconstitute domainEvents
                |> AsyncResult.ofResult
            let isBornEvent = function
                | {DomainEvent.Data = (DogEvent.Born dog)} -> Some dog
                | _ -> None
            let dogDtoOpt = 
                domainEvents
                |> List.choose isBornEvent 
                |> function
                   | [] -> None
                   | l -> l |> List.head |> DogDto.fromDomain |> Some
            return
                { state = currentState.ToString()
                  dog = dogDtoOpt }
        }