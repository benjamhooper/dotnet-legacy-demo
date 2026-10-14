using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Tallgrass.Common
{
    /// <summary>
    /// Thin wrapper over ADO.NET. Every call is a stored procedure. Every result is a
    /// dictionary, because the 2011 team did not trust ORMs and the 2017 team did not
    /// have time to argue about it.
    /// </summary>
    public class SqlDb : IDisposable
    {
        private readonly DbConnection _connection;

        public SqlDb(string connectionString)
        {
            // System.Data.SqlClient was the provider in 2011; the invariant name survived every port.
            var factory = DbProviderFactories.GetFactory("System.Data.SqlClient");
            _connection = factory.CreateConnection();
            _connection.ConnectionString = connectionString;
            _connection.Open();
        }

        public IDictionary<string, object> QuerySingle(string procedure, params object[] args)
        {
            var rows = Query(procedure, args);
            return rows.Count == 0 ? null : rows[0];
        }

        public List<IDictionary<string, object>> Query(string procedure, params object[] args)
        {
            using (var command = CreateCommand(procedure, args))
            using (var reader = command.ExecuteReader())
            {
                var rows = new List<IDictionary<string, object>>();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.GetValue(i);
                    rows.Add(row);
                }
                return rows;
            }
        }

        public int Execute(string procedure, params object[] args)
        {
            using (var command = CreateCommand(procedure, args))
            {
                return command.ExecuteNonQuery();
            }
        }

        public object Scalar(string procedure, params object[] args)
        {
            using (var command = CreateCommand(procedure, args))
            {
                return command.ExecuteScalar();
            }
        }

        private DbCommand CreateCommand(string procedure, object[] args)
        {
            var command = _connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedure;
            for (var i = 0; i + 1 < args.Length; i += 2)
            {
                var p = command.CreateParameter();
                p.ParameterName = (string)args[i];
                p.Value = args[i + 1] ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            return command;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
