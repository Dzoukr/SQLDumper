module SQLDumper.MSSQL

module internal Commands =

    let getTables =
        """
        SELECT TABLE_SCHEMA, TABLE_NAME
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_TYPE='BASE TABLE'
        """

    let getColumns tableName =
        """
        SELECT c.COLUMN_NAME, c.DATA_TYPE, k.ORDINAL_POSITION
        FROM INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k ON
          c.TABLE_SCHEMA = k.TABLE_SCHEMA AND
          c.TABLE_NAME = k.TABLE_NAME AND
          c.COLUMN_NAME = k.COLUMN_NAME AND
          OBJECTPROPERTY(OBJECT_ID(k.CONSTRAINT_SCHEMA + '.' + QUOTENAME(k.CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
        WHERE c.TABLE_SCHEMA = @{SchemaParam} AND c.TABLE_NAME = @{TableParam}
        ORDER BY ISNULL(k.ORDINAL_POSITION, 30000), 1
        """