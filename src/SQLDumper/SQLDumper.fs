namespace SQLDumper

type SQLDump = {
    UseGoStatements : bool
    StatementsInTransaction : int
    RowsInStatement : int
    IgnoredTables : string list
}

[<RequireQualifiedAccess>]
module SQLDump =
    let init = {
        UseGoStatements = true
        StatementsInTransaction = 1000
        RowsInStatement = 100
        IgnoredTables = []
    }

    let useGoStatements flag s = { s with UseGoStatements = flag }
    let statementsInTransaction num s = { s with StatementsInTransaction = num }
    let rowsInStatement num s = { s with RowsInStatement = num }
    let ignoreTables tables s = { s with IgnoredTables = tables }
