module Dog.Implementation

open Dog
open Ouroboros
open Ouroboros.EventStore

type Filter =
    RecordedEvent<DogEvent> list
     -> RecordedEvent<DogEvent> list

type SortBy =
    Event<DogEvent>
     -> EffectiveDate * EffectiveOrder

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

module Evolve =
    let born _ = function
        | NoDog -> Hungry
        | _ -> "dog cannot be born; dog already exists" |> Corrupt
    let ate = function
        | NoDog -> "dog cannot eat; dog does not exist" |> Corrupt
        | Hungry -> Bored
        | state -> sprintf "dog cannot eat in state: %A" state |> Corrupt
    let slept = function
        | NoDog -> "dog cannot sleep; dog does not exist" |> Corrupt
        | Tired -> Asleep
        | state -> sprintf "dog cannot sleep in state: %A" state |> Corrupt
    let woke = function
        | NoDog -> "dog cannot wake; dog does not exist" |> Corrupt
        | Asleep -> Hungry
        | state -> sprintf "dog cannot wake in state: %A" state |> Corrupt
    let played = function
        | NoDog -> "dog cannot play; dog does not exist" |> Corrupt
        | Bored -> Tired
        | state -> sprintf "dog cannot play in state: %A" state |> Corrupt

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
        | DogEvent.Reversed _ -> createEventType "Reversed"
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

module Decide =
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
        | state ->
            sprintf "cannot create dog; dog already exists in state %A" state 
            |> fail
    let eat source effectiveDate = function
        | NoDog ->
            "dog cannot eat; dog does not exist" 
            |> fail
        | Hungry ->
            DogEvent.Ate 
            |> success source effectiveDate
        | state ->
            sprintf "dog cannot eat; dog is %A" state 
            |> fail
    let sleep source effectiveDate = function
        | NoDog ->
            "dog cannot sleep; dog does not exist" 
            |> fail
        | Tired _ ->
            DogEvent.Slept 
            |> success source effectiveDate
        | state ->
            sprintf "dog cannot sleep; dog is %A" state 
            |> fail
    let wake source effectiveDate = function
        | NoDog ->
            "dog cannot wake up; dog does not exist" 
            |> fail
        | Asleep _ ->
            DogEvent.Woke |> success source effectiveDate
        | state ->
            sprintf "dog cannot wake; dog is %A" state 
            |> fail
    let play source effectiveDate = function
        | NoDog ->
            "dog cannot play; dog does not exist" |> fail
        | Bored _ ->
            DogEvent.Played |> success source effectiveDate
        | state ->
            sprintf "dog cannot play; dog is %A" state 
            |> fail

let decide : Decide =
    fun state command ->
        let { Command.Data = commandData 
              Meta = commandMeta } = command
        let { CommandMeta.EffectiveDate = effectiveDate
              Source = source } = commandMeta
        match commandData with
        | DogCommand.Reverse eventNumber ->
            Decide.reverse source effectiveDate eventNumber state
        | DogCommand.Create dog -> 
            Decide.create source effectiveDate dog state
        | DogCommand.Eat ->
            Decide.eat source effectiveDate state
        | DogCommand.Sleep ->
            Decide.sleep source effectiveDate state
        | DogCommand.Wake ->
            Decide.wake source effectiveDate state
        | DogCommand.Play ->
            Decide.play source effectiveDate state
let evolve : Evolve =
    fun state -> function
        | DogEvent.Reversed _ -> state
        | DogEvent.Born dog -> Evolve.born dog state
        | DogEvent.Ate -> Evolve.ate state
        | DogEvent.Slept -> Evolve.slept state
        | DogEvent.Woke -> Evolve.woke state
        | DogEvent.Played -> Evolve.played state

let filter : Filter<DogEvent> =
    fun (recordedEvents:RecordedEvent<DogEvent> list) ->
        let extractReversedEventNumbers (recordedEvent:RecordedEvent<DogEvent>) =
            match recordedEvent.Data with
            | DogEvent.Reversed eventNumber -> Some eventNumber
            | _ -> None
        let (reversedEventNumbers, domainEvents) =
            recordedEvents
            |> List.divide extractReversedEventNumbers
        domainEvents
        |> List.filter (fun domainEvent ->
            reversedEventNumbers 
            |> List.contains domainEvent.EventNumber 
            |> not) 

let sortBy: SortBy<DogEvent,EffectiveDate * EffectiveOrder> =
    fun (event:Event<DogEvent>) ->
        event.Meta.EffectiveDate, event.Meta.EffectiveOrder

let aggregate =
    { zero = NoDog
      filter = filter
      enrich = id
      sortBy = sortBy
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

let handlerResult =
    result {
        let! repo = repoResult
        return Handler.create aggregate repo
    }
