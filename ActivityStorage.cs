using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class ActivityStorage
{
    private static readonly string FilePath =
        Path.Combine(
            AppContext.BaseDirectory,
            "activity-data.json"
        );

    public static void SaveSession(
        ActivitySession session)
    {
        List<ActivitySession> sessions =
            LoadSessions();

        sessions.Add(session);

        string json =
            JsonSerializer.Serialize(
                sessions,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );

        File.WriteAllText(
            FilePath,
            json
        );
    }

    public static List<ActivitySession> LoadSessions()
    {
        if (!File.Exists(FilePath))
        {
            return new List<ActivitySession>();
        }

        try
        {
            string json =
                File.ReadAllText(FilePath);

            return JsonSerializer.Deserialize<
                List<ActivitySession>
            >(json)
            ?? new List<ActivitySession>();
        }
        catch
        {
            return new List<ActivitySession>();
        }
    }
}