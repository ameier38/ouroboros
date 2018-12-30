module Dog.Api

open Dog
open Dog.Implementation
open Suave

let executeCommand dogId domainCommand =
    asyncResult {
        let! commandHandler = commandHandlerResult |> AsyncResult.ofResult
        let handle = commandHandler.handle dogId
        return! handle [ domainCommand ]
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
            |> GetRequestDto.deserialize
            |> AsyncResult.ofResult
        let dogId, asOfDate =
            dto
            |> GetRequestDto.toDomain
        let! dogStateDto =
            (dogId, asOfDate)
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
            |> CreateDogCommandRequestDto.deserialize
            |> AsyncResult.ofResult
        let! dogId, domainCommand =
            dto
            |> CreateDogCommandRequestDto.toDomain DogError.mapOuroborosError
            |> AsyncResult.ofResult
        let! events =
            (dogId, domainCommand)
            ||> executeCommand
        return!
            events
            |> Json.serializeToBytes
            |> AsyncResult.ofResult
    }

let handleCommand commandDto (body:byte []) =
    asyncResult {
        let! dto = 
            body 
            |> DogCommandRequestDto.deserialize
            |> AsyncResult.ofResult
        let! dogId, domainCommand =
            dto
            |> DogCommandRequestDto.toDomain DogError.mapOuroborosError commandDto
            |> AsyncResult.ofResult
        let! events =
            (dogId, domainCommand)
            ||> executeCommand
        return!
            events
            |> Json.serializeToBytes
            |> AsyncResult.ofResult
    }
