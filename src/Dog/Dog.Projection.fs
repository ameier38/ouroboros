[<RequireQualifiedAccess>]
module Dog.Projection

open Ouroboros
open Dog

let mealsFolder acc event =
    match event with
    | DogEvent.Ate -> acc + 1
    | _ -> acc

let mealCount
    (queryHandler:QueryHandler<DogState,DogEvent>) =
    fun dogId observationDate ->
        asyncResult {
            let initialMealCount = 0
            let! recordedEvents = 
                (dogId, observationDate)
                ||> queryHandler.replay 
            return
                recordedEvents
                |> List.map (fun e -> e.Data)
                |> List.fold mealsFolder initialMealCount
        }

let dogState
    (queryHandler:QueryHandler<DogState,DogEvent>) =
    fun dogId observationDate ->
        asyncResult {
            let! recordedEvents =
                (dogId, observationDate)
                ||> queryHandler.replay 
            let currentState = 
                recordedEvents
                |> queryHandler.reconstitute
                |> fun state -> state.ToString()
            let chooseBornEvent = function
                | {RecordedEvent.Data = (DogEvent.Born dog)} -> Some dog
                | _ -> None
            let dogOpt = 
                recordedEvents
                |> List.choose chooseBornEvent 
                |> function
                   | [] -> None
                   | head::_ -> Some head
            return!
                match dogOpt with
                | Some dog ->
                    let dogDto = dog |> DogDto.fromDomain
                    new DogStateDto(
                        state = currentState,
                        dog = dogDto)
                    |> Ok
                | None ->
                    "no dog event found"
                    |> DomainError
                    |> Error
                |> AsyncResult.ofResult
        }
