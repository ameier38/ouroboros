[<AutoOpen>]
module Ouroboros.Implementation

let [<Literal>] ReversedEventType = "Reversed"

module internal Event =
    let toSerializedEvent 
        (convertDomainError:'DomainError -> OuroborosError)
        (serializer:Serializer<'DomainEvent,'DomainError>) 
        : Event<'DomainEvent> -> Result<SerializedEvent,OuroborosError> =
        fun event ->
            result {
                let { Event.Type = eventType
                      Data = domainEvent
                      Meta = meta } = event
                let! serializedDomainEvent =
                    domainEvent
                    |> serializer.serializeToBytes
                    |> Result.mapError convertDomainError
                let! serializedEventMeta =
                    meta
                    |> EventMetaDto.fromDomain
                    |> EventMetaDto.serializeToBytes
                return
                    { SerializedEvent.Type = eventType
                      Data = serializedDomainEvent
                      Meta = serializedEventMeta }
            }

module internal SerializedRecordedEvent =
    let toRecordedEvent
        (convertDomainError:'DomainError -> OuroborosError)
        (serializer:Serializer<'DomainEvent,'DomainError>)
        : SerializedRecordedEvent -> Result<RecordedEvent<'DomainEvent>,OuroborosError> =
        fun ({ SerializedRecordedEvent.Id = eventId
               EventNumber = eventNumber
               CreatedDate = createdDate
               Type = eventType 
               Data = serializedDomainEvent
               Meta = serializedEventMeta }) ->
            result {
                let! domainEvent = 
                    serializedDomainEvent 
                    |> serializer.deserializeFromBytes
                    |> Result.mapError convertDomainError
                let! eventMeta = 
                    serializedEventMeta 
                    |> EventMetaDto.deserializeFromBytes
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
    let create
        (store:Store<'StoreError>) 
        (serializer:Serializer<'DomainEvent,'DomainError>)
        (convertStoreError:'StoreError -> OuroborosError)
        (convertDomainError:'DomainError -> OuroborosError)
        (entityType:EntityType) 
        : Repository<'DomainEvent> =
        let createStreamId = StreamId.create entityType
        let version entityId =
            asyncResult {
                let! lastEvent =
                    entityId
                    |> createStreamId
                    |> store.readLast
                    |> AsyncResult.mapError convertStoreError
                return!
                    lastEvent.EventNumber
                    |> EventNumber.value
                    |> SpecificExpectedVersion.create
                    |> Result.map ExpectedVersion.Specific
                    |> Result.mapError OuroborosError
                    |> AsyncResult.ofResult
            }
        let load entityId = 
            asyncResult {
                let! serializedRecordedEvents = 
                    entityId
                    |> createStreamId
                    |> store.readStream
                    |> AsyncResult.mapError convertStoreError
                let toRecordedEvent = 
                    (convertDomainError, serializer)
                    ||> SerializedRecordedEvent.toRecordedEvent 
                return!
                    serializedRecordedEvents
                    |> List.map toRecordedEvent
                    |> Result.sequence
                    |> AsyncResult.ofResult
            }
        let commit entityId expectedVersion events = 
            asyncResult {
                let toSerializedEvent = 
                    (convertDomainError, serializer)
                    ||> Event.toSerializedEvent
                let! serializedEvents =
                    events
                    |> List.map toSerializedEvent
                    |> Result.sequence
                    |> AsyncResult.ofResult
                return!
                    entityId
                    |> createStreamId
                    |> store.writeStream expectedVersion serializedEvents
                    |> AsyncResult.mapError convertStoreError
            }
        { version = version
          load = load
          commit = commit }

module QueryHandler =
    let create
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError,'T>)
        (repo:Repository<'DomainEvent>) =
        let replay
            (observationDate:ObservationDate)
            (entityId:EntityId) =
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
                    |> aggregate.filter
            }
        let reconstitute
            (recordedEvents:RecordedEvent<'DomainEvent> list) =
            recordedEvents
            |> List.sortBy aggregate.sortBy
            |> List.map (fun e -> e.Data)
            |> List.fold aggregate.evolve aggregate.zero
        { replay = replay
          reconstitute = reconstitute }

module CommandHandler =
    let create
        (convertDomainError:'DomainError -> OuroborosError)
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError,'T>)
        (repo:Repository<'DomainEvent>) =
        let execute 
            (entityId:EntityId) 
            (command:Command<'DomainCommand>) =
            asyncResult {
                let queryHandler = QueryHandler.create aggregate repo
                let { Command.Meta = { EffectiveDate = (EffectiveDate effectiveDate) } } = command
                let observationDate = effectiveDate |> AsAt
                let! expectedVersion = repo.version entityId
                let! recordedEvents = queryHandler.replay observationDate entityId
                let state =
                    recordedEvents
                    |> aggregate.filter
                    |> queryHandler.reconstitute
                let! newEvents = 
                    aggregate.decide state command
                    |> AsyncResult.mapError convertDomainError
                do! repo.commit entityId expectedVersion newEvents
                return newEvents
            }
        { execute = execute }

module Defaults =
    let filter : Filter<'DomainEvent> =
        fun (recordedEvents:RecordedEvent<'DomainEvent> list) ->
            let extractReversedEvent (recordedEvent:RecordedEvent<'DomainEvent>) =
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

    let sortBy : SortBy<'DomainEvent,EffectiveDate * EffectiveOrder> =
        fun (recordedEvent:RecordedEvent<'DomainEvent>) ->
            recordedEvent.Meta.EffectiveDate, recordedEvent.Meta.EffectiveOrder
