# BBH.CP.Atlas.Data / Platform

Copy this folder to the target project `BBH.CP.Atlas.Data` (same path).

| File | Purpose | Spec |
|---|---|---|
| `AtlasTable.cs` | Table and column metadata (`AtlasTable`, `AtlasColumn`) | 05, 6.2 |
| `AtlasSqlTypes.cs` | SQL type to CLR type and EF Core store type | 05, 6.2, 6.4 |
| `AtlasColumnMetadataReader.cs` | Reads the columns from `sys.columns` at start-up | 05, 6.2, 6.8 |
| `AtlasTableRegistry.cs` | The registered tables (singleton) | 05, 6.1 |
| `AtlasPlatformContext.cs` | Shared-type entities, no C# class for each table | 05, 6.2; 12, question 1 |
| `AtlasValueConverter.cs` | Wire format to column value and back, invariant culture | 05, 6.4 |
| `AtlasDataExtensions.cs` | DI registration, shared transaction with `CPSharepointContext`, new record, load by key | 05, 6.5 |

Notes:

- The platform context uses the connection of the scoped `CPSharepointContext` (`AddAtlasPlatformData`).
- A non-nullable value column has the sentinel = type default (EF Core property bags use null otherwise; a new
  record would then send `ID = 0` to an identity column).
- Supported column types: int, bigint, smallint, tinyint, bit, decimal, numeric, money, smallmoney, date, datetime,
  datetime2, smalldatetime, char, varchar, nchar, nvarchar, uniqueidentifier, rowversion. Other types stop the model
  build with a clear message (the start-up check, C8, reports them first).
