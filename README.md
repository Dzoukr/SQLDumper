# SQLDumper ![GitHub](https://img.shields.io/github/license/Dzoukr/SQLDumper?style=flat-square) ![Nuget](https://img.shields.io/nuget/v/SQLDumper?label=SQLDumper&style=flat-square) ![Nuget](https://img.shields.io/nuget/v/SQLDumper.Tool?label=SQLDumper.Tool&style=flat-square)

Dump your MSSQL library into file or stream using F# library or .NET CLI tool.

## Using F# library

### Installation

If you want to install this package manually, use usual NuGet package command

    Install-Package SQLDumper

or using [Paket](http://fsprojects.github.io/Paket/getting-started.html)

    paket add SQLDumper

### Usage

Library is rather simple:

```f#
open SQLDumper

SQLDumper.init "myconnectionstring"
|> SQLDumper.useGoStatements true // default
|> SQLDumper.statementsInTransaction 1000 // default
|> SQLDumper.rowsInStatement 100 // default
|> SQLDumper.ignoreTables ["ignore1";"ignore2"] // empty by default
|> MSSQL.dumpToFile "path/to/file.sql"
```

If you prefer using `TextWriter` from BCL, you can use `MSSQL.dumpToWriter` function.

## Using .NET CLI tool

### Installation

To install .NET tool use this command

    dotnet tool install SQLDumper.Tool

### Usage

Again, this CLI tool is rather simple:

```
USAGE:
    sqldump (-h | --help)
    sqldump <connectionstring> <outputfile> [options]

OPTIONS [options]:
    -h --help                          Shows help
    --usego <bool>                     Use GO statements [default: True]
    --statements <number>              Number of statements in transaction [default: 1000]
    --rows <number>                    Number of rows in transaction [default: 100]
    --ignore <tables>                  Tables to ignore when doing SQL dump (use comma for more tables separation)
```

## Kudos 👏

Thanks to [@ArtemAvramenko](https://github.com/ArtemAvramenko) for his [SqlDump](https://github.com/ArtemAvramenko/SqlDump) C# library used as reference project for this library.