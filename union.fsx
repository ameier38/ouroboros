#r "netstandard"
#r @"packages\Newtonsoft.Json\lib\netstandard2.0\Newtonsoft.Json.dll"
#r @"packages\Microsoft.OpenApi\lib\netstandard2.0\Microsoft.OpenApi.dll"
#r @"packages\Microsoft.OpenApi.Readers\lib\netstandard2.0\Microsoft.OpenApi.Readers.dll"
#load @"src\Ouroboros\Ouroboros.Infrastructure.fs"

open System
open System.IO
open Ouroboros
open Microsoft.OpenApi.Readers

let readOpenApi path =
    let reader = OpenApiStringReader()
    reader.Read(path)
    |> fst


type Event =
    | Born of DateTime
    | Slept
    | Played
    | Woke

let born = (DateTime(2019, 1, 1), dogDto) |> Born
let slept = Slept

for event in [born; slept] do
    let serializedEventJson = Json.serializeToJson event
    printfn "serialedEventJson:\n%A" serializedEventJson
    let serializedEvent = Json.serializeToBytes event
    printfn "serializedEvent: %A" serializedEvent
    let deserializedEvent = 
        serializedEvent
        |> Result.bind Json.deserializeFromBytes<Event>
    printfn "deserializedEvent: %A" deserializedEvent
