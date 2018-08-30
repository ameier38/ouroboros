namespace Test.Dog

open System

type Dog =
    { Name: string
      Breed: string }

type DogEvent =
    | Born of Dog
    | Ate
    | Slept
    | Woke
    | Played

type DogCommand =
    | Create of DateTime * Dog
    | Eat of DateTime
    | Sleep of DateTime
    | Wake of DateTime
    | Play of DateTime

type DogError =
    | IO of string
    | Validation of string

type DogState =
    | NoDog
    | Bored
    | Hungry
    | Tired
    | Asleep