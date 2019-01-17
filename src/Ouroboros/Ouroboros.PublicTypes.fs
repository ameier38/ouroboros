namespace Ouroboros

open System

/// Error types
type OuroborosError =
    | OuroborosError of string
    | DomainError of string
    | StoreError of string

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

type Decide<'DomainState,'DomainCommand,'DomainEvent> =
    'DomainState
     -> Command<'DomainCommand>
     -> AsyncResult<Event<'DomainEvent> list,OuroborosError>

type Aggregate<'DomainState,'DomainCommand,'DomainEvent,'T when 'T : comparison> =
    { zero: 'DomainState 
      filter: Filter<'DomainEvent>
      sortBy: SortBy<'DomainEvent,'T>
      evolve: Evolve<'DomainState,'DomainEvent>
      decide: Decide<'DomainState,'DomainCommand,'DomainEvent> }

type Serialize<'DomainEvent> =
    'DomainEvent
     -> Result<byte array,OuroborosError>

type Deserialize<'DomainEvent> =
    byte array
     -> Result<'DomainEvent,OuroborosError>

type Serializer<'DomainEvent> =
    { serializeToBytes: Serialize<'DomainEvent>
      deserializeFromBytes: Deserialize<'DomainEvent> }

/// Store types

type ReadLast =
    StreamId
     -> AsyncResult<SerializedRecordedEvent option,OuroborosError>

type ReadStream = 
    StreamId 
     -> AsyncResult<SerializedRecordedEvent list,OuroborosError>

type WriteStream = 
    ExpectedVersion 
     -> SerializedEvent list 
     -> StreamId 
     -> AsyncResult<unit,OuroborosError>

type Store =
    { readLast: ReadLast
      readStream: ReadStream
      writeStream: WriteStream }

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
