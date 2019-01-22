namespace Dog

open SimpleType

/// Name of the dog
type Name = private Name of String50
module Name =
    let value (Name name) = String50.value name
    let create name = 
        String50.create name 
        |> Result.map Name

/// Bread of the dog
type Breed = private Breed of String50
module Breed =
    let value (Breed breed) = String50.value breed
    let create breed = String50.create breed |> Result.map Breed
