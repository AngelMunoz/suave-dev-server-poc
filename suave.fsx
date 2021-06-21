#!/usr/bin/env -S dotnet fsi
#r "nuget: Suave"
#r "nuget: CliWrap"
#r "nuget: FSharp.Control.AsyncSeq"


open System
open System.IO
open System.Runtime.InteropServices
open FSharp.Control

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
        .WithArguments("fable watch src/App.fsproj -o ./public -e fs.js")
        .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
        .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
        .WithValidation(CommandResultValidation.None)

let mutable activeFable = None

let killActiveProcess pid =
    try
        let activeProcess =
            System.Diagnostics.Process.GetProcessById pid

        activeProcess.Kill()
    with
    | ex -> printfn $"Failed to Kill Procees with PID: [{pid}]\n{ex.Message}"

let stopFable () =
    match activeFable with
    | Some pid -> killActiveProcess pid
    | None -> printfn "No active Fable found"

let startFable () =
    match activeFable with
    | Some _ -> stopFable ()
    | None -> ()

    async {
        let task = fableCmd().ExecuteAsync()
        activeFable <- Some task.ProcessId
        return! task.Task |> Async.AwaitTask
    }

let restartFable () =
    stopFable ()
    startFable ()


let (|RestartFable|StartFable|StopFable|Clear|Exit|Unknown|) =
    function
    | "restart:fable" -> RestartFable
    | "start:fable" -> StartFable
    | "stop:fable" -> StopFable
    | "clear" -> Clear
    | "exit"
    | "stop" -> Exit
    | value -> Unknown value


let onStdinAsync (value: string) =
    async {
        match value with
        | StartFable ->
            async {
                printfn "Starting Fable"

                let! result = startFable ()
                printfn "Finished in: %s - %i" (result.RunTime.ToString()) result.ExitCode
            }
            |> Async.Start
        | StopFable ->
            printfn "Stoping Fable"
            stopFable ()
        | RestartFable ->
            async {
                printfn "Restarting Fable"

                let! result = restartFable ()
                printfn "Finished in: %s - %i" (result.RunTime.ToString()) result.ExitCode
            }
            |> Async.Start
        | Clear -> Console.Clear()
        | Exit ->
            printfn "Finishing the session"
            stopFable ()
            exit 0
        | Unknown value -> printfn "Unknown option [%s]" value
    }

let stdinAsyncSeq () =
    let readFromStdin () =
        Console.In.ReadLineAsync() |> Async.AwaitTask

    asyncSeq {
        while true do
            let! value = readFromStdin ()
            value
    }
    |> AsyncSeq.distinctUntilChanged
    |> AsyncSeq.iterAsync onStdinAsync

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

stdinAsyncSeq () |> Async.Start

startWebServer config app
