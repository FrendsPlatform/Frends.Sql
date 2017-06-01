using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Frends.Tasks.Attributes;
using Newtonsoft.Json.Linq;
using Dapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
#pragma warning disable 1573
#pragma warning disable 1591

namespace Frends.Sql
{
    public enum SqlTransactionIsolationLevel { Default, ReadCommitted, None, Serializable, ReadUncommitted, RepeatableRead, Snapshot }
    public enum SqlCommandType { Text, StoredProcedure }
    public class Parameter
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class InputBatchOperation
    {
        /// <summary>
        /// Query string for batch operation.
        /// </summary>
        [DefaultDisplayType(DisplayType.Sql)]
        [DefaultValue("insert into MyTable(ID,NAME) VALUES (@Id, @FirstName)")]
        public string Query { get; set; }

        /// <summary>
        /// Input json for batch operation. Needs to be a Json array.
        /// </summary>
        [DefaultDisplayType(DisplayType.Json)]
        [DefaultValue("[{\"Id\":15,\"FirstName\":\"Foo\"},{\"Id\":20,\"FirstName\":\"Bar\"}]")]
        public string InputJson { get; set; }

        /// <summary>
        /// Connection string
        /// </summary>
        [PasswordPropertyText]
        [DefaultValue("\"Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;\"")]
        public string ConnectionString { get; set; }
    }

    public class InputProcedure
    {
        /// <summary>
        /// Name of stored procedure to execute.
        /// </summary>
        public string Execute { get; set; }

        /// <summary>
        /// Parameters for stored procedure.
        /// </summary>
        public Parameter[] Parameters { get; set; }

        [PasswordPropertyText]
        [DefaultValue("\"Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;\"")]
        public string ConnectionString { get; set; }
    }

    public class InputQuery
    {
        /// <summary>
        /// Query text.
        /// </summary>
        [DefaultDisplayType(DisplayType.Sql)]
        public string Query { get; set; }
        /// <summary>
        /// Parameters for query.
        /// </summary>
        public Parameter[] Parameters { get; set; }

        /// <summary>
        /// Connection string
        /// </summary>
        [PasswordPropertyText]
        [DefaultValue("\"Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;\"")]
        public string ConnectionString { get; set; }
    }

    public class Options
    {
        /// <summary>
        /// Command timeout in seconds
        /// </summary>
        [DefaultValue(60)]
        public int CommandTimeoutSeconds { get; set; }

        /// <summary>
        /// Transactions specify an isolation level that defines the degree to which one transaction must be isolated from resource or data modifications made by other transactions. Default is Serializable.
        /// </summary>
        public SqlTransactionIsolationLevel SqlTransactionIsolationLevel { get; set; }
    }

    public class BulkInsertInput
    {
        /// <summary>
        /// Json Array of objects. All object property names need to match with the destination table column names.
        /// </summary>
        [DefaultDisplayType(DisplayType.Json)]
        [DefaultValue("[{\"Column1\":\"Value1\", \"Column2\":15},{\"Column1\":\"Value2\", \"Column2\":30}]")]
        public string InputData { get; set; }

        /// <summary>
        /// Destination table name.
        /// </summary>
        [DefaultValue("\"TestTable\"")]
        public string TableName { get; set; }

        /// <summary>
        /// Connection string
        /// </summary>
        [PasswordPropertyText]
        [DefaultValue("\"Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;\"")]
        public string ConnectionString { get; set; }
    }

    public class BulkInsertOptions
    {
        [DefaultValue(60)]
        public int CommandTimeoutSeconds { get; set; }
        /// <summary>
        /// When specified, cause the server to fire the insert triggers for the rows being inserted into the database.
        /// </summary>
        public bool FireTriggers { get; set; }
        /// <summary>
        /// Preserve source identity values. When not specified, identity values are assigned by the destination.
        /// </summary>
        public bool KeepIdentity { get; set; }

