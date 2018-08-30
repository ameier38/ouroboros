namespace Ouroboros

open System

/// Metadata about the event.
/// `EffectiveDate`: Date at which the event is effective.
/// `EffectiveOrder`: If two events are effective at the same time, the order to apply.
/// `Source`: The source which generated the event.
type EventMeta =
    { EffectiveDate: DateTime
      EffectiveOrder: int
      Source: string }

type Event<'DomainEvent> =
    { Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta }


type SerializedEvent =
    { Type: EventType
      Data: byte array
      Meta: byte array }

type SerializedRecordedEvent =
    { Id: EventId
      CreatedDate: DateTime
      Type: EventType
      Data: byte array
      Meta: byte array }

type RecordedEvent<'DomainEvent> =
    { Id: EventId
      CreatedDate: DateTime
      Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta }

type ReadStream<'StoreError> = 
    StreamSlice 
     -> StreamId 
     -> AsyncResult<SerializedRecordedEvent list, 'StoreError>

type ReadEntireStream<'StoreError> =
    StreamStart
     -> StreamId
     -> AsyncResult<SerializedRecordedEvent list, 'StoreError>

type WriteStream<'StoreError> = 
    ExpectedVersion 
     -> SerializedEvent list 
     -> StreamId 
     -> AsyncResult<unit, 'StoreError>

type Store<'StoreError> =
    { readStream: ReadStream<'StoreError>
      readEntireStream: ReadEntireStream<'StoreError>
      writeStream: WriteStream<'StoreError> }

type Serialize<'DomainEvent,'DomainError> = 
    Event<'DomainEvent> 
     -> Result<SerializedEvent, 'DomainError>

type Deserialize<'DomainEvent,'DomainError> = 
    SerializedRecordedEvent 
     -> Result<RecordedEvent<'DomainEvent>, 'DomainError>

type Serializer<'DomainEvent,'DomainError> =
    { serialize: Serialize<'DomainEvent,'DomainError>
      deserialize: Deserialize<'DomainEvent,'DomainError> }

type Load<'DomainEvent,'DomainError> = 
    EntityId 
     -> AsyncResult<RecordedEvent<'DomainEvent> list, 'DomainError>

type Commit<'DomainEvent,'DomainError> = 
    EntityId 
     -> ExpectedVersion 
     -> Event<'DomainEvent> list
     -> AsyncResult<unit, 'DomainError>

/// Provides access to stored streams
type Repository<'DomainEvent,'DomainError> =
    { load: Load<'DomainEvent,'DomainError>
      commit: Commit<'DomainEvent,'DomainError> }
module Repository =
    let create<'DomainEvent,'DomainError,'StoreError> 
        (store:Store<'StoreError>) 
        (mapError:'StoreError -> 'DomainError)
        (serializer:Serializer<'DomainEvent,'DomainError>) 
        (entityType:EntityType) =
        let createStreamId = StreamId.create entityType
        let load entityId = asyncResult {
            let streamStart  = StreamStart.zero
            let! events = 
                createStreamId entityId
                |> store.readEntireStream streamStart
                |> AsyncResult.mapError mapError
            return! 
                events 
                |> List.map serializer.deserialize 
                |> Result.sequence
                |> AsyncResult.ofResult
        }
        let commit entityId expectedVersion events = 
            asyncResult {
                let! serializedEvents =
                    events
                    |> List.map serializer.serialize 
                    |> Result.sequence
                    |> AsyncResult.ofResult
                return!
                    createStreamId entityId
                    |> store.writeStream expectedVersion serializedEvents
                    |> AsyncResult.mapError mapError
            }
        { load = load
          commit = commit }

type Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError> =
    { zero: 'DomainState 
      apply: 'DomainState -> RecordedEvent<'DomainEvent> -> 'DomainState
      execute: 'DomainState -> 'DomainCommand -> AsyncResult<Event<'DomainEvent> list,'DomainError> }
module Aggregate =
    let createHandler<'DomainState,'DomainCommand,'DomainEvent,'DomainError> 
        (repo:Repository<'DomainEvent,'DomainError>) 
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError>) =
        fun entityId asOfDate command ->
            asyncResult {
                let! recordedEvents = repo.load entityId
                printfn "getting events for %A as of %A" entityId asOfDate
                let events =
                    recordedEvents
                    |> List.filter (fun re -> re.CreatedDate <= asOfDate)
                    |> List.sortBy (fun re -> (re.Meta.EffectiveDate, re.Meta.EffectiveOrder))
                printfn "events:\n%A" events
                printfn "computing state"
                let state = List.fold aggregate.apply aggregate.zero events
                printfn "state: %A" state
                printfn "executing command: %A" command
                let! newEvents = aggregate.execute state command
                printfn "new events\n%A" newEvents
                printfn "committing events"
                do! repo.commit entityId Any newEvents
                printfn "command successfully executed"
            }
