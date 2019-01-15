module Dog.Api

open Dog
open Dog.Implementation
open Suave
open Suave.Successful
open Suave.ServerErrors
open Suave.Operators

let executeCommand dogId dogCommand =
    asyncResult {
        let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
        return! commandHandler.handle dogId dogCommand
    }

let JSON data =
    OK data 
    >=> Writers.setMimeType "application/json; charset=utf-8"

let createHandler (handle:byte [] -> AsyncResult<string, DogError>) =
    fun (ctx:HttpContext) ->
        let { request = { rawForm = body }} = ctx
        async {
            match! handle body with
            | Ok data -> 
                return! JSON data ctx
            | Error (DogError err) ->
                return! INTERNAL_ERROR err ctx
        }

let handleGet (body:byte []) : AsyncResult<string, DogError> =
    asyncResult {
        let! queryHandler = 
            queryHandlerResult 
            |> AsyncResult.ofResult
        let! getRequest =
            body
            |> GetDogRequestDto.deserializeFromBytes
            |> Result.bind GetDogRequestDto.toDomain
            |> AsyncResult.ofResult
        printfn "received get request %A" getRequest
        let! dogStateDto =
            getRequest
            ||> Projection.dogState queryHandler
        let data =
            dogStateDto
            |> DogStateDto.serializeToJson
        return data
    }

let handleReverse (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> ReverseRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, command =
            dto
            |> ReverseRequestDto.toDomain
            |> AsyncResult.ofResult
        let! events =
            (dogId, command)
            ||> executeCommand
        return
            events
            |> CommandResponseDto.fromEvents
            |> CommandResponseDto.serializeToJson
    }

let handleCreate (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> CreateDogRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, command =
            dto
            |> CreateDogRequestDto.toDomain
            |> AsyncResult.ofResult
        let! events =
            (dogId, command)
            ||> executeCommand
        return
            events
            |> CommandResponseDto.fromEvents
            |> CommandResponseDto.serializeToJson
    }

let handleCommand (commandDto:DogCommandDto) (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> CommandRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, domainCommand =
            dto
            |> CommandRequestDto.toDomain commandDto
            |> AsyncResult.ofResult
        let! events =
            (dogId, domainCommand)
            ||> executeCommand
        return
            events
            |> CommandResponseDto.fromEvents
            |> CommandResponseDto.serializeToJson
    }
