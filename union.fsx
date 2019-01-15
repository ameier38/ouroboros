#r "netstandard"
#r @"packages\Newtonsoft.Json\lib\netstandard2.0\Newtonsoft.Json.dll"
#r @"src\Ouroboros\out\Ouroboros.dll"

open System
open Ouroboros

type Dog =
    { Name: string
      Breed: string }

type Event =
    | Born of DateTime * Dog
    | Slept
    | Played
    | Woke

let dog =
    { Name = "Benji"
      Breed = "Maltipoo" }
let born = (DateTime(2019, 1, 1), dog) |> Born
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
