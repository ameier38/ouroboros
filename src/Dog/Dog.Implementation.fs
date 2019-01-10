module Dog.Implementation

open Dog
open Ouroboros
open Ouroboros.EventStore

type Filter =
    

type Apply =
    DogState
     -> DogEvent
     -> Result<DogState, DogError>

type Execute =
    DogState
     -> DomainCommand<DogCommand>
     -> AsyncResult<DomainEvent<DogEvent> list, DogError>

module DogError =
    let io s = DogError.IO s |> Error
    let validation s = DogError.Validation s |> Error
    let mapOuroborosError (OuroborosError error) = error |> DogError.Validation
    let mapEventStoreError (EventStoreError error) = error |> DogError.IO

module Dog =
    let create name breed =
        result {
            let! name' = name |> Name.create |> Result.mapError DogError.Validation
            let! breed' = breed |> Breed.create |> Result.mapError DogError.Validation
            return
                { Name = name'
                  Breed = breed' }
        }

module DogEvent =
    let serialize event =
        event
        |> DogEventDto.fromDomain
        |> DogEventDto.serializeToBytes
    let deserialize bytes =
        bytes
        |> DogEventDto.deserializeFromBytes
        |> Result.bind DogEventDto.toDomain

module Apply =
    let success state = state |> Ok
    let fail message = message |> DogError.validation
    let born _ = function
        | NoDog -> Hungry |> success
        | _ -> "dog cannot be born; dog already exists" |> fail
    let ate = function
        | NoDog -> "dog cannot eat; dog does not exist" |> fail
        | Hungry -> Bored |> success
        | state -> sprintf "dog cannot eat in state:\n%A" state |> fail
    let slept = function
        | NoDog -> "dog cannot sleep; dog does not exist" |> fail
        | Tired -> Asleep |> success
        | state -> sprintf "dog cannot sleep in state: \n%A" state |> fail
    let woke = function
        | NoDog -> "dog cannot wake; dog does not exist" |> fail
        | Asleep -> Hungry |> success
        | state -> sprintf "dog cannot wake in state: \n%A" state |> fail
    let played = function
        | NoDog -> "dog cannot play; dog does not exist" |> fail
        | Bored -> Tired |> success
        | state -> sprintf "dog cannot play in state: \n%A" state |> fail

module DomainEvent =
    let createEffectiveOrder order =
        order
        |> EffectiveOrder.create
        |> Result.mapError DogError.mapOuroborosError

    let getEffectiveOrder = function
        | DogEvent.Born _ -> createEffectiveOrder 1
        | DogEvent.Ate -> createEffectiveOrder 2
        | DogEvent.Slept -> createEffectiveOrder 3
        | DogEvent.Woke -> createEffectiveOrder 4
        | DogEvent.Played -> createEffectiveOrder 5
    let createEventType eventType =
        eventType
        |> DomainEventType.create
        |> Result.mapError DogError.mapOuroborosError
    let getEventType = function
        | DogEvent.Born _ -> createEventType "Born"
        | DogEvent.Ate -> createEventType "Ate"
        | DogEvent.Slept -> createEventType "Slept"
        | DogEvent.Woke -> createEventType "Woke"
        | DogEvent.Played -> createEventType "Played"
    let fromDogEvent source effectiveDate =
        fun dogEvent ->
            result {
                let! effectiveOrder = getEffectiveOrder dogEvent 
                let eventMeta = 
                    (effectiveDate, effectiveOrder, source)
                    |||> DomainEventMeta.create
                let! eventType = getEventType dogEvent
                return 
                    { DomainEvent.Type = eventType
                      Data = dogEvent
                      Meta = eventMeta }
            }

module Execute =
    let success source effectiveDate event = 
        event
        |> DomainEvent.fromDogEvent source effectiveDate
        |> Result.map List.singleton
        |> AsyncResult.ofResult
    let fail message =
        message
        |> DogError.Validation
        |> AsyncResult.ofError
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
        let { DomainCommand.Source = source 
              EffectiveDate = effectiveDate 
              Data = commandData } = command
        match commandData with
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
    fun state -> function
        | DogEvent.Born dog -> Apply.born dog state
        | DogEvent.Ate -> Apply.ate state
        | DogEvent.Slept -> Apply.slept state
        | DogEvent.Woke -> Apply.woke state
        | DogEvent.Played -> Apply.played state

let serializer =
    { serialize = DogEvent.serialize
      deserialize = DogEvent.deserialize }

let aggregate =
    { zero = NoDog
      apply = apply
      execute = execute }

let repoResult =
    result {
        let! config = 
            EventStoreConfig.load () 
            |> Result.mapError DogError.IO
        let store = eventStore config.Uri
        let! entityType = 
            EntityType.create "dog" 
            |> Result.mapError DogError.mapOuroborosError
        let mapError err = DogError.Validation err
        let repo = 
            Repository.create 
                store 
                DogError.mapOuroborosError
                DogError.mapEventStoreError 
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
        return CommandHandler.create DogError.mapOuroborosError aggregate repo
    }