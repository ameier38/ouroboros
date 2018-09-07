namespace Test.Dog

open System
open Vertigo.Json
open Ouroboros

type DogDto =
    { name: string
      breed: string }
module DogDto =
    let serialize (dto:DogDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DogDto %A\n%A" dto ex |> DogError.IO |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DogDto> json |> Ok
        with ex -> sprintf "could not deserialize DogDto %A\n%A" json ex |> DogError.IO |> Error
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
    | Renamed of string
    | Ate of string
    | Slept
    | Woke
    | Played
module DogEventDto =
    let serialize (dto:DogEventDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DogEventDto %A\n%A" dto ex |> DogError.IO |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DogEventDto> json |> Ok
        with ex -> sprintf "could not deserialize DogEventDto %A\n%A" json ex |> DogError.IO |> Error
    let fromDomain = function
        | DogEvent.Born dog ->
            dog
            |> DogDto.fromDomain
            |> DogEventDto.Born
        | DogEvent.Renamed name ->
            name
            |> Name.value
            |> DogEventDto.Renamed
        | DogEvent.Ate name ->
            name
            |> Name.value
            |> DogEventDto.Ate
        | DogEvent.Slept ->
            DogEventDto.Slept
        | DogEvent.Woke ->
            DogEventDto.Woke
        | DogEvent.Played ->
            DogEventDto.Played
    let toDomain = function
        | DogEventDto.Born dogDto ->
            dogDto
            |> DogDto.toDomain
            |> Result.map DogEvent.Born
            |> Result.mapError DogError.Validation
        | DogEventDto.Renamed name ->
            name            
            |> Name.create
            |> Result.map DogEvent.Renamed
            |> Result.mapError DogError.Validation
        | DogEventDto.Ate name ->
            name            
            |> Name.create
            |> Result.map DogEvent.Ate
            |> Result.mapError DogError.Validation
        | DogEventDto.Slept ->
            DogEvent.Slept
            |> Ok
        | DogEventDto.Woke ->
            DogEvent.Woke
            |> Ok
        | DogEventDto.Played ->
            DogEvent.Played
            |> Ok

type DogCommandDto =
    | Create of DateTime * DogDto
    | ChangeName of DateTime * string
    | CallToEat of DateTime * string
    | Sleep of DateTime
    | Wake of DateTime
    | Play of DateTime
module DogCommandDto =
    let serialize (dto:DogCommandDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DogCommandDto %A\n%A" dto ex |> DogError.IO |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DogCommandDto> json |> Ok
        with ex -> sprintf "could not deserialize DogCommandDto %A\n%A" json ex |> DogError.IO |> Error
    let fromDomain = function
        | DogCommand.Create (EffectiveDate effectiveDate, dog) ->
            let dogDto = dog |> DogDto.fromDomain
            (effectiveDate, dogDto)
            |> DogCommandDto.Create
        | DogCommand.Rename (EffectiveDate effectiveDate, name) ->
            let name' = name |> Name.value
            (effectiveDate, name')
            |> DogCommandDto.ChangeName
        | DogCommand.CallToEat (EffectiveDate effectiveDate, name) ->
            let name' = Name.value name
            DogCommandDto.CallToEat (effectiveDate, name')
        | DogCommand.Sleep (EffectiveDate effectiveDate) ->
            DogCommandDto.Sleep effectiveDate
        | DogCommand.Wake (EffectiveDate effectiveDate) ->
            DogCommandDto.Wake effectiveDate
        | DogCommand.Play (EffectiveDate effectiveDate) ->
            DogCommandDto.Play effectiveDate
    let toDomain = function
        | DogCommandDto.Create (effectiveDate, dogDto) ->
            result {
                let! dog = dogDto |> DogDto.toDomain
                return
                    (EffectiveDate effectiveDate, dog)
                    |> DogCommand.Create
            }
        | DogCommandDto.ChangeName (effectiveDate, name) ->
            result {
                let! name' = name |> Name.create
                return
                    (EffectiveDate effectiveDate, name')
                    |> DogCommand.Rename
            }
        | DogCommandDto.CallToEat (effectiveDate, name) ->
            result {
                let! name' = Name.create name
                return
                    (EffectiveDate effectiveDate, name')
                    |> DogCommand.CallToEat
            }
        | DogCommandDto.Sleep effectiveDate ->
            EffectiveDate effectiveDate |> DogCommand.Sleep |> Ok
        | DogCommandDto.Wake effectiveDate ->
            EffectiveDate effectiveDate |> DogCommand.Wake |> Ok
        | DogCommandDto.Play effectiveDate ->
            EffectiveDate effectiveDate |> DogCommand.Play |> Ok
