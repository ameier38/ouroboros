namespace Dog

open System
open Ouroboros
open Ouroboros.Api
open Vertigo.Json

module Json =
    let trySerializeToBytes x =
        try Json.serializeToBytes x |> Ok
        with ex -> sprintf "could not serialize %A\n%A" x ex |> DogError.IO |> Error
    let trySerializeToJson x =
        try Json.serialize x |> Ok
        with ex -> sprintf "could not serialize %A\n%A" x ex |> DogError.IO |> Error
    let tryDeserializeFromBytes<'Dto> bytes =
        try Json.deserializeFromBytes<'Dto> bytes |> Ok
        with ex -> sprintf "could not deserialize %A\n%A" bytes ex |> DogError.IO |> Error
    let tryDeserializeFromJson<'Dto> json =
        try Json.deserialize<'Dto> json |> Ok
        with ex -> sprintf "could not deserialize %A\n%A" json ex |> DogError.IO |> Error


type DogDto =
    { name: string
      breed: string }
module DogDto =
    let serializeToBytes (dto:DogDto) = Json.trySerializeToBytes dto
    let deserializeFromBytes = Json.tryDeserializeFromBytes<DogDto>
    let fromDomain (dog:Dog) =
        { name = Name.value dog.Name
          breed = Breed.value dog.Breed }
    let toDomain (dto:DogDto) =
        result {
            let! name = Name.create dto.name
            let! breed = Breed.create dto.breed
            return
                { Name = name
                  Breed = breed }
        }

type DogEventDto =
    | Born of DogDto
    | Ate
    | Slept
    | Woke
    | Played
module DogEventDto =
    let serializeToBytes (dto:DogEventDto) = Json.trySerializeToBytes dto
    let serializeToJson (dto:DogEventDto) = Json.trySerializeToJson dto
    let deserializeFromBytes = Json.tryDeserializeFromBytes<DogEventDto>
    let fromDomain = function
        | DogEvent.Born dog ->
            dog
            |> DogDto.fromDomain
            |> DogEventDto.Born
        | DogEvent.Ate -> DogEventDto.Ate
        | DogEvent.Slept -> DogEventDto.Slept
        | DogEvent.Woke -> DogEventDto.Woke
        | DogEvent.Played -> DogEventDto.Played
    let toDomain = function
        | DogEventDto.Born dogDto ->
            dogDto
            |> DogDto.toDomain
            |> Result.map DogEvent.Born
            |> Result.mapError DogError.Validation
        | DogEventDto.Ate -> DogEvent.Ate |> Ok
        | DogEventDto.Slept -> DogEvent.Slept |> Ok
        | DogEventDto.Woke -> DogEvent.Woke |> Ok
        | DogEventDto.Played -> DogEvent.Played |> Ok

type DogCommandDto =
    | Create of DogDto
    | Eat
    | Sleep
    | Wake
    | Play
module DogCommandDto =
    let fromDomain = function
        | DogCommand.Create dog -> 
            dog
            |> DogDto.fromDomain
            |> Create
        | DogCommand.Eat -> Eat
        | DogCommand.Sleep -> Sleep
        | DogCommand.Wake -> Wake
        | DogCommand.Play -> Play
    let toDomain = function
        | Create dogDto ->
            dogDto
            |> DogDto.toDomain
            |> Result.map DogCommand.Create
        | Eat -> DogCommand.Eat |> Ok
        | Sleep -> DogCommand.Sleep |> Ok
        | Wake -> DogCommand.Wake |> Ok
        | Play -> DogCommand.Play |> Ok

type DogStateDto =
    { state: string
      dog: DogDto option }
module DogStateDto =
    let serializeToJson (dto:DogStateDto) = Json.trySerializeToJson dto

type CreateCommandInputDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime 
      name: string
      breed: string }
module CreateCommandInputDto =
    let deserialize = Json.tryDeserializeFromJson<CreateCommandInputDto>
    let toDomain dto =
        result {
            let dogId = dto.dogId |> EntityId
            let! command =
                { DogDto.name = dto.name
                  breed = dto.breed }
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
    let toDomain dto =
        (dto.dogId |> EntityId, dto.asOfDate |> EffectiveDate)
