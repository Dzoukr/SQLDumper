module SQLDumper.Tool.Program

open System
open Fake.Core
open SQLDumper

// CLI interface definition
let cli =
    """
USAGE:
    sqldump (-h | --help)
    sqldump <connectionstring> <outputfile> [options]

OPTIONS [options]:
    -h --help                          Shows help
    --usego <bool>                     Use GO statements [default: True]
    --statements <number>              Number of statements in transaction [default: 1000]
    --rows <number>                    Number of rows in transaction [default: 100]
    --ignore <tables>                  Tables to ignore when doing SQL dump (use comma for more tables separation)
"""

[<RequireQualifiedAccess>]
module DocoptResult =
    let getArgument arg map =
        map
        |> DocoptResult.tryGetArgument arg
        |> Option.defaultWith (fun _ -> failwithf "Missing required argument %s." arg)

[<EntryPoint>]
let main argv =
    try
        let cliArgs = Docopt(cli).Parse(argv)
        if cliArgs |> DocoptResult.hasFlag "-h"
        then
            printfn "%s" cli
            0
        else
            let connString = cliArgs |> DocoptResult.getArgument "<connectionstring>"
            let outputFile = cliArgs |> DocoptResult.getArgument "<outputfile>"
            let useGo = cliArgs |> DocoptResult.getArgument "--usego" |> Boolean.Parse
            let statements = cliArgs |> DocoptResult.getArgument "--statements" |> int
            let rows = cliArgs |> DocoptResult.getArgument "--rows" |> int
            let ignores =
                cliArgs
                |> DocoptResult.tryGetArgument "--ignore"
                |> Option.map (fun x -> x.Split(",", StringSplitOptions.RemoveEmptyEntries))
                |> Option.map Array.toList
                |> Option.defaultValue []
            try
                SQLDumper.init connString
                |> SQLDumper.useGoStatements useGo
                |> SQLDumper.statementsInTransaction statements
                |> SQLDumper.rowsInStatement rows
                |> SQLDumper.ignoreTables ignores
                |> MSSQL.dumpToFile outputFile
                |> Async.AwaitTask
                |> Async.RunSynchronously

                Console.ForegroundColor <- ConsoleColor.Green
                printfn "Database successfully dumped into %s" outputFile
                0
            with ex ->
                Console.ForegroundColor <- ConsoleColor.Red
                eprintfn "Exception occured: %s" ex.Message
                1
    with ex ->
        Console.ForegroundColor <- ConsoleColor.Red
        printfn "Error while parsing command line, usage is:"
        printfn "%s" cli
        eprintfn "Exception occured: %s" ex.Message
        1