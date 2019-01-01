open Expecto

open Tests.Integration

[<EntryPoint>]
let main argv =
    runTests defaultConfig testOuroboros
