using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

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

    static DateTime sessionStartedAt;

    static long activeSeconds = 0;
    static long idleSeconds = 0;

    const uint IDLE_THRESHOLD_SECONDS = 10;

    static readonly HttpClient httpClient = new HttpClient();

    const string API_URL =
    "http://192.168.0.246:5000/api/employee-activity";

const string EMPLOYEE_ID = "EMP004";


    static async Task Main()
    {
        SQLiteStorage.Initialize();

        Console.Title = "ERP Windows Agent";

        Console.WriteLine("ERP Windows Agent Started");
        Console.WriteLine("==========================");

        // Upload existing unsynced records once when agent starts
        await UploadUnsyncedSessions();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;

            Console.WriteLine();
            Console.WriteLine("Stopping ERP Windows Agent...");

            if (!string.IsNullOrEmpty(currentApplication))
            {
                SaveSession();
            }

            SQLiteStorage.PrintSessions();

            Environment.Exit(0);
        };

        while (true)
        {
            try
            {
                TrackActivity();

                Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");

                Thread.Sleep(2000);
            }
        }
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
        currentIdleSeconds >= IDLE_THRESHOLD_SECONDS;


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
        $"Idle threshold: {IDLE_THRESHOLD_SECONDS} seconds"
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
        var sessions =
            SQLiteStorage.GetUnsyncedSessions();

        foreach (var session in sessions)
        {
            var data = new
            {
                employeeId = EMPLOYEE_ID,

                computerName =
                    Environment.MachineName,

                application =
                    session.Application,

                startedAt =
                    session.StartedAt,

                endedAt =
                    session.EndedAt,

                activeSeconds =
                    session.ActiveSeconds,

                idleSeconds =
                    session.IdleSeconds
            };

            try
            {
                var response =
                    await httpClient.PostAsJsonAsync(
                        API_URL,
                        data
                    );

                if (response.IsSuccessStatusCode)
                {
                    SQLiteStorage.MarkAsSynced(
                        session.Id
                    );

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
            }
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