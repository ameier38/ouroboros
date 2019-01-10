namespace Ouroboros

open System

type AsDate =
    | Latest
    | AsOf of AsOfDate
    | AsAt of AsAtDate

type EventMeta<'DomainEventMeta> =
    { EffectiveDate: EffectiveDate
      EffectiveOrder: EffectiveOrder
      DomainEventMeta: 'DomainEventMeta }

type Event<'DomainEvent, 
           'DomainEventMeta> =
    { Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta<'DomainEventMeta> }

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

type RecordedEvent<'DomainEvent, 
                   'DomainEventMeta> =
    { Id: EventId
      EventNumber: EventNumber
      CreatedDate: CreatedDate
      Type: EventType
      Data: 'DomainEvent
      Meta: EventMeta<'DomainEventMeta> }

type CommandMeta<'DomainCommandMeta> =
    { EffectiveDate: EffectiveDate
      DomainCommandMeta: 'DomainCommandMeta }

type Command<'DomainCommand, 
             'DomainCommandMeta> =
    { Data: 'DomainCommand
      Meta: CommandMeta<'DomainCommandMeta> }

type ReadStream<'StoreError> = 
    StreamSlice 
     -> StreamId 
     -> AsyncResult<SerializedRecordedEvent list,'StoreError>

type ReadEntireStream<'StoreError> =
    StreamStart
     -> StreamId
     -> AsyncResult<SerializedRecordedEvent list,'StoreError>

type WriteStream<'StoreError> = 
    ExpectedVersion 
     -> SerializedEvent list 
     -> StreamId 
     -> AsyncResult<unit,'StoreError>

type Store<'StoreError> =
    { readStream: ReadStream<'StoreError>
      readEntireStream: ReadEntireStream<'StoreError>
      writeStream: WriteStream<'StoreError> }

type Load<'DomainEvent, 
          'DomainEventMeta, 
          'DomainError> =
    EntityId
     -> AsyncResult<RecordedEvent<'DomainEvent,'DomainEventMeta> list,'DomainError>

type Filter<'DomainEvent, 
            'DomainEventMeta> =
    RecordedEvent<'DomainEvent,'DomainEventMeta> list
     -> RecordedEvent<'DomainEvent,'DomainEventMeta> list

type SortBy<'DomainEvent, 
            'DomainEventMeta, 
            'T when 'T : comparison> =
    RecordedEvent<'DomainEvent,'DomainEventMeta> 
     -> 'T

type Commit<'DomainEvent, 
            'DomainEventMeta, 
            'DomainError> = 
    EntityId 
     -> ExpectedVersion 
     -> Event<'DomainEvent,'DomainEventMeta> list
     -> AsyncResult<unit,'DomainError>

type Apply<'DomainState, 
           'DomainEvent, 
           'DomainError> =
    'DomainState
     -> 'DomainEvent
     -> Result<'DomainState,'DomainError>

type Execute<'DomainState, 
             'DomainCommand, 
             'DomainCommandMeta, 
             'DomainEvent, 
             'DomainEventMeta, 
             'DomainError> =
    'DomainState
     -> Command<'DomainCommand,'DomainCommandMeta>
     -> AsyncResult<Event<'DomainEvent,'DomainEventMeta> list,'DomainError>

type Handle<'DomainCommand, 
            'DomainCommandMeta, 
            'DomainEvent, 
            'DomainEventMeta, 
            'DomainError> =
    EntityId
     -> Command<'DomainCommand,'DomainCommandMeta>
     -> AsyncResult<Event<'DomainEvent,'DomainEventMeta> list,'DomainError>

type Replay<'DomainEvent, 
            'DomainEventMeta, 
            'DomainError> =
    EntityId
     -> AsDate
     -> AsyncResult<RecordedEvent<'DomainEvent,'DomainEventMeta> list,'DomainError>

type Reconstitute<'DomainEvent, 
                  'DomainEventMeta, 
                  'DomainState, 
                  'DomainError> =
    RecordedEvent<'DomainEvent,'DomainEventMeta> list
     -> Result<'DomainState,'DomainError>

type Repository<'DomainEvent, 
                'DomainEventMeta, 
                'DomainError> =
    { load: Load<'DomainEvent,'DomainEventMeta,'DomainError>
      commit: Commit<'DomainEvent,'DomainEventMeta,'DomainError> }

type Aggregate<'DomainState,
               'DomainCommand, 
               'DomainCommandMeta, 
               'DomainEvent, 
               'DomainEventMeta, 
               'DomainError,
               'T when 'T : comparison> =
    { zero: 'DomainState 
      filter: Filter<'DomainEvent,'DomainEventMeta>
      sortBy: SortBy<'DomainEvent,'DomainEventMeta,'T>
      apply: Apply<'DomainState,'DomainEvent,'DomainError>
      execute: Execute<'DomainState,'DomainCommand,'DomainCommandMeta,'DomainEvent,'DomainEventMeta,'DomainError> }

type CommandHandler<'DomainCommand, 
                    'DomainCommandMeta,
                    'DomainEvent, 
                    'DomainEventMeta,
                    'DomainError> =
    { handle: Handle<'DomainCommand,'DomainCommandMeta,'DomainEvent,'DomainEventMeta,'DomainError> }

type QueryHandler<'DomainState,'DomainEvent,'DomainEventMeta,'DomainError> =
    { replayAll: Replay<'DomainEvent,'DomainEventMeta,'DomainError>
      replay: Replay<'DomainEvent,'DomainEventMeta,'DomainError>
      reconstitute: Reconstitute<'DomainEvent,'DomainEventMeta,'DomainState,'DomainError> }
