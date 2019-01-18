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
let cleanPublish = 
    !! "src/*/out"
let cleanBuild = 
    !! "src/*/bin"
    ++ "src/*/obj"
let ouroborosSolution = "Ouroboros.sln"

Target.create "CleanPublish" (fun _ ->
    Trace.trace "Cleaning out directories..."
    Shell.cleanDirs cleanPublish)

Target.create "CleanBuild" (fun _ ->
    Trace.trace "Cleaning build directories..."
    Shell.cleanDirs cleanBuild)

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

Target.create "Publish" (fun _ ->
    Trace.trace "Publishing solution..."
    DotNet.publish 
        (fun args -> { args with OutputPath = Some "out"})
        ouroborosSolution)

Target.create "Serve" (fun _ ->
    Trace.trace "Serving test API..."
    DotNet.exec id "run" "--project src/Dog/Dog.fsproj"
    |> ignore)

Target.create "Test" (fun _ ->
    Trace.trace "Running unit tests..."
    DotNet.exec id "run" "--project src/Tests/Tests.fsproj"
    |> ignore)

open Fake.Core.TargetOperators

"CleanPublish" 
 ?=> "Install"
 ?=> "Restore"
 ?=> "Test"
 ?=> "Publish"

Target.runOrDefault "Test"
