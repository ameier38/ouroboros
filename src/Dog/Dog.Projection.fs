[<RequireQualifiedAccess>]
module Dog.Projection

open Ouroboros
open Dog
open Dog.Implementation

let mealsFolder acc event =
    match event with
    | DogEvent.Ate -> acc + 1
    | _ -> acc

let mealCount
    (queryHandler:QueryHandler<DogState,DogEventDto,DogEventMetaDto,DogError>) =
    fun dogId observationDate ->
        asyncResult {
            let initialMealCount = 0
            let! recordedEvents = 
                (dogId, observationDate)
                ||> queryHandler.replay 
            return!
                recordedEvents
                |> List.map (fun e -> e.Data)
                |> List.map DogEventDto.toDomain
                |> Result.sequence
                |> Result.map (List.fold mealsFolder initialMealCount)
                |> AsyncResult.ofResult
        }

let dogState
    (queryHandler:QueryHandler<DogState,DogEventDto,DogEventMetaDto,DogError>) =
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
                | {RecordedEvent.Data = (DogEventDto.Born dogDto)} -> Some dogDto
                | _ -> None
            let dogDtoOpt = 
                recordedEvents
                |> List.choose chooseBornEvent 
                |> function
                   | [] -> None
                   | head::_ -> Some head
            return!
                match dogDtoOpt with
                | Some dogDto ->
                    new DogStateDto(
                        state = currentState,
                        dog = dogDto)
                    |> Ok
                | None ->
                    "no dog event found"
                    |> DogError
                    |> Error
                |> AsyncResult.ofResult
        }
