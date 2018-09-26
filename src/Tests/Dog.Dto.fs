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
    | Ate
    | Slept
    | Woke
    | Played
module DogEventDto =
    let serialize (dto:DogEventDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> 
            sprintf "could not serialize DogEventDto %A\n%A" dto ex 
            |> DogError.IO 
            |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DogEventDto> json |> Ok
        with ex -> 
            sprintf "could not deserialize DogEventDto %A\n%A" json ex 
            |> DogError.IO 
            |> Error
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
    | Create of string * DateTime * DogDto
    | Eat of string * DateTime
    | Sleep of string * DateTime
    | Wake of string * DateTime
    | Play of string * DateTime
module DogCommandDto =
    let serialize (dto:DogCommandDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> 
            sprintf "could not serialize DogCommandDto %A\n%A" dto ex 
            |> DogError.IO 
            |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DogCommandDto> json |> Ok
        with ex -> 
            sprintf "could not deserialize DogCommandDto %A\n%A" json ex 
            |> DogError.IO 
            |> Error
    let fromDomain = function
        | DogCommand.Create (source, effectiveDate, dog) ->
            let source' = source |> Source.value
            let effectiveDate' = effectiveDate |> EffectiveDate.value
            let dogDto = dog |> DogDto.fromDomain
            (source', effectiveDate', dogDto)
            |> DogCommandDto.Create
        | DogCommand.Eat (source, effectiveDate) ->
            let source' = source |> Source.value
            let effectiveDate' = effectiveDate |> EffectiveDate.value
            (source', effectiveDate')
            |> DogCommandDto.Eat
        | DogCommand.Sleep (source, effectiveDate) ->
            let source' = source |> Source.value
            let effectiveDate' = effectiveDate |> EffectiveDate.value
            (source', effectiveDate')
            |> DogCommandDto.Sleep
        | DogCommand.Wake (source, effectiveDate) ->
            let source' = source |> Source.value
            let effectiveDate' = effectiveDate |> EffectiveDate.value
            (source', effectiveDate')
            |> DogCommandDto.Wake
        | DogCommand.Play (source, effectiveDate) ->
            let source' = source |> Source.value
            let effectiveDate' = effectiveDate |> EffectiveDate.value
            (source', effectiveDate')
            |> DogCommandDto.Play
    let toDomain = function
        | DogCommandDto.Create (source, effectiveDate, dogDto) ->
            result {
                let! source' = source |> Source.create
                let effectiveDate' = effectiveDate |> EffectiveDate
                let! dog = dogDto |> DogDto.toDomain
                return
                    (source', effectiveDate', dog)
                    |> DogCommand.Create
            }
        | DogCommandDto.Eat (source, effectiveDate) ->
            result {
                let! source' = source |> Source.create
                let effectiveDate' = effectiveDate |> EffectiveDate
                return
                    (source', effectiveDate')
                    |> DogCommand.Eat
            }
        | DogCommandDto.Sleep (source, effectiveDate) ->
            result {
                let! source' = source |> Source.create
                let effectiveDate' = effectiveDate |> EffectiveDate
                return
                    (source', effectiveDate')
                    |> DogCommand.Sleep
            }
        | DogCommandDto.Wake (source, effectiveDate) ->
            result {
                let! source' = source |> Source.create
                let effectiveDate' = effectiveDate |> EffectiveDate
                return
                    (source', effectiveDate')
                    |> DogCommand.Wake
            }
        | DogCommandDto.Play (source, effectiveDate) ->
            result {
                let! source' = source |> Source.create
                let effectiveDate' = effectiveDate |> EffectiveDate
                return
                    (source', effectiveDate')
                    |> DogCommand.Play
            }
