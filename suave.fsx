#!/usr/bin/env -S dotnet fsi
#r "nuget: Suave"
#r "nuget: CliWrap"
#r "nuget: System.Reactive, 5.0.0"


open System
open System.IO
open System.Runtime.InteropServices
open System.Reactive
open System.Reactive.Concurrency
open System.Reactive.Linq

open CliWrap

open Suave
open Suave.Redirection
open Suave.Filters
open Suave.Operators


let fableCmd () =
    let isWindows =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

    Cli
        .Wrap(if isWindows then "dotnet.exe" else "")
        .WithArguments("fable watch src/App.fsproj -o ./public")
        .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
        .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
        .WithValidation(CommandResultValidation.None)

let stdinObs =
    let readlineAsyncCb =
        Func<Threading.Tasks.Task<string>>(fun _ -> Console.In.ReadLineAsync())

    Observable
        .FromAsync<string>(readlineAsyncCb)
        .Repeat()
        .Publish()
        .RefCount()
        .SubscribeOn(Scheduler.Default)

let mutable activeFable = None

let killActiveProcess pid =
    try
        let activeProcess =
            System.Diagnostics.Process.GetProcessById pid

        activeProcess.Kill()
    with ex -> printfn $"Failed to Kill Procees with PID: [{pid}]\n{ex.Message}"

let stopFable () =
    match activeFable with
    | Some pid -> killActiveProcess pid
    | None -> printfn "No active pid found"

let startFable () =
    async {
        let task = fableCmd().ExecuteAsync()
        activeFable <- Some task.ProcessId
        return! task.Task |> Async.AwaitTask
    }

let restartFable () =
    stopFable ()
    startFable ()


let (|RestartFable|StartFable|StopFable|Unknown|) =
    function
    | "restart:fable" -> RestartFable
    | "start:fable" -> StartFable
    | "stop:fable" -> StopFable
    | value -> Unknown value

stdinObs.SubscribeSafe(
    { new IObserver<string> with
        override this.OnCompleted() : unit = printfn "Stdin completed"

        override this.OnError(error: exn) : unit =
            eprintfn $"Stdin error: {error.Message}"

        override this.OnNext(value: string) : unit =
            match value with
            | StartFable -> startFable () |> Async.Ignore |> Async.Start
            | StopFable -> stopFable ()
            | RestartFable -> restartFable () |> Async.Ignore |> Async.Start
            | Unknown value -> printfn "Unknown option [%s]" value }
)

let app =
    choose [ path "/"
             >=> GET
             >=> Files.browseFileHome "index.html"
             GET >=> Files.browseHome
             RequestErrors.NOT_FOUND "Not Found"
             >=> redirect "/" ]

let config =
    { defaultConfig with
          bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 3000 ]
          homeFolder = Some(Path.GetFullPath "./public")
          compressedFilesFolder = Some(Path.GetFullPath "./.compressed") }


startWebServer config app
