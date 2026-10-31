using System.Text.Json;
using PMGM.Qa;

namespace PMGM.Qa.Cli;

internal static class MigrationProgram
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 0) throw new InvalidOperationException();
            var expected = JsonSerializer.Deserialize<string[]>(
                Environment.GetEnvironmentVariable("QA525_EXPECTED_EF_HISTORY") ?? "")!;
            await Support00MigrationRunner.RunAsync(
                Environment.GetEnvironmentVariable("QA525_DDL_CONNECTION") ?? "", expected,
                CancellationToken.None);
            Console.WriteLine("{\"status\":\"SUPPORT00_EF100_MIGRATED\"}");
            return 0;
        }
        catch
        {
            // Never emit connection strings, SQL or database exception details.
            Console.WriteLine("{\"status\":\"SUPPORT00_MIGRATION_BLOCKED_RECONCILE_REQUIRED\"}");
            return 2;
        }
    }
}
