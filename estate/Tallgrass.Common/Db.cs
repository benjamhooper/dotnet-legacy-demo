using System;

namespace Tallgrass.Common
{
    /// <summary>
    /// The front door to the database. Everyone uses it; nobody injects it.
    /// </summary>
    public static class Db
    {
        public const string DefaultConnectionName = "TallgrassDb";

        public static SqlDb Open()
        {
            return Open(DefaultConnectionName);
        }

        public static SqlDb Open(string connectionName)
        {
            var connectionString = AppSettings.ConnectionString(connectionName);
            return new SqlDb(connectionString);
        }
    }
}
