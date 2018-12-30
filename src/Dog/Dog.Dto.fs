namespace Dog

open System
open System.Text
open Newtonsoft.Json
open Ouroboros

module String =
    let toBytes (s:string) = s |> Encoding.UTF8.GetBytes
    let fromBytes (bytes:byte[]) = bytes |> Encoding.UTF8.GetString

module Json =
    let serializeToJson (o:obj) =
        try
            JsonConvert.SerializeObject o
            |> Ok
        with ex ->
            sprintf "could not serialize: %A\n%A" o ex
            |> DogError.IO
            |> Error
    let serializeToBytes (o:obj) =
        serializeToJson o
        |> Result.map String.toBytes
    let deserializeFromJson<'Object> (json:string) =
        try
            json
            |> JsonConvert.DeserializeObject<'Object>
            |> Ok
        with ex ->
            sprintf "could not deserialize: %A" ex
            |> DogError.IO
            |> Error
    let deserializeFromBytes<'Object> (bytes:byte[]) =
        bytes
        |> String.fromBytes
        |> deserializeFromJson<'Object>

type DogDto =
    { name: string
      breed: string }
module DogDto =
    let serializeToBytes (dto:DogDto) = Json.serializeToBytes dto
    let deserializeFromBytes = Json.deserializeFromBytes<DogDto>
    let fromDomain (dog:Dog) =
        { name = Name.value dog.Name
          breed = Breed.value dog.Breed }
    let toDomain (dto:DogDto) =
        result {
            let! name = 
                Name.create dto.name 
                |> Result.mapError DogError.Validation
            let! breed = 
                Breed.create dto.breed 
                |> Result.mapError DogError.Validation
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
    let serializeToBytes (dto:DogEventDto) = Json.serializeToBytes dto
    let deserializeFromBytes = Json.deserializeFromBytes<DogEventDto>
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
    let serializeToBytes (dto:DogStateDto) = Json.serializeToBytes dto

type CreateDogCommandRequestDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime 
      name: string
      breed: string }
module CreateDogCommandRequestDto =
    let deserialize = Json.deserializeFromBytes<CreateDogCommandRequestDto>
    let toDomain
        (mapOuroborosError:OuroborosError -> DogError) =
        fun dto ->
            result {
                let dogId = dto.dogId |> EntityId
                let! command =
                    { DogDto.name = dto.name
                      breed = dto.breed }
                    |> DogCommandDto.Create
                    |> DogCommandDto.toDomain
                let! domainCommand =
                    (dto.source, dto.effectiveDate, command)
                    |||> Command.createDomainCommand mapOuroborosError
                return dogId, domainCommand
            }

type DogCommandRequestDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime }
module DogCommandRequestDto =
    let deserialize = Json.deserializeFromBytes<DogCommandRequestDto>
    let toDomain 
        (mapOuroborosError:OuroborosError -> DogError) =
        fun commandDto dto ->
            result {
                let dogId = dto.dogId |> EntityId
                let! command = commandDto |> DogCommandDto.toDomain
                let! domainCommand =
                    (dto.source, dto.effectiveDate, command)
                    |||> Command.createDomainCommand mapOuroborosError
                return dogId, domainCommand
            }

type GetRequestDto =
    { dogId: Guid
      asOfDate: DateTime }
module GetRequestDto =
    let deserialize = Json.deserializeFromBytes<GetRequestDto>
    let toDomain dto =
        (dto.dogId |> EntityId, dto.asOfDate |> AsOfDate)
