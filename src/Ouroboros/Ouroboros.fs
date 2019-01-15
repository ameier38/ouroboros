[<AutoOpen>]
module Ouroboros.Implementation

let [<Literal>] ReversedEventType = "Reversed"

module internal Event =
    let serialize 
        (domainEventSerializer:Serializer<'DomainEvent,'DomainError>) 
        (domainEventMetaSerializer:Serializer<'DomainEventMeta,'DomainError>) 
        : Event<'DomainEvent,'DomainEventMeta> -> Result<SerializedEvent, OuroborosError> =
        fun event ->
            result {
                let { Event.Type = eventType
                      Data = domainEvent
                      Meta = meta } = event
                let! serializedDomainEvent =
                    domainEvent
                    |> serializer.serializeDomainEvent
                    |> Result.mapError OuroborosError
                let! serializedEventMeta =
                    meta
                    |> EventMetaDto.fromDomain
                    |> EventMetaDto.serialize
                return
                    { SerializedEvent.Type = eventType
                      Data = serializedDomainEvent
                      Meta = serializedEventMeta }
            }

module internal SerializedRecordedEvent =
    let deserialize<'DomainEvent,'DomainEventMeta> 
        : SerializedRecordedEvent -> Result<RecordedEvent<'DomainEvent,'DomainEventMeta>,OuroborosError> =
        fun ({ SerializedRecordedEvent.Id = eventId
               EventNumber = eventNumber
               CreatedDate = createdDate
               Type = eventType 
               Data = serializedDomainEvent
               Meta = serializedEventMeta }) ->
            result {
                let! domainEvent = 
                    serializedDomainEvent 
                    |> Json.deserializeFromBytes<'DomainEvent>
                    |> Result.mapError OuroborosError
                let! eventMeta = 
                    serializedEventMeta 
                    |> EventMetaDto.deserialize<'DomainEventMeta>
                    |> Result.bind EventMetaDto.toDomain
                return
                    { RecordedEvent.Id = eventId
                      EventNumber = eventNumber
                      CreatedDate = createdDate
                      Type = eventType
                      Data = domainEvent
                      Meta = eventMeta }
            }

module internal ExpectedVersion =
    let create nEvents =
        match nEvents with
        | n when n = 0 -> NoStream |> Ok
        | n -> 
            n - 1
            |> int64
            |> SpecificExpectedVersion.create
            |> Result.map ExpectedVersion.Specific
        |> Result.mapError OuroborosError

module Repository =
    let create<'DomainEvent,'DomainEventMeta,'StoreError,'DomainError>
        (store:Store<'StoreError>) 
        (convertStoreError:'StoreError -> 'DomainError)
        (convertOuroborosError:OuroborosError -> 'DomainError)
        (entityType:EntityType) 
        : Repository<'DomainEvent,'DomainEventMeta,'DomainError> =
        let createStreamId = StreamId.create entityType
        let load entityId = 
            asyncResult {
                let streamStart = StreamStart.zero
                let! serializedRecordedEvents = 
                    entityId
                    |> createStreamId
                    |> store.readEntireStream streamStart
                    |> AsyncResult.mapError convertStoreError
                return!
                    serializedRecordedEvents
                    |> List.map SerializedRecordedEvent.deserialize<'DomainEvent,'DomainEventMeta>
                    |> Result.sequence
                    |> Result.mapError convertOuroborosError
                    |> AsyncResult.ofResult
            }
        let commit entityId expectedVersion events = 
            asyncResult {
                let! serializedEvents =
                    events
                    |> List.map Event.serialize
                    |> Result.sequence
                    |> Result.mapError convertOuroborosError
                    |> AsyncResult.ofResult
                return!
                    entityId
                    |> createStreamId
                    |> store.writeStream expectedVersion serializedEvents
                    |> AsyncResult.mapError convertStoreError
            }
        { load = load
          commit = commit }

module QueryHandler =
    let create
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainCommandMeta,'DomainEvent,'DomainEventMeta,'DomainError,'T>)
        (repo:Repository<'DomainEvent,'DomainEventMeta,'DomainError>) =
        let replayAll
            (entityId:EntityId)
            (observationDate:ObservationDate) =
            asyncResult {
                let! recordedEvents = repo.load entityId
                let onOrBeforeObservationDate 
                    ({ RecordedEvent.CreatedDate = (CreatedDate createdDate) 
                       Meta = { EffectiveDate = (EffectiveDate effectiveDate) }}) =
                    match observationDate with
                    | Latest -> true
                    | AsOf asOfDate ->
                        createdDate <= asOfDate
                    | AsAt asAtDate ->
                        effectiveDate <= asAtDate
                return
                    recordedEvents
                    |> List.filter onOrBeforeObservationDate
            }
        let replay
            (entityId:EntityId)
            (observationDate:ObservationDate) =
            asyncResult {
                let! recordedEvents = replayAll entityId observationDate
                return
                    recordedEvents
                    |> aggregate.filter
            }
        let reconstitute
            (recordedEvents:RecordedEvent<'DomainEvent,'DomainEventMeta> list) =
            recordedEvents
            |> List.sortBy aggregate.sortBy
            |> List.map (fun e -> e.Data)
            |> List.fold aggregate.apply aggregate.zero
        { replayAll = replayAll
          replay = replay
          reconstitute = reconstitute }

module CommandHandler =
    let create
        (convertOuroborosError:OuroborosError -> 'DomainError)
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainCommandMeta,'DomainEvent,'DomainEventMeta,'DomainError,'T>)
        (repo:Repository<'DomainEvent,'DomainEventMeta,'DomainError>) =
        let handle 
            (entityId:EntityId) 
            (command:Command<'DomainCommand,'DomainCommandMeta>) =
            asyncResult {
                let queryHandler = QueryHandler.create aggregate repo
                let! recordedEvents = repo.load entityId
                let onOrBeforeEffectiveDate ({RecordedEvent.Meta = { EffectiveDate = effectiveDate }}) =
                    effectiveDate <= command.Meta.EffectiveDate
                let! expectedVersion = 
                    recordedEvents 
                    |> List.length 
                    |> ExpectedVersion.create
                    |> Result.mapError convertOuroborosError
                    |> AsyncResult.ofResult
                let state =
                    recordedEvents
                    |> List.filter onOrBeforeEffectiveDate
                    |> aggregate.filter
                    |> queryHandler.reconstitute
                let! newEvents = aggregate.execute state command
                do! repo.commit entityId expectedVersion newEvents
                return newEvents
            }
        { handle = handle }

module Defaults =
    let filter : Filter<'DomainEvent,'DomainEventMeta> =
        fun (recordedEvents:RecordedEvent<'DomainEvent,'DomainEventMeta> list) ->
            let extractReversedEvent recordedEvent =
                recordedEvent.Type
                |> EventType.value
                |> function
                   | ReversedEventType -> Some recordedEvent
                   | _ -> None
            let (reversedEvents, domainEvents) =
                recordedEvents
                |> List.divide extractReversedEvent
            let reversedEventNumbers =
                reversedEvents
                |> List.map (fun ({RecordedEvent.EventNumber = eventNumber}) -> eventNumber)
            domainEvents
            |> List.filter (fun domainEvent ->
                reversedEventNumbers 
                |> List.contains domainEvent.EventNumber 
                |> not) 

    let sortBy : SortBy<'DomainEvent,'DomainEventMeta,EffectiveDate * EffectiveOrder> =
        fun (recordedEvent:RecordedEvent<'DomainEvent,'DomainEventMeta>) ->
            recordedEvent.Meta.EffectiveDate, recordedEvent.Meta.EffectiveOrder
