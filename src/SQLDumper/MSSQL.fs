module SQLDumper.MSSQL

open System
open System.Data
open System.Data.Common
open System.Data.SqlTypes
open System.Globalization
open System.IO
open System.Text
open System.Linq
open System.Threading.Tasks
open Dapper
open FSharp.Control.Tasks.V2
open Microsoft.Data.SqlClient

module internal SqlCommands =
    type GetTablesRow = { TABLE_SCHEMA : string; TABLE_NAME : string }
    let getTables =
        """
        SELECT TABLE_SCHEMA, TABLE_NAME
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_TYPE='BASE TABLE'
        """

    type GetColumnsRow = { COLUMN_NAME:string; DATA_TYPE:string; ORDINAL_POSITION:int }
    let getColumns =
        """
        SELECT c.COLUMN_NAME, c.DATA_TYPE, k.ORDINAL_POSITION
        FROM INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k ON
          c.TABLE_SCHEMA = k.TABLE_SCHEMA AND
          c.TABLE_NAME = k.TABLE_NAME AND
          c.COLUMN_NAME = k.COLUMN_NAME AND
          OBJECTPROPERTY(OBJECT_ID(k.CONSTRAINT_SCHEMA + '.' + QUOTENAME(k.CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
        WHERE c.TABLE_SCHEMA = @SchemaParam AND c.TABLE_NAME = @TableParam
        ORDER BY ISNULL(k.ORDINAL_POSITION, 30000), 1
        """

module internal Converters =

    let private formatAsN (s:string) = sprintf "N'%s'" s


    let private toStringCustom isNullCond (value:'a) convertFn =
        if isNullCond then "NULL" else value |> convertFn

    let private toStringMap isNullCond (value:'a) mapFn =
        toStringCustom isNullCond value ((fun v -> Convert.ToString(v, CultureInfo.InvariantCulture)) >> mapFn)

    let private toString isNullCond (value:'a) = toStringMap isNullCond value id

    let private genericReader (colType:string) (r:DbDataReader) (i:int) =
        match r.GetProviderSpecificValue(i) with
        | null
        | :? DBNull -> "NULL"
        | :? INullable as n when n.IsNull -> "NULL"
        | :? SqlDouble as v -> toString v.IsNull v.Value
        | :? SqlDecimal as v -> toString v.IsNull (v.ToString())
        | :? SqlSingle as v -> toString v.IsNull v.Value
        | :? SqlInt64 as v -> toString v.IsNull v.Value
        | :? SqlInt32 as v -> toString v.IsNull v.Value
        | :? SqlInt16 as v -> toString v.IsNull v.Value
        | :? SqlByte as v -> toString v.IsNull v.Value
        | :? SqlMoney as v -> toString v.IsNull v.Value
        | :? SqlBoolean as v -> toStringCustom v.IsNull v.Value (fun x -> if x then "1" else "0")
        | :? SqlString as v -> toStringMap v.IsNull v.Value formatAsN
        | :? SqlChars as v -> toStringMap v.IsNull v.Value formatAsN
        | :? SqlBinary as v -> toStringCustom v.IsNull v.Value (fun x -> "0x" + BitConverter.ToString(x).Replace("-",""))
        | :? SqlXml as v -> toStringMap v.IsNull v.Value formatAsN
        | :? SqlGuid as v -> toStringMap v.IsNull v.Value formatAsN
        | :? SqlDateTime as v when colType = "datetime" ->
            toStringCustom v.IsNull v.Value ((fun x -> x.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)) >> formatAsN)
        | :? SqlDateTime as v when colType = "smalldatetime" ->
            toStringCustom v.IsNull v.Value ((fun x -> x.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)) >> formatAsN)
        | :? DateTime as v when colType = "datetime2" ->
            toStringCustom false v ((fun x -> x.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)) >> formatAsN)
        | :? DateTime as v when colType = "date" ->
            toStringCustom false v ((fun x -> x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) >> formatAsN)
        | :? DateTimeOffset as v ->
            toStringCustom false v ((fun x -> x.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)) >> formatAsN)
        | :? TimeSpan as v ->
            toStringCustom false v ((fun x -> x.ToString("c", CultureInfo.InvariantCulture)) >> formatAsN)
        | v -> toStringMap false v formatAsN

    let tryGetConverter (t:string) =
        match t with
        | "timestamp" -> None
        | _ -> Some (genericReader t)

let private safeName (n:string) = sprintf "[%s]" n

type internal ColumnDefinition = {
    Name : string
    DataType : string
    IsSorted : bool
    Converter : DbDataReader -> int -> string
}

[<RequireQualifiedAccess>]
module internal ColumnDefinition =
    let toSelectSql (c:ColumnDefinition) =
        let name = safeName c.Name
        match c.DataType with
        | "geometry" | "geography" | "hierarchyid" -> sprintf "%s.ToString() as %s" name name
        | _ -> name

    let safeName (c:ColumnDefinition) = c.Name |> safeName

type internal TableDefinition = {
    Schema : string
    Name : string
    Columns : ColumnDefinition list
}

