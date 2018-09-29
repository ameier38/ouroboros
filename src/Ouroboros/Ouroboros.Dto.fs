namespace Ouroboros

open System
open Vertigo.Json

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
    let serialize (dto:DomainEventMetaDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DomainEventMetaDto: %A\n%A" ex dto |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DomainEventMetaDto> json |> Ok
        with ex -> sprintf "could not deserialize DomainEventMetaDto: %A" ex |> Error

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
    let serialize (dto:DeletedEventMetaDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DeletedEventMetaDto: %A\n%A" ex dto |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DeletedEventMetaDto> json |> Ok
        with ex -> sprintf "could not deserialize DeletedEventMetaDto: %A" ex |> Error

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
    let serialize (dto:DeletionDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize DeletionDto: %A\n%A" ex dto |> Error
    let deserialize json =
        try Json.deserializeFromBytes<DeletionDto> json |> Ok
        with ex -> sprintf "could not deserialize DeletionDto: %A" ex |> Error
