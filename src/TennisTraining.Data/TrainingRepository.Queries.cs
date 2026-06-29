using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TennisTraining.Core;

namespace TennisTraining.Data
{
    /// <summary>SQLite 训练数据仓储（partial：增删查）。</summary>
    public sealed partial class TrainingRepository
    {
        /// <summary>新建会话，返回会话 ID。</summary>
        public long InsertSession(TrainingSession s)
        {
            using var c = OpenConn(); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO sessions
(user_name,mode,position,difficulty,start_time,end_time,total,hits,misses,avg_speed,max_speed,avg_quality,consistency,duration_ms)
VALUES(@u,@m,@p,@d,@st,@et,@total,@hits,@miss,@avg,@max,@avgq,@cons,@dur);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@u", s.UserName);
            cmd.Parameters.AddWithValue("@m", (int)s.Mode);
            cmd.Parameters.AddWithValue("@p", (int)s.Position);
            cmd.Parameters.AddWithValue("@d", s.Difficulty);
            cmd.Parameters.AddWithValue("@st", s.StartTime.ToString("u"));
            cmd.Parameters.AddWithValue("@et", s.EndTime == default ? DBNull.Value : (object)s.EndTime.ToString("u"));
            AddStats(cmd, s.Stats);
            return (long)cmd.ExecuteScalar();
        }

        public void InsertBall(long sessionId, int seq, ProcessedBallData b)
        {
            Exec(@"INSERT INTO balls
(session_id,seq,speed_mps,quality,hit,landing_x,landing_y,hit_x,hit_y,hit_z,spin_type,spin_rpm,timestamp,note)
VALUES(@sid,@seq,@sp,@q,@hit,@lx,@ly,@hx,@hy,@hz,@st,@rpm,@ts,@note);",
                ("@sid", sessionId), ("@seq", seq), ("@sp", b.Trajectory?.Speed ?? 0), ("@q", b.QualityScore),
                ("@hit", b.Hit ? 1 : 0), ("@lx", b.ActualLanding?.X ?? 0), ("@ly", b.ActualLanding?.Y ?? 0),
                ("@hx", b.HitPoint?.X ?? 0), ("@hy", b.HitPoint?.Y ?? 0), ("@hz", b.HitPoint?.Z ?? 0),
                ("@st", (int)(b.Trajectory?.Spin?.Type ?? SpinType.Flat)), ("@rpm", b.Trajectory?.Spin?.Rpm ?? 0),
                ("@ts", b.Timestamp.ToString("u")), ("@note", (object)b.Note ?? DBNull.Value));
        }

        public void UpdateSessionStats(long sessionId, TrainingStats st, DateTime endTime)
        {
            Exec(@"UPDATE sessions SET total=@total,hits=@hits,misses=@miss,avg_speed=@avg,max_speed=@max,
avg_quality=@avgq,consistency=@cons,duration_ms=@dur,end_time=@et WHERE id=@id;",
                ("@total", st.TotalBalls), ("@hits", st.Hits), ("@miss", st.Misses),
                ("@avg", st.AvgSpeed), ("@max", st.MaxSpeed), ("@avgq", st.AvgQuality),
                ("@cons", st.Consistency), ("@dur", (long)st.Duration.TotalMilliseconds),
                ("@et", endTime.ToString("u")), ("@id", sessionId));
        }

        public List<TrainingSession> GetRecentSessions(int n = 20)
        {
            var list = new List<TrainingSession>();
            using var c = OpenConn(); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT * FROM sessions ORDER BY id DESC LIMIT @n;";
            cmd.Parameters.AddWithValue("@n", n);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadSession(r));
            return list;
        }

        public int CountSessions()
        {
            using var c = OpenConn(); c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sessions;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static void AddStats(SqliteCommand cmd, TrainingStats st)
        {
            cmd.Parameters.AddWithValue("@total", st.TotalBalls);
            cmd.Parameters.AddWithValue("@hits", st.Hits);
            cmd.Parameters.AddWithValue("@miss", st.Misses);
            cmd.Parameters.AddWithValue("@avg", st.AvgSpeed);
            cmd.Parameters.AddWithValue("@max", st.MaxSpeed);
            cmd.Parameters.AddWithValue("@avgq", st.AvgQuality);
            cmd.Parameters.AddWithValue("@cons", st.Consistency);
            cmd.Parameters.AddWithValue("@dur", (long)st.Duration.TotalMilliseconds);
        }

        private static TrainingSession ReadSession(System.Data.Common.DbDataReader r) => new()
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            UserName = r.GetString(r.GetOrdinal("user_name")),
            Mode = (TrainingMode)r.GetInt32(r.GetOrdinal("mode")),
            Position = (DevicePosition)r.GetInt32(r.GetOrdinal("position")),
            Difficulty = r.GetInt32(r.GetOrdinal("difficulty")),
            StartTime = DateTime.Parse(r.GetString(r.GetOrdinal("start_time"))),
            Stats = new TrainingStats
            {
                TotalBalls = r.GetInt32(r.GetOrdinal("total")),
                Hits = r.GetInt32(r.GetOrdinal("hits")),
                Misses = r.GetInt32(r.GetOrdinal("misses")),
                AvgSpeed = r.GetDouble(r.GetOrdinal("avg_speed")),
                MaxSpeed = r.GetDouble(r.GetOrdinal("max_speed")),
                AvgQuality = r.GetDouble(r.GetOrdinal("avg_quality")),
                Consistency = r.GetDouble(r.GetOrdinal("consistency")),
                Duration = TimeSpan.FromMilliseconds(r.GetInt64(r.GetOrdinal("duration_ms")))
            }
        };
    }
}
