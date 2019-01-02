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
            (filter:Filter<'DomainEvent, 'DomainEventMeta>)
            (entityType:EntityType) =
            let createStreamId = StreamId.create entityType
            let loadAll entityId = 
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
            let load entityId =
                asyncResult {
                    let! recordedEvents = loadAll entityId
                    let filteredRecordedEvents = filter recordedEvents
                    return filteredRecordedEvents
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
              loadAll = loadAll
              commit = commit }

    module QueryHandler =
        let create
            (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainCommandMeta,'DomainEvent,'DomainEventMeta,'DomainError>)
            (repo:Repository<'DomainEvent,'DomainEventMeta,'DomainError>) =
            let apply = Result.bind2 aggregate.apply
            let zero = aggregate.zero |> Ok
            let replay
                (entityId:EntityId)
                (when:When) =
                asyncResult {
                    let! recordedDomainEvents = repo.load entityId
                    let atOrBeforeAsOf (recordedDomainEvent:RecordedDomainEvent<'DomainEvent>) =
                        match asOf with
                        | Latest -> true
                        | Specific (AsOfDate asOfDate) ->
                            let createdDate = 
                                recordedDomainEvent.CreatedDate 
                                |> CreatedDate.value
                            createdDate <= asOfDate
                    return
                        recordedDomainEvents
                        |> List.filter atOrBeforeAsOf
                }
            let reconstitute
                (domainEvents:DomainEvent<'DomainEvent> list) =
                result {
                    return! 
                        domainEvents
                        |> List.sortBy (fun e -> (e.Meta.EffectiveDate, e.Meta.EffectiveOrder))
                        |> List.map (fun e -> e.Data)
                        |> List.map Ok
                        |> List.fold apply zero
                }
            { replay = replay
              reconstitute = reconstitute }

    module CommandHandler =
        let create<'DomainState,'DomainCommand,'DomainEvent,'DomainError> 
            (mapOuroborosError:OuroborosError -> 'DomainError)
            (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError>)
            (repo:Repository<'DomainEvent,'DomainError>) =
            let queryHandler = QueryHandler.create aggregate repo
            let reconstitute
                (effectiveDate:EffectiveDate) 
                (events:DomainEvent<'DomainEvent> list) =
                events
                |> List.filter (fun e -> e.Meta.EffectiveDate <= effectiveDate)
                |> queryHandler.reconstitute
            let execute 
                eventAccumulator
                (domainCommand:DomainCommand<'DomainCommand>) =
                asyncResult {
                    let effectiveDate = domainCommand.EffectiveDate
                    let! (allDomainEvents, newDomainEvents) = eventAccumulator
                    let! state = reconstitute effectiveDate allDomainEvents |> AsyncResult.ofResult
                    let! generatedDomainEvents = aggregate.execute state domainCommand
                    let newAllDomainEvents = generatedDomainEvents @ allDomainEvents
                    let newNewDomainEvents = generatedDomainEvents @ newDomainEvents
                    return (newAllDomainEvents, newNewDomainEvents)
                }
            let handle 
                (entityId:EntityId) 
                (commands:Command<'DomainCommand> list) =
                asyncResult {
                    let! recordedDomainEvents = repo.load entityId
                    let! expectedVersion = 
                        recordedDomainEvents 
                        |> List.length 
                        |> ExpectedVersion.create
                        |> Result.mapError mapOuroborosError
                        |> AsyncResult.ofResult
                    let (domainCommands, deleteCommands) =
                        commands
                        |> List.divide 
                            Command.extractDomainCommand 
                            Command.extractDeleteCommand
                    let deletedEventNumbers =
                        deleteCommands
                        |> List.map (fun c -> c.Data.EventNumber)
                    let newDeletedEvents =
                        deleteCommands
                        |> List.map DeleteCommand.toEvent
                    let filteredDomainEvents =
                        recordedDomainEvents
                        |> List.filter (fun e ->
                            deletedEventNumbers 
                            |> List.contains e.EventNumber 
                            |> not)
                        |> List.map RecordedDomainEvent.toDomainEvent
                    let initialState =
                        (filteredDomainEvents, [])
                        |> AsyncResult.ofSuccess
                    let! newDomainEvents = 
                        domainCommands
                        |> List.fold execute initialState
                        |> AsyncResult.map (snd >> List.map DomainEvent)
                    let newEvents = newDomainEvents @ newDeletedEvents
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
        (extractDeletedEvent:ExtractRecordedEvent<'DomainEvent, 'DomainEventMeta>) 
        : Filter<'DomainEvent, 'DomainEventMeta> =
        fun recordedEvents ->
            let (domainEvents, deletedEvents) =
                recordedEvents
                |> List.divide 
                    extractDomainEvent
                    extractDeletedEvent
            let deletedEventNumbers =
                deletedEvents
                |> List.map (fun e -> e.Data.EventNumber)
            domainEvents
            |> List.filter (fun e ->
                deletedEventNumbers 
                |> List.contains e.EventNumber 
                |> not)
