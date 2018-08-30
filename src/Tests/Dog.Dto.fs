namespace Test.Dog

open Vertigo.Json
open System

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
        { name = dog.Name
          breed = dog.Breed }
    let toDomain (dto:DogDto) =
        { Name = dto.name
          Breed = dto.breed }

type DogEventDto =
    | Born of DogDto
    | Ate
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
        | DogEvent.Ate ->
            DogEventDto.Ate
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
            |> DogEvent.Born
        | DogEventDto.Ate ->
            DogEvent.Ate
        | DogEventDto.Slept ->
            DogEvent.Slept
        | DogEventDto.Woke ->
            DogEvent.Woke
        | DogEventDto.Played ->
            DogEvent.Played

type DogCommandDto =
    | Create of DateTime * DogDto
    | Eat of DateTime
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
        | DogCommand.Create (effectiveDate, dog) ->
            let dogDto = dog |> DogDto.fromDomain
            (effectiveDate, dogDto)
            |> DogCommandDto.Create
        | DogCommand.Eat effectiveDate ->
            DogCommandDto.Eat effectiveDate
        | DogCommand.Sleep effectiveDate ->
            DogCommandDto.Sleep effectiveDate
        | DogCommand.Wake effectiveDate ->
            DogCommandDto.Wake effectiveDate
        | DogCommand.Play effectiveDate ->
            DogCommandDto.Play effectiveDate
    let toDomain = function
        | DogCommandDto.Create (effectiveDate, dogDto) ->
            let dog = dogDto |> DogDto.toDomain
            (effectiveDate, dog)
            |> DogCommand.Create
        | DogCommandDto.Eat effectiveDate ->
            DogCommand.Eat effectiveDate
        | DogCommandDto.Sleep effectiveDate ->
            DogCommand.Sleep effectiveDate
        | DogCommandDto.Wake effectiveDate ->
            DogCommand.Wake effectiveDate
        | DogCommandDto.Play effectiveDate ->
            DogCommand.Play effectiveDate
