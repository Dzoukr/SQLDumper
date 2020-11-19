module SQLDumper.Tool.Program

open System
open Fake.Core

// CLI interface definition
let cli = """
usage: dotnet sqldump [options]

options:
 -c --connectionstring <string>        Connection string
 -o --output <filename>                Output file path
"""

// helper module to read easily required/optional arguments
module DocoptResult =
    let getArgumentOrDefault arg def map =
        map
        |> DocoptResult.tryGetArgument arg
        |> Option.defaultValue def

    let getArgument arg map =
        map
        |> DocoptResult.tryGetArgument arg
        |> Option.defaultWith (fun _ ->
            failwithf "Missing required argument %s.%sPlease use this CLI interface: %s" arg Environment.NewLine cli)

[<EntryPoint>]
let main argv =
    let cliArgs = Docopt(cli).Parse(argv)
    //let connectionString = cliArgs |> DocoptResult.getArgument "-c"
    //let outputFile = cliArgs |> DocoptResult.getArgument "-o"


    printfn "Hello World from F#!"
    0 // return an integer exit code
