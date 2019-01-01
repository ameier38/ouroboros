namespace Dog

open System
open SimpleType

/// Source which caused the creation of the event
type Source = private Source of String50
module Source =
    let value (Source source) = String50.value source
    let create source = 
        String50.create source 
        |> Result.map Source

/// Date at which event is effective in the domain
type EffectiveDate = EffectiveDate of DateTime
module EffectiveDate =
    let value (EffectiveDate date) = date

/// If two events occur at the exact same time, the order in which to apply them
type EffectiveOrder = private EffectiveOrder of PositiveInt
module EffectiveOrder =
    let value (EffectiveOrder order) = PositiveInt.value order
    let create order = 
        PositiveInt.create order 
        |> Result.map EffectiveOrder

type Name = private Name of String50
module Name =
    let value (Name name) = String50.value name
    let create name = 
        String50.create name 
        |> Result.map Name

type Breed = private Breed of String50
module Breed =
    let value (Breed breed) = String50.value breed
    let create breed = String50.create breed |> Result.map Breed
