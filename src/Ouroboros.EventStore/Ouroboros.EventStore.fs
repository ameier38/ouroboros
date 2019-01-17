module Ouroboros.EventStore

open Env
open System
open SimpleType
open EventStore.ClientAPI
open FSharp.Control

let [<Literal>] MaxStreamCount = 500

type EventStoreExpectedVersion = EventStore.ClientAPI.ExpectedVersion

type StoreStreamStart = int64

type StoreStreamId = string

type StoreExpectedVersion = int64

type ReadLastEvent =
    StoreStreamId
     -> Async<EventReadResult>

type ReadEvents = 
    StoreStreamStart 
     -> StoreStreamId
     -> Async<StreamEventsSlice>

type WriteEvents = 
    StoreExpectedVersion
     -> StoreStreamId
     -> EventData[]
     -> Async<WriteResult>

type EventStoreConfig =
    { Uri: Uri }
module EventStoreConfig =
    let createUri protocol host port user password =
        sprintf "%s://%s:%s@%s:%i" protocol user password host port
        |> Uri

    let load () =
        result {
            let! protocol = Some "tcp" |> getEnv "EVENTSTORE_PROTOCOL"
            let! host = Some "localhost" |> getEnv "EVENTSTORE_HOST"
            let! port = Some "1113" |> getEnv "EVENTSTORE_PORT"
            let! user = Some "admin" |> getEnv "EVENTSTORE_USER"
            let! password = Some "changeit" |> getEnv "EVENTSTORE_PASSWORD"
            let port' = port |> int
            let uri = createUri protocol host port' user password
            let config =
                { Uri = uri }
            return config
        } |> Result.mapError StoreError

module SerializedRecordedEvent =
    let fromResolvedEvent (resolvedEvent:ResolvedEvent) = 
        result {
            let eventId = 
                resolvedEvent.Event.EventId
                |> EventId
            let! eventNumber = 
                resolvedEvent.Event.EventNumber 
                |> EventNumber.create
                |> Result.mapError StoreError
            let createdDate = 
                resolvedEvent.Event.Created 
                |> CreatedDate
            let! eventType = 
                resolvedEvent.Event.EventType 
                |> EventType.create
                |> Result.mapError StoreError
            return
                { SerializedRecordedEvent.Id = eventId
                  EventNumber = eventNumber
                  CreatedDate = createdDate
                  Type = eventType 
                  Data = resolvedEvent.Event.Data
                  Meta = resolvedEvent.Event.Metadata }
        }

module SerializedEvent =
    let toEventData (event:SerializedEvent) =
        let guid = Guid.NewGuid()
        let eventType = event.Type |> EventType.value
        EventData(guid, eventType, true, event.Data, event.Meta)

module ExpectedVersion =
    let value = function
        | Any -> EventStoreExpectedVersion.Any |> int64
        | NoStream -> EventStoreExpectedVersion.NoStream |> int64
        | EmptyStream -> EventStoreExpectedVersion.EmptyStream |> int64
        | StreamExists -> EventStoreExpectedVersion.StreamExists |> int64
        | ExpectedVersion.Specific version -> SpecificExpectedVersion.value version

let readLastEvent (conn:IEventStoreConnection) : ReadLastEvent =
    fun (streamId:StoreStreamId) ->
        let endOfStream = StreamPosition.End |> int64
        conn.ReadEventAsync(streamId, endOfStream, false)
        |> Async.AwaitTask

let readEvents (conn:IEventStoreConnection) : ReadEvents =
    fun (streamStart:StoreStreamStart) (streamId:StoreStreamId) ->
        conn.ReadStreamEventsForwardAsync(streamId, streamStart, MaxStreamCount, false)
        |> Async.AwaitTask

let writeEvents (conn:IEventStoreConnection) : WriteEvents =
    fun (expectedVersion:StoreExpectedVersion) (streamId:StoreStreamId) (eventData:EventData []) ->
        conn.AppendToStreamAsync(streamId, expectedVersion, eventData)
        |> Async.AwaitTask

let readLast
    (readLastEvent:ReadLastEvent)
    : ReadLast =
    fun streamId ->
        async {
            let streamId' = streamId |> StreamId.value
            try
                let! eventReadResult = readLastEvent streamId'
                return 
                    match eventReadResult.Event with
                    | resolvedEvent when resolvedEvent.HasValue -> 
                        resolvedEvent.Value
                        |> SerializedRecordedEvent.fromResolvedEvent
                        |> Result.map Some
                    | _ -> None |> Ok
            with ex ->
                return
                    sprintf "Error!:\n%A" ex
                    |> StoreError
                    |> Error
        }

let readStream 
    (readEvents:ReadEvents)
    : ReadStream =
    fun streamId ->
        let streamId' = streamId |> StreamId.value
        let rec read streamStart : Result<AsyncSeq<ResolvedEvent>,string> =
            try
                asyncSeq {
                    match! readEvents streamStart streamId' with
                    | slice when slice.IsEndOfStream ->
                        yield! slice.Events |> AsyncSeq.ofSeq
                    | slice ->
                        let newStreamStart = slice.NextEventNumber 
                        match read newStreamStart with
                        | Ok newEvents -> yield! newEvents
                        | (Error e) -> failwith e
                } |> Ok
            with ex ->
                sprintf "Error!\n%A" ex 
                |> Error
        let transform resolvedEvents =
            resolvedEvents
            |> AsyncSeq.map (SerializedRecordedEvent.fromResolvedEvent)
            |> AsyncSeq.toListAsync
            |> Async.map Result.sequence
        read 0L
        |> AsyncResult.ofResult
        |> AsyncResult.mapError StoreError
        |> AsyncResult.bind transform

let writeStream 
    (writeEvents:WriteEvents)
    : WriteStream =
    fun expectedVersion events streamId ->
        let expectedVersion' = expectedVersion |> ExpectedVersion.value
        let streamId' = streamId |> StreamId.value
        events
        |> List.map SerializedEvent.toEventData
        |> List.toArray
        |> writeEvents expectedVersion' streamId'
        |> Async.Ignore
        |> AsyncResult.ofAsync
        |> AsyncResult.mapError StoreError

let eventStore 
    (uri:Uri) 
    : Store =
    let conn = EventStoreConnection.Create(uri)
    conn.ConnectAsync().Wait()
    let readLast' = readLastEvent conn |> readLast
    let readStream' = readEvents conn |> readStream
    let writeStream' = writeEvents conn |> writeStream
    { readLast = readLast'
      readStream = readStream' 
      writeStream = writeStream' }
