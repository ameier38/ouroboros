module internal Test.Dog.Implementation

open Ouroboros
open Ouroboros.Api
open Ouroboros.EventStore
open Test.Dog
open Test.Config

type Apply =
    DogState
     -> RecordedEvent<DogEvent>
     -> Result<DogState, DogError>

type Execute =
    DogState
     -> DogCommand
     -> AsyncResult<Event<DogEvent> list, DogError>

module DogError =
    let io s = DogError.IO s |> Error
    let validation s = DogError.Validation s |> Error

let (|DogState|_|) = function
    | NoDog -> None
    | Bored dog -> Some dog
    | Hungry dog -> Some dog
    | Tired dog -> Some dog
    | Asleep dog -> Some dog

module DogEvent =
    let serialize event =
        event
        |> DogEventDto.fromDomain
        |> DogEventDto.serialize
    let deserialize json =
        json
        |> DogEventDto.deserialize
        |> Result.bind DogEventDto.toDomain

module Apply =
    let success state = state |> Ok
    let fail message = message |> DogError.validation
    let born dog = function
        | NoDog -> Hungry dog |> success
        | _ -> "dog cannot be born; dog already exists" |> fail
    let renamed name = function
        | NoDog -> "dog cannot be renamed; dog does not exist" |> fail
        | Bored dog -> { dog with Name = name } |> Bored |> success
        | Hungry dog -> { dog with Name = name } |> Hungry |> success
        | Tired dog -> { dog with Name = name } |> Tired |> success
        | Asleep dog -> { dog with Name = name } |> Asleep |> success
    let ate calledName = function
        | NoDog -> "dog cannot eat; dog does not exist" |> fail
        | DogState dog ->
            if dog.Name = calledName
            then Bored dog |> success
            else "dog cannot eat; incorrect name" |> fail
        | state -> sprintf "dog cannot eat in state:\n%A" state |> fail
    let slept = function
        | NoDog -> "dog cannot sleep; dog does not exist" |> fail
        | Tired dog -> Hungry dog |> success
        | state -> sprintf "dog cannot sleep in state: \n%A" state |> fail
    let woke = function
        | NoDog -> "dog cannot wake; dog does not exist" |> fail
        | Asleep dog -> Bored dog |> success
        | state -> sprintf "dog cannot wake in state: \n%A" state |> fail
    let played = function
        | NoDog -> "dog cannot play; dog does not exist" |> fail
        | Bored dog -> Tired dog |> success
        | state -> sprintf "dog cannot play in state: \n%A" state |> fail


module Event =
    let getEffectiveOrder = function
        | DogEvent.Born _ -> 1
        | DogEvent.Renamed _ -> 2
        | DogEvent.Ate _ -> 3
        | DogEvent.Slept -> 4
        | DogEvent.Woke -> 5
        | DogEvent.Played -> 6
    let createEventType eventType =
        eventType
        |> EventType.create
        |> Result.mapError DogError.Validation
    let getEventType = function
        | DogEvent.Born _ -> createEventType "Born"
        | DogEvent.Renamed _ -> createEventType "Renamed"
        | DogEvent.Ate _ -> createEventType "Ate"
        | DogEvent.Slept _ -> createEventType "Slept"
        | DogEvent.Woke _ -> createEventType "Woke"
        | DogEvent.Played _ -> createEventType "Played"
    let fromDomain source effectiveDate =
        fun dogEvent ->
            result {
                let! effectiveOrder = 
                    getEffectiveOrder dogEvent
                    |> EffectiveOrder.create
                    |> Result.mapError DogError.Validation
                let eventMeta = EventMeta.create effectiveDate effectiveOrder source
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

module Execute =
    let success effectiveDate event = 
        asyncResult {
            let! source = 
                "test" 
                |> Source.create 
                |> Result.mapError DogError.Validation
                |> AsyncResult.ofResult
            return!
                event
                |> Event.fromDomain source effectiveDate
                |> Result.map List.singleton
                |> AsyncResult.ofResult
        }
    let fail message =
        message
        |> DogError.Validation
        |> AsyncResult.ofError
    let create effectiveDate dog = function
        | NoDog ->
            DogEvent.Born dog |> success effectiveDate
        | _ ->
            "cannot create dog; dog already exists" |> fail
    let changeName effectiveDate newName = function
        | NoDog ->
            "cannot change name; dog does not exist" |> fail
        | _ ->
            DogEvent.Renamed newName |> success effectiveDate
    let callToEat effectiveDate calledName = function
        | NoDog ->
            "dog cannot eat; dog does not exist" |> fail
        | Hungry { Name = currentName } ->
            if currentName = calledName
            then DogEvent.Ate calledName |> success effectiveDate
            else sprintf "dog won't come to eat; current name: %A, called name %A" currentName calledName |> fail
        | _ ->
            "dog cannot eat; dog is not hungry" |> fail
    let sleep effectiveDate = function
        | NoDog ->
            "dog cannot sleep; dog does not exist" |> fail
        | Tired _ ->
            DogEvent.Slept |> success effectiveDate
        | _ ->
            "dog cannot sleep; dog is not tired" |> fail
    let wake effectiveDate = function
        | NoDog ->
            "dog cannot wake up; dog does not exist" |> fail
        | Asleep _ ->
            DogEvent.Woke |> success effectiveDate
        | _ ->
            "dog cannot wake up; dog is not asleep" |> fail
    let play effectiveDate = function
        | NoDog ->
            "dog cannot play; dog does not exist" |> fail
        | Bored _ ->
            DogEvent.Played |> success effectiveDate
        | _ ->
            "dog cannot play; dog is not bored" |> fail

let execute : Execute =
    fun state -> function
        | DogCommand.Create (effectiveDate, dog) -> Execute.create effectiveDate dog state
        | DogCommand.Rename (effectiveDate, newName) -> Execute.changeName effectiveDate newName state
        | DogCommand.CallToEat (effectiveDate, calledName) -> Execute.callToEat effectiveDate calledName state
        | DogCommand.Sleep effectiveDate -> Execute.sleep effectiveDate state
        | DogCommand.Wake effectiveDate -> Execute.wake effectiveDate state
        | DogCommand.Play effectiveDate -> Execute.play effectiveDate state

let apply : Apply =
    fun state -> function
        | { Data = DogEvent.Born dog } -> Apply.born dog state
        | { Data = DogEvent.Renamed name } -> Apply.renamed name state
        | { Data = DogEvent.Ate name } -> Apply.ate name state
        | { Data = DogEvent.Slept } -> Apply.slept state
        | { Data = DogEvent.Woke } -> Apply.woke state
        | { Data = DogEvent.Played } -> Apply.played state

let serializer =
    { serialize = Event.serialize
      deserialize = Event.deserialize }

let aggregate =
    { zero = NoDog
      apply = apply
      execute = execute }

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
