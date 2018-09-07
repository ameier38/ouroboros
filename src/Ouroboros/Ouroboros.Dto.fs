namespace Ouroboros

open System
open Vertigo.Json

type EventMetaDto =
    { EffectiveDate: DateTime
      EffectiveOrder: int
      Source: string }
module EventMetaDto =
    let fromDomain (meta:EventMeta) =
        { EventMetaDto.EffectiveDate = meta.EffectiveDate |> EffectiveDate.value
          EffectiveOrder = meta.EffectiveOrder |> EffectiveOrder.value
          Source = meta.Source |> Source.value }
    let toDomain (dto:EventMetaDto) =
        result {
            let effectiveDate = dto.EffectiveDate |> EffectiveDate
            let! effectiveOrder = dto.EffectiveOrder |> EffectiveOrder.create
            let! source = dto.Source |> Source.create
            return
                { EventMeta.EffectiveDate = effectiveDate
                  EffectiveOrder = effectiveOrder
                  Source = source }
        }
    let serialize (dto:EventMetaDto) =
        try Json.serializeToBytes dto |> Ok
        with ex -> sprintf "could not serialize EventMetaDto: %A\n%A" ex dto |> Error
    let deserialize json =
        try Json.deserializeFromBytes json |> Ok
        with ex -> sprintf "could not deserialize EventMetaDto: %A" ex |> Error
