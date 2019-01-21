open Suave
open Suave.Operators
open Suave.Filters
open Dog
open Dog.Api

[<EntryPoint>]
let main argv = 
    let api = 
        choose 
            [ POST >=> choose
                [ path "/get" >=> createHandler handleGet
                  path "/reverse" >=> createHandler handleReverse
                  path "/create" >=> createHandler handleCreate
                  path "/eat" >=> createHandler (handleCommand DogCommandDto.Eat)
                  path "/sleep" >=> createHandler (handleCommand DogCommandDto.Sleep)
                  path "/wake" >=> createHandler (handleCommand DogCommandDto.Wake)
                  path "/play" >=> createHandler (handleCommand DogCommandDto.Play) ] ]
    let config =
        { defaultConfig with
            bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 8080 ] }
    startWebServer config api
    0