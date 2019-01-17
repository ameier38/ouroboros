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
            |> DomainError
            |> Error
    let fromDomain (dog:Dog) =
        new DogDto(
            name = (dog.Name |> Name.value),
            breed = (dog.Breed |> Breed.value))
    let toDomain (dto:DogDto) =
        result {
            let! name = 
                Name.create dto.Name 
                |> Result.mapError DomainError
            let! breed = 
                Breed.create dto.Breed 
                |> Result.mapError DomainError
            return
                { Name = name
                  Breed = breed }
        }

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
            |> Result.mapError DomainError
        | Born dogDto ->
            dogDto
            |> DogDto.toDomain
            |> Result.map DogEvent.Born
        | Ate -> DogEvent.Ate |> Ok
        | Slept -> DogEvent.Slept |> Ok
        | Woke -> DogEvent.Woke |> Ok
        | Played -> DogEvent.Played |> Ok

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
            |> Result.mapError DomainError
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
            |> DomainError
            |> Error
    let toDomain (dto:CreateDogRequestDto) =
        result {
            let dogId = 
                dto.DogId 
                |> EntityId
            let effectiveDate = 
                dto.EffectiveDate 
                |> EffectiveDate
            let! source = 
                dto.Source 
                |> Source.create 
                |> Result.mapError DomainError
            let commandMeta =
                { EffectiveDate = effectiveDate
                  Source = source }
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
            |> DomainError
            |> Error
    let toDomain (dto:ReverseRequestDto) =
        result {
            let dogId = 
                dto.DogId 
                |> EntityId
            let effectiveDate = 
                dto.EffectiveDate 
                |> EffectiveDate
            let! source = 
                dto.Source 
                |> Source.create 
                |> Result.mapError DomainError
            let commandMeta =
                { EffectiveDate = effectiveDate
                  Source = source }
            let! dogCommand =
                dto.EventNumber 
                |> int64
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
            |> DomainError
            |> Error
    let toDomain 
        (commandDto:DogCommandDto) =
        fun (dto:CommandRequestDto) ->
            result {
                let dogId = 
                    dto.DogId 
                    |> EntityId
                let effectiveDate = 
                    dto.EffectiveDate 
                    |> EffectiveDate
                let! source = 
                    dto.Source 
                    |> Source.create 
                    |> Result.mapError DomainError
                let commandMeta =
                    { CommandMeta.EffectiveDate = effectiveDate
                      Source = source }
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
            |> DomainError
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
            |> fun obsDate -> (obsDate, dogId)
            |> Ok
        | At ->
            dto.ObservationDate 
            |> AsAt 
            |> fun obsDate -> (obsDate, dogId)
            |> Ok
        | Invalid as obsType ->
            sprintf "%s is not a valid observation type; options are 'as' or 'of'" obsType
            |> DomainError
            |> Error

type CommandResponseDto = OpenApi.DogApi.Schemas.CommandResponse
module CommandResponseDto =
    let serializeToJson (dto:CommandResponseDto) =
        dto.ToJson()
    let serializeToBytes (dto:CommandResponseDto) =
        dto.ToJson()
        |> String.toBytes
    let fromEvents (events:Event<'DomainEvent> list) =
        events
        |> List.map (fun ({Event.Type = eventType}) -> eventType |> EventType.value)
        |> fun eventTypes ->
            new CommandResponseDto(
                committedEvents = eventTypes)
