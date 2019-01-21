#r "paket:
nuget Expecto
nuget Fake.DotNet.Fsi
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Core.Target //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open System.IO

let paketExe = Path.Combine(__SOURCE_DIRECTORY__, ".paket", "paket.exe")
let ouroborosProject = Path.Combine(__SOURCE_DIRECTORY__, "src", "Ouroboros", "Ouroboros.fsproj")

let publishDir = 
    !! "src/*/out"
let buildDir = 
    !! "src/*/bin"
    ++ "src/*/obj"
let ouroborosSolution = "Ouroboros.sln"

Target.create "CleanPublish" (fun _ ->
    Trace.trace "Cleaning out directories..."
    Shell.cleanDirs publishDir)

Target.create "CleanBuild" (fun _ ->
    Trace.trace "Cleaning build directories..."
    Shell.cleanDirs buildDir)

Target.create "Install" (fun _ ->
    Trace.trace "Installing dependencies..."
    Command.RawCommand(paketExe, Arguments.OfArgs ["install"])
    |> CreateProcess.fromCommand
    |> CreateProcess.withFramework
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore)

Target.create "Restore" (fun _ ->
    Trace.trace "Restoring solution..."
    DotNet.restore id ouroborosSolution)

Target.create "Build" (fun _ ->
    Trace.trace "Building solution..."
    DotNet.build id ouroborosSolution)

Target.create "Publish" (fun _ ->
    Trace.trace "Publishing solution..."
    DotNet.publish 
        (fun args -> { args with OutputPath = Some "out"})
        ouroborosSolution)

Target.create "Serve" (fun _ ->
    Trace.trace "Serving test API..."
    match DotNet.exec id "run" "--project src/Dog/Dog.fsproj" with
    | {ExitCode = 0} -> printfn "Success!"
    | errorResult ->
        errorResult.Errors
        |> String.concat "\n"
        |> failwith)

Target.create "Test" (fun _ ->
    Trace.trace "Running unit tests..."
    DotNet.exec id "run" "--project src/Tests/Tests.fsproj"
    |> ignore)

Target.create "Smoke" (fun _ ->
    Trace.trace "Running smoke tests..."
    let (exitCode, messages) = 
        Fsi.exec 
            (fun p -> { p with TargetProfile = Fsi.Profile.NetStandard } ) 
            "smoke.fsx" 
            []
    match exitCode with
    | 0 -> 
        messages
        |> List.iter Trace.trace
    | _ -> 
        messages
        |> List.iter Trace.traceError
        failwith "Error!")

Target.create "Pack" (fun _ ->
    Trace.trace "Creating nuget package..."
    
    DotNet.pack (fun p ->
        { p with
            Configuration = DotNet.BuildConfiguration.Release})
        ouroborosProject)

Target.create "Push" (fun _ ->
    Trace.trace "Pushing nuget package..."
    // FIXME: use DotNet.push https://github.com/fsharp/FAKE/pull/2229
    let accessKey = Environment.environVar "NUGET_API_KEY"
    let packagePath = 
        !! "src/Ouroboros/bin/Release/*.nupkg"
        |> Seq.toList
        |> List.head
    Trace.tracefn "found nuget package %s" packagePath
    let keyArg = sprintf "-k %s" accessKey
    let sourceArg = "-s https://api.nuget.org/v3/index.json"
    let args = sprintf "%s %s %s" packagePath keyArg sourceArg
    match DotNet.exec id "nuget push" args with
    | { ExitCode = 0 } ->
        printfn "Success!"
    | errorResult ->
        errorResult.Errors
        |> String.concat "\n"
        |> failwith) 

open Fake.Core.TargetOperators

"Install" 
 ==> "Smoke"

"Install"
 ==> "Pack"
 ==> "Push"

Target.runOrDefault "Test"
