namespace Dog

type DogError = DogError of string

type Dog =
    { Name: Name
      Breed: Breed }

type DogEventMeta =
    { Source: Source 
      EffectiveDate: EffectiveDate
      EffectiveOrder: EffectiveOrder }

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

type DogState =
    | NoDog
    | Bored
    | Hungry
    | Tired
    | Asleep
