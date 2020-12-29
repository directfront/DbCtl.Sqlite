// Copyright 2020 Direct Front Systems (Pty) Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using System.Linq;
using System.Text;
using System;
using DbCtl.Connectors;

namespace DbCtl.Sqlite.UnitTests
{

    public abstract class SqliteConnectorTestsBase : IDisposable
    {
        private const string DatabaseFileName = "DbCtl.Sqlite.UnitTest.db";
        protected string ConnectionString;
        protected SqliteConnection Connection;
        protected SqliteConnector Connector;

        [SetUp]
        public virtual void Setup()
        {
            Connection = new SqliteConnection();
            Connector = new SqliteConnector(Connection);

            ConnectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = DatabaseFileName,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();
        }

        [TearDown]
        public virtual void Teardown()
        {
            Dispose();

            if (File.Exists(DatabaseFileName))
                File.Delete(DatabaseFileName);
        }

        public void Dispose()
        {
            Connector?.Dispose();
            Connection?.Dispose();
        }
    }

    [TestFixture]
    public class When_calling_create_change_log_table_on_sqlite_connector : SqliteConnectorTestsBase
    {
        [Test]
        public async Task It_should_create_the_change_log_table()
        {
            var createTableTask = Connector.CreateChangeLogTableAsync(ConnectionString, new CancellationToken());
            var selectTableTask = Connector.ExecuteScriptAsync(ConnectionString,
                "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='DbCtlChangeLog'",
                new CancellationToken()
            );

            await createTableTask
                .ContinueWith(task => selectTableTask
                    .ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                            Assert.Fail("Failed to query the DbCtlChangeLog table.");
                        else
                            Assert.AreEqual(1, task.Result);
                    }));
        }
    }

    [TestFixture]
    public class When_calling_add_change_log_entry_on_sqlite_connector : SqliteConnectorTestsBase
    {
        [Test]
        public async Task It_should_insert_the_change_log_entry()
        {
            await Connector.CreateChangeLogTableAsync(ConnectionString, new CancellationToken());

            using var contents = new MemoryStream(Encoding.UTF8.GetBytes("CREATE TABLE Fake (ID INTEGER)"));

            var expectedEntry = new ChangeLogEntry("f-1.0.2-Initialise_database_change_log.ddl", "JoeSoap", new DateTime(2020, 12, 16, 09, 43, 22), contents);
            var createTableTask = await Connector.AddChangeLogEntryAsync(ConnectionString, expectedEntry, new CancellationToken());

            var actualEntry = await Connection.QueryAsync<ChangeLogEntry>("SELECT * FROM DbCtlChangeLog");

            Assert.IsTrue(expectedEntry.Equals(actualEntry.Single()));
        }
    }

    [TestFixture]
    public class When_calling_fetch_change_log_entries_on_sql_server_connector : SqliteConnectorTestsBase
    {
        private CancellationToken _CancellationToken = new CancellationToken();

        [Test]
        public async Task It_should_fetch_all_the_change_log_entries()
        {
            await Connector.CreateChangeLogTableAsync(ConnectionString, _CancellationToken);

            var expectedEntry = new ChangeLogEntry("f-1.0.2-Initialise_database_change_log.ddl", "JoeSoap", new DateTime(2020, 12, 16, 09, 43, 22), Stream.Null);
            await Connector.AddChangeLogEntryAsync(ConnectionString, expectedEntry, _CancellationToken);

            var actualEntry = await Connector.FetchChangeLogEntriesAsync(ConnectionString, _CancellationToken);

            Assert.IsTrue(expectedEntry.Equals(actualEntry.Single()));
        }
    }
}