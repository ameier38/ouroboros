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
            |> GetDogRequestDto.deserializeFromBytes
            |> AsyncResult.ofResult
        let! getRequest =
            dto
            |> GetDogRequestDto.toDomain
            |> AsyncResult.ofResult
        let! dogStateDto =
            getRequest
            ||> Projection.dogState queryHandler
        let data =
            dogStateDto
            |> DogStateDto.serializeToBytes
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
            |> CommandResponseDto.serializeToBytes
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
            |> CommandResponseDto.serializeToBytes
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
            |> CommandResponseDto.serializeToBytes
    }
