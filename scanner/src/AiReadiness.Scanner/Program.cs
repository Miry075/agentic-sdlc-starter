using AiReadiness.Scanner;
using AiReadiness.Scanner.Files;

return new ScannerCli(new DiskRepoOpener(), TimeProvider.System).Run(args, Console.Out, Console.Error);
