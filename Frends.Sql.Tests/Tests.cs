using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace Frends.Sql.Tests
{
    [TestFixture]
    public class Tests
    {

        private string _connectionString;
        private const string TableName = "Test";
        private const string DatabaseName = "FrendsSqlTest";
        private const string procedureName = "TestProcedure";

        [OneTimeSetUp]
        public void SetupDatabase()
        {
            _connectionString = ConfigurationManager.AppSettings["TestDbConnectionString"];
            using (var connection = new SqlConnection(_connectionString))
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
          
            _connectionString = _connectionString + $"Database={DatabaseName};";

            InsertStoredProcedure(procedureName);
        }

        [OneTimeTearDown]
        public void RemoveDatabase()
        {
            using (var connection = new SqlConnection(_connectionString))
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

        [SetUp]
        public void Setup()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"TRUNCATE TABLE {TableName}";
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        [TestCase(false,false, ExpectedResult = 0)]
        [TestCase(true, false, ExpectedResult = 16)]
        [TestCase(false, true, ExpectedResult = 1)]
        [TestCase(true, true, ExpectedResult = 17)]
        public int GetCombinedFlags(bool fireTriggers, bool keepIdentity)
        {
            var flagEnum = fireTriggers.GetFlag(SqlBulkCopyOptions.FireTriggers) |
                                 keepIdentity.GetFlag(SqlBulkCopyOptions.KeepIdentity);
            return (int) flagEnum;
        }


        [TestCase(true)]
        [TestCase(false)]
        public async Task TestQueryWithParameters(bool useTransaction)
        {
            var dataToQuery = new List<TestRow>()
            {
                new TestRow() {FirstName = "Etu", Id = 1, LastName = "Suku"},
                new TestRow() {FirstName = "First", Id = 2, LastName = "Last"},
                new TestRow() {FirstName = "Some", Id = 3, LastName = "Name"},
            };
            await AddDataToTable(dataToQuery);

            var result =
                await
                    Sql.ExecuteQuery(
                        new InputQuery()
                        {
                            ConnectionString = _connectionString,
                            Query = $"select * FROM {TableName} where LastName = @name",
                            Parameters = new[] {new Parameter() {Name = "Name", Value = "Last" } }
                        }, new Options() {CommandTimeoutSeconds = 60, SqlTransactionIsolationLevel = useTransaction ? SqlTransactionIsolationLevel.Default : SqlTransactionIsolationLevel.None}, CancellationToken.None);

              Assert.That(result.Count(), Is.EqualTo(1));
              Assert.That(result.First()["Id"].Value<int>(), Is.EqualTo(2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestSimpleInsertQueryWithParameters(bool useTransaction)
        {
            var result =
                await
                    Sql.ExecuteQuery(
                        new InputQuery()
                        {
                            ConnectionString = _connectionString,
                            Query = $"INSERT INTO {TableName} VALUES(@Id, @LastName, @FirstName); ",
                            Parameters = new[] { new Parameter() { Name = "LastName", Value = "Last" }, new Parameter() { Name = "FirstName", Value = "First" }, new Parameter() { Name = "Id", Value = "15" } }
                        }, new Options() { CommandTimeoutSeconds = 60, SqlTransactionIsolationLevel = useTransaction ? SqlTransactionIsolationLevel.Default : SqlTransactionIsolationLevel.None }, CancellationToken.None);

            Assert.That(result.Count(), Is.EqualTo(0));
            var query = await GetAllResults();

            Assert.That(query.Count(), Is.EqualTo(1));
            Assert.That(query[0]["Id"].Value<int>, Is.EqualTo(15));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestBatchOperationInsert(bool useTransaction)
        {
            var result =
               await
                   Sql.BatchOperation(
                       new InputBatchOperation()
                       {
                           ConnectionString = _connectionString,
                           Query = $"insert {TableName}(Id,FirstName,LastName) VALUES(1,@FirstName,'LastName')",
                           InputJson = "[{\"FirstName\":\"Onunous\"},{\"FirstName\":\"Doosshits\"}]",
                       }, new Options() { CommandTimeoutSeconds = 60, SqlTransactionIsolationLevel = useTransaction ? SqlTransactionIsolationLevel.Serializable : SqlTransactionIsolationLevel.None }, CancellationToken.None);
            Assert.That(result,Is.EqualTo(2));
            var query = await GetAllResults();

            Assert.That(query.Count(), Is.EqualTo(2));
            Assert.That(query[0]["FirstName"].ToString(), Is.EqualTo("Onunous"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TestStoredProcedureThatInsertsRow(bool useTransaction)
        {

            var result =
               await
                   Sql.ExecuteProcedure(
                       new InputProcedure()
                       {
                           ConnectionString = _connectionString,
                           Execute = procedureName,
                           Parameters = new[] { new Parameter() { Name = "FirstName", Value = "First" }, new Parameter() { Name = "LastName", Value = "Last" } }
                       }, new Options() { CommandTimeoutSeconds = 60, SqlTransactionIsolationLevel = useTransaction ? SqlTransactionIsolationLevel.ReadCommitted : SqlTransactionIsolationLevel.None }, CancellationToken.None);
            Assert.That(result,Is.Not.Null);
            Assert.That(result.Count(), Is.EqualTo(0));

            var query = await GetAllResults();

            Assert.That(query.Count(), Is.EqualTo(1));
            Assert.That(query[0]["FirstName"].ToString(), Is.EqualTo("First"));
        }

        [TestCase(true)]
        [TestCase(false)]
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
                           ConnectionString = _connectionString,
                           TableName = TableName,
                           InputData = inputJson
                       }, new BulkInsertOptions() { CommandTimeoutSeconds = 60,FireTriggers = true, KeepIdentity = false, SqlTransactionIsolationLevel = useTransaction ? SqlTransactionIsolationLevel.ReadCommitted : SqlTransactionIsolationLevel.None }, CancellationToken.None);
            Assert.That(result, Is.EqualTo(3));

            var query = await GetAllResults();
            Assert.That(query.Count(),Is.EqualTo(3));
            Assert.That(query[0]["LastName"].ToString(), Is.EqualTo("Suku"));
        }

        [TestCase(true)]
        [TestCase(false)]
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
                           ConnectionString = _connectionString,
                           TableName = TableName,
                           InputData = inputJson
                       }, new BulkInsertOptions() { CommandTimeoutSeconds = 60, FireTriggers = true, KeepIdentity = false, ConvertEmptyPropertyValuesToNull = true, SqlTransactionIsolationLevel = useTransaction ? SqlTransactionIsolationLevel.Default : SqlTransactionIsolationLevel.None }, CancellationToken.None);
            Assert.That(result, Is.EqualTo(3));

            var query = await GetAllResults();
            Assert.That(query.Count(), Is.EqualTo(3));
            Assert.That(query[2]["FirstName"].Value<DBNull>(), Is.EqualTo(null));
        }


        private async Task AddDataToTable(IEnumerable<TestRow> tastTableData )
        {
            await
                Sql.BatchOperation(
                    new InputBatchOperation()
                    {
                        ConnectionString = _connectionString,
                        Query = $"insert {TableName}(Id,FirstName,LastName) VALUES(@Id,@FirstName,@LastName)",
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
           return await
                   Sql.ExecuteQuery(
                       new InputQuery()
                       {
                           ConnectionString = _connectionString,
                           Query = $"select * FROM {TableName}",
                           Parameters = new Parameter[0]
                       }, new Options() { CommandTimeoutSeconds = 60 }, CancellationToken.None);
        }

        public void InsertStoredProcedure(string procedureName)
        {
            string procedure = $@"CREATE PROCEDURE [{procedureName}]
                @FirstName varchar(255),@LastName varchar(255) as BEGIN
                SET NOCOUNT ON; 
                INSERT INTO {TableName} (FirstName, LastName, Id) VALUES (@FirstName,@LastName,1) 
                END";
            using (var connection = new SqlConnection(_connectionString))
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

    public class TestRow
    {
        public int Id { get; set; }
        public string LastName { get; set; }

        public string FirstName { get; set; }
    }


}
