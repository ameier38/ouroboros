module Test.Ouroboros

open System
open Ouroboros
open Expecto
open Expecto.Flip

type DomainEventMeta =
    { Source: string }

type DomainEvent =
    | MovedLeft
    | MovedRight
    | MovedUp
    | MovedDown

let movedDown = MovedDown

let serializedRecordedEvents =
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
                        "DomainEventMeta": {
                            "Source": "test"
                        }
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
            } ]

let mockReadLast : ReadLast<string> =
    fun streamId ->
        serializedRecordedEvents
        |> List.last
        |> AsyncResult.ofResult

let mockReadStream : ReadStream<string> =
    fun streamId ->
        serializedRecordedEvents
        |> List.take 3
        |> Result.sequence
        |> AsyncResult.ofResult

let mockWriteStream : WriteStream<string> =
    fun (expectedVersion:ExpectedVersion) (events:SerializedEvent list) (streamId:StreamId) ->
        printfn "wrote"
        |> AsyncResult.ofSuccess

let mockStore =
    { readLast = mockReadLast
      readStream = mockReadStream
      writeStream = mockWriteStream }

let convertStoreError = OuroborosError
let convertDomainError = OuroborosError

let serialize (event:DomainEvent) = Json.serializeToBytes event
let deserialize (bytes:byte []) = Json.deserializeFromBytes<DomainEvent> bytes

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
                convertStoreError
                convertDomainError
                entityType
    }

let testRepository =
    test "test load" {
        Expect.isTrue "should be true" true                    
    }