module Dog.Implementation

open Dog
open Ouroboros
open Ouroboros.EventStore

type Filter =
    RecordedEvent<DogEventDto, DogEventMetaDto> list
     -> RecordedEvent<DogEventDto, DogEventMetaDto> list

type Apply =
    DogState
     -> DogEventDto
     -> DogState

type Execute =
    DogState
     -> Command<DogCommand, DogCommandMeta>
     -> AsyncResult<Event<DogEventDto, DogEventMetaDto> list, DogError>

module DogError =
    let convertOuroborosError (OuroborosError error) = error |> DogError
    let convertEventStoreError (EventStoreError error) = error |> DogError

module Dog =
    let create name breed =
        result {
            let! name' = name |> Name.create |> Result.mapError DogError
            let! breed' = breed |> Breed.create |> Result.mapError DogError
            return
                { Name = name'
                  Breed = breed' }
        }

module DogEventMeta =
    let create source =
        { DogEventMeta.EventSource = source }

module DogCommandMeta =
    let create source =
        { DogCommandMeta.CommandSource = source }

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
        |> Result.mapError DogError

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
        |> Result.mapError DogError
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
                let dogEventDto =
                    dogEvent
                    |> DogEventDto.fromDomain
                let dogEventMetaDto = 
                    source 
                    |> DogEventMeta.create
                    |> DogEventMetaDto.fromDomain
                let eventMeta =
                    { EventMeta.EffectiveDate = effectiveDate
                      EffectiveOrder = effectiveOrder
                      DomainEventMeta = dogEventMetaDto }
                let! eventType = getEventType dogEvent
                return 
                    { Event.Type = eventType
                      Data = dogEventDto
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
        |> DogError
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

let execute : Execute =
    fun state command ->
        let { Command.Data = commandData 
              Meta = commandMeta } = command
        let { EffectiveDate = effectiveDate
              DomainCommandMeta = dogCommandMeta } = commandMeta
        let { DogCommandMeta.CommandSource = source } = dogCommandMeta
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
let apply : Apply =
    fun state dogEventDto ->
        dogEventDto
        |> DogEventDto.toDomain
        |> function
           | Ok dogEventDto ->
                match dogEventDto with 
                | DogEvent.Reversed _ -> state
                | DogEvent.Born dog -> Apply.born dog state
                | DogEvent.Ate -> Apply.ate state
                | DogEvent.Slept -> Apply.slept state
                | DogEvent.Woke -> Apply.woke state
                | DogEvent.Played -> Apply.played state
           | Error (DogError err) -> err |> Corrupt

let aggregate =
    { zero = NoDog
      filter = Defaults.filter
      sortBy = Defaults.sortBy
      apply = apply
      execute = execute }

let repoResult =
    result {
        let! config = 
            EventStoreConfig.load () 
            |> Result.mapError DogError
        let store = eventStore config.Uri
        let! entityType = 
            EntityType.create "dog" 
            |> Result.mapError DogError
        let convertStoreError (EventStoreError err) = err |> DogError
        let convertOuroborosError (OuroborosError err) = err |> DogError
        let repo = 
            Repository.create 
                store 
                convertStoreError
                convertOuroborosError
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
        return CommandHandler.create DogError.convertOuroborosError aggregate repo
    }
