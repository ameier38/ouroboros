namespace Dog

open Vertigo.Json

type DogDto =
    { name: string
      breed: string }
module DogDto =
    let serialize (dto:DogDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DogDto %A\n%A" dto ex |> DogError.IO |> Error
    let deserializeBytes bytes =
        try Json.deserializeFromBytes<DogDto> bytes |> Ok
        with ex -> sprintf "could not deserialize DogDto %A\n%A" bytes ex |> DogError.IO |> Error
    let deserializeJson json =
        try Json.deserialize<DogDto> json |> Ok
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
    let deserializeBytes bytes =
        try Json.deserializeFromBytes<DogEventDto> bytes |> Ok
        with ex -> 
            sprintf "could not deserialize DogEventDto %A\n%A" bytes ex 
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
