using System;

public class ActivitySession
{
    public int Id { get; set; }

    public string Application { get; set; } = "";

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public long ActiveSeconds { get; set; }

    public long IdleSeconds { get; set; }
}