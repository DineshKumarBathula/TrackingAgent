using Microsoft.Extensions.Hosting;

public class AgentWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("ERP Windows Agent Worker Started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Program.TrackActivityForWorker();

                await Task.Delay(
                    Program.GetPollIntervalMilliseconds(),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Agent Error: {ex.Message}");

                try
                {
                    await Task.Delay(
                        Program.GetPollIntervalMilliseconds(),
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        Console.WriteLine("ERP Windows Agent Worker Stopped");
    }
}