namespace Ouroboros

[<AutoOpen>]
module Implementation =

    module Event =
        let create
            (eventType:EventType) 
            (domainEvent:'DomainEvent) 
            (meta:EventMeta<'DomainEventMeta>) =
            { Event.Type = eventType
              Data = domainEvent
              Meta = meta }
        let serialize 
            (event:Event<'DomainEvent, 'DomainEventMeta>)
            : Result<SerializedEvent, OuroborosError> =
            result {
                let { Event.Type = eventType
                      Data = domainEvent
                      Meta = meta } = event
                let! serializedDomainEvent =
                    domainEvent
                    |> Json.serializeToBytes
                let! serializedEventMeta =
                    meta
                    |> EventMetaDto.fromDomain
                    |> EventMetaDto.serialize
                return
                    { SerializedEvent.Type = eventType
                      Data = serializedDomainEvent
                      Meta = serializedEventMeta }
            }

    module EventMeta =
        let create effectiveDate effectiveOrder domainEventMeta =
            { EffectiveDate = effectiveDate
              EffectiveOrder = effectiveOrder
              DomainEventMeta = domainEventMeta }

    module Command =
        let create (domainCommand:'DomainCommand) (meta:CommandMeta<'DomainCommandMeta>) =
            { Data = domainCommand
              Meta = meta}

    module CommandMeta =
        let create effectiveDate domainCommandMeta =
            { EffectiveDate = effectiveDate
              DomainCommandMeta = domainCommandMeta }

    module SerializedRecordedEvent =
        let deserialize<'DomainEvent> 
            : SerializedRecordedEvent -> Result<RecordedEvent<'DomainEvent, 'DomainEventMeta>, OuroborosError> =
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
                    let! eventMeta = 
                        serializedEventMeta 
                        |> EventMetaDto.deserialize
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
        let create nEvents =
            match nEvents with
            | n when n = 0 -> NoStream |> Ok
            | n -> 
                n - 1
                |> int64
                |> SpecificExpectedVersion.create
                |> Result.map ExpectedVersion.Specific

    module Repository =
        let create
            (store:Store<'StoreError>) 
            (mapStoreError:'StoreError -> OuroborosError)
            (entityType:EntityType) =
            let createStreamId = StreamId.create entityType
            let load entityId = 
                asyncResult {
                    let streamStart  = StreamStart.zero
                    let! serializedRecordedEvents = 
                        createStreamId entityId
                        |> store.readEntireStream streamStart
                        |> AsyncResult.mapError mapStoreError
                    return!
                        serializedRecordedEvents
                        |> List.map SerializedRecordedEvent.deserialize<'DomainEvent>
                        |> Result.sequence
                        |> AsyncResult.ofResult
                }
            let commit entityId expectedVersion events = 
                asyncResult {
                    let! serializedEvents =
                        events
                        |> List.map Event.serialize
                        |> Result.sequence
                        |> AsyncResult.ofResult
                    return!
                        entityId
                        |> createStreamId
                        |> store.writeStream expectedVersion serializedEvents
                        |> AsyncResult.mapError mapStoreError
                }
            { load = load
              commit = commit }

    module QueryHandler =
        let create
            (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainCommandMeta,'DomainEvent,'DomainEventMeta,'DomainError,'T>)
            (repo:Repository<'DomainEvent,'DomainEventMeta,'DomainError>) =
            let apply = Result.bind2 aggregate.apply
            let zero = aggregate.zero |> Ok
            let replayAll
                (entityId:EntityId)
                (asDate:AsDate) =
                asyncResult {
                    let! recordedEvents = repo.load entityId
                    let onOrBeforeAsDate ({ RecordedEvent.CreatedDate = (CreatedDate createdDate); 
                                            Meta = { EffectiveDate = (EffectiveDate effectiveDate) }}) =
                        match asDate with
                        | Latest -> true
                        | AsOf (AsOfDate asOfDate) ->
                            createdDate <= asOfDate
                        | AsAt (AsAtDate asAtDate) ->
                            effectiveDate <= asAtDate
                    return
                        recordedEvents
                        |> List.filter onOrBeforeAsDate
                }
            let replay
                (entityId:EntityId)
                (asDate:AsDate) =
                asyncResult {
                    let! recordedEvents = replayAll entityId asDate
                    let filter = aggregate.filter recordedEvents
                    return
                        recordedEvents
                        |> List.filter filter
                }
            let reconstitute
                (recordedEvents:RecordedEvent<'DomainEvent, 'DomainEventMeta> list) =
                result {
                    return! 
                        recordedEvents
                        |> List.sortBy aggregate.sortBy
                        |> List.map (fun e -> e.Data)
                        |> List.map Ok
                        |> List.fold apply zero
                }
            { replayAll = replayAll
              replay = replay
              reconstitute = reconstitute }

    module CommandHandler =
        let create<'DomainState,'DomainCommand,'DomainEvent,'DomainError> 
            (mapOuroborosError:OuroborosError -> 'DomainError)
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
                        |> Result.mapError mapOuroborosError
                        |> AsyncResult.ofResult
                    let! state =
                        recordedEvents
                        |> List.filter (aggregate.filter recordedEvents)
                        |> List.filter onOrBeforeEffectiveDate
                        |> queryHandler.reconstitute
                        |> AsyncResult.ofResult
                    let! newEvents = aggregate.execute state command
                    do! repo.commit entityId expectedVersion newEvents
                    return newEvents
                }
            { handle = handle }

module Defaults =
    type ExtractRecordedEvent<'DomainEvent, 'DomainEventMeta> =
        RecordedEvent<'DomainEvent, 'DomainEventMeta>
         -> RecordedEvent<'DomainEvent, 'DomainEventMeta> option
    let filter 
        (extractDomainEvent:ExtractRecordedEvent<'DomainEvent, 'DomainEventMeta>)
        (extractReversedEvent:ExtractRecordedEvent<'DomainEvent, 'DomainEventMeta>) 
        : Filter<'DomainEvent, 'DomainEventMeta> =
        fun 
            (recordedEvents:RecordedEvent<'DomainEvent, 'DomainEventMeta> list)
            (recordedEvent:RecordedEvent<'DomainEvent, 'DomainEventMeta>) ->
            let (domainEvents, reversedEvents) =
                recordedEvents
                |> List.divide 
                    extractDomainEvent
                    extractReversedEvent
            let reversedEventNumbers =
                reversedEvents
                |> List.map (fun e -> e.Data.EventNumber)
            reversedEventNumbers 
            |> List.contains recordedEvent.EventNumber 
            |> not
