namespace Dog

open OpenAPITypeProvider
open Ouroboros

module OpenApi =
    let [<Literal>] DogApiSchema = "openapi.yaml"
    type DogApi = OpenAPIV3Provider<DogApiSchema>

type DogDto = OpenApi.DogApi.Schemas.Dog
module DogDto =
    let serializeToJson (dogDto:DogDto) =
        dogDto.ToJson()
    let serializeToBytes (dogDto:DogDto) = 
        dogDto.ToJson()
        |> String.toBytes
    let deserializeFromBytes (bytes:byte []) = 
        try
            bytes
            |> String.fromBytes
            |> DogDto.Parse
            |> Ok
        with ex ->
            sprintf "could not parse DogDto %A" ex
            |> DogError
            |> Error
    let fromDomain (dog:Dog) =
        new DogDto(
            name = (dog.Name |> Name.value),
            breed = (dog.Breed |> Breed.value))
    let toDomain (dto:DogDto) =
        result {
            let! name = 
                Name.create dto.Name 
                |> Result.mapError DogError
            let! breed = 
                Breed.create dto.Breed 
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

type DogStateDto = OpenApi.DogApi.Schemas.DogState
module DogStateDto =
    let serializeToJson (dto:DogStateDto) =
        dto.ToJson()
    let serializeToBytes (dto:DogStateDto) = 
        dto.ToJson()
        |> String.toBytes

type CreateDogRequestDto = OpenApi.DogApi.Schemas.CreateDogRequest
module CreateDogRequestDto =
    let deserializeFromBytes (bytes:byte []) = 
        try
            bytes
            |> String.fromBytes
            |> CreateDogRequestDto.Parse
            |> Ok
        with ex ->
            sprintf "could not parse CreateDogRequestDto %A" ex
            |> DogError
            |> Error
    let toDomain (dto:CreateDogRequestDto) =
        result {
            let dogId = dto.DogId |> EntityId
            let effectiveDate = dto.EffectiveDate |> EffectiveDate
            let! dogCommandMeta =
                { CommandSource = dto.Source }
                |> DogCommandMetaDto.toDomain
            let commandMeta =
                { EffectiveDate = effectiveDate
                  DomainCommandMeta = dogCommandMeta }
            let! dogCommand =
                dto.Dog
                |> DogCommandDto.Create
                |> DogCommandDto.toDomain
            let command =
                { Data = dogCommand
                  Meta = commandMeta }
            return dogId, command
        }

type ReverseRequestDto = OpenApi.DogApi.Schemas.ReverseRequest
module ReverseRequestDto =
    let deserializeFromBytes (bytes:byte []) =
        try
            bytes
            |> String.fromBytes
            |> ReverseRequestDto.Parse
            |> Ok
        with ex ->
            sprintf "could not parse ReverseRequestDto %A" ex
            |> DogError
            |> Error
    let toDomain (dto:ReverseRequestDto) =
        result {
            let dogId = dto.DogId |> EntityId
            let effectiveDate = dto.EffectiveDate |> EffectiveDate
            let! dogCommandMeta =
                { CommandSource = dto.Source }
                |> DogCommandMetaDto.toDomain
            let commandMeta =
                { EffectiveDate = effectiveDate
                  DomainCommandMeta = dogCommandMeta }
            let eventNumber = dto.EventNumber |> int64
            let! dogCommand =
                eventNumber
                |> DogCommandDto.Reverse
                |> DogCommandDto.toDomain
            let command =
                { Data = dogCommand
                  Meta = commandMeta }
            return dogId, command
        }

type CommandRequestDto = OpenApi.DogApi.Schemas.CommandRequest
module CommandRequestDto =
    let deserializeFromBytes (bytes:byte []) = 
        try
            bytes
            |> String.fromBytes
            |> CommandRequestDto.Parse
            |> Ok
        with ex ->
            sprintf "could not parse CommandRequestDto %A" ex
            |> DogError
            |> Error
    let toDomain 
        (commandDto:DogCommandDto) =
        fun (dto:CommandRequestDto) ->
            result {
                let dogId = dto.DogId |> EntityId
                let effectiveDate = dto.EffectiveDate |> EffectiveDate
                let! dogCommandMeta =
                    { CommandSource = dto.Source }
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

type GetDogRequestDto = OpenApi.DogApi.Schemas.GetDogRequest
module GetDogRequestDto =
    let deserializeFromBytes (bytes:byte []) = 
        try
            bytes
            |> String.fromBytes
            |> GetDogRequestDto.Parse
            |> Ok
        with ex ->
            sprintf "could not parse GetRequestDto %A" ex
            |> DogError
            |> Error
    let (|Of|At|Invalid|) obsType =
        match obsType with
        | s when (s |> String.lower) = "of" -> Of
        | s when (s |> String.lower) = "at" -> At
        | _ -> Invalid
    let toDomain (dto:GetDogRequestDto) =
        let dogId = dto.DogId |> EntityId
        match dto.ObservationType with 
        | Of -> 
            dto.ObservationDate 
            |> AsOf 
            |> fun obsDate -> (dogId, obsDate)
            |> Ok
        | At ->
            dto.ObservationDate 
            |> AsAt 
            |> fun obsDate -> (dogId, obsDate)
            |> Ok
        | Invalid as obsType ->
            sprintf "%s is not a valid observation type; options are 'as' or 'of'" obsType
            |> DogError
            |> Error

type CommandResponseDto = OpenApi.DogApi.Schemas.CommandResponse
module CommandResponseDto =
    let serializeToJson (dto:CommandResponseDto) =
        dto.ToJson()
    let serializeToBytes (dto:CommandResponseDto) =
        dto.ToJson()
        |> String.toBytes
    let fromEvents (events:Event<'DomainEvent,'DomainEventMeta> list) =
        events
        |> List.map (fun ({Event.Type = eventType}) -> eventType |> EventType.value)
        |> fun eventTypes ->
            new CommandResponseDto(
                committedEvents = eventTypes)
