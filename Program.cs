using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

class Program
{
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId
    );

    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(
        ref LASTINPUTINFO plii
    );

    static string currentApplication = "";
    static string configuredDeviceId = "";
static string configuredDeviceToken = "";

    static DateTime sessionStartedAt;

    static long activeSeconds = 0;
    static long idleSeconds = 0;

    static uint idleThresholdSeconds = 10;
static int pollIntervalMilliseconds = 2000;
public static int GetPollIntervalMilliseconds()
{
    return pollIntervalMilliseconds;
}

static readonly HttpClient httpClient = new HttpClient();
static readonly SemaphoreSlim uploadLock = new SemaphoreSlim(1, 1);

static string apiUrl = "";





static bool LoadConfiguration()
{
    string configPath = Path.Combine(
        AppContext.BaseDirectory,
        "config.txt"
    );

    if (!File.Exists(configPath))
    {
        Console.WriteLine("ERROR: config.txt not found.");
        Console.WriteLine($"Expected location: {configPath}");
        return false;
    }

    string[] lines = File.ReadAllLines(configPath);

    foreach (string line in lines)
    {
        string trimmedLine = line.Trim();

        if (trimmedLine.StartsWith("DEVICE_ID="))
        {
            configuredDeviceId = trimmedLine
                .Substring("DEVICE_ID=".Length)
                .Trim();
        }
        else if (trimmedLine.StartsWith("DEVICE_TOKEN="))
        {
            configuredDeviceToken = trimmedLine
                .Substring("DEVICE_TOKEN=".Length)
                .Trim();
        }
        else if (trimmedLine.StartsWith("API_URL="))
        {
            apiUrl = trimmedLine
                .Substring("API_URL=".Length)
                .Trim();
        }
        else if (trimmedLine.StartsWith("IDLE_THRESHOLD_SECONDS="))
{
    string value = trimmedLine
        .Substring("IDLE_THRESHOLD_SECONDS=".Length)
        .Trim();

    if (!uint.TryParse(value, out idleThresholdSeconds))
    {
        Console.WriteLine("ERROR: Invalid IDLE_THRESHOLD_SECONDS.");
        return false;
    }
}
else if (trimmedLine.StartsWith("POLL_INTERVAL_MILLISECONDS="))
{
    string value = trimmedLine
        .Substring("POLL_INTERVAL_MILLISECONDS=".Length)
        .Trim();

    if (!int.TryParse(value, out pollIntervalMilliseconds))
    {
        Console.WriteLine("ERROR: Invalid POLL_INTERVAL_MILLISECONDS.");
        return false;
    }
}
    }

    if (string.IsNullOrWhiteSpace(configuredDeviceId))
    {
        Console.WriteLine("ERROR: DEVICE_ID is missing in config.txt.");
        return false;
    }

    if (string.IsNullOrWhiteSpace(configuredDeviceToken))
    {
        Console.WriteLine("ERROR: DEVICE_TOKEN is missing in config.txt.");
        return false;
    }

    if (string.IsNullOrWhiteSpace(apiUrl))
    {
        Console.WriteLine("ERROR: API_URL is missing in config.txt.");
        return false;
    }

    return true;
}
   static async Task Main()
{
    if (!LoadConfiguration())
    {
        Console.WriteLine();
        Console.WriteLine("Agent cannot start without configuration.");
        Console.ReadKey();
        return;
    }

    SQLiteStorage.Initialize();

    Console.Title = "ERP Windows Agent";

    Console.WriteLine("ERP Windows Agent Started");
    Console.WriteLine("==========================");

    // Upload existing unsynced records once when agent starts
    await UploadUnsyncedSessions();

    var builder = Host.CreateApplicationBuilder();

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "ERP Windows Agent";
    });

    builder.Services.AddHostedService<AgentWorker>();

    var host = builder.Build();

    await host.RunAsync();
}

public static void TrackActivityForWorker()
{
    TrackActivity();
}
    static void TrackActivity()
{
    IntPtr hwnd = GetForegroundWindow();

    if (hwnd == IntPtr.Zero)
        return;

    GetWindowThreadProcessId(
        hwnd,
        out uint processId
    );

    string processName = "Unknown";

    try
    {
        Process process =
            Process.GetProcessById((int)processId);

        processName = process.ProcessName;
    }
    catch
    {
        return;
    }

    uint currentIdleSeconds = GetIdleSeconds();

   bool isIdle =
    currentIdleSeconds >= idleThresholdSeconds;


    // ==========================================
    // FIRST APPLICATION
    // ==========================================

    if (string.IsNullOrEmpty(currentApplication))
    {
        currentApplication = processName;

        sessionStartedAt = DateTime.Now;

        activeSeconds = 0;
        idleSeconds = 0;
    }


    // ==========================================
    // APPLICATION CHANGED
    // ==========================================

    if (currentApplication != processName)
    {
        // Save previous application
        SaveSession();

        // Upload saved session
        _ = UploadUnsyncedSessions();


        // Start new application session
        currentApplication = processName;

        sessionStartedAt = DateTime.Now;

        activeSeconds = 0;
        idleSeconds = 0;
    }


    // ==========================================
    // ACTIVE / IDLE TIME
    // ==========================================

    if (isIdle)
    {
        idleSeconds += 2;
    }
    else
    {
        activeSeconds += 2;
    }


    // ==========================================
    // DISPLAY
    // ==========================================

    DisplayStatus(
        processName,
        processId,
        isIdle
    );
}