[<RequireQualifiedAccess>]
module internal TableDefinition =
    let safeFullName (td:TableDefinition) = (safeName td.Schema) + "." + (safeName td.Name)

let private getColumns (conn:IDbConnection) schema name =
    task {
        let! results = conn.QueryAsync<SqlCommands.GetColumnsRow>(SqlCommands.getColumns, {| SchemaParam = schema; TableParam = name |})
        return
            results
            |> Seq.choose (fun x ->
                x.DATA_TYPE
                |> Converters.tryGetConverter
                |> Option.map (fun conv -> { Name = x.COLUMN_NAME; DataType = x.DATA_TYPE; IsSorted = x.ORDINAL_POSITION = 1; Converter = conv })
            )
            |> Seq.toList
    }

let private getTables (conn:IDbConnection) =
    task {
        let! results = conn.QueryAsync<SqlCommands.GetTablesRow>(SqlCommands.getTables)
        let mutable tables = ResizeArray<_>()
        for tbl in results do
            let schema = tbl.TABLE_SCHEMA
            let name = tbl.TABLE_NAME
            let! cols = getColumns conn schema name
            tables.Add({ Name = name; Schema = schema; Columns = cols })
        return tables |> Seq.toList
    }

type internal Writer = {
    Go : unit -> Task
    EmptyLine : unit -> Task
    Line : string -> Task
    Append : string -> Task
}

module internal Writer =
    let create (writer:TextWriter) (dump:SQLDumper) =
        {
            Go = fun _ -> if dump.UseGoStatements then writer.WriteLineAsync("GO") else Task.CompletedTask
            EmptyLine = writer.WriteLineAsync
            Line = writer.WriteLineAsync
            Append = writer.WriteAsync
        }

let private dumpTable (conn:IDbConnection) (dump:SQLDumper) (w:Writer) (table:TableDefinition) =
    task {
        let tableName = table |> TableDefinition.safeFullName
        let colsSelect = table.Columns |> List.map ColumnDefinition.toSelectSql |> String.concat ", "
        let colsInsert = table.Columns |> List.map ColumnDefinition.safeName |> String.concat ", "
        let cmdText = StringBuilder()
        cmdText.Append (sprintf "SELECT %s FROM %s" colsSelect tableName) |> ignore
        if table.Columns |> List.exists (fun x -> x.IsSorted) then
            table.Columns
            |> List.filter (fun x -> x.IsSorted)
            |> List.map (fun x -> safeName x.Name)
            |> String.concat ", "
            |> (fun cols ->
                cmdText.Append (sprintf " ORDER BY %s" cols)
            )
            |> ignore

        do! w.EmptyLine()
        do! w.Line (sprintf "-- Table %s --" tableName)

        let identityFlag (v:string) =
            w.Line (sprintf "IF OBJECTPROPERTY(OBJECT_ID('%s'), 'TableHasIdentity') = 1 SET IDENTITY_INSERT %s %s;" tableName tableName v)

        let! reader = conn.ExecuteReaderAsync(cmdText.ToString())
        let mutable rows = 0
        while reader.Read() do
            if rows%dump.RowsInStatement = 0 then
                if rows = 0 then do! identityFlag "ON" else do! w.Line ";"
                if dump.StatementsInTransaction = 0 && rows > 0 then do! w.Go()
                else if rows / dump.RowsInStatement % dump.StatementsInTransaction = 0 then
                    if rows > 0 then
                        do! w.Line "COMMIT;"
                        do! w.Go()
                        do! w.EmptyLine()
                    do! w.Line "BEGIN TRANSACTION;"
                do! w.Line(sprintf "INSERT INTO %s (%s) VALUES" tableName colsInsert)
            else
                do! w.Line ", "

            do!
                table.Columns
                |> List.mapi (fun i x ->
                    (x.Name, x.DataType) |> printfn "%A"
                    x.Converter (reader :?> DbDataReader) i
                )
                |> String.concat ", "
                |> sprintf "(%s)"
                |> w.Append
            rows <- rows + 1
        reader.Close()
        if rows > 0 then
            do! w.Line ";"
            if dump.StatementsInTransaction > 0 then do! w.Line "COMMIT;"
            do! identityFlag "OFF"
            do! w.Go()
    }

/// Dump MSSQL database into TextWriter instance
let dumpToWriter (writer:TextWriter) (dump:SQLDumper) =
    let w = Writer.create writer dump
    task {
        use conn = new SqlConnection(dump.ConnectionString)
        let! tables = getTables conn
        // begin
        do! w.Line("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'")

        // process tables
        for table in (tables |> List.filter (fun x -> not <| dump.IgnoredTables.Contains(x.Name))) do
            do! table |> dumpTable conn dump w

        // end
        do! w.EmptyLine()
        do! w.Line("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'")
        do! w.Go()
    }

/// Dump MSSQL database into file
let dumpToFile (filePath:string) (dump:SQLDumper) =
    task {
        use file = File.CreateText(filePath)
        do! dump  |> dumpToWriter file
        do! file.FlushAsync()
    }