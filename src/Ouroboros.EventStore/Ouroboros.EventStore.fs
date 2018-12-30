module Ouroboros.EventStore

open DotEnv
open System
open SimpleType
open EventStore.ClientAPI

type EventStoreConfig =
    { Uri: Uri }
module EventStoreConfig =
    let createUri protocol host port user password =
        sprintf "%s://%s:%s@%s:%i"
        <| protocol
        <| user
        <| password
        <| host
        <| port
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
            let eventId = EventId resolvedEvent.Event.EventId
            let! eventNumber = resolvedEvent.Event.EventNumber |> EventNumber.create
            let createdDate = resolvedEvent.Event.Created |> CreatedDate
            let! eventType = resolvedEvent.Event.EventType |> EventType.create
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

type EventStoreError = EventStoreError of string
module EventStoreError =
    let mapOuroborosError (OuroborosError error) = EventStoreError error

type ReadEvents = StreamId -> StreamSlice -> Async<StreamEventsSlice>

type WriteEvents = StreamId -> Ouroboros.ExpectedVersion -> EventData[] -> Async<WriteResult>

let readEvents (conn:IEventStoreConnection) : ReadEvents =
    fun streamId streamSlice ->
        let (StreamSlice (streamStart, streamCount)) = streamSlice
        let streamId' = StreamId.value streamId
        let streamStart' = StreamStart.value streamStart
        let streamCount' = StreamCount.value streamCount
        conn.ReadStreamEventsForwardAsync(streamId', streamStart', streamCount', false)
        |> Async.AwaitTask

let writeEvents (conn:IEventStoreConnection) : WriteEvents =
    fun streamId expectedVersion eventData ->
        let streamId' = StreamId.value streamId
        let expectedVersion' = ExpectedVersion.value expectedVersion
        conn.AppendToStreamAsync(streamId', expectedVersion', eventData)
        |> Async.AwaitTask

let readStream (readEvents:ReadEvents) =
    fun streamSlice streamId ->
        asyncResult {
            let! slice = readEvents streamId streamSlice |> AsyncResult.ofAsync
            let! events =
                slice.Events
                |> Array.map (SerializedRecordedEvent.fromResolvedEvent >> AsyncResult.ofResult)
                |> Array.toList
                |> AsyncResult.sequenceM
                |> AsyncResult.mapError EventStoreError.mapOuroborosError
            return events
        }

let readEntireStream (readEvents:ReadEvents) =
    fun streamStart streamId ->
        let rec read streamStart =
            asyncResult {
                let! streamCount = StreamCount.create 1000 |> AsyncResult.ofResult
                let streamSlice = StreamSlice (streamStart, streamCount)
                let! slice = readEvents streamId streamSlice |> AsyncResult.ofAsync
                return 
                    seq {
                        if slice.IsEndOfStream then
                            yield! slice.Events
                        else
                            let newStreamStart = StreamStart.create slice.NextEventNumber |> AsyncResult.ofResult
                            let eventsResult = newStreamStart |> AsyncResult.bind read |> Async.RunSynchronously
                            match eventsResult with
                            | Ok newEvents -> yield! newEvents
                            | (Error (OuroborosError e)) -> failwith e
                    }
            }
        let transform recordedEvents =
            recordedEvents
            |> Seq.map (SerializedRecordedEvent.fromResolvedEvent >> AsyncResult.ofResult)
            |> Seq.toList
            |> AsyncResult.sequenceM
        read streamStart
        |> AsyncResult.bind transform
        |> AsyncResult.mapError EventStoreError.mapOuroborosError
        

let writeStream (writeEvents:WriteEvents) =
    fun expectedVersion events streamId ->
        events
        |> List.map SerializedEvent.toEventData
        |> List.toArray
        |> writeEvents streamId expectedVersion
        |> Async.Ignore
        |> AsyncResult.ofAsync
        |> AsyncResult.mapError EventStoreError

let eventStore (uri:Uri) : Store<EventStoreError> =
    let conn = EventStoreConnection.Create(uri)
    conn.ConnectAsync().Wait()
    let readStream' = readEvents conn |> readStream
    let readEntireStream' = readEvents conn |> readEntireStream
    let writeStream' = writeEvents conn |> writeStream
    { readStream = readStream' 
      readEntireStream = readEntireStream'
      writeStream = writeStream' }
