[<AutoOpen>]
module Ouroboros.Implementation

module Event =
    let toSerializedEvent 
        (serializer:Serializer<'DomainEvent>) 
        : Event<'DomainEvent> -> Result<SerializedEvent,OuroborosError> =
        fun event ->
            result {
                let { Event.Type = eventType
                      Data = domainEvent
                      Meta = meta } = event
                let! serializedDomainEvent =
                    domainEvent
                    |> serializer.serializeToBytes
                let! serializedEventMeta =
                    meta
                    |> EventMetaDto.fromDomain
                    |> EventMetaDto.serializeToBytes
                return
                    { SerializedEvent.Type = eventType
                      Data = serializedDomainEvent
                      Meta = serializedEventMeta }
            }

module SerializedRecordedEvent =
    let toRecordedEvent
        (serializer:Serializer<'DomainEvent>)
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

module ExpectedVersion =
    let fromLastEvent lastEventOpt =
        match lastEventOpt with
        | Some lastEvent ->
            lastEvent.EventNumber
            |> EventNumber.value
            |> SpecificExpectedVersion.create
            |> Result.map ExpectedVersion.Specific
        | None ->
            EmptyStream
            |> Ok
        |> Result.mapError OuroborosError

module Repository =
    let create
        (store:Store) 
        (serializer:Serializer<'DomainEvent>)
        (entityType:EntityType) 
        : Repository<'DomainEvent> =
        let createStreamId = StreamId.create entityType
        let version entityId =
            asyncResult {
                let! lastEventOpt =
                    entityId
                    |> createStreamId
                    |> store.readLast
                return!
                    lastEventOpt
                    |> ExpectedVersion.fromLastEvent
                    |> AsyncResult.ofResult
            }
        let load entityId = 
            asyncResult {
                let! serializedRecordedEvents = 
                    entityId
                    |> createStreamId
                    |> store.readStream
                let toRecordedEvent = SerializedRecordedEvent.toRecordedEvent serializer
                return!
                    serializedRecordedEvents
                    |> List.map toRecordedEvent
                    |> Result.sequence
                    |> AsyncResult.ofResult
            }
        let commit entityId expectedVersion events = 
            asyncResult {
                let toSerializedEvent = Event.toSerializedEvent serializer
                let! serializedEvents =
                    events
                    |> List.map toSerializedEvent
                    |> Result.sequence
                    |> AsyncResult.ofResult
                return!
                    entityId
                    |> createStreamId
                    |> store.writeStream expectedVersion serializedEvents
            }
        { version = version
          load = load
          commit = commit }

module QueryHandler =
    let create
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'T>)
        (repo:Repository<'DomainEvent>) =
        let replay
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
                    |> fun events ->
                        events
                        |> List.map (fun event -> 
                            event.EventNumber |> EventNumber.value,
                            event.Type |> EventType.value)
                        |> printfn "recordedEvents:\n%A"
                        events
                    |> List.filter onOrBeforeObservationDate
                    |> aggregate.filter
                    |> fun events ->
                        events
                        |> List.map (fun event -> 
                            event.EventNumber |> EventNumber.value,
                            event.Type |> EventType.value)
                        |> printfn "filtered recordedEvents:\n%A"
                        events
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
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'T>)
        (repo:Repository<'DomainEvent>) =
        let queryHandler = QueryHandler.create aggregate repo
        let execute 
            (entityId:EntityId) 
            (command:Command<'DomainCommand>) =
            asyncResult {
                printfn "-----------------"
                printfn "command: %A" command.Data
                let { Command.Meta = { EffectiveDate = (EffectiveDate effectiveDate) } } = command
                let observationDate = effectiveDate |> AsAt
                let! expectedVersion = repo.version entityId
                let! recordedEvents = queryHandler.replay entityId observationDate
                let state =
                    recordedEvents
                    |> queryHandler.reconstitute
                printfn "state: %A" state
                let! newEvents = aggregate.decide state command
                newEvents
                |> List.map (fun event -> event.Type |> EventType.value)
                |> printfn "newEvents:\n%A"
                do! repo.commit entityId expectedVersion newEvents
                printfn "-----------------"
                return newEvents
            }
        { execute = execute }
