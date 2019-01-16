namespace Ouroboros

open System

/// Domain types

/// `AsOf` means as it was or will be on and after that date.
/// Specifically, include events whose `CreatedDate` <= `ObservationDate`.
/// `AsAt` means as it is at that particular time only. It implies there may be changes.
/// Specifically, include events whose `EffectiveDate` <= `ObservationDate`. 
/// `Latest` means as it currently is. Specifically, include all all events in the stream.
type ObservationDate =
    | Latest
    | AsOf of DateTime
    | AsAt of DateTime

type EventMeta =
    { EffectiveDate: EffectiveDate
      EffectiveOrder: EffectiveOrder
      Source: Source }

type Event<'DomainEvent> =
    { Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta }

type CommandMeta =
    { EffectiveDate: EffectiveDate
      Source: Source }

type Command<'DomainCommand> =
    { Data: 'DomainCommand
      Meta: CommandMeta }

type Filter<'DomainEvent> =
    RecordedEvent<'DomainEvent> list
     -> RecordedEvent<'DomainEvent> list

type SortBy<'DomainEvent, 
            'T when 'T : comparison> =
    RecordedEvent<'DomainEvent> 
     -> 'T

type Evolve<'DomainState,'DomainEvent> =
    'DomainState
     -> 'DomainEvent
     -> 'DomainState

type Decide<'DomainState,'DomainCommand,'DomainEvent,'DomainError> =
    'DomainState
     -> Command<'DomainCommand>
     -> AsyncResult<Event<'DomainEvent> list,'DomainError>

type Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError,'T when 'T : comparison> =
    { zero: 'DomainState 
      filter: Filter<'DomainEvent>
      sortBy: SortBy<'DomainEvent,'T>
      evolve: Evolve<'DomainState,'DomainEvent>
      decide: Decide<'DomainState,'DomainCommand,'DomainEvent,'DomainError> }

type Serialize<'DomainEvent,'DomainError> =
    'DomainEvent
     -> Result<byte array,'DomainError>

type Deserialize<'DomainEvent,'DomainError> =
    byte array
     -> Result<'DomainEvent,'DomainError>

type Serializer<'DomainEvent,'DomainError> =
    { serializeToBytes: Serialize<'DomainEvent,'DomainError>
      deserializeFromBytes: Deserialize<'DomainEvent,'DomainError> }

/// Store types

type ReadStream<'StoreError> = 
    Direction 
     -> StreamSlice 
     -> StreamId 
     -> AsyncResult<SerializedRecordedEvent list,'StoreError>

type WriteStream<'StoreError> = 
    ExpectedVersion 
     -> SerializedEvent list 
     -> StreamId 
     -> AsyncResult<unit,'StoreError>

type Store<'StoreError> =
    { readStream: ReadStream<'StoreError>
      writeStream: WriteStream<'StoreError> }
