namespace Dog

open System.Text

module internal String =
    let toBytes (s:string) = s |> Encoding.UTF8.GetBytes
    let fromBytes (bytes:byte []) = bytes |> Encoding.UTF8.GetString

    let lower (s:string) = s.ToLower()
