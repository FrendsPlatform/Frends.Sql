using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Microsoft.Extensions.Configuration;
namespace Frends.Sql.Tests
{
    public class TestFixture : IDisposable
    {
        public readonly string ConnectionString;
        public readonly string TableName = "Test";
        public readonly string DatabaseName = "FrendsSqlTest";
        public readonly string ProcedureName = "TestProcedure";

        public TestFixture()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").Build();

            ConnectionString = configuration["ConnectionStrings:TestDbConnectionString"];

            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                var createDatabaseCommand = connection.CreateCommand();
                createDatabaseCommand.CommandText = $@"USE master
                                        IF EXISTS(select * from sys.databases where name = '{DatabaseName}')
                                        DROP DATABASE {DatabaseName} 
                                        CREATE DATABASE {DatabaseName}";
                createDatabaseCommand.ExecuteNonQuery();

                var createTable = connection.CreateCommand();
                createTable.CommandText = $@"USE {DatabaseName}
                                            CREATE TABLE {TableName}
                                            (
                                                Id int,
                                                LastName varchar(255),
                                                FirstName varchar(255),
                                            );";
                createTable.ExecuteNonQuery();
            }

            ConnectionString = ConnectionString + $"Database={DatabaseName};";

            InsertStoredProcedure(ProcedureName);
        }

        public void Dispose()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                SqlConnection.ClearAllPools();
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $@"USE master
                                        IF EXISTS(select * from sys.databases where name = '{DatabaseName}')
                                        DROP DATABASE {DatabaseName}";
                command.ExecuteNonQuery();
            }
        }

        private void InsertStoredProcedure(string procedureName)
        {
            string procedure = $@"CREATE PROCEDURE [{procedureName}]
                @FirstName varchar(255),@LastName varchar(255) as BEGIN
                SET NOCOUNT ON; 
                INSERT INTO {TableName} (FirstName, LastName, Id) VALUES (@FirstName,@LastName,1) 
                END";
            using (var connection = new SqlConnection(ConnectionString))
            {
                using (var cmd = new SqlCommand(procedure, connection))
                {
                    connection.Open();
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                    connection.Close();
                }
            }
        }
    }

    public class Tests : IClassFixture<TestFixture>
    {
        private readonly TestFixture _fixture;

        public Tests(TestFixture fixture)
        {
            _fixture = fixture;

            using (var connection = new SqlConnection(_fixture.ConnectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"TRUNCATE TABLE {_fixture.TableName}";
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        [Theory]
        [InlineData(false, false, 0)]
        [InlineData(true, false, 16)]
        [InlineData(false, true, 1)]
        [InlineData(true, true, 17)]
        public void GetCombinedFlags(bool fireTriggers, bool keepIdentity, int expectedResult)
        {
            var flagEnum = fireTriggers.GetFlag(SqlBulkCopyOptions.FireTriggers) |
                           keepIdentity.GetFlag(SqlBulkCopyOptions.KeepIdentity);
            Assert.Equal(expectedResult, (int) flagEnum);
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestQueryWithParameters(bool useTransaction)
        {
            var dataToQuery = new List<TestRow>()
            {
                new TestRow() {FirstName = "Etu", Id = 1, LastName = "Suku"},
                new TestRow() {FirstName = "First", Id = 2, LastName = "Last"},
                new TestRow() {FirstName = "Some", Id = 3, LastName = "Name"},
            };
            await AddDataToTable(dataToQuery);

            var result = (JArray)
                await
                    Sql.ExecuteQuery(
                        new InputQuery()
                        {
                            ConnectionString = _fixture.ConnectionString,
                            Query = $"select * FROM {_fixture.TableName} where LastName = @name",
                            Parameters = new[] {new Parameter() {Name = "Name", Value = "Last"}}
                        },
                        new Options()
                        {
                            CommandTimeoutSeconds = 60,
                            SqlTransactionIsolationLevel = useTransaction
                                ? SqlTransactionIsolationLevel.Default
                                : SqlTransactionIsolationLevel.None
                        }, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(2, result.First()["Id"].Value<int>());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestSimpleInsertQueryWithParameters(bool useTransaction)
        {
            var result = (JArray)
                await
                    Sql.ExecuteQuery(
                        new InputQuery()
                        {
                            ConnectionString = _fixture.ConnectionString,
                            Query = $"INSERT INTO {_fixture.TableName} VALUES(@Id, @LastName, @FirstName); ",
                            Parameters = new[]
                            {
                                new Parameter() {Name = "LastName", Value = "Last"},
                                new Parameter() {Name = "FirstName", Value = "First"},
                                new Parameter() {Name = "Id", Value = "15"}
                            }
                        },
                        new Options()
                        {
                            CommandTimeoutSeconds = 60,
                            SqlTransactionIsolationLevel = useTransaction
                                ? SqlTransactionIsolationLevel.Default
                                : SqlTransactionIsolationLevel.None
                        }, CancellationToken.None);

            Assert.Empty(result);
            var query = await GetAllResults();

            Assert.Single(query);
            Assert.Equal(15, query[0]["Id"].Value<int>());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestBatchOperationInsert(bool useTransaction)
        {
            var result =
                await
                    Sql.BatchOperation(
                        new InputBatchOperation()
                        {
                            ConnectionString = _fixture.ConnectionString,
                            Query = $"insert {_fixture.TableName}(Id,FirstName,LastName) VALUES(1,@FirstName,'LastName')",
                            InputJson = "[{\"FirstName\":\"Onunous\"},{\"FirstName\":\"Doosshits\"}]",
                        },
                        new Options()
                        {
                            CommandTimeoutSeconds = 60,
                            SqlTransactionIsolationLevel = useTransaction
                                ? SqlTransactionIsolationLevel.Serializable
                                : SqlTransactionIsolationLevel.None
                        }, CancellationToken.None);
            Assert.Equal(2, result);
            var query = await GetAllResults();

            Assert.Equal(2, query.Count());
            Assert.Equal("Onunous", query[0]["FirstName"].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestStoredProcedureThatInsertsRow(bool useTransaction)
        {
            var result = (JArray)
                await
                    Sql.ExecuteProcedure(
                        new InputProcedure()
                        {
                            ConnectionString = _fixture.ConnectionString,
                            Execute = _fixture.ProcedureName,
                            Parameters = new[]
                            {
                                new Parameter() {Name = "FirstName", Value = "First"},
                                new Parameter() {Name = "LastName", Value = "Last"}
                            }
                        },
                        new Options()
                        {
                            CommandTimeoutSeconds = 60,
                            SqlTransactionIsolationLevel = useTransaction
                                ? SqlTransactionIsolationLevel.ReadCommitted
                                : SqlTransactionIsolationLevel.None
                        }, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Empty(result);

            var query = await GetAllResults();

            Assert.Single(query);
            Assert.Equal("First", query[0]["FirstName"].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestBulkInsert(bool useTransaction)
        {
            var dataToInsert = new List<TestRow>()
            {
                new TestRow() {FirstName = "Etu", Id = 1, LastName = "Suku"},
                new TestRow() {FirstName = "First", Id = 2, LastName = "Last"},
                new TestRow() {FirstName = "Eka", Id = 3, LastName = "Name"}
            };
            var inputJson = JsonConvert.SerializeObject(dataToInsert);

            var result =
                await
                    Sql.BulkInsert(
                        new BulkInsertInput()
                        {
                            ConnectionString = _fixture.ConnectionString,
                            TableName = _fixture.TableName,
                            InputData = inputJson
                        },
                        new BulkInsertOptions()
                        {
                            CommandTimeoutSeconds = 60, FireTriggers = true, KeepIdentity = false,
                            SqlTransactionIsolationLevel = useTransaction
                                ? SqlTransactionIsolationLevel.ReadCommitted
                                : SqlTransactionIsolationLevel.None
                        }, CancellationToken.None);
            Assert.Equal(3, result);

            var query = await GetAllResults();
            Assert.Equal(3, query.Count());
            Assert.Equal("Suku", query[0]["LastName"].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BulkInsertWithEmptyPropertyValuesShouldBeNull(bool useTransaction)
        {
            var dataToInsert = new List<TestRow>()
            {
                new TestRow() {FirstName = "Etu", Id = 1, LastName = "Suku"},
                new TestRow() {FirstName = "First", Id = 2, LastName = "Last"},
                new TestRow() {FirstName = "", Id = 3, LastName = "Name"}
            };
            var inputJson = JsonConvert.SerializeObject(dataToInsert);

            var result =
                await
                    Sql.BulkInsert(
                        new BulkInsertInput()
                        {
                            ConnectionString = _fixture.ConnectionString,
                            TableName = _fixture.TableName,
                            InputData = inputJson
                        },
                        new BulkInsertOptions()
                        {
                            CommandTimeoutSeconds = 60, FireTriggers = true, KeepIdentity = false,
                            ConvertEmptyPropertyValuesToNull = true,
                            SqlTransactionIsolationLevel = useTransaction
                                ? SqlTransactionIsolationLevel.Default
                                : SqlTransactionIsolationLevel.None
                        }, CancellationToken.None);
            Assert.Equal(3, result);

            var query = await GetAllResults();
            Assert.Equal(3, query.Count());
            Assert.Null(query[2]["FirstName"].Value<DBNull>());
        }


        private async Task AddDataToTable(IEnumerable<TestRow> tastTableData)
        {
            await
                Sql.BatchOperation(
                    new InputBatchOperation()
                    {
                        ConnectionString = _fixture.ConnectionString,
                        Query = $"insert {_fixture.TableName}(Id,FirstName,LastName) VALUES(@Id,@FirstName,@LastName)",
                        InputJson = JsonConvert.SerializeObject(tastTableData),
                    },
                    new Options()
                    {
                        CommandTimeoutSeconds = 60,
                        SqlTransactionIsolationLevel = SqlTransactionIsolationLevel.Serializable
                    },
                    CancellationToken.None);
        }

        private async Task<JToken> GetAllResults()
        {
            return (JArray) await
                Sql.ExecuteQuery(
                    new InputQuery()
                    {
                        ConnectionString = _fixture.ConnectionString,
                        Query = $"select * FROM {_fixture.TableName}",
                        Parameters = new Parameter[0]
                    }, new Options() {CommandTimeoutSeconds = 60}, CancellationToken.None);
        }
    }

    public class TestRow
    {
        public int Id { get; set; }
        public string LastName { get; set; }

        public string FirstName { get; set; }
    }


}
