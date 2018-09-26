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
    | Create of Source * EffectiveDate * Dog
    | Eat of Source * EffectiveDate
    | Sleep of Source * EffectiveDate
    | Wake of Source * EffectiveDate
    | Play of Source * EffectiveDate

type DogError =
    | IO of string
    | Validation of string

type DogState =
    | NoDog
    | Bored
    | Hungry
    | Tired
    | Asleep
