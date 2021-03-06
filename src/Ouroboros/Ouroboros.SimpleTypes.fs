namespace Ouroboros

open System
open SimpleType

/// Type of entity (read: aggregate)
type EntityType = private EntityType of String50
module EntityType =
    let value (EntityType entityType) = String50.value entityType
    let create entityType = 
        String50.create entityType 
        |> Result.map EntityType

/// Unique identifier for the entity.
type EntityId = EntityId of Guid
module EntityId =
    let value (EntityId guid) = guid

/// Unique identifier for aggregate event stream.
/// It is a combination of the entity type and entity id.
type StreamId = private StreamId of string
module StreamId =
    let value (StreamId streamId) = streamId
    let create entityType (EntityId entityId) =
        let entityType' = EntityType.value entityType
        entityType' + "-" + entityId.ToString("N").ToLower()
        |> StreamId

/// Specific expected version
type SpecificExpectedVersion = private SpecificExpectedVersion of PositiveLong
module SpecificExpectedVersion =
    let value (SpecificExpectedVersion version) = PositiveLong.value version
    let create version =
        PositiveLong.create version
        |> Result.map SpecificExpectedVersion

/// Expected version of the event when writing to a stream.
/// Used for optimistic concurrency check.
type ExpectedVersion =
    | Any
    | NoStream
    | EmptyStream
    | StreamExists
    | Specific of SpecificExpectedVersion

/// Date at which the event was created
type CreatedDate = CreatedDate of DateTime
module CreatedDate =
    let value (CreatedDate date) = date

/// Unique identifier for an event.
type EventId = EventId of Guid
module EventId =
    let value (EventId guid) = guid

// Number of the event in the stream
type EventNumber = private EventNumber of PositiveLong
module EventNumber =
    let value (EventNumber number) = number |> PositiveLong.value
    let create number = 
        PositiveLong.create number 
        |> Result.map EventNumber

/// Friendly name for the type of event
type EventType = private EventType of string
module EventType =
    let value (EventType eventType) = eventType
    let create eventType =
        ConstrainedType.createString EventType 50 eventType

/// Date at which event is effective in the domain
type EffectiveDate = EffectiveDate of DateTime
module EffectiveDate =
    let value (EffectiveDate date) = date

/// If two events occur at the exact same time, the order in which to apply them
type EffectiveOrder = private EffectiveOrder of PositiveInt
module EffectiveOrder =
    let value (EffectiveOrder order) = PositiveInt.value order
    let create order = 
        PositiveInt.create order 
        |> Result.map EffectiveOrder

/// Source which caused the creation of the event
type Source = private Source of String50
module Source =
    let value (Source source) = String50.value source
    let create source = 
        String50.create source 
        |> Result.map Source
