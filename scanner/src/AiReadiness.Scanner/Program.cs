using AiReadiness.Scanner;
using AiReadiness.Scanner.Files;
using AiReadiness.Scanner.Remediation;

return new ScannerCli(new DiskRepoOpener(), TimeProvider.System, new EmbeddedAssets()).Run(args, Console.Out, Console.Error);
