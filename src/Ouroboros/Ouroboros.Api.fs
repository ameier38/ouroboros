module Ouroboros.Api

open Ouroboros.Constants

module List =
    let divide extractA extractB items =
        let listA = items |> List.choose extractA
        let listB = items |> List.choose extractB
        (listA, listB)

module SerializedRecordedEvent =
    let deserialize 
        (mapError:string -> 'DomainError)
        (serializer:Serializer<'DomainEvent, 'DomainError>)
        : SerializedRecordedEvent -> Result<RecordedEvent<'DomainEvent>, 'DomainError> =
        fun ({ SerializedRecordedEvent.Id = eventId
               CreatedDate = createdDate
               Type = eventType 
               Data = serializedEventData
               Meta = serializedEventMeta }) ->
            match eventType |> EventType.value with
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
                          CreatedDate = createdDate
                          Data = deletion
                          Meta = deletedEventMeta }
                        |> RecordedDeletedEvent
                }
            | _ ->
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
                          CreatedDate = createdDate
                          Type = eventType
                          Data = domainEvent
                          Meta = domainEventMeta }
                        |> RecordedDomainEvent
                }

module Event =
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
                let! eventType = 
                    DeletedEventType 
                    |> EventType.create
                    |> Result.mapError mapError
                return
                    { SerializedEvent.Type = eventType
                      Data = serializedEventData
                      Meta = serializedEventMeta }
            }
        | DomainEvent { Type = eventType; Data = domainEvent; Meta = meta } ->
            result {
                let! serializedEventData =
                    domainEvent
                    |> serializer.serialize
                let! serializedEventMeta =
                    meta
                    |> DomainEventMetaDto.fromDomain
                    |> DomainEventMetaDto.serialize
                    |> Result.mapError mapError
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
        (mapStoreError:'StoreError -> 'DomainError)
        (mapError:string -> 'DomainError)
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
            |> List.map Ok
            |> List.fold apply zero
        let decide 
            (domainEvents:Result<DomainEvent<'DomainEvent> list, 'DomainError>) 
            (domainCommand:DomainCommand<'DomainCommand>) =
            result {
                let! domainEvents' = domainEvents
                let! state = getState domainCommand.EffectiveDate domainEvents'
                let! newEvents = aggregate.execute state domainCommand.Data
                return newEvents @ domainEvents'
            }
        let handle 
            (entityId:EntityId) 
            (commands:Command<'DomainCommand> list) =
            asyncResult {
                let! recordedEvents = repo.load entityId
                let extractDomainEvent = function
                    | RecordedDomainEvent recordedDomainEvent ->
                        recordedDomainEvent
                        |> RecordedDomainEvent.toDomainEvent
                        |> Some
                    | _ -> None
                let extractDeletedEvent = function
                    | RecordedDeletedEvent deletedEvent ->
                        Some deletedEvent
                    | _ -> None
                let extractDomainCommand = function
                    | DomainCommand domainCommand -> Some domainCommand
                    | _ -> None
                let extractDeleteCommand = function
                    | Delete deleteCommand -> Some deleteCommand
                    | _ -> None
                let (domainEvents, deletedEvents) =
                    recordedEvents
                    |> List.divide extractDomainEvent extractDeletedEvent
                let (domainCommands, deleteCommands) =
                    commands
                    |> List.divide extractDomainCommand extractDeleteCommand
                let! newDomainEvents = 
                    domainCommands
                    |> List.fold decide (Ok domainEvents)
                    |> Result.map (List.map DomainEvent)
                    |> AsyncResult.ofResult
                let newDeletedEvents =
                    deleteCommands
                    |> List.map DeleteCommand.toEvent
                let newEvents = newDomainEvents @ newDeletedEvents
                // TODO: change Any to the count of events
                do! repo.commit entityId Any newEvents
                return newEvents
            }
        { handle = handle }
