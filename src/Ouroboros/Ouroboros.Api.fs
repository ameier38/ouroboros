module Ouroboros.Api

module EventMeta =
    let create effectiveDate effectiveOrder source =
        result {
            let effectiveDate' = EffectiveDate effectiveDate
            let! effectiveOrder' = EffectiveOrder.create effectiveOrder
            let! source' = Source.create source
            return
                { EventMeta.EffectiveDate = effectiveDate'
                  EffectiveOrder = effectiveOrder'
                  Source = source' }
        }
    let serialize (meta:EventMeta) =
        meta
        |> EventMetaDto.fromDomain
        |> EventMetaDto.serialize
    let deserialize json =
        json
        |> EventMetaDto.deserialize
        |> Result.bind EventMetaDto.toDomain

module Repository =
    let create<'DomainEvent,'DomainError,'StoreError> 
        (store:Store<'StoreError>) 
        (mapError:'StoreError -> 'DomainError)
        (serializer:Serializer<'DomainEvent,'DomainError>) 
        (entityType:EntityType) =
        let createStreamId = StreamId.create entityType
        let load entityId = asyncResult {
            let streamStart  = StreamStart.zero
            let! events = 
                createStreamId entityId
                |> store.readEntireStream streamStart
                |> AsyncResult.mapError mapError
            return! 
                events 
                |> List.map serializer.deserialize 
                |> Result.sequence
                |> AsyncResult.ofResult
        }
        let commit entityId expectedVersion events = 
            asyncResult {
                let! serializedEvents =
                    events
                    |> List.map serializer.serialize 
                    |> Result.sequence
                    |> AsyncResult.ofResult
                return!
                    entityId
                    |> createStreamId
                    |> store.writeStream expectedVersion serializedEvents
                    |> AsyncResult.mapError mapError
            }
        { load = load
          commit = commit }

module Handler =
    let create<'DomainState,'DomainCommand,'DomainEvent,'DomainError> 
        (repo:Repository<'DomainEvent,'DomainError>) 
        (aggregate:Aggregate<'DomainState,'DomainCommand,'DomainEvent,'DomainError>) =
        let handle entityId command =
            asyncResult {
                let! recordedEvents = repo.load entityId
                let events =
                    recordedEvents
                    |> List.sortBy (fun re -> (re.Meta.EffectiveDate, re.Meta.EffectiveOrder))
                let state = List.fold aggregate.apply aggregate.zero events
                let! newEvents = aggregate.execute state command
                do! repo.commit entityId Any newEvents
                return newEvents
            }
        { handle = handle }
