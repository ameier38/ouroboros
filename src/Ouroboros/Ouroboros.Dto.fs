namespace Ouroboros

open System

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
            let effectiveDate = 
                dto.EffectiveDate 
                |> EffectiveDate
            let! effectiveOrder = 
                dto.EffectiveOrder 
                |> EffectiveOrder.create
                |> Result.mapError OuroborosError
            let! source =
                dto.Source
                |> Source.create
                |> Result.mapError OuroborosError
            return
                { EventMeta.EffectiveDate = effectiveDate
                  EffectiveOrder = effectiveOrder
                  Source = source }
        }
    let serializeToBytes (dto:EventMetaDto) =
        dto
        |> Json.serializeToBytes
        |> Result.mapError OuroborosError

    let deserializeFromBytes (bytes:byte []) =
        bytes
        |> Json.deserializeFromBytes<EventMetaDto>
        |> Result.mapError OuroborosError
