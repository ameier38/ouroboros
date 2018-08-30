module Test.Config

open DotEnv
open System

type EventStoreConfig =
    { Host: string
      Port: int
      User: string
      Password: string
      Uri: Uri }
module EventStoreConfig =
    let createUri host port user password =
        sprintf "tcp://%s:%s@%s:%i"
        <| user
        <| password
        <| host
        <| port
        |> Uri

    let load () =
        result {
            let! host = Some "localhost" |> getEnv "EVENTSTORE_HOST"
            let! port = Some "1113" |> getEnv "EVENTSTORE_PORT"
            let! user = Some "admin" |> getEnv "EVENTSTORE_USER"
            let! password = Some "changeit" |> getEnv "EVENTSTORE_PASSWORD"
            let port' = port |> int
            let uri = createUri host port' user password
            return
                { Host = host 
                  Port = port'
                  User = user 
                  Password = password
                  Uri = uri }
        }