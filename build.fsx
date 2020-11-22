#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"

open System.IO
open Fake.IO
open Fake.Core
open Fake.DotNet
open Fake.IO.FileSystemOperators
open Fake.Core.TargetOperators

module Tools =
    let private findTool tool winTool =
        let tool = if Environment.isUnix then tool else winTool
        match ProcessUtils.tryFindFileOnPath tool with
        | Some t -> t
        | _ ->
            let errorMsg =
                tool + " was not found in path. " +
                "Please install it and make sure it's available from your path. "
            failwith errorMsg

    let private runTool (cmd:string) args workingDir =
        let arguments = args |> String.split ' ' |> Arguments.OfArgs
        Command.RawCommand (cmd, arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> CreateProcess.ensureExitCode
        |> Proc.run
        |> ignore

    let dotnet cmd workingDir =
        let result =
            DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
        if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let librarySrcPath = "src" </> "SQLDumper"
let toolSrcPath = "src" </> "SQLDumper.Tool"

let clean proj =
    [
        proj </> "bin"
        proj </> "obj"
    ]
    |> Shell.deleteDirs

let pack proj =
    Tools.dotnet "restore --no-cache" proj
    Tools.dotnet "pack -c Release" proj

let publish proj =
    let nugetKey =
        match Environment.environVarOrNone "NUGET_KEY" with
        | Some nugetKey -> nugetKey
        | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
    let nupkg =
        Directory.GetFiles(proj </> "bin" </> "Release")
        |> Seq.head
        |> Path.GetFullPath
    Tools.dotnet (sprintf "nuget push %s -s nuget.org -k %s" nupkg nugetKey) proj

Target.create "CleanLibrary" (fun _ -> librarySrcPath |> clean)
Target.create "PackLibrary" (fun _ -> librarySrcPath |> pack)
Target.create "PublishLibrary" (fun _ -> librarySrcPath |> publish)

"CleanLibrary" ==> "PackLibrary" ==> "PublishLibrary"

Target.create "CleanTool" (fun _ -> toolSrcPath |> clean)
Target.create "PackTool" (fun _ -> toolSrcPath |> pack)
Target.create "PublishTool" (fun _ -> toolSrcPath |> publish)

"CleanTool" ==> "PackTool" ==> "PublishTool"

Target.runOrDefaultWithArguments "CleanLibrary"