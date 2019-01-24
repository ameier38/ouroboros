[<RequireQualifiedAccess>]
module Dog.Projection

open Ouroboros

let mealsFolder acc event =
    match event with
    | DogEvent.Ate -> acc + 1
    | _ -> acc

let mealCount
    (handler:Handler<DogState,DogCommand,DogEvent>) =
    fun (dogId:EntityId) (observationDate:ObservationDate) ->
        asyncResult {
            let initialMealCount = 0
            let! recordedEvents = 
                (dogId, observationDate)
                ||> handler.replay 
            return
                recordedEvents
                |> List.map (fun e -> e.Data)
                |> List.fold mealsFolder initialMealCount
        }

let dogState
    (handler:Handler<DogState,DogCommand,DogEvent>) =
    fun (dogId:EntityId) (observationDate:ObservationDate) ->
        asyncResult {
            let! events =
                (dogId, observationDate)
                ||> handler.replay 
            let currentState = 
                events
                |> handler.reconstitute
                |> fun state -> state.ToString()
            let chooseBornEvent = function
                | {Event.Data = (DogEvent.Born dog)} -> Some dog
                | _ -> None
            let dogOpt = 
                events
                |> List.choose chooseBornEvent 
                |> function
                   | [] -> None
                   | head::_ -> Some head
            return!
                match dogOpt with
                | Some dog ->
                    let dogSchema = 
                        dog 
                        |> DogDto.fromDomain
                        |> DogSchema.fromDto
                    DogStateSchema(
                        state = currentState,
                        dog = dogSchema)
                    |> Ok
                | None ->
                    "no dog event found"
                    |> DomainError
                    |> Error
                |> AsyncResult.ofResult
        }
