module Ouroboros.EventStore

open Env
open System
open SimpleType
open EventStore.ClientAPI
open FSharp.Control

type EventStoreError = EventStoreError of string

type ReadEvents = 
    Direction
     -> StreamStart 
     -> SpecificStreamCount
     -> StreamId
     -> Async<StreamEventsSlice>

type WriteEvents = 
    Ouroboros.ExpectedVersion
     -> StreamId
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
        }

module SerializedRecordedEvent =
    let fromResolvedEvent (resolvedEvent:ResolvedEvent) = 
        result {
            let eventId = 
                resolvedEvent.Event.EventId
                |> EventId
            let! eventNumber = 
                resolvedEvent.Event.EventNumber 
                |> EventNumber.create
                |> Result.mapError EventStoreError
            let createdDate = 
                resolvedEvent.Event.Created 
                |> CreatedDate
            let! eventType = 
                resolvedEvent.Event.EventType 
                |> EventType.create
                |> Result.mapError EventStoreError
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
        | Any -> EventStore.ClientAPI.ExpectedVersion.Any |> int64
        | NoStream -> EventStore.ClientAPI.ExpectedVersion.NoStream |> int64
        | EmptyStream -> EventStore.ClientAPI.ExpectedVersion.EmptyStream |> int64
        | StreamExists -> EventStore.ClientAPI.ExpectedVersion.StreamExists |> int64
        | ExpectedVersion.Specific version -> SpecificExpectedVersion.value version

let readEvents (conn:IEventStoreConnection) : ReadEvents =
    fun direction streamStart streamCount streamId ->
        let streamId' = StreamId.value streamId
        let streamStart' = StreamStart.value streamStart
        match direction with
        | Forward ->
            conn.ReadStreamEventsForwardAsync(streamId', streamStart', streamCount', false)
        | Backward ->
            conn.ReadStreamEventsBackwardAsync(streamId', streamStart', streamCount', false)
        |> Async.AwaitTask

let writeEvents (conn:IEventStoreConnection) : WriteEvents =
    fun expectedVersion streamId eventData ->
        let expectedVersion' = ExpectedVersion.value expectedVersion
        let streamId' = StreamId.value streamId
        conn.AppendToStreamAsync(streamId', expectedVersion', eventData)
        |> Async.AwaitTask

let readStream 
    (readEvents:ReadEvents)
    : ReadStream<EventStoreError> =
    fun direction streamSlice streamId ->
        asyncResult {
            let! slice = 
                readEvents direction streamSlice streamId 
                |> AsyncResult.ofAsync
            let! events =
                slice.Events
                |> Array.map (SerializedRecordedEvent.fromResolvedEvent >> AsyncResult.ofResult)
                |> Array.toList
                |> AsyncResult.sequenceM
            return events
        }

let readStream 
    (readEvents:ReadEvents)
    : ReadEntireStream<EventStoreError> =
    fun direction streamSlice streamId ->
        let streamSlice = StreamSlice (streamStart, streamCount)
        let rec read streamStart streamCount =
            asyncResult {
                return 
                    asyncSeq {
                        match! readEvents direction streamSlice streamId with
                        | slice when slice.IsEndOfStream ->
                            yield! slice.Events |> AsyncSeq.ofSeq
                        | slice ->
                            let newStreamStart = 
                                slice.NextEventNumber 
                                |> StreamStart.create 
                                |> AsyncResult.ofResult
                            let! eventsResult = 
                                newStreamStart 
                                |> AsyncResult.bind read
                            match eventsResult with
                            | Ok newEvents -> yield! newEvents
                            | (Error e) -> failwith e
                    }
            }
        let transform recordedEvents =
            recordedEvents
            |> AsyncSeq.map (SerializedRecordedEvent.fromResolvedEvent >> AsyncResult.ofResult)
            |> AsyncSeq.toList
            |> AsyncResult.sequenceM
        read streamStart
        |> AsyncResult.mapError EventStoreError
        |> AsyncResult.bind transform

let writeStream 
    (writeEvents:WriteEvents)
    : WriteStream<EventStoreError> =
    fun expectedVersion events streamId ->
        events
        |> List.map SerializedEvent.toEventData
        |> List.toArray
        |> writeEvents streamId expectedVersion
        |> Async.Ignore
        |> AsyncResult.ofAsync
        |> AsyncResult.mapError EventStoreError

let eventStore 
    (uri:Uri) 
    : Store<EventStoreError> =
    let conn = EventStoreConnection.Create(uri)
    conn.ConnectAsync().Wait()
    let readStream' = readEvents conn |> readStream
    let readEntireStream' = readEvents conn |> readEntireStream
    let writeStream' = writeEvents conn |> writeStream
    { readStream = readStream' 
      readEntireStream = readEntireStream'
      writeStream = writeStream' }
