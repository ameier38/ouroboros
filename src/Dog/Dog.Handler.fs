module Dog.Handler

open Dog
open Dog.Implementation
open Ouroboros
open System
open Vertigo.Json

type CreateCommandInputDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime 
      name: string
      breed: string }
module CreateCommandInputDto =
    let deserialize json =
        try Json.deserialize<CreateCommandInputDto> json |> Ok
        with ex -> 
            sprintf "could not deserialize CreateCommandInputDto %A\n%A" json ex 
            |> DogError.IO 
            |> Error
    let toDomain dto =
        result {
            let dogId = dto.dogId |> EntityId
            let! command =
                { DogDto.name = dto.name
                  breed = dogRequest.breed }
                |> DogCommandDto.Create
                |> DogCommandDto.toDomain
            let! domainCommand =
                (dto.source, dto.effectiveDate, command)
                |||> Command.createDomainCommand
            return dogId, domainCommand
        }


type OtherCommandInputDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime }
module OtherCommandInputDto =
    let deserialize json =
        try Json.deserialize<OtherCommandInputDto> json |> Ok
        with ex -> 
            sprintf "could not deserialize OtherCommandInputDto %A\n%A" json ex 
            |> DogError.IO 
            |> Error
    let toDomain commandDto dto =
        result {
            let dogId = dto.dogId |> EntityId
            let! command = commandDto |> DogCommandDto.toDomain
            let! domainCommand =
                (dto.source, dto.effectiveDate, command)
                |||> Command.createDomainCommand
            return dogId, domainCommand
        }

type GetInputDto =
    { dogId: Guid
      asOfDate: DateTime }
module GetInputDto =
    let deserialize json =
        try Json.deserialize<GetInputDto> json |> Ok
        with ex ->
            sprintf "could not deserialize GetInputDto %A\n%A" json ex 
            |> DogError.IO 
            |> Error

let (|Matches|_|) pattern value =
    if value = pattern 
    then Some Matches
    else None

let (|OtherPath|_|) = function
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
let handlePost path input =
    match path with
    | Matches "create" ->
        asyncResult {
            let! dto = 
                input 
                |> CreateCommandInputDto.deserialize
            let! dogId, domainCommand =
                dto
                |> CreateCommandInputDto.toDomain
            return!
                (dogId, domainCommand)
                ||> executeCommand
        }
    | OtherPath commandDto ->
        asyncResult {
            let! dto = 
                input 
                |> OtherCommandInputDto.deserialize
            let! dogId, domainCommand =
                dto
                |> OtherCommandInputDto.toDomain commandDto
            return!
                (dogId, domainCommand)
                ||> executeCommand
        }
    | invalid ->
        sprintf "invalid path %s" invalid
        |> DogError.Validation
        |> AsyncResult.ofError
