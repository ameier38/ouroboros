open Suave
open Suave.Operators
open Suave.Successful
open Suave.Filters
open Dog.Api

let bytesToString (bytes:byte[]) =
    System.Text.Encoding.UTF8.GetString(bytes)

let getInputFromRequest (req: HttpRequest) =
    req.rawForm |> bytesToString

[<EntryPoint>]
let main argv = 
    let api = 
        choose 
            [ POST >=> choose
                [ path "/get" >=> createHandler handleGet
                  path "/create" >=> createHandler handleCreate ] ]
    startWebServer defaultConfig api
    0