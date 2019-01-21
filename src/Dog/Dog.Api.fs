module Dog.Api

open Implementation
open Ouroboros
open Suave
open Suave.Successful
open Suave.ServerErrors
open Suave.Operators

let executeCommand dogId dogCommand =
    asyncResult {
        let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
        return! commandHandler.execute dogId dogCommand
    }

let JSON data =
    OK data 
    >=> Writers.setMimeType "application/json; charset=utf-8"

let createHandler (handle:byte [] -> AsyncResult<string,OuroborosError>) =
    fun (ctx:HttpContext) ->
        let { request = { rawForm = body }} = ctx
        async {
            match! handle body with
            | Ok data -> 
                return! JSON data ctx
            | Error err ->
                let errStr = err.ToString()
                return! INTERNAL_ERROR errStr ctx
        }

let handleGet (body:byte []) : AsyncResult<string,OuroborosError> =
    asyncResult {
        let! queryHandler = 
            queryHandlerResult 
            |> AsyncResult.ofResult
        let! getRequest =
            body
            |> GetDogRequestSchema.deserializeFromBytes
            |> Result.bind GetDogRequestSchema.toDomain
            |> AsyncResult.ofResult
        printfn "received get request %A" getRequest
        let! dogStateSchema =
            getRequest
            ||> Projection.dogState queryHandler
        let data =
            dogStateSchema
            |> DogStateSchema.serializeToJson
        return data
    }

let handleReverse (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> ReverseRequestSchema.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, command =
            dto
            |> ReverseRequestSchema.toDomain
            |> AsyncResult.ofResult
        let! events =
            (dogId, command)
            ||> executeCommand
        return
            events
            |> CommandResponseSchema.fromEvents
            |> CommandResponseSchema.serializeToJson
    }

let handleCreate (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> CreateDogRequestSchema.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, command =
            dto
            |> CreateDogRequestSchema.toDomain
            |> AsyncResult.ofResult
        let! events =
            (dogId, command)
            ||> executeCommand
        return
            events
            |> CommandResponseSchema.fromEvents
            |> CommandResponseSchema.serializeToJson
    }

let handleCommand (commandDto:DogCommandDto) (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> CommandRequestSchema.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, domainCommand =
            dto
            |> CommandRequestSchema.toDomain commandDto
            |> AsyncResult.ofResult
        let! events =
            (dogId, domainCommand)
            ||> executeCommand
        return
            events
            |> CommandResponseSchema.fromEvents
            |> CommandResponseSchema.serializeToJson
    }
