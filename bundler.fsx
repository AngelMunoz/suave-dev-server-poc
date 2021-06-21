#!/usr/bin/env -S dotnet fsi
#r "nuget: Ply"
#r "nuget: CliWrap"
#r "nuget: AngleSharp"
#r "nuget: SharpZipLib"

open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open System.Threading.Tasks
open FSharp.Control.Tasks
open AngleSharp
open AngleSharp.Html.Parser
open ICSharpCode.SharpZipLib.Tar
open ICSharpCode.SharpZipLib.GZip
open CliWrap

let tryGetEntrypoint () =
    let context =
        BrowsingContext.New(Configuration.Default)

    let content = File.ReadAllText("./public/index.html")
    let parser = context.GetService<IHtmlParser>()
    let doc = parser.ParseDocument content

    let el =
        doc.QuerySelector("[data-entry-point][type=module]")
        |> Option.ofObj

    match el with
    | Some el ->
        let src =
            match el.Attributes
                  |> Seq.tryFind (fun item -> item.Name = "src") with
            | Some src -> src.Value
            | None -> ""

        let combined = Path.Combine("./", "public", src)
        Some(Path.GetFullPath combined)
    | None -> None


let platformString =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
        "windows"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
        "linux"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
        "darwin"
    else if RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) then
        "freebsd"
    else
        failwith "Unsupported OS"

let archString =
    match RuntimeInformation.OSArchitecture with
    | Architecture.Arm -> "arm"
    | Architecture.Arm64 -> "arm64"
    | Architecture.X64 -> "64"
    | Architecture.X86 -> "32"
    | _ -> failwith "Unsupported Architecture"

let tgzDownloadPath =
    Path.Combine("./", ".suavedevserver", "esbuild.tgz")

let esbuildExec =
    Path.Combine("./", ".suavedevserver", "package", "esbuild.exe")

let tryDownloadEsBuild () : Task<string option> =
    let binString = $"esbuild-{platformString}-{archString}"

    let url =
        $"https://registry.npmjs.org/{binString}/-/{binString}-0.12.9.tgz"

    Directory.CreateDirectory(Path.GetDirectoryName(tgzDownloadPath))
    |> ignore

    task {
        try
            use client = new HttpClient()
            printfn "Downloading esbuild from: %s" url

            use! stream = client.GetStreamAsync(url)
            use file = File.OpenWrite(tgzDownloadPath)

            do! stream.CopyToAsync file
            return Some(file.Name)
        with
        | ex ->
            eprintfn "%O" ex
            return None
    }

let decompressFile (path: Task<string option>) =
    task {
        match! path with
        | Some path ->

            use stream = new GZipInputStream(File.OpenRead path)

            use archive =
                TarArchive.CreateInputTarArchive(stream, Text.Encoding.UTF8)

            archive.ExtractContents(Path.Combine(Path.GetDirectoryName path))
            return Some path
        | None -> return None
    }

let cleanup (path: Task<string option>) =
    task {
        match! path with
        | Some path -> File.Delete(path)
        | None -> ()
    }

let setupEsbuild () =
    tryDownloadEsBuild ()
    |> decompressFile
    |> cleanup
    |> Async.AwaitTask


let cmd (entryPoint: string) =
    Cli
        .Wrap(esbuildExec)
        .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
        .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()))
        .WithArguments($"{entryPoint} --bundle --minify --target=es2015 --format=esm --outdir=./dist")

let execBuild () =
    task {
        if not <| File.Exists(esbuildExec) then
            do! setupEsbuild ()

        try
            Directory.Delete("./dist", true)
        with
        | ex -> ()

        Directory.CreateDirectory("./dist") |> ignore

        match tryGetEntrypoint () with
        | Some entrypoint ->
            let cmd = cmd(entrypoint).ExecuteAsync()
            printfn $"Starting esbuild with pid: [{cmd.ProcessId}]"

            let! result = cmd.Task
            printfn $"Finished esbuild - {result.RunTime}"
        | None -> printfn "No Entrypoint found in index.html"

        let opts = EnumerationOptions()
        opts.RecurseSubdirectories <- true

        Directory.EnumerateFiles(Path.GetFullPath("./public"), "*.*", opts)
        |> Seq.filter
            (fun file ->
                not <| file.Contains(".fable")
                && not <| file.Contains(".js"))
        |> Seq.iter (fun path -> File.Copy(path, $"./dist/{Path.GetFileName(path)}"))
    }

execBuild ()
|> Async.AwaitTask
|> Async.RunSynchronously
