namespace Ouroboros

open System

/// Metadata about the event.
/// `EffectiveDate`: Date at which the event is effective.
/// `EffectiveOrder`: If two events are effective at the same time, the order to apply.
/// `Source`: The source which generated the event.
type EventMeta =
    { EffectiveDate: EffectiveDate
      EffectiveOrder: EffectiveOrder
      Source: Source }

/// Wrapper for domain event to persist
/// `Type`: the type of event
/// `Data`: the domain event
/// `Meta`: meta data about the event
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
      CreatedDate: CreatedDate
      Type: EventType
      Data: byte array
      Meta: byte array }

type RecordedEvent<'DomainEvent> =
    { Id: EventId
      CreatedDate: CreatedDate
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

type Apply<'DomainState, 'DomainEvent> =
    'DomainState
     -> RecordedEvent<'DomainEvent>
     -> 'DomainState

type Execute<'DomainState, 'DomainCommand, 'DomainEvent, 'DomainError> =
    'DomainState
     -> 'DomainCommand
     -> AsyncResult<Event<'DomainEvent> list, 'DomainError>

type Handle<'DomainCommand, 'DomainEvent, 'DomainError> =
    EntityId
     -> 'DomainCommand
     -> AsyncResult<Event<'DomainEvent> list, 'DomainError>

type Repository<'DomainEvent,'DomainError> =
    { load: Load<'DomainEvent,'DomainError>
      commit: Commit<'DomainEvent,'DomainError> }

type Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError> =
    { zero: 'DomainState 
      apply: Apply<'DomainState, 'DomainEvent>
      execute: Execute<'DomainState, 'DomainCommand, 'DomainEvent, 'DomainError> }

type Handler<'DomainCommand, 'DomainEvent, 'DomainError> =
    { handle: Handle<'DomainCommand, 'DomainEvent, 'DomainError> }