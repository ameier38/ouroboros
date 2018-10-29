module Dog.Handler

open Dog
open Dog.Implementation
open Ouroboros
open Ouroboros.Api
open System
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
        let! handler = handlerResult |> AsyncResult.ofResult
        let handle = handler.handle dogId
        return! handle [ domainCommand ]
    }

let reconstituteState dogId effectiveDate =
    asyncResult {
        let! handler = handlerResult |> AsyncResult.ofResult
        let reconstitute = handler.reconstitute dogId
        return! reconstitute effectiveDate
    }

let handlePost path input =
    match path with
    | Matches "get" ->
        asyncResult {
            let! dto =
                input
                |> GetInputDto.deserialize
                |> AsyncResult.ofResult
            let dogId, effectiveDate =
                dto
                |> GetInputDto.toDomain
            return! reconstituteState dogId effectiveDate
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
            return!
                (dogId, domainCommand)
                ||> executeCommand
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
            return!
                (dogId, domainCommand)
                ||> executeCommand
        }
    | invalid ->
        sprintf "invalid path %s" invalid
        |> DogError.Validation
        |> AsyncResult.ofError
