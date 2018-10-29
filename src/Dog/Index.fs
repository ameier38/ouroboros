open DotEnv
open System
open System.Text
open Dog.Handler

[<EntryPoint>]
let main argv = 
    let buffer = StringBuilder()
    
    let rec readInput (sb:StringBuilder) = 
        match Console.ReadLine() with
        | null -> sb.ToString()
        | str -> 
            sb.AppendLine(str) |> ignore
            readInput sb

    let onSuccess result =
        printfn "Success %A" result
        0
    let onError err =
        printfn "Error :(\n%A" err
        1
    
    let input = readInput buffer
    let source = "unknown" |> getEnv "Http_X_Forwarded_By"
    let method = None |> getEnv "Http_Method"
    let path = None |> getEnv "Http_Path"

    match method with
    | m when m = "POST" -> 
        handlePost path input
        |> Async.RunSyncronously
        |> Result.bimap onSuccess onError
    | _ ->
        printfn "method %s not implemented" method
        1
