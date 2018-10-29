namespace Ouroboros

open Ouroboros.Constants

open System
open SimpleType

/// Type of entity (read: aggregate) (e.g., loan, series, membership).
type EntityType = private EntityType of String50
module EntityType =
    let value (EntityType entityType) = String50.value entityType
    let create entityType = String50.create entityType |> Result.map EntityType

/// Unique identifier for the entity (e.g., for a loan, the loan id).
type EntityId = EntityId of Guid

/// Unique identifier for aggregate event stream.
/// It is a combination of the entity type and entity id.
type StreamId = private StreamId of string
module StreamId =
    let value (StreamId streamId) = streamId
    let create entityType (EntityId entityId) =
        let entityType' = EntityType.value entityType
        entityType' + "-" + entityId.ToString("N").ToLower()
        |> StreamId

/// Starting event number when reading a stream
type StreamStart = private StreamStart of PositiveLong
module StreamStart =
    let value (StreamStart start) = PositiveLong.value start
    let create start = PositiveLong.create start |> Result.map StreamStart
    let zero = StreamStart PositiveLong.zero
    let max = StreamStart PositiveLong.max

/// Count of events to read
type StreamCount = private StreamCount of PositiveInt
module StreamCount =
    let value (StreamCount cnt) = PositiveInt.value cnt
    let create cnt = PositiveInt.create cnt |> Result.map StreamCount
    let one = StreamCount PositiveInt.one
    let max = StreamCount PositiveInt.max

type StreamSlice = StreamSlice of StreamStart * StreamCount

/// Expected version of the event when writing to a stream.
/// Used for optimistic concurrency check.
type ExpectedVersion =
    | Any
    | NoStream
    | EmptyStream
    | StreamExists
    | Specific of PositiveLong

/// Date at which the event was created
type CreatedDate = CreatedDate of DateTime
module CreatedDate =
    let value (CreatedDate date) = date
    let create date = CreatedDate date

/// Unique identifier for an event.
type EventId = EventId of Guid
module EventId =
    let value (EventId guid) = guid
    let create guid = EventId guid

// Number of the event in the stream
type EventNumber = private EventNumber of PositiveLong
module EventNumber =
    let value (EventNumber number) = number |> PositiveLong.value
    let create number = PositiveLong.create number |> Result.map EventNumber

/// Friendly name for the type of event (e.g., Dog Played)
type DomainEventType = private DomainEventType of string
module DomainEventType =
    let value (DomainEventType eventType) = eventType
    let create = function 
        | DeletedEventTypeValue -> sprintf "%s is a reserved EventType" DeletedEventTypeValue |> Error
        | eventType -> ConstrainedType.createString DomainEventType 50 eventType

/// Date at which event is effective in the domain
type EffectiveDate = EffectiveDate of DateTime
module EffectiveDate =
    let value (EffectiveDate date) = date
    let create date = EffectiveDate date

/// If two events occur at the exact same time, the order in which to apply them
type EffectiveOrder = private EffectiveOrder of PositiveInt
module EffectiveOrder =
    let value (EffectiveOrder order) = PositiveInt.value order
    let create order = PositiveInt.create order |> Result.map EffectiveOrder

/// Source which caused the creation of the event
type Source = private Source of String50
module Source =
    let value (Source source) = String50.value source
    let create source = String50.create source |> Result.map Source

/// Date at which to view the domain
type AsOfDate = AsOfDate of DateTime
module AsOfDate =
    let value (AsOfDate date) = date
    let create date = AsOfDate date

// Reason for why the event is deleted
type DeletionReason = private DeletionReason of String50
module DeletionReason =
    let value (DeletionReason reason) = reason |> String50.value
    let create reason = reason |> String50.create |> Result.map DeletionReason
