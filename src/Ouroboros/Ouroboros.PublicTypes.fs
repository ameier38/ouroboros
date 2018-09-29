namespace Ouroboros

type DomainEventMeta =
    { Source: Source 
      EffectiveDate: EffectiveDate
      EffectiveOrder: EffectiveOrder }

type DeletedEventMeta =
    { Source: Source }

type Deletion =
    { EventNumber: EventNumber
      Reason: DeletionReason }

type DomainEvent<'DomainEvent> =
    { Type: EventType
      Data: 'DomainEvent
      Meta: DomainEventMeta }

type DeletedEvent =
    { Data: Deletion
      Meta: DeletedEventMeta }

type Event<'DomainEvent> = 
    | DomainEvent of DomainEvent<'DomainEvent>
    | DeletedEvent of DeletedEvent

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

type RecordedDomainEvent<'DomainEvent> =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Type: EventType
      Data: 'DomainEvent
      Meta: DomainEventMeta }

type RecordedDeletedEvent =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Data: Deletion
      Meta: DeletedEventMeta }

type RecordedEvent<'DomainEvent> =
    | RecordedDomainEvent of RecordedDomainEvent<'DomainEvent>
    | RecordedDeletedEvent of RecordedDeletedEvent

type DomainCommand<'DomainCommand> =
    { Source: Source
      EffectiveDate: EffectiveDate
      Data: 'DomainCommand }

type DeleteCommand =
    { Source: Source
      Data: Deletion }

type Command<'DomainCommand> =
    | DomainCommand of DomainCommand<'DomainCommand>
    | Delete of DeleteCommand

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

type Serialize<'DomainEvent, 'DomainError> = 
    'DomainEvent 
     -> Result<byte array, 'DomainError>

type Deserialize<'DomainEvent, 'DomainError> = 
    byte array 
     -> Result<'DomainEvent, 'DomainError>

type Serializer<'DomainEvent, 'DomainError> =
    { serialize: Serialize<'DomainEvent, 'DomainError>
      deserialize: Deserialize<'DomainEvent, 'DomainError> }

type Load<'DomainEvent, 'DomainError> = 
    EntityId 
     -> AsyncResult<RecordedEvent<'DomainEvent> list, 'DomainError>

type Commit<'DomainEvent, 'DomainError> = 
    EntityId 
     -> ExpectedVersion 
     -> Event<'DomainEvent> list
     -> AsyncResult<unit, 'DomainError>

type Apply<'DomainState, 'DomainEvent, 'DomainError> =
    'DomainState
     -> 'DomainEvent
     -> Result<'DomainState, 'DomainError>

type Execute<'DomainState, 'DomainCommand, 'DomainEvent, 'DomainError> =
    'DomainState
     -> DomainCommand<'DomainCommand>
     -> Result<DomainEvent<'DomainEvent> list, 'DomainError>

type Handle<'DomainCommand, 'DomainEvent, 'DomainError> =
    EntityId
     -> Command<'DomainCommand> list
     -> AsyncResult<Event<'DomainEvent> list, 'DomainError>

type Repository<'DomainEvent, 'DomainError> =
    { load: Load<'DomainEvent, 'DomainError>
      commit: Commit<'DomainEvent, 'DomainError> }

type Aggregate<'DomainState, 'DomainCommand, 'DomainEvent, 'DomainError> =
    { zero: 'DomainState 
      apply: Apply<'DomainState, 'DomainEvent, 'DomainError>
      execute: Execute<'DomainState, 'DomainCommand, 'DomainEvent, 'DomainError> }

type Handler<'DomainCommand, 'DomainEvent, 'DomainError> =
    { handle: Handle<'DomainCommand, 'DomainEvent, 'DomainError> }
