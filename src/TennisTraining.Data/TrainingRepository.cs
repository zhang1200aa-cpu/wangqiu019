using System;
using Microsoft.Data.Sqlite;
using TennisTraining.Core;

namespace TennisTraining.Data
{
    /// <summary>SQLite 训练数据仓储（partial：连接与 schema）。</summary>
    public sealed partial class TrainingRepository : IDisposable
    {
        private readonly string _connStr;
        private SqliteConnection _keepAlive;

        public string DatabasePath { get; }

        public TrainingRepository(string dbPath)
        {
            DatabasePath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();
            _keepAlive = new SqliteConnection(_connStr);
            _keepAlive.Open();
            InitSchema();
        }

        private void InitSchema()
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS sessions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_name TEXT NOT NULL,
  mode INTEGER NOT NULL,
  position INTEGER NOT NULL,
  difficulty INTEGER NOT NULL,
  start_time TEXT NOT NULL,
  end_time TEXT,
  total INTEGER DEFAULT 0,
  hits INTEGER DEFAULT 0,
  misses INTEGER DEFAULT 0,
  avg_speed REAL DEFAULT 0,
  max_speed REAL DEFAULT 0,
  avg_quality REAL DEFAULT 0,
  consistency REAL DEFAULT 0,
  duration_ms INTEGER DEFAULT 0
);
CREATE TABLE IF NOT EXISTS balls (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id INTEGER NOT NULL,
  seq INTEGER DEFAULT 0,
  speed_mps REAL DEFAULT 0,
  quality REAL DEFAULT 0,
  hit INTEGER DEFAULT 0,
  landing_x REAL DEFAULT 0,
  landing_y REAL DEFAULT 0,
  hit_x REAL DEFAULT 0,
  hit_y REAL DEFAULT 0,
  hit_z REAL DEFAULT 0,
  spin_type INTEGER DEFAULT 0,
  spin_rpm REAL DEFAULT 0,
  timestamp TEXT,
  note TEXT,
  FOREIGN KEY(session_id) REFERENCES sessions(id)
);
CREATE INDEX IF NOT EXISTS idx_balls_session ON balls(session_id);
";
            Exec(sql);
        }

        internal SqliteConnection OpenConn() => new(_connStr);

        private void Exec(string sql, params (string, object)[] ps)
        {
            using var c = new SqliteConnection(_connStr);
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            try { _keepAlive?.Dispose(); } catch { }
        }
    }
}
