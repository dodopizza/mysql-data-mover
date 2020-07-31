using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace Dodo.DataMover
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DataMoverSettings
    {
        private ConnectionStrings _connectionStrings;
        private int _dataReadCommandTimeoutSeconds = 30;

        private int _insertCommandTimeoutSeconds = 30;
        private int _insertConcurrency = 1;
        private double _jobTimeoutMinutes = 10 * 60;
        private long? _limit;
        private int _readBatchSize = 5000;
        private int _readConcurrency = 1;
        private int _schemaReadCommandTimeoutSeconds = 60;

        public int ReadConcurrency
        {
            get => _readConcurrency;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be positive integer");

                _readConcurrency = value;
            }
        }

        public int ReadBatchSize
        {
            get => _readBatchSize;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be positive integer");

                _readBatchSize = value;
            }
        }

        public int InsertConcurrency
        {
            get => _insertConcurrency;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be positive integer");

                _insertConcurrency = value;
            }
        }

        public double JobTimeoutMinutes
        {
            get => _jobTimeoutMinutes;
            set
            {
                if (value < 60) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be >= 60 minutes");

                _jobTimeoutMinutes = value;
            }
        }

        public long? Limit
        {
            get => _limit;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be >= 0");

                _limit = value;
            }
        }

        public int InsertCommandTimeoutSeconds
        {
            get => _insertCommandTimeoutSeconds;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be >= 1");

                _insertCommandTimeoutSeconds = value;
            }
        }

        public int DataReadCommandTimeoutSeconds
        {
            get => _dataReadCommandTimeoutSeconds;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be >= 1");

                _dataReadCommandTimeoutSeconds = value;
            }
        }

        public int SchemaReadCommandTimeoutSeconds
        {
            get => _schemaReadCommandTimeoutSeconds;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), value, "Must be >= 1");

                _schemaReadCommandTimeoutSeconds = value;
            }
        }

        public string[] IncludeTableRegexes { get; set; } = Array.Empty<string>();

        public string[] ExcludeTableRegexes { get; set; } = Array.Empty<string>();

        public bool CreateSchema { get; set; } = true;

        public bool InsertIgnore { get; set; } = false;

        public ConnectionStrings ConnectionStrings
        {
            get => _connectionStrings;
            set
            {
                static void ValidateConnectionString(string type, string connectionString)
                {
                    var connectionBuilder = new MySqlConnectionStringBuilder(connectionString);
                    if (string.IsNullOrWhiteSpace(connectionBuilder.Database))
                        throw new ArgumentException($"Initial catalog was not set for {type} connection string");
                }

                ValidateConnectionString("Src", value.Src);
                ValidateConnectionString("Dst", value.Dst);

                _connectionStrings = value;
            }
        }

        public string SqlMode { get; set; }
        public bool DropDatabase { get; set; } = false;

        public double DebugDelaySeconds { get; set; } = 0;

        public Dictionary<string, List<string>> SkipColumnsRegexes { get; set; } =
            new Dictionary<string, List<string>>();

        public int RetryInitialDelaySeconds { get; set; } = 3;
        public int RetryCount { get; set; } = 5;
    }


    public class ConnectionStrings
    {
        public string Src { get; set; }
        public string Dst { get; set; }
    }
}