        /// <summary>
        /// If the input properties have empty values i.e. "", the values will be converted to null if this parameter is set to true.
        /// </summary>
        public bool ConvertEmptyPropertyValuesToNull { get; set; }
        /// <summary>
        /// Transactions specify an isolation level that defines the degree to which one transaction must be isolated from resource or data modifications made by other transactions. Default is Serializable.
        /// </summary>
        public SqlTransactionIsolationLevel SqlTransactionIsolationLevel { get; set; }
    }


    public class Sql
    {
        /// <summary>
        /// Execute a sql query. Can only return one dataset per query. https://github.com/FrendsPlatform/Frends.Sql
        /// </summary>
        /// <param name="input">Input parameters</param>
        /// <param name="options">Optional parameters with default values</param>
        /// <returns>JToken</returns>
        public static async Task<object> ExecuteQuery([CustomDisplay(DisplayOption.Tab)]InputQuery input, [CustomDisplay(DisplayOption.Tab)]Options options, CancellationToken cancellationToken)
        {
            return await GetSqlCommandResult(input.Query, input.ConnectionString, input.Parameters, options, SqlCommandType.Text, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a stored procedure. Can only return one dataset per execution. https://github.com/FrendsPlatform/Frends.Sql
        /// </summary>
        /// <param name="input">Input parameters</param>
        /// <param name="options">Optional parameters with default values</param>
        /// <returns>JToken</returns>
        public static async Task<object> ExecuteProcedure([CustomDisplay(DisplayOption.Tab)]InputProcedure input, [CustomDisplay(DisplayOption.Tab)]Options options, CancellationToken cancellationToken)
        {
            return await GetSqlCommandResult(input.Execute, input.ConnectionString, input.Parameters, options, SqlCommandType.StoredProcedure, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a query for a batch operation like insert. The query is executed with Dapper ExecuteAsync. https://github.com/FrendsPlatform/Frends.Sql
        /// </summary>
        /// <param name="input">Input parameters</param>
        /// <param name="options">Optional parameters with default values</param>
        /// <returns>Number of affected rows</returns>
        public static async Task<int> BatchOperation([CustomDisplay(DisplayOption.Tab)]InputBatchOperation input, [CustomDisplay(DisplayOption.Tab)]Options options, CancellationToken cancellationToken)
        {
            using (var sqlConnection = new SqlConnection(input.ConnectionString))
            {
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var obj = JsonConvert.DeserializeObject<ExpandoObject[]>(input.InputJson, new ExpandoObjectConverter());

                if (options.SqlTransactionIsolationLevel == SqlTransactionIsolationLevel.None)
                {
                    var result = await sqlConnection.ExecuteAsync(
                            input.Query,
                            param: obj,
                            commandTimeout: options.CommandTimeoutSeconds,
                            commandType: CommandType.Text)
                        .ConfigureAwait(false);
                    return result;
                }

                using (var tx = options.SqlTransactionIsolationLevel == SqlTransactionIsolationLevel.Default
                        ? sqlConnection.BeginTransaction()
                        : sqlConnection.BeginTransaction(options.SqlTransactionIsolationLevel.GetSqlTransactionIsolationLevel()))
                {

                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await sqlConnection.ExecuteAsync(
                            input.Query,
                            param: obj,
                            commandTimeout: options.CommandTimeoutSeconds,
                            commandType: CommandType.Text,
                            transaction: tx)
                        .ConfigureAwait(false);
                    tx.Commit();
                    return result;
                }
            }
        }

        /// <summary>
        /// Bulk insert json data to a SQL table. https://github.com/FrendsPlatform/Frends.Sql
        /// </summary>
        /// <param name="input">Input parameters</param>
        /// <param name="options">Optional parameters with default values</param>
        /// <returns>Copied row count</returns>
        public static async Task<int> BulkInsert([CustomDisplay(DisplayOption.Tab)]BulkInsertInput input, [CustomDisplay(DisplayOption.Tab)]BulkInsertOptions options, CancellationToken cancellationToken)
        {
            var inputJson = "{\"Table\" : " + input.InputData + " }";
            var dataset = JsonConvert.DeserializeObject<DataSet>(inputJson);
            using (var connection = new SqlConnection(input.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                //Get the combined flags for multiple booleans that match a flag
                var flagEnum = options.FireTriggers.GetFlag(SqlBulkCopyOptions.FireTriggers) |
                                options.KeepIdentity.GetFlag(SqlBulkCopyOptions.KeepIdentity);

                if (options.ConvertEmptyPropertyValuesToNull)
                {
                    // convert string.Empty values to null (this allows inserting data to fields which are different than text (int, ..)
                    dataset.SetEmptyDataRowsToNull();
                }

                if (options.SqlTransactionIsolationLevel == SqlTransactionIsolationLevel.None)
                {
                    using (var sqlBulkCopy = new SqlBulkCopy(connection.ConnectionString, flagEnum))
                    {
                        sqlBulkCopy.BulkCopyTimeout = options.CommandTimeoutSeconds;
                        sqlBulkCopy.DestinationTableName = input.TableName;

                        await sqlBulkCopy.WriteToServerAsync(dataset.Tables[0], cancellationToken).ConfigureAwait(false);

                        return sqlBulkCopy.RowsCopiedCount();
                    }
                }

                using (var transaction =
                    options.SqlTransactionIsolationLevel == SqlTransactionIsolationLevel.Default
                        ? connection.BeginTransaction()
                        : connection.BeginTransaction(options.SqlTransactionIsolationLevel.GetSqlTransactionIsolationLevel()))
                {


                    int rowsCopyCount;
                    using (var sqlBulkCopy = new SqlBulkCopy(connection, flagEnum, transaction))
                    {
                        sqlBulkCopy.BulkCopyTimeout = options.CommandTimeoutSeconds;
                        sqlBulkCopy.DestinationTableName = input.TableName;

                        await sqlBulkCopy.WriteToServerAsync(dataset.Tables[0], cancellationToken).ConfigureAwait(false);

                        rowsCopyCount = sqlBulkCopy.RowsCopiedCount();
                    }
                    transaction.Commit();
                    return rowsCopyCount;
                }
            }
        }

        private static async Task<JToken> GetSqlCommandResult(string query, string connectionString, IEnumerable<Parameter> parameters, Options options, SqlCommandType commandType, CancellationToken cancellationToken)
        {
            using (var sqlConnection = new SqlConnection(connectionString))
            {
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                IDictionary<string, object> parameterObject = new ExpandoObject();
                foreach (var parameter in parameters)
                {
                    parameterObject.Add(parameter.Name, parameter.Value);
                }

                if (options.SqlTransactionIsolationLevel == SqlTransactionIsolationLevel.None)
                {
                    using (var result = await sqlConnection.ExecuteReaderAsync(
                            query,
                            parameterObject,
                            commandTimeout: options.CommandTimeoutSeconds,
                            commandType: commandType.GetSqlCommandType())
                        .ConfigureAwait(false))
                    {
                        var table = new DataTable();
                        table.Load(result);
                        return JToken.FromObject(table);
                    }
                }

                using (var transaction =
                    options.SqlTransactionIsolationLevel == SqlTransactionIsolationLevel.Default
                        ? sqlConnection.BeginTransaction()
                        : sqlConnection.BeginTransaction(options.SqlTransactionIsolationLevel.GetSqlTransactionIsolationLevel()))
                {

                    using (var result = await sqlConnection.ExecuteReaderAsync(
                            query,
                            parameterObject,
                            commandTimeout: options.CommandTimeoutSeconds,
                            commandType: commandType.GetSqlCommandType(), transaction: transaction)
                        .ConfigureAwait(false))
                    {
                        var table = new DataTable();
                        table.Load(result);
                        transaction.Commit();
                        return JToken.FromObject(table);
                    }
                }
            }
        }
    }
}
