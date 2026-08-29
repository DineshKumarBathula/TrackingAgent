using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

public static class SQLiteStorage
{
    private static readonly string DbPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "activity.db"
        );

    private static readonly string ConnectionString =
        $"Data Source={DbPath}";


    public static void Initialize()
    {
        using var connection =
            new SqliteConnection(ConnectionString);

        connection.Open();

        string sql = @"
            CREATE TABLE IF NOT EXISTS ActivitySessions
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,

                Application TEXT NOT NULL,

                StartedAt TEXT NOT NULL,

                EndedAt TEXT NOT NULL,

                ActiveSeconds INTEGER NOT NULL,

                IdleSeconds INTEGER NOT NULL,

                Synced INTEGER NOT NULL DEFAULT 0
            );
        ";

        using var command =
            new SqliteCommand(
                sql,
                connection
            );

        command.ExecuteNonQuery();
    }


    public static void SaveSession(
        ActivitySession session
    )
    {
        using var connection =
            new SqliteConnection(ConnectionString);

        connection.Open();

        string sql = @"
            INSERT INTO ActivitySessions
            (
                Application,
                StartedAt,
                EndedAt,
                ActiveSeconds,
                IdleSeconds,
                Synced
            )
            VALUES
            (
                @Application,
                @StartedAt,
                @EndedAt,
                @ActiveSeconds,
                @IdleSeconds,
                0
            );
        ";

        using var command =
            new SqliteCommand(
                sql,
                connection
            );

        command.Parameters.AddWithValue(
            "@Application",
            session.Application
        );

        command.Parameters.AddWithValue(
            "@StartedAt",
            session.StartedAt.ToString("O")
        );

        command.Parameters.AddWithValue(
            "@EndedAt",
            session.EndedAt.ToString("O")
        );

        command.Parameters.AddWithValue(
            "@ActiveSeconds",
            session.ActiveSeconds
        );

        command.Parameters.AddWithValue(
            "@IdleSeconds",
            session.IdleSeconds
        );

        command.ExecuteNonQuery();
    }


    public static List<ActivitySession> GetUnsyncedSessions()
    {
        var sessions =
            new List<ActivitySession>();

        using var connection =
            new SqliteConnection(ConnectionString);

        connection.Open();

        string sql = @"
            SELECT
                Id,
                Application,
                StartedAt,
                EndedAt,
                ActiveSeconds,
                IdleSeconds
            FROM ActivitySessions
            WHERE Synced = 0
            ORDER BY Id;
        ";

        using var command =
            new SqliteCommand(
                sql,
                connection
            );

        using var reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            sessions.Add(
                new ActivitySession
                {
                    Id =
                        Convert.ToInt32(
                            reader["Id"]
                        ),

                    Application =
                        reader["Application"]
                            .ToString() ?? "",

                    StartedAt =
                        DateTime.Parse(
                            reader["StartedAt"]
                                .ToString()!
                        ),

                    EndedAt =
                        DateTime.Parse(
                            reader["EndedAt"]
                                .ToString()!
                        ),

                    ActiveSeconds =
                        Convert.ToInt64(
                            reader["ActiveSeconds"]
                        ),

                    IdleSeconds =
                        Convert.ToInt64(
                            reader["IdleSeconds"]
                        )
                }
            );
        }

        return sessions;
    }


    public static void MarkAsSynced(int id)
    {
        using var connection =
            new SqliteConnection(ConnectionString);

        connection.Open();

        string sql = @"
            UPDATE ActivitySessions
            SET Synced = 1
            WHERE Id = @Id;
        ";

        using var command =
            new SqliteCommand(
                sql,
                connection
            );

        command.Parameters.AddWithValue(
            "@Id",
            id
        );

        command.ExecuteNonQuery();
    }


    public static void PrintSessions()
    {
        using var connection =
            new SqliteConnection(ConnectionString);

        connection.Open();

        string sql = @"
            SELECT
                Id,
                Application,
                StartedAt,
                EndedAt,
                ActiveSeconds,
                IdleSeconds,
                Synced
            FROM ActivitySessions
            ORDER BY Id;
        ";

        using var command =
            new SqliteCommand(
                sql,
                connection
            );

        using var reader =
            command.ExecuteReader();

        Console.WriteLine();
        Console.WriteLine(
            "DATABASE RECORDS"
        );

        Console.WriteLine(
            "=============================="
        );

        while (reader.Read())
        {
            Console.WriteLine(
                $"ID          : {reader["Id"]}"
            );

            Console.WriteLine(
                $"Application : {reader["Application"]}"
            );

            Console.WriteLine(
                $"Started     : {reader["StartedAt"]}"
            );

            Console.WriteLine(
                $"Ended       : {reader["EndedAt"]}"
            );

            Console.WriteLine(
                $"Active      : {reader["ActiveSeconds"]} sec"
            );

            Console.WriteLine(
                $"Idle        : {reader["IdleSeconds"]} sec"
            );

            Console.WriteLine(
                $"Synced      : {reader["Synced"]}"
            );

            Console.WriteLine(
                "------------------------------"
            );
        }
    }
}