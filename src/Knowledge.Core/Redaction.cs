namespace Knowledge.Core;

public sealed record RedactionResult(string Text, int RedactionCount, IReadOnlyDictionary<string, int> ByKind);

public interface IDataRedactor
{
    RedactionResult Redact(string text);
}
