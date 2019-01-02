namespace Ouroboros

open System.Text
open Newtonsoft.Json

module List =
    let divide extractA extractB items =
        let listA = items |> List.choose extractA
        let listB = items |> List.choose extractB
        (listA, listB)

module Json =
    let serializeToBytes (o:obj) =
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
