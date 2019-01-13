module Dog.Api

open Dog
open Dog.Implementation
open Ouroboros
open Suave

let executeCommand dogId dogCommand =
    asyncResult {
        let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
        return! commandHandler.handle dogId dogCommand
    }

let createHandler (handle:byte [] -> AsyncResult<byte [], DogError>) : WebPart =
    fun (ctx:HttpContext) ->
        let { request = { rawForm = body }} = ctx
        asyncResult {
            let! data = handle body
            return { ctx with response = { ctx.response with status = HTTP_200.status; content = Bytes data }}
        }
        |> Async.map Result.toOption

let handleGet (body:byte []) : AsyncResult<byte [], DogError> =
    asyncResult {
        let! queryHandler = 
            queryHandlerResult 
            |> AsyncResult.ofResult
        let! dto =
            body
            |> GetRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! getRequest =
            dto
            |> GetRequestDto.toDomain
            |> AsyncResult.ofResult
        let! dogStateDto =
            getRequest
            ||> Projection.dogState queryHandler
        let! data =
            dogStateDto
            |> DogStateDto.serializeToBytes
            |> AsyncResult.ofResult
        return data
    }

let handleCreate (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> CreateDogCommandRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, command =
            dto
            |> CreateDogCommandRequestDto.toDomain
            |> AsyncResult.ofResult
        let! events =
            (dogId, command)
            ||> executeCommand
        return!
            events
            |> Json.serializeToBytes
            |> Result.mapError DogError
            |> AsyncResult.ofResult
    }

let handleCommand (commandDto:DogCommandDto) (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> DogCommandRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! dogId, domainCommand =
            dto
            |> DogCommandRequestDto.toDomain commandDto
            |> AsyncResult.ofResult
        let! events =
            (dogId, domainCommand)
            ||> executeCommand
        return!
            events
            |> Json.serializeToBytes
            |> Result.mapError DogError
            |> AsyncResult.ofResult
    }
