namespace Dog

open System
open Ouroboros

type DogDto =
    { name: string
      breed: string }
module DogDto =
    let serializeToBytes (dto:DogDto) = 
        Json.serializeToBytes dto
        |> Result.mapError DogError
    let deserializeFromBytes (bytes:byte []) = 
        Json.deserializeFromBytes<DogDto> bytes
        |> Result.mapError DogError
    let fromDomain (dog:Dog) =
        { name = Name.value dog.Name
          breed = Breed.value dog.Breed }
    let toDomain (dto:DogDto) =
        result {
            let! name = 
                Name.create dto.name 
                |> Result.mapError DogError
            let! breed = 
                Breed.create dto.breed 
                |> Result.mapError DogError
            return
                { Name = name
                  Breed = breed }
        }

type DogEventMetaDto =
    { EventSource: string }
module DogEventMetaDto =
    let toDomain (dto:DogEventMetaDto) =
        result {
            let! source = 
                dto.EventSource 
                |> Source.create
                |> Result.mapError DogError
            return
                { DogEventMeta.EventSource = source }
        }

    let fromDomain (meta:DogEventMeta) =
        { EventSource = meta.EventSource |> Source.value }

type DogEventDto =
    | Reversed of int64
    | Born of DogDto
    | Ate
    | Slept
    | Woke
    | Played
module DogEventDto =
    let fromDomain = function
        | DogEvent.Reversed eventNumber ->
            eventNumber  
            |> EventNumber.value
            |> Reversed
        | DogEvent.Born dog ->
            dog
            |> DogDto.fromDomain
            |> Born
        | DogEvent.Ate -> Ate
        | DogEvent.Slept -> Slept
        | DogEvent.Woke -> Woke
        | DogEvent.Played -> Played
    let toDomain = function
        | Reversed eventNumber ->
            eventNumber
            |> EventNumber.create
            |> Result.map DogEvent.Reversed
            |> Result.mapError DogError
        | Born dogDto ->
            dogDto
            |> DogDto.toDomain
            |> Result.map DogEvent.Born
        | Ate -> DogEvent.Ate |> Ok
        | Slept -> DogEvent.Slept |> Ok
        | Woke -> DogEvent.Woke |> Ok
        | Played -> DogEvent.Played |> Ok

type DogCommandMetaDto =
    { CommandSource: string }
module DogCommandMetaDto =
    let toDomain (dto:DogCommandMetaDto) =
        result {
            let! source = 
                dto.CommandSource 
                |> Source.create
                |> Result.mapError DogError
            return
                { DogCommandMeta.CommandSource = source }
        }

type DogCommandDto =
    | Reverse of int64
    | Create of DogDto
    | Eat
    | Sleep
    | Wake
    | Play
module DogCommandDto =
    let fromDomain = function
        | DogCommand.Reverse eventNumber ->
            eventNumber
            |> EventNumber.value
            |> Reverse
        | DogCommand.Create dog -> 
            dog
            |> DogDto.fromDomain
            |> Create
        | DogCommand.Eat -> Eat
        | DogCommand.Sleep -> Sleep
        | DogCommand.Wake -> Wake
        | DogCommand.Play -> Play
    let toDomain = function
        | Reverse eventNumber ->
            eventNumber
            |> EventNumber.create
            |> Result.map DogCommand.Reverse
            |> Result.mapError DogError
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
    let serializeToBytes (dto:DogStateDto) = 
        Json.serializeToBytes dto
        |> Result.mapError DogError

type CreateDogCommandRequestDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime 
      name: string
      breed: string }
module CreateDogCommandRequestDto =
    let deserializeFromBytes (bytes:byte []) = 
        bytes
        |> Json.deserializeFromBytes<CreateDogCommandRequestDto>
        |> Result.mapError DogError
    let toDomain (dto:CreateDogCommandRequestDto) =
        result {
            let dogId = dto.dogId |> EntityId
            let effectiveDate = dto.effectiveDate |> EffectiveDate
            let! dogCommandMeta =
                { CommandSource = dto.source }
                |> DogCommandMetaDto.toDomain
            let commandMeta =
                { EffectiveDate = effectiveDate
                  DomainCommandMeta = dogCommandMeta }
            let! dogCommand =
                { DogDto.name = dto.name
                  breed = dto.breed }
                |> DogCommandDto.Create
                |> DogCommandDto.toDomain
            let command =
                { Data = dogCommand
                  Meta = commandMeta }
            return dogId, command
        }

type DogCommandRequestDto =
    { dogId: Guid
      source: string
      effectiveDate: DateTime }
module DogCommandRequestDto =
    let deserializeFromBytes (bytes:byte []) = 
        bytes
        |> Json.deserializeFromBytes<DogCommandRequestDto>
        |> Result.mapError DogError
    let toDomain 
        (commandDto:DogCommandDto) =
        fun dto ->
            result {
                let dogId = dto.dogId |> EntityId
                let effectiveDate = dto.effectiveDate |> EffectiveDate
                let! dogCommandMeta =
                    { CommandSource = dto.source }
                    |> DogCommandMetaDto.toDomain
                let commandMeta =
                    { CommandMeta.EffectiveDate = effectiveDate
                      DomainCommandMeta = dogCommandMeta }
                let! dogCommand = 
                    commandDto 
                    |> DogCommandDto.toDomain
                let command =
                    { Command.Data = dogCommand
                      Meta = commandMeta }
                return dogId, command
            }

type GetRequestDto =
    { dogId: Guid
      observationDate: DateTime
      observationType: string }
module GetRequestDto =
    let deserializeFromBytes (bytes:byte []) = 
        bytes
        |> Json.deserializeFromBytes<GetRequestDto>
        |> Result.mapError DogError
    let toDomain (dto:GetRequestDto) =
        let dogId = dto.dogId |> EntityId
        dto.observationType
        |> fun ot -> ot.ToLower()
        |> function
           | obsType when obsType = "of" -> 
                dto.observationDate 
                |> AsOf 
                |> fun obsDate -> (dogId, obsDate)
                |> Ok
           | obsType when obsType = "at" ->
                dto.observationDate 
                |> AsAt 
                |> fun obsDate -> (dogId, obsDate)
                |> Ok
           | other -> 
                sprintf "%s is not a valid observationType" other 
                |> DogError 
                |> Error
