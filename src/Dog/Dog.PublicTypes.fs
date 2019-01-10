namespace Dog

type Dog =
    { Name: Name
      Breed: Breed }

type DogEventMeta =
    { Source: Source }

type DogEvent =
    | Born of Dog
    | Ate
    | Slept
    | Woke
    | Played

type DogCommandMeta =
    { Source: Source }

type DogCommand =
    | Create of Dog
    | Eat
    | Sleep
    | Wake
    | Play

type DogState =
    | Corrupted
    | NoDog
    | Bored
    | Hungry
    | Tired
    | Asleep
