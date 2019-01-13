#r "paket:
nuget Expecto
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
let outDirs = !! "src/*/out"
let ouroborosSolution = "Ouroboros.sln"

Target.create "Echo" (fun _ ->
    Trace.trace "Hi!")

Target.create "Clean" (fun _ ->
    Trace.trace "Cleaning out directories..."
    Shell.cleanDirs outDirs)

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

open Fake.Core.TargetOperators

"Echo"
 ==> "Clean"
 ==> "Install"
 ==> "Restore"
 ==> "Publish"

Target.runOrDefault "Echo"
