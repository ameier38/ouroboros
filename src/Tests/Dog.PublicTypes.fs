namespace Test.Dog

open System
open Ouroboros

type Dog =
    { Name: Name
      Breed: Breed }

type DogEvent =
    | Born of Dog
    | Renamed of Name
    | Ate of Name
    | Slept
    | Woke
    | Played

type DogCommand =
    | Create of EffectiveDate * Dog
    | Rename of EffectiveDate * Name
    | CallToEat of EffectiveDate * Name
    | Sleep of EffectiveDate
    | Wake of EffectiveDate
    | Play of EffectiveDate

type DogError =
    | IO of string
    | Validation of string

type DogState =
    | NoDog
    | Bored of Dog
    | Hungry of Dog
    | Tired of Dog
    | Asleep of Dog
