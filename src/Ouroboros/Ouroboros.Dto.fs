namespace Ouroboros

open System
open System.Text
open Newtonsoft.Json

module Json =
    let serializeToBytes o =
        try
            JsonConvert.SerializeObject o
            |> Encoding.UTF8.GetBytes
            |> Ok
        with ex ->
            sprintf "could not serialize: %A\n%A" o ex
            |> OuroborosError
            |> Error
    let deserializeFromBytes<'Object> (bytes:byte[]) =
        try
            Encoding.UTF8.GetString bytes
            |> JsonConvert.DeserializeObject<'Object>
            |> Ok
        with ex ->
            sprintf "could not deserialize: %A" ex
            |> OuroborosError
            |> Error

type DomainEventMetaDto =
    { Source: string 
      EffectiveDate: DateTime
      EffectiveOrder: int }
module DomainEventMetaDto =
    let fromDomain (meta:DomainEventMeta) =
        { EffectiveDate = meta.EffectiveDate |> EffectiveDate.value
          EffectiveOrder = meta.EffectiveOrder |> EffectiveOrder.value
          Source = meta.Source |> Source.value }
    let toDomain (dto:DomainEventMetaDto) =
        result {
            let effectiveDate = dto.EffectiveDate |> EffectiveDate
            let! effectiveOrder = dto.EffectiveOrder |> EffectiveOrder.create
            let! source = dto.Source |> Source.create
            return
                { DomainEventMeta.EffectiveDate = effectiveDate
                  EffectiveOrder = effectiveOrder
                  Source = source }
        }
    let serialize (dto:DomainEventMetaDto) = dto |> Json.serializeToBytes
    let deserialize = Json.deserializeFromBytes<DomainEventMetaDto>

type DeletedEventMetaDto =
    { Source: string }
module DeletedEventMetaDto =
    let fromDomain (meta:DeletedEventMeta) =
        { Source = meta.Source |> Source.value }
    let toDomain (dto:DeletedEventMetaDto) =
        result {
            let! source = dto.Source |> Source.create
            return
                { DeletedEventMeta.Source = source }
        }
    let serialize (dto:DeletedEventMetaDto) = dto |> Json.serializeToBytes
    let deserialize = Json.deserializeFromBytes<DeletedEventMetaDto>

type DeletionDto =
    { EventNumber: int64
      Reason: string }
module DeletionDto =
    let fromDomain (deletion:Deletion) =
        { EventNumber = deletion.EventNumber |> EventNumber.value
          Reason = deletion.Reason |> DeletionReason.value }
    let toDomain (dto:DeletionDto) =
        result {
            let! eventNumber = dto.EventNumber |> EventNumber.create
            let! reason = dto.Reason |> DeletionReason.create
            return
                { Deletion.EventNumber = eventNumber
                  Reason = reason }
        }
    let serialize (dto:DeletionDto) = dto |> Json.serializeToBytes
    let deserialize = Json.deserializeFromBytes<DeletionDto>
