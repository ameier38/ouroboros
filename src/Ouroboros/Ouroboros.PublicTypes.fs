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

type SerializedEvent =
    { Type: EventType
      Data: byte array
      Meta: byte array }

type RecordedEvent<'DomainEvent> =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta }

type SerializedRecordedEvent =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Type: EventType
      Data: byte array
      Meta: byte array }

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

type ReadLast<'StoreError> =
    StreamId
     -> AsyncResult<SerializedRecordedEvent,'StoreError>

type ReadStream<'StoreError> = 
    StreamId 
     -> AsyncResult<SerializedRecordedEvent list,'StoreError>

type WriteStream<'StoreError> = 
    ExpectedVersion 
     -> SerializedEvent list 
     -> StreamId 
     -> AsyncResult<unit,'StoreError>

type Store<'StoreError> =
    { readLast: ReadLast<'StoreError>
      readStream: ReadStream<'StoreError>
      writeStream: WriteStream<'StoreError> }

/// Repository types

type Version =
    EntityId
     -> AsyncResult<ExpectedVersion,OuroborosError>

type Load<'DomainEvent> =
    EntityId
     -> AsyncResult<RecordedEvent<'DomainEvent> list,OuroborosError>

type Commit<'DomainEvent> = 
    EntityId 
     -> ExpectedVersion 
     -> Event<'DomainEvent> list
     -> AsyncResult<unit,OuroborosError>

type Repository<'DomainEvent> =
    { version: Version 
      load: Load<'DomainEvent>
      commit: Commit<'DomainEvent> }

/// Command handler types

type Execute<'DomainCommand,'DomainEvent> =
    EntityId
     -> Command<'DomainCommand>
     -> AsyncResult<Event<'DomainEvent> list,OuroborosError>

type CommandHandler<'DomainCommand,'DomainEvent> =
    { execute: Execute<'DomainCommand,'DomainEvent> }

/// Query handler types

type Replay<'DomainEvent> =
    ObservationDate
     -> EntityId
     -> AsyncResult<RecordedEvent<'DomainEvent> list,OuroborosError>

type Reconstitute<'DomainEvent,'DomainState> =
    RecordedEvent<'DomainEvent> list
     -> 'DomainState

type QueryHandler<'DomainState,'DomainEvent> =
    { replay: Replay<'DomainEvent>
      reconstitute: Reconstitute<'DomainEvent,'DomainState> }
