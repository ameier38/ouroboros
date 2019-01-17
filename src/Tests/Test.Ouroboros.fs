module Test.Ouroboros

open System
open Ouroboros
open Expecto
open Expecto.Flip

type DomainEvent =
    | MovedLeft
    | MovedRight
    | MovedUp
    | MovedDown

let movedDown = MovedDown

let serializedRecordedEventsResult =
    [ for i in 1 .. 5 do
        yield
            result {
                let eventId = Guid.NewGuid() |> EventId
                let! eventNumber = i |> int64 |> EventNumber.create
                let createdDate = DateTime(2018, 1, i) |> CreatedDate
                let! eventType = "Test" |> EventType.create
                let data = 
                    """
                    {
                        "Case": "MovedLeft"
                    }
                    """
                let meta = 
                    sprintf """
                    {
                        "EffectiveDate": "2019-01-0%dT00:00:00",
                        "EffectiveOrder": 1,
                        "Source": "test"
                    }
                    """
                    <| i
                return
                    { SerializedRecordedEvent.Id = eventId
                      EventNumber = eventNumber
                      CreatedDate = createdDate
                      Type = eventType
                      Data = data |> String.toBytes
                      Meta = meta |> String.toBytes }
            }
    ] |> Result.sequence

let recordedEventsResult =
    result {
        let! serializedRecordedEvents = serializedRecordedEventsResult
        let! effectiveOrder = 1 |> EffectiveOrder.create
        let! source = "test" |> Source.create
        return
            [ for i, event in serializedRecordedEvents |> List.zip [1 .. 5] do
                let meta =
                    { EffectiveDate = DateTime(2019, 1, i) |> EffectiveDate
                      EffectiveOrder = effectiveOrder
                      Source = source }
                yield
                    { RecordedEvent.Id = event.Id
                      EventNumber = event.EventNumber
                      CreatedDate = event.CreatedDate
                      Type = event.Type
                      Data = MovedLeft
                      Meta = meta }
            ]
    }


let mockReadLast : ReadLast =
    fun streamId ->
        serializedRecordedEventsResult
        |> AsyncResult.ofResult
        |> AsyncResult.map List.last
        |> AsyncResult.mapError StoreError

let mockReadStream : ReadStream =
    fun streamId ->
        serializedRecordedEventsResult
        |> AsyncResult.ofResult
        |> AsyncResult.map (List.take 3)
        |> AsyncResult.mapError StoreError

let mockWriteStream : WriteStream =
    fun (expectedVersion:ExpectedVersion) (events:SerializedEvent list) (streamId:StreamId) ->
        printfn "wrote"
        |> AsyncResult.ofSuccess
        |> AsyncResult.mapError StoreError

let mockStore =
    { readLast = mockReadLast
      readStream = mockReadStream
      writeStream = mockWriteStream }

let serialize (event:DomainEvent) = 
    Json.serializeToBytes event
    |> Result.mapError DomainError

let deserialize (bytes:byte []) = 
    Json.deserializeFromBytes<DomainEvent> bytes
    |> Result.mapError DomainError

let mockSerializer =
    { serializeToBytes = serialize
      deserializeFromBytes = deserialize }

let mockRepo =
    result {
        let! entityType = "Test" |> EntityType.create
        return
            Repository.create
                mockStore
                mockSerializer
                entityType
    } |> Result.mapError OuroborosError

[<Tests>]
let testRepository =
    test "test load" {
        asyncResult {
            let! repo = mockRepo |> AsyncResult.ofResult
            let entityId = Guid.NewGuid() |> EntityId 
            let! actualRecordedEvents = repo.load entityId
            let! recordedEvents = 
                recordedEventsResult 
                |> Result.mapError OuroborosError 
                |> Result.map (List.take 3)
                |> AsyncResult.ofResult
            actualRecordedEvents
            |> Expect.sequenceEqual "sequences should equal" recordedEvents
        } 
        |> Async.RunSynchronously
        |> Expect.isOk "should be ok"
    }