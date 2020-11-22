namespace SQLDumper

type SQLDumper = {
    UseGoStatements : bool
    StatementsInTransaction : int
    RowsInStatement : int
    IgnoredTables : string list
    ConnectionString : string
}

[<RequireQualifiedAccess>]
module SQLDumper =
    /// Setup SQL dump for connection string
    let init connString = {
        UseGoStatements = true
        StatementsInTransaction = 1000
        RowsInStatement = 100
        IgnoredTables = []
        ConnectionString = connString
    }

    /// Sets connection string
    let connectionString str s = { s with ConnectionString = str }
    /// Use GO statements?
    let useGoStatements flag s = { s with UseGoStatements = flag }
    /// Number of statements per transaction
    let statementsInTransaction num s = { s with StatementsInTransaction = num }
    /// Number of rows per statements
    let rowsInStatement num s = { s with RowsInStatement = num }
    /// List of tables to ignore during dump
    let ignoreTables tables s = { s with IgnoredTables = tables }
