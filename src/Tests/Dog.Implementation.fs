module internal Test.Dog.Implementation

open Ouroboros
open Ouroboros.Api
open Ouroboros.EventStore
open Test.Dog
open Test.Config

type Apply =
    DogState
     -> RecordedEvent<DogEvent>
     -> DogState

type Execute =
    DogState
     -> DogCommand
     -> AsyncResult<Event<DogEvent> list, DogError>

module DogError =
    let io s = DogError.IO s |> Error
    let validation s = DogError.Validation s |> Error

module DogEvent =
    let serialize event =
        event
        |> DogEventDto.fromDomain
        |> DogEventDto.serialize
    let deserialize json =
        json
        |> DogEventDto.deserialize
        |> Result.map DogEventDto.toDomain

module Event =
    let getEffectiveOrder = function
        | DogEvent.Born _ -> 1
        | DogEvent.Ate _ -> 2
        | DogEvent.Slept _ -> 3
        | DogEvent.Woke _ -> 4
        | DogEvent.Played _ -> 5
    let createEventType eventType =
        eventType
        |> EventType.create
        |> Result.mapError DogError.Validation
    let getEventType = function
        | DogEvent.Born _ -> createEventType "Born"
        | DogEvent.Ate _ -> createEventType "Ate"
        | DogEvent.Slept _ -> createEventType "Slept"
        | DogEvent.Woke _ -> createEventType "Woke"
        | DogEvent.Played _ -> createEventType "Played"
    let fromDomain source effectiveDate =
        fun dogEvent ->
            result {
                let effectiveOrder = getEffectiveOrder dogEvent
                let! eventMeta = 
                    EventMeta.create effectiveDate effectiveOrder source
                    |> Result.mapError DogError.Validation
                let! eventType = getEventType dogEvent
                return 
                    { Event.Type = eventType
                      Data = dogEvent
                      Meta = eventMeta }
            }
    let serialize (event:Event<DogEvent>) =
        result {
            let! eventMeta = 
                EventMeta.serialize event.Meta
                |> Result.mapError DogError.IO
            let! eventData = DogEvent.serialize event.Data
            return
                { Type = event.Type
                  Data = eventData
                  Meta = eventMeta }
        }
    let deserialize (event:SerializedRecordedEvent) =
        result {
            let! eventMeta = 
                EventMeta.deserialize event.Meta
                |> Result.mapError DogError.IO
            let! eventData = DogEvent.deserialize event.Data
            return
                { Id = event.Id
                  CreatedDate = event.CreatedDate
                  Type = event.Type
                  Data = eventData 
                  Meta = eventMeta }
        }

module DogCommand =
    let create =
        fun effectiveDate dog ->
            DogEvent.Born dog
            |> Event.fromDomain "test" effectiveDate
            |> AsyncResult.ofResult
    let eat =
        fun effectiveDate ->
            DogEvent.Ate
            |> Event.fromDomain "test" effectiveDate
            |> AsyncResult.ofResult
    let sleep =
        fun effectiveDate ->
            DogEvent.Slept
            |> Event.fromDomain "test" effectiveDate
            |> AsyncResult.ofResult
    let wake =
        fun effectiveDate ->
            DogEvent.Woke
            |> Event.fromDomain "test" effectiveDate
            |> AsyncResult.ofResult
    let play =
        fun effectiveDate ->
            DogEvent.Played
            |> Event.fromDomain "test" effectiveDate
            |> AsyncResult.ofResult

let execute
    create
    eat
    sleep
    wake
    play
    : Execute =
    fun state command ->
        match (state, command) with
        | NoDog, DogCommand.Create (effectiveDate, dog) ->
            create effectiveDate dog |> AsyncResult.map List.singleton
        | Hungry, DogCommand.Eat effectiveDate ->
            eat effectiveDate |> AsyncResult.map List.singleton
        | Tired, DogCommand.Sleep effectiveDate ->
            sleep effectiveDate |> AsyncResult.map List.singleton
        | Asleep, DogCommand.Wake effectiveDate ->
            wake effectiveDate |> AsyncResult.map List.singleton
        | Bored, DogCommand.Play effectiveDate ->
            play effectiveDate |> AsyncResult.map List.singleton
        | _ ->
            sprintf "invalid command %A on state %A" command state
            |> AsyncResult.ofError
            |> AsyncResult.mapError DogError.Validation

let apply : Apply =
    fun state recordedEvent ->
        match (state, recordedEvent) with
        | _, { Data = DogEvent.Born _ } ->
            Hungry
        | _, { Data = DogEvent.Ate } ->
            Bored
        | _, { Data = DogEvent.Slept } ->
            Hungry
        | _, { Data = DogEvent.Woke } -> 
            Bored
        | _, { Data = DogEvent.Played } ->
            Tired

let serializer =
    { serialize = Event.serialize
      deserialize = Event.deserialize }

let aggregate =
    let execute' =
        execute
            DogCommand.create
            DogCommand.eat
            DogCommand.sleep
            DogCommand.wake
            DogCommand.play
    { zero = NoDog
      apply = apply
      execute = execute' }

let repoResult =
    result {
        let! config = EventStoreConfig.load () |> Result.mapError DogError.IO
        let store = eventStore config.Uri
        let! entityType = EntityType.create "dog" |> Result.mapError DogError.Validation
        let mapError (EventStoreError e) = DogError.IO e
        let repo = Repository.create store mapError serializer entityType
        return repo
    }

let handlerResult =
    result {
        let! repo = repoResult
        return Handler.create repo aggregate
    }
