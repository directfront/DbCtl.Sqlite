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

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.ComponentModel.Composition;
using Microsoft.Data.Sqlite;
using DbCtl.Connectors;
using System.Collections.Generic;
using Dapper;

[assembly: InternalsVisibleTo("DbCtl.Sqlite.Tests")]

namespace DbCtl.Sqlite
{
    [Export(typeof(IDbConnector))]
    [ExportMetadata("Name", "SQLite")]
    [ExportMetadata("Description", "Database connector for SQLite")]
    [ExportMetadata("Version", "1.0.0")]
    public class SqliteConnector : IDbConnector
    {
        private DbConnection _DbConnection;
        private DbCommand _DbCommand;

        public SqliteConnector()
        {
        }

        internal SqliteConnector(DbConnection dbConnection)
        {
            _DbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
            _DbCommand = _DbConnection.CreateCommand();
        }

        public async Task<int> AddChangeLogEntryAsync(string connectionString, ChangeLogEntry entry, CancellationToken cancellationToken)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var commandText = $"INSERT INTO DbCtlChangeLog (MigrationType, Version, Description, Filename, Hash, AppliedBy, ChangeDateTime) " +
                                $"VALUES ('{entry.MigrationType}', '{entry.Version}', '{entry.Description}', '{entry.Filename}', '{entry.Hash}', '{entry.AppliedBy}', '{entry.ChangeDateTime:s}')";

            return await ExecuteCommandAsync(connectionString, commandText, cancellationToken);
        }

        public async Task<int> CreateChangeLogTableAsync(string connectionString, CancellationToken cancellationToken)
        {
            return await ExecuteScriptAsync(connectionString,
                @"CREATE TABLE DbCtlChangeLog (
                    MigrationType VARCHAR(15) NOT NULL,
                    Version VARCHAR(10),
                    Description VARCHAR(255),
                    Filename VARCHAR(255) NOT NULL,
                    Hash VARCHAR(64) NOT NULL,
                    AppliedBy VARCHAR(50) NOT NULL,
                    ChangeDateTime DATETIME NOT NULL,
                    CONSTRAINT PK_DbCtlChangeLog PRIMARY KEY (ChangeDateTime DESC, Version DESC)
                )", cancellationToken);
        }

        public async Task<int> ExecuteScriptAsync(string connectionString, string script, CancellationToken cancellationToken)
        {
            return await ExecuteCommandAsync(connectionString, script, cancellationToken);
        }

        private async Task<int> ExecuteCommandAsync(string connectionString, string commandText, CancellationToken cancellationToken)
        {
            SetConnectionString(connectionString);
            SetCommandText(commandText);

            try
            {
                await _DbConnection.OpenAsync();
                return await _DbCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (_DbConnection.State != ConnectionState.Closed)
                    await _DbConnection.CloseAsync();
            }
        }

        private void SetCommandText(string commandText)
        {
            if (string.IsNullOrEmpty(commandText))
                throw new ArgumentNullException(nameof(commandText));

            _DbCommand.CommandText = commandText;
        }

        private void SetConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (_DbConnection == null)
            {
                _DbConnection = new SqliteConnection(connectionString);
                _DbCommand = _DbConnection.CreateCommand();

                return;
            }

            _DbConnection.ConnectionString = connectionString;
        }

        public void Dispose()
        {
            _DbCommand?.Dispose();
            _DbConnection?.Dispose();
        }

        public async Task<IEnumerable<ChangeLogEntry>> FetchChangeLogEntriesAsync(string connectionString, CancellationToken cancellationToken)
        {
            SetConnectionString(connectionString);

            try
            {
                SetConnectionString(connectionString);
                return await _DbConnection.QueryAsync<ChangeLogEntry>("SELECT * FROM DbCtlChangeLog");
            }
            finally
            {
                if (_DbConnection.State != ConnectionState.Closed)
                    await _DbConnection.CloseAsync();
            }
        }
    }
}
