module Ouroboros.Api

open Ouroboros.Constants
open SimpleType

module List =
    let divide extractA extractB items =
        let listA = items |> List.choose extractA
        let listB = items |> List.choose extractB
        (listA, listB)

module EventType =
    let create = function
        | DeletedEventTypeValue ->
            DeletedEventType
            |> Ok
        | domainEventType ->
            domainEventType
            |> DomainEventType.create 
            |> Result.map DomainEventType
    let value = function
        | DeletedEventType -> 
            DeletedEventTypeValue
        | DomainEventType eventType -> 
            eventType 
            |> DomainEventType.value

module Deletion =
    let create eventNumber reason =
        result {
            let! eventNumber' = eventNumber |> EventNumber.create
            let! reason' = reason |> DeletionReason.create
            return
                { Deletion.EventNumber = eventNumber'
                  Reason = reason' }
        }

module DomainEventMeta =
    let create effectiveDate effectiveOrder source =
        { DomainEventMeta.EffectiveDate = effectiveDate
          EffectiveOrder = effectiveOrder
          Source = source }

module SerializedRecordedEvent =
    let deserialize 
        (mapError:string -> 'DomainError)
        (serializer:Serializer<'DomainEvent, 'DomainError>)
        : SerializedRecordedEvent -> Result<RecordedEvent<'DomainEvent>, 'DomainError> =
        fun ({ SerializedRecordedEvent.Id = eventId
               EventNumber = eventNumber
               CreatedDate = createdDate
               Type = eventType 
               Data = serializedEventData
               Meta = serializedEventMeta }) ->
            match eventType with
            | DeletedEventType ->
                result {
                    let! deletion = 
                        serializedEventData 
                        |> DeletionDto.deserialize
                        |> Result.bind DeletionDto.toDomain
                        |> Result.mapError mapError 
                    let! deletedEventMeta = 
                        serializedEventMeta
                        |> DeletedEventMetaDto.deserialize
                        |> Result.bind DeletedEventMetaDto.toDomain
                        |> Result.mapError mapError
                    return
                        { RecordedDeletedEvent.Id = eventId
                          EventNumber = eventNumber
                          CreatedDate = createdDate
                          Data = deletion
                          Meta = deletedEventMeta }
                        |> RecordedDeletedEvent
                }
            | DomainEventType domainEventType ->
                result {
                    let! domainEvent = 
                        serializedEventData 
                        |> serializer.deserialize
                    let! domainEventMeta = 
                        serializedEventMeta 
                        |> DomainEventMetaDto.deserialize
                        |> Result.bind DomainEventMetaDto.toDomain
                        |> Result.mapError mapError
                    return
                        { RecordedDomainEvent.Id = eventId
                          EventNumber = eventNumber
                          CreatedDate = createdDate
                          Type = domainEventType
                          Data = domainEvent
                          Meta = domainEventMeta }
                        |> RecordedDomainEvent
                }

module Command =
    let extractDomainCommand = function
        | DomainCommand domainCommand -> Some domainCommand
        | _ -> None
    let extractDeleteCommand = function
        | Delete deleteCommand -> Some deleteCommand
        | _ -> None
    let createDomainCommand source effectiveDate command =
        result {
            let! source' = source |> Source.create
            let effectiveDate' = effectiveDate |> EffectiveDate
            return
                { Source = source'
                  EffectiveDate = effectiveDate'
                  Data = command }
                |> DomainCommand
        }
    let createDeleteCommand source deletion =
        result {
            let! source' = source |> Source.create
            return
                { Source = source'
                  Data = deletion }
                |> Delete
        }

module Event =
    let createDomainEvent eventType event meta =
        { DomainEvent.Type = eventType
          Data = event
          Meta = meta }
        |> DomainEvent
    let createDeletedEvent deletion meta =
        { Data = deletion
          Meta = meta }
        |> DeletedEvent
    let serialize
        (mapError:string -> 'DomainError)
        (serializer:Serializer<'DomainEvent, 'DomainError>) 
        : Event<'DomainEvent> -> Result<SerializedEvent, 'DomainError> = function
        | DeletedEvent { Data = deletion; Meta = meta } ->
            result {
                let! serializedEventData =
                    deletion
                    |> DeletionDto.fromDomain
                    |> DeletionDto.serialize
                    |> Result.mapError mapError
                let! serializedEventMeta =
                    meta
                    |> DeletedEventMetaDto.fromDomain
                    |> DeletedEventMetaDto.serialize
                    |> Result.mapError mapError
                return
                    { SerializedEvent.Type = DeletedEventType
                      Data = serializedEventData
                      Meta = serializedEventMeta }
            }
        | DomainEvent { Type = domainEventType; Data = domainEvent; Meta = meta } ->
            result {
                let! serializedEventData =
                    domainEvent
                    |> serializer.serialize
                let! serializedEventMeta =
                    meta
                    |> DomainEventMetaDto.fromDomain
                    |> DomainEventMetaDto.serialize
                    |> Result.mapError mapError
                let eventType = domainEventType |> DomainEventType
                return
                    { SerializedEvent.Type = eventType
                      Data = serializedEventData
                      Meta = serializedEventMeta }
            }

module DeleteCommand =
    let toEvent (command:DeleteCommand) =
        let meta = { DeletedEventMeta.Source = command.Source }
        { Data = command.Data
          Meta = meta }
        |> DeletedEvent

module RecordedEvent =
    let extractDomainEvent = function
        | RecordedDomainEvent recordedDomainEvent ->
            Some recordedDomainEvent
        | _ -> None
    let extractDeletedEvent = function
        | RecordedDeletedEvent deletedEvent ->
            Some deletedEvent
        | _ -> None

module RecordedDomainEvent =
    let toDomainEvent recordedDomainEvent =
        { DomainEvent.Type = recordedDomainEvent.Type
          Data = recordedDomainEvent.Data
          Meta = recordedDomainEvent.Meta }

module ExpectedVersion =
    let create nEvents =
        match nEvents with
        | n when n = 0 -> NoStream |> Ok
        | n -> 
            n 
            |> int64
            |> PositiveLong.create
            |> Result.map ExpectedVersion.Specific

module Repository =
    let create
        (store:Store<'StoreError>) 
        (mapError:string -> 'DomainError)
        (mapStoreError:'StoreError -> 'DomainError)
        (serializer:Serializer<'DomainEvent,'DomainError>) 
        (entityType:EntityType) =
        let createStreamId = StreamId.create entityType
        let deserialize = SerializedRecordedEvent.deserialize mapError serializer
        let serialize = Event.serialize mapError serializer
        let loadAll entityId = 
            asyncResult {
                let streamStart  = StreamStart.zero
                let! serializedRecordedEvents = 
                    createStreamId entityId
                    |> store.readEntireStream streamStart
                    |> AsyncResult.mapError mapStoreError
                return!
                    serializedRecordedEvents
                    |> List.map deserialize
                    |> Result.sequence
                    |> AsyncResult.ofResult
            }
        let load entityId =
            asyncResult {
                let! recordedEvents = loadAll entityId
                let (domainEvents, deletedEvents) =
                    recordedEvents
                    |> List.divide 
                        RecordedEvent.extractDomainEvent 
                        RecordedEvent.extractDeletedEvent
                let deletedEventNumbers =
                    deletedEvents
                    |> List.map (fun e -> e.Data.EventNumber)
                let filteredDomainEvents =
                    domainEvents
                    |> List.filter (fun e ->
                        deletedEventNumbers 
                        |> List.contains e.EventNumber 
                        |> not)
                return filteredDomainEvents
            }
        let commit entityId expectedVersion events = 
            asyncResult {
                let! serializedEvents =
                    events
                    |> List.map serialize
                    |> Result.sequence
                    |> AsyncResult.ofResult
                return!
                    entityId
                    |> createStreamId
                    |> store.writeStream expectedVersion serializedEvents
                    |> AsyncResult.mapError mapStoreError
            }
        { load = load
          loadAll = loadAll
          commit = commit }

module QueryHandler =
    let create
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError>)
        (repo:Repository<'DomainEvent,'DomainError>) =
        let apply = Result.bind2 aggregate.apply
        let zero = aggregate.zero |> Ok
        let replay
            (entityId:EntityId)
            (asOf:AsOf) =
            asyncResult {
                let! recordedDomainEvents = repo.load entityId
                let atOrBeforeAsOf (recordedDomainEvent:RecordedDomainEvent<'DomainEvent>) =
                    match asOf with
                    | Latest -> true
                    | Specific (AsOfDate asOfDate) ->
                        let createdDate = 
                            recordedDomainEvent.CreatedDate 
                            |> CreatedDate.value
                        createdDate <= asOfDate
                return
                    recordedDomainEvents
                    |> List.filter atOrBeforeAsOf
            }
        let reconstitute
            (domainEvents:DomainEvent<'DomainEvent> list) =
            result {
                return! 
                    domainEvents
                    |> List.sortBy (fun e -> (e.Meta.EffectiveDate, e.Meta.EffectiveOrder))
                    |> List.map (fun e -> e.Data)
                    |> List.map Ok
                    |> List.fold apply zero
            }
        { replay = replay
          reconstitute = reconstitute }

module CommandHandler =
    let create<'DomainState,'DomainCommand,'DomainEvent,'DomainError> 
        (mapError:string -> 'DomainError)
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError>)
        (repo:Repository<'DomainEvent,'DomainError>) =
        let queryHandler = QueryHandler.create aggregate repo
        let reconstitute
            (effectiveDate:EffectiveDate) 
            (events:DomainEvent<'DomainEvent> list) =
            events
            |> List.filter (fun e -> e.Meta.EffectiveDate <= effectiveDate)
            |> queryHandler.reconstitute
        let execute 
            eventAccumulator
            (domainCommand:DomainCommand<'DomainCommand>) =
            asyncResult {
                let effectiveDate = domainCommand.EffectiveDate
                let! (allDomainEvents, newDomainEvents) = eventAccumulator
                let! state = reconstitute effectiveDate allDomainEvents |> AsyncResult.ofResult
                let! generatedDomainEvents = aggregate.execute state domainCommand
                let newAllDomainEvents = generatedDomainEvents @ allDomainEvents
                let newNewDomainEvents = generatedDomainEvents @ newDomainEvents
                return (newAllDomainEvents, newNewDomainEvents)
            }
        let handle 
            (entityId:EntityId) 
            (commands:Command<'DomainCommand> list) =
            asyncResult {
                let! recordedDomainEvents = repo.load entityId
                let! expectedVersion = 
                    recordedDomainEvents 
                    |> List.length 
                    |> ExpectedVersion.create
                    |> Result.mapError mapError
                    |> AsyncResult.ofResult
                let (domainCommands, deleteCommands) =
                    commands
                    |> List.divide 
                        Command.extractDomainCommand 
                        Command.extractDeleteCommand
                let deletedEventNumbers =
                    deleteCommands
                    |> List.map (fun c -> c.Data.EventNumber)
                let newDeletedEvents =
                    deleteCommands
                    |> List.map DeleteCommand.toEvent
                let filteredDomainEvents =
                    recordedDomainEvents
                    |> List.filter (fun e ->
                        deletedEventNumbers 
                        |> List.contains e.EventNumber 
                        |> not)
                    |> List.map RecordedDomainEvent.toDomainEvent
                let initialState =
                    (filteredDomainEvents, [])
                    |> AsyncResult.ofSuccess
                let! newDomainEvents = 
                    domainCommands
                    |> List.fold execute initialState
                    |> AsyncResult.map (snd >> List.map DomainEvent)
                let newEvents = newDomainEvents @ newDeletedEvents
                do! repo.commit entityId expectedVersion newEvents
                return newEvents
            }
        { handle = handle }
