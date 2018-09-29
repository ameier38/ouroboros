module Ouroboros.Api

open Ouroboros.Constants

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
                    printfn "deserializing deleted event"
                    let! deletion = 
                        serializedEventData 
                        |> DeletionDto.deserialize
                        |> Result.bind DeletionDto.toDomain
                        |> Result.mapError mapError 
                    printfn "deserialized deleted event"
                    printfn "deserializing deleted event meta"
                    let! deletedEventMeta = 
                        serializedEventMeta
                        |> DeletedEventMetaDto.deserialize
                        |> Result.bind DeletedEventMetaDto.toDomain
                        |> Result.mapError mapError
                    printfn "deserialized deleted event meta"
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
                    printfn "deserializing domain event"
                    let! domainEvent = 
                        serializedEventData 
                        |> serializer.deserialize
                    printfn "deserialized domain event"
                    printfn "deserializing domain event meta"
                    let! domainEventMeta = 
                        serializedEventMeta 
                        |> DomainEventMetaDto.deserialize
                        |> Result.bind DomainEventMetaDto.toDomain
                        |> Result.mapError mapError
                    printfn "deserialized domain event meta"
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
                printfn "serializing deleted event"
                let! serializedEventData =
                    deletion
                    |> DeletionDto.fromDomain
                    |> DeletionDto.serialize
                    |> Result.mapError mapError
                printfn "serialized deleted event"
                printfn "serializing deleted event meta"
                let! serializedEventMeta =
                    meta
                    |> DeletedEventMetaDto.fromDomain
                    |> DeletedEventMetaDto.serialize
                    |> Result.mapError mapError
                printfn "serialized deleted event meta"
                return
                    { SerializedEvent.Type = DeletedEventType
                      Data = serializedEventData
                      Meta = serializedEventMeta }
            }
        | DomainEvent { Type = domainEventType; Data = domainEvent; Meta = meta } ->
            result {
                printfn "serializing domain event"
                let! serializedEventData =
                    domainEvent
                    |> serializer.serialize
                printfn "serialized domain event"
                printfn "serializing domain event meta"
                let! serializedEventMeta =
                    meta
                    |> DomainEventMetaDto.fromDomain
                    |> DomainEventMetaDto.serialize
                    |> Result.mapError mapError
                printfn "serialized domain event meta"
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

module RecordedDomainEvent =
    let toDomainEvent recordedDomainEvent =
        { DomainEvent.Type = recordedDomainEvent.Type
          Data = recordedDomainEvent.Data
          Meta = recordedDomainEvent.Meta }

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
        let load entityId = 
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
        let commit entityId expectedVersion events = 
            asyncResult {
                printfn "serializing events"
                let! serializedEvents =
                    events
                    |> List.map serialize
                    |> Result.sequence
                    |> AsyncResult.ofResult
                printfn "serialized events"
                return!
                    entityId
                    |> createStreamId
                    |> store.writeStream expectedVersion serializedEvents
                    |> AsyncResult.mapError mapStoreError
            }
        { load = load
          commit = commit }

module Handler =
    let create<'DomainState,'DomainCommand,'DomainEvent,'DomainError> 
        (repo:Repository<'DomainEvent,'DomainError>) 
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError>) =
        let apply = Result.bind2 aggregate.apply
        let zero = aggregate.zero |> Ok
        let getState 
            (effectiveDate:EffectiveDate) 
            (events:DomainEvent<'DomainEvent> list) =
            events
            |> List.filter (fun e -> e.Meta.EffectiveDate <= effectiveDate)
            |> List.sortBy (fun e -> (e.Meta.EffectiveDate, e.Meta.EffectiveOrder))
            |> List.map (fun e -> e.Data)
            |> List.map Ok
            |> List.fold apply zero
        let decide 
            eventAccumulator
            (domainCommand:DomainCommand<'DomainCommand>) =
            result {
                let! (allDomainEvents, newDomainEvents) = eventAccumulator
                printfn "getting state"
                let! state = getState domainCommand.EffectiveDate allDomainEvents
                printfn "got state %A" state
                printfn "executing command %A" domainCommand
                let! generatedDomainEvents = aggregate.execute state domainCommand
                printfn "generated events %A" generatedDomainEvents
                let newAllDomainEvents = generatedDomainEvents @ allDomainEvents
                let newNewDomainEvents = generatedDomainEvents @ newDomainEvents
                return (newAllDomainEvents, newNewDomainEvents)
            }
        let handle 
            (entityId:EntityId) 
            (commands:Command<'DomainCommand> list) =
            asyncResult {
                printfn "loading events"
                let! recordedEvents = repo.load entityId
                printfn "loaded events"
                let extractDomainEvent = function
                    | RecordedDomainEvent recordedDomainEvent ->
                        Some recordedDomainEvent
                    | _ -> None
                let extractDeletedEventNumber = function
                    | RecordedDeletedEvent deletedEvent ->
                        Some deletedEvent.Data.EventNumber
                    | _ -> None
                let extractDomainCommand = function
                    | DomainCommand domainCommand -> Some domainCommand
                    | _ -> None
                let extractDeleteCommand = function
                    | Delete deleteCommand -> Some deleteCommand
                    | _ -> None
                printfn "extracting events"
                let (domainEvents, deletedEventNumbers) =
                    recordedEvents
                    |> List.divide extractDomainEvent extractDeletedEventNumber
                printfn "extracted domain events"
                printfn "extracted deleted event numbers"
                printfn "extracting commands"
                let (domainCommands, deleteCommands) =
                    commands
                    |> List.divide extractDomainCommand extractDeleteCommand
                printfn "extracted domain commands"
                printfn "extracted delete commands"
                let newDeletedEventNumbers =
                    deleteCommands
                    |> List.map (fun c -> c.Data.EventNumber)
                let newDeletedEvents =
                    deleteCommands
                    |> List.map DeleteCommand.toEvent
                let allDeletedEventNumbers = 
                    deletedEventNumbers 
                    @ newDeletedEventNumbers
                let filteredDomainEvents =
                    domainEvents
                    |> List.filter (fun e ->
                        allDeletedEventNumbers 
                        |> List.contains e.EventNumber 
                        |> not)
                    |> List.map RecordedDomainEvent.toDomainEvent
                let! newDomainEvents = 
                    domainCommands
                    |> List.fold decide (Ok (filteredDomainEvents, []))
                    |> Result.map snd
                    |> Result.map (List.map DomainEvent)
                    |> AsyncResult.ofResult
                printfn "new domain events %A" newDomainEvents
                let newEvents = newDomainEvents @ newDeletedEvents
                // TODO: change Any to the count of events
                printfn "commiting events"
                do! repo.commit entityId Any newEvents
                printfn "commited events"
                return newEvents
            }
        { handle = handle }
