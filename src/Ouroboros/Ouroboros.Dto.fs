namespace Ouroboros

open System

type EventMetaDto<'DomainEventMeta> =
    { EffectiveDate: DateTime
      EffectiveOrder: int
      DomainEventMeta: 'DomainEventMeta }
module EventMetaDto =
    let fromDomain (meta:EventMeta<'DomainEventMeta>) =
        { EventMetaDto.EffectiveDate = meta.EffectiveDate |> EffectiveDate.value
          EffectiveOrder = meta.EffectiveOrder |> EffectiveOrder.value
          DomainEventMeta = meta.DomainEventMeta }
    let toDomain (dto:EventMetaDto<'DomainEventMeta>) =
        result {
            let effectiveDate = dto.EffectiveDate |> EffectiveDate
            let! effectiveOrder = dto.EffectiveOrder |> EffectiveOrder.create
            return
                { EventMeta.EffectiveDate = effectiveDate
                  EffectiveOrder = effectiveOrder
                  DomainEventMeta = dto.DomainEventMeta }
        }
    let serialize (dto:EventMetaDto<'DomainEventMeta>) = dto |> Json.serializeToBytes
    let deserialize<'DomainEventMeta> = Json.deserializeFromBytes<EventMetaDto<'DomainEventMeta>>
