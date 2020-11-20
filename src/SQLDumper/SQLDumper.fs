namespace SQLDumper

type SQLDump = {
    UseGoStatements : bool
    StatementsInTransaction : int
    RowsInStatement : int
    IgnoredTables : string list
    ConnectionString : string
}

[<RequireQualifiedAccess>]
module SQLDump =
    let init connString = {
        UseGoStatements = true
        StatementsInTransaction = 1000
        RowsInStatement = 100
        IgnoredTables = []
        ConnectionString = connString
    }

    let connectionString str s = { s with ConnectionString = str }
    let useGoStatements flag s = { s with UseGoStatements = flag }
    let statementsInTransaction num s = { s with StatementsInTransaction = num }
    let rowsInStatement num s = { s with RowsInStatement = num }
    let ignoreTables tables s = { s with IgnoredTables = tables }
