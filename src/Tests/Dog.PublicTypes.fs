namespace Test.Dog

open Ouroboros

type Dog =
    { Name: Name
      Breed: Breed }

type DogEvent =
    | Born of Dog
    | Ate
    | Slept
    | Woke
    | Played

type DogCommand =
    | Create of Dog
    | Eat
    | Sleep
    | Wake
    | Play

type DogError =
    | IO of string
    | Validation of string

type DogState =
    | NoDog
    | Bored
    | Hungry
    | Tired
    | Asleep
