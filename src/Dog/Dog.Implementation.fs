module Dog.Implementation

open Dog
open Ouroboros
open Ouroboros.EventStore

type Filter =
    RecordedEvent<DogEvent> list
     -> RecordedEvent<DogEvent> list

type Evolve =
    DogState
     -> DogEvent
     -> DogState

type Decide =
    DogState
     -> Command<DogCommand>
     -> AsyncResult<Event<DogEvent> list,OuroborosError>

module Dog =
    let create name breed : Result<Dog,OuroborosError> =
        result {
            let! name' = 
                name 
                |> Name.create 
                |> Result.mapError DomainError
            let! breed' = 
                breed 
                |> Breed.create 
                |> Result.mapError DomainError
            return
                { Name = name'
                  Breed = breed' }
        }

module DogEvent =
    let serializeToBytes (dogEvent:DogEvent) =
        dogEvent
        |> DogEventDto.fromDomain
        |> Json.serializeToBytes
        |> Result.mapError DomainError
    let deserializeFromBytes (bytes:byte []) =
        bytes
        |> Json.deserializeFromBytes<DogEventDto>
        |> Result.mapError DomainError
        |> Result.bind DogEventDto.toDomain

module Apply =
    let born _ = function
        | NoDog -> Hungry
        | _ -> "dog cannot be born; dog already exists" |> Corrupt
    let ate = function
        | NoDog -> "dog cannot eat; dog does not exist" |> Corrupt
        | Hungry -> Bored
        | state -> sprintf "dog cannot eat in state:\n%A" state |> Corrupt
    let slept = function
        | NoDog -> "dog cannot sleep; dog does not exist" |> Corrupt
        | Tired -> Asleep
        | state -> sprintf "dog cannot sleep in state: \n%A" state |> Corrupt
    let woke = function
        | NoDog -> "dog cannot wake; dog does not exist" |> Corrupt
        | Asleep -> Hungry
        | state -> sprintf "dog cannot wake in state: \n%A" state |> Corrupt
    let played = function
        | NoDog -> "dog cannot play; dog does not exist" |> Corrupt
        | Bored -> Tired
        | state -> sprintf "dog cannot play in state: \n%A" state |> Corrupt

module Event =
    let createEffectiveOrder order =
        order
        |> EffectiveOrder.create
        |> Result.mapError DomainError
    let getEffectiveOrder = function
        | DogEvent.Reversed _ -> createEffectiveOrder 0
        | DogEvent.Born _ -> createEffectiveOrder 1
        | DogEvent.Ate -> createEffectiveOrder 2
        | DogEvent.Slept -> createEffectiveOrder 3
        | DogEvent.Woke -> createEffectiveOrder 4
        | DogEvent.Played -> createEffectiveOrder 5
    let createEventType eventType =
        eventType
        |> EventType.create
        |> Result.mapError DomainError
    let getEventType = function
        | DogEvent.Reversed _ -> createEventType ReversedEventType
        | DogEvent.Born _ -> createEventType "Born"
        | DogEvent.Ate -> createEventType "Ate"
        | DogEvent.Slept -> createEventType "Slept"
        | DogEvent.Woke -> createEventType "Woke"
        | DogEvent.Played -> createEventType "Played"
    let fromDogEvent (source:Source) (effectiveDate:EffectiveDate) =
        fun (dogEvent:DogEvent) ->
            result {
                let! effectiveOrder = getEffectiveOrder dogEvent 
                let eventMeta =
                    { EventMeta.EffectiveDate = effectiveDate
                      EffectiveOrder = effectiveOrder
                      Source = source }
                let! eventType = getEventType dogEvent
                return 
                    { Event.Type = eventType
                      Data = dogEvent
                      Meta = eventMeta }
            }

module Execute =
    let success source effectiveDate event = 
        event
        |> Event.fromDogEvent source effectiveDate
        |> Result.map List.singleton
        |> AsyncResult.ofResult
    let fail message =
        message
        |> DomainError
        |> AsyncResult.ofError
    let reverse source effectiveDate eventNumber (_:DogState) =
        DogEvent.Reversed eventNumber
        |> success source effectiveDate
    let create source effectiveDate dog = function
        | NoDog ->
            DogEvent.Born dog 
            |> success source effectiveDate
        | _ ->
            "cannot create dog; dog already exists" 
            |> fail
    let eat source effectiveDate = function
        | NoDog ->
            "dog cannot eat; dog does not exist" 
            |> fail
        | Hungry ->
            DogEvent.Ate 
            |> success source effectiveDate
        | _ ->
            "dog cannot eat; dog is not hungry" 
            |> fail
    let sleep source effectiveDate = function
        | NoDog ->
            "dog cannot sleep; dog does not exist" 
            |> fail
        | Tired _ ->
            DogEvent.Slept 
            |> success source effectiveDate
        | _ ->
            "dog cannot sleep; dog is not tired" 
            |> fail
    let wake source effectiveDate = function
        | NoDog ->
            "dog cannot wake up; dog does not exist" 
            |> fail
        | Asleep _ ->
            DogEvent.Woke |> success source effectiveDate
        | _ ->
            "dog cannot wake up; dog is not asleep" |> fail
    let play source effectiveDate = function
        | NoDog ->
            "dog cannot play; dog does not exist" |> fail
        | Bored _ ->
            DogEvent.Played |> success source effectiveDate
        | _ ->
            "dog cannot play; dog is not bored" |> fail

let decide : Decide =
    fun state command ->
        let { Command.Data = commandData 
              Meta = commandMeta } = command
        let { CommandMeta.EffectiveDate = effectiveDate
              Source = source } = commandMeta
        match commandData with
        | DogCommand.Reverse eventNumber ->
            Execute.reverse source effectiveDate eventNumber state
        | DogCommand.Create dog -> 
            Execute.create source effectiveDate dog state
        | DogCommand.Eat ->
            Execute.eat source effectiveDate state
        | DogCommand.Sleep ->
            Execute.sleep source effectiveDate state
        | DogCommand.Wake ->
            Execute.wake source effectiveDate state
        | DogCommand.Play ->
            Execute.play source effectiveDate state
let evolve : Evolve =
    fun state -> function
        | DogEvent.Reversed _ -> state
        | DogEvent.Born dog -> Apply.born dog state
        | DogEvent.Ate -> Apply.ate state
        | DogEvent.Slept -> Apply.slept state
        | DogEvent.Woke -> Apply.woke state
        | DogEvent.Played -> Apply.played state

let aggregate =
    { zero = NoDog
      filter = Defaults.filter
      sortBy = Defaults.sortBy
      evolve = evolve
      decide = decide }

let serializer =
    { serializeToBytes = DogEvent.serializeToBytes
      deserializeFromBytes = DogEvent.deserializeFromBytes }

let repoResult =
    result {
        let! config = EventStoreConfig.load () 
        let store = eventStore config.Uri
        let! entityType = 
            EntityType.create "dog" 
            |> Result.mapError OuroborosError
        let repo = 
            Repository.create 
                store 
                serializer
                entityType
        return repo
    }

let queryHandlerResult =
    result {
        let! repo = repoResult
        return QueryHandler.create aggregate repo
    }

let commandHandlerResult =
    result {
        let! repo = repoResult
        return 
            CommandHandler.create 
                aggregate 
                repo
    }
