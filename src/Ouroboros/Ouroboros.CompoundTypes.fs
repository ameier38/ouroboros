namespace Ouroboros

type Direction =
    | Forward
    | Backward

type StreamCount =
    | One
    | All

/// Slice of a stream
type StreamSlice = StreamSlice of StreamStart * StreamCount
module StreamSice =
    let value (StreamSlice (start, count)) = start, count

type SerializedEvent =
    { Type: EventType
      Data: byte array
      Meta: byte array }

type SerializedRecordedEvent =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Type: EventType
      Data: byte array
      Meta: byte array }

type RecordedEvent<'DomainEvent> =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta }

type Load<'DomainEvent> =
    Direction
     -> EntityId
     -> AsyncResult<RecordedEvent<'DomainEvent> list,OuroborosError>

type Commit<'DomainEvent> = 
    EntityId 
     -> ExpectedVersion 
     -> Event<'DomainEvent> list
     -> AsyncResult<unit,OuroborosError>

type Execute<'DomainCommand,'DomainEvent> =
    EntityId
     -> Command<'DomainCommand>
     -> AsyncResult<Event<'DomainEvent> list,OuroborosError>

type Replay<'DomainEvent> =
    Direction
     -> ObservationDate
     -> EntityId
     -> AsyncResult<RecordedEvent<'DomainEvent> list,OuroborosError>

type Reconstitute<'DomainEvent,'DomainState> =
    RecordedEvent<'DomainEvent> list
     -> 'DomainState

type Repository<'DomainEvent> =
    { load: Load<'DomainEvent>
      commit: Commit<'DomainEvent> }

type CommandHandler<'DomainCommand,'DomainEvent> =
    { execute: Execute<'DomainCommand,'DomainEvent> }

type QueryHandler<'DomainState,'DomainEvent> =
    { replay: Replay<'DomainEvent>
      reconstitute: Reconstitute<'DomainEvent,'DomainState> }
