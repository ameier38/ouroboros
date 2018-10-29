module Dog.Handler

open Dog
open Dog.Implementation
open Vertigo.Json

let (|Matches|_|) pattern value =
    if value = pattern 
    then Some Matches
    else None

let (|OtherCommandPath|_|) = function
    | Matches "eat" -> DogCommandDto.Eat |> Some
    | Matches "sleep" -> DogCommandDto.Sleep |> Some
    | Matches "wake" -> DogCommandDto.Wake |> Some
    | Matches "play" -> DogCommandDto.Play |> Some
    | _ -> None

let executeCommand dogId domainCommand =
    asyncResult {
        let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
        let handle = commandHandler.handle dogId
        return! handle [ domainCommand ]
    }

let handlePost path input =
    match path with
    | Matches "get" ->
        asyncResult {
            let! queryHandler = 
                queryHandlerResult 
                |> AsyncResult.ofResult
            let! dto =
                input
                |> GetInputDto.deserialize
                |> AsyncResult.ofResult
            let dogId, asOfDate =
                dto
                |> GetInputDto.toDomain
            let! dogStateDto =
                (dogId, asOfDate)
                ||> Projection.dogState queryHandler
            return!
                dogStateDto
                |> Json.trySerializeToJson
                |> AsyncResult.ofResult
        }
    | Matches "create" ->
        asyncResult {
            let! dto = 
                input 
                |> CreateCommandInputDto.deserialize
                |> AsyncResult.ofResult
            let! dogId, domainCommand =
                dto
                |> CreateCommandInputDto.toDomain
                |> AsyncResult.ofResult
                |> AsyncResult.mapError DogError.Validation
            let! events =
                (dogId, domainCommand)
                ||> executeCommand
            return!
                events
                |> Json.trySerializeToJson
                |> AsyncResult.ofResult
        }
    | OtherCommandPath commandDto ->
        asyncResult {
            let! dto = 
                input 
                |> OtherCommandInputDto.deserialize
                |> AsyncResult.ofResult
            let! dogId, domainCommand =
                dto
                |> OtherCommandInputDto.toDomain commandDto
                |> AsyncResult.ofResult
                |> AsyncResult.mapError DogError.Validation
            let! events =
                (dogId, domainCommand)
                ||> executeCommand
            return!
                events
                |> Json.trySerializeToJson
                |> AsyncResult.ofResult
        }
    | invalid ->
        sprintf "invalid path %s" invalid
        |> DogError.Validation
        |> AsyncResult.ofError