static void DisplayStatus(
    string processName,
    uint processId,
    bool isIdle
)
{
    Console.Clear();

    TimeSpan totalTime =
        TimeSpan.FromSeconds(
            activeSeconds + idleSeconds
        );

    TimeSpan activeTime =
        TimeSpan.FromSeconds(
            activeSeconds
        );

    TimeSpan idleTime =
        TimeSpan.FromSeconds(
            idleSeconds
        );

    Console.WriteLine(
        "ERP Windows Agent"
    );

    Console.WriteLine(
        "=========================="
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Application : {processName}"
    );

    Console.WriteLine(
        $"Process ID  : {processId}"
    );

    Console.WriteLine(
        $"Status      : {(isIdle ? "IDLE" : "ACTIVE")}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Session Start : {sessionStartedAt}"
    );

    Console.WriteLine(
        $"Open Time     : {totalTime:hh\\:mm\\:ss}"
    );

    Console.WriteLine(
        $"Active Time   : {activeTime:hh\\:mm\\:ss}"
    );

    Console.WriteLine(
        $"Idle Time     : {idleTime:hh\\:mm\\:ss}"
    );

    Console.WriteLine();

    Console.WriteLine(
        $"Idle threshold: {idleThresholdSeconds} seconds"
    );
}
    static void SaveSession()
    {
        ActivitySession session =
            new ActivitySession
            {
                Application = currentApplication,

                StartedAt = sessionStartedAt,

                EndedAt = DateTime.Now,

                ActiveSeconds = activeSeconds,

                IdleSeconds = idleSeconds
            };

        SQLiteStorage.SaveSession(session);

        Console.WriteLine();

        Console.WriteLine(
            "SESSION SAVED"
        );

        Console.WriteLine(
            "--------------------------"
        );

        Console.WriteLine(
            $"Application : {session.Application}"
        );

        Console.WriteLine(
            $"Started     : {session.StartedAt}"
        );

        Console.WriteLine(
            $"Ended       : {session.EndedAt}"
        );

        Console.WriteLine(
            $"Active Time : {TimeSpan.FromSeconds(session.ActiveSeconds)}"
        );

        Console.WriteLine(
            $"Idle Time   : {TimeSpan.FromSeconds(session.IdleSeconds)}"
        );

        Console.WriteLine();
    }


  static async Task UploadUnsyncedSessions()
{
    // Prevent multiple upload operations from running at the same time
    if (!await uploadLock.WaitAsync(0))
    {
        Console.WriteLine("Upload already in progress. Skipping.");
        return;
    }

    try
    {
        var sessions = SQLiteStorage.GetUnsyncedSessions();

        foreach (var session in sessions)
        {
            var data = new
            {
                deviceId = configuredDeviceId,
                deviceToken = configuredDeviceToken,
                computerName = Environment.MachineName,
                application = session.Application,
                startedAt = session.StartedAt,
                endedAt = session.EndedAt,
                activeSeconds = session.ActiveSeconds,
                idleSeconds = session.IdleSeconds
            };

            try
            {
                var response = await httpClient.PostAsJsonAsync(
                    apiUrl,
                    data
                );

                if (response.IsSuccessStatusCode)
                {
                    SQLiteStorage.MarkAsSynced(session.Id);

                    Console.WriteLine(
                        $"Uploaded: {session.Application}"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"Upload failed: {response.StatusCode}"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"API unavailable: {ex.Message}"
                );

                // Keep Synced = 0
                // It will be retried later.
            }
        }
    }
    finally
    {
        uploadLock.Release();
    }
}


    static uint GetIdleSeconds()
    {
        LASTINPUTINFO lastInputInfo =
            new LASTINPUTINFO();

        lastInputInfo.cbSize =
            (uint)Marshal.SizeOf(
                lastInputInfo
            );

        if (!GetLastInputInfo(
            ref lastInputInfo))
        {
            return 0;
        }

        uint currentTick =
            (uint)Environment.TickCount;

        uint idleMilliseconds =
            currentTick -
            lastInputInfo.dwTime;

        return idleMilliseconds / 1000;
    }
}