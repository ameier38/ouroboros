namespace Dog

open Ouroboros

type Dog =
    { Name: Name
      Breed: Breed }

type DogEvent =
    | Reversed of EventNumber
    | Born of Dog
    | Ate
    | Slept
    | Woke
    | Played

type DogCommand =
    | Reverse of EventNumber
    | Create of Dog
    | Eat
    | Sleep
    | Wake
    | Play

type DogState =
    | Corrupt of string
    | NoDog
    | Bored
    | Hungry
    | Tired
    | Asleep
