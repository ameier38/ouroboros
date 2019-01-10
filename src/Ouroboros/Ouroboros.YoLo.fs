/// Utilities. Naming inspired by haf/YoLo
namespace Ouroboros

open System.Text
open Newtonsoft.Json

module List =
    let divide extract items =
        let rec loop (extracted, other) remainingItems =
            match remainingItems with
            | [] -> (extracted, other)
            | head::tail ->
                let accumulator =
                    match extract head with
                    | Some _ -> head::extracted, other
                    | None -> extracted, head::other
                loop accumulator tail
        loop ([], []) items

module String =
    let toBytes (s:string) = s |> Encoding.UTF8.GetBytes
    let fromBytes (bytes:byte []) = bytes |> Encoding.UTF8.GetString

module Json =
    let serializeToJson (o:obj) =
        try
            JsonConvert.SerializeObject o
            |> Ok
        with ex ->
            sprintf "could not serialize: %A\n%A" o ex
            |> Error
    let serializeToBytes (o:obj) =
        serializeToJson o
        |> Result.map String.toBytes
    let deserializeFromJson<'T> (json:string) =
        try
            json
            |> JsonConvert.DeserializeObject<'T>
            |> Ok
        with ex ->
            sprintf "could not deserialize: %A" ex
            |> Error
    let deserializeFromBytes<'T> (bytes:byte []) =
        bytes
        |> String.fromBytes
        |> deserializeFromJson<'T>
