using AiReadiness.Scanner;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;

IBrowserLauncher? browser = AutoOpen.IsAllowed(Console.IsOutputRedirected, Environment.GetEnvironmentVariable)
    ? new SystemBrowserLauncher()
    : null;

return new ScannerCli(new DiskRepoOpener(), TimeProvider.System, new EmbeddedAssets(), browser).Run(args, Console.Out, Console.Error);
