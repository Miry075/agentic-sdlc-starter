namespace Knowledge.Upstreams;

/// <summary>Refuses upstream settings that are unsafe or not supported yet. Fails at startup, not at the first call.</summary>
public static class UpstreamConfigValidator
{
    public static IReadOnlyList<string> Validate(IEnumerable<UpstreamConfig> configs)
    {
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var config in configs)
        {
            var label = $"Upstream '{config.Name}'";

            if (string.IsNullOrWhiteSpace(config.Name) || config.Name.Contains('.'))
            {
                errors.Add($"{label}: the name is required and cannot contain '.'.");
            }
            else if (!names.Add(config.Name))
            {
                errors.Add($"{label}: the name is used twice.");
            }

            if (config.TimeoutSeconds is < 1 or > 300)
            {
                errors.Add($"{label}: 'TimeoutSeconds' must be between 1 and 300.");
            }

            if (config.Transport == UpstreamTransport.Stdio && string.IsNullOrWhiteSpace(config.Command))
            {
                errors.Add($"{label}: 'Command' is required for the Stdio transport.");
            }

            if (config.Transport == UpstreamTransport.Http)
            {
                ValidateUrl(config, label, errors);
            }

            ValidateAuth(config, label, errors);
        }

        return errors;
    }

    private static void ValidateUrl(UpstreamConfig config, string label, List<string> errors)
    {
        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var url))
        {
            errors.Add($"{label}: 'Url' is required for the Http transport.");
            return;
        }

        var isLocal = url.IsLoopback;
        if (url.Scheme != Uri.UriSchemeHttps && !(isLocal && url.Scheme == Uri.UriSchemeHttp))
        {
            errors.Add($"{label}: 'Url' must use https (http is only accepted for localhost).");
        }
    }

    private static void ValidateAuth(UpstreamConfig config, string label, List<string> errors)
    {
        switch (config.Auth.Mode)
        {
            case UpstreamAuthMode.KeyVaultSecret when string.IsNullOrWhiteSpace(config.Auth.Secret):
                errors.Add($"{label}: Auth 'Secret' is required for KeyVaultSecret.");
                break;

            case UpstreamAuthMode.ManagedIdentity when string.IsNullOrWhiteSpace(config.Auth.Scope):
                errors.Add($"{label}: Auth 'Scope' is required for ManagedIdentity.");
                break;

            case UpstreamAuthMode.Obo:
                errors.Add($"{label}: Auth mode Obo is planned for v0.2. Use KeyVaultSecret or ManagedIdentity.");
                break;
        }

        if (config.Auth.Mode != UpstreamAuthMode.None && config.Transport == UpstreamTransport.Stdio)
        {
            errors.Add($"{label}: authentication applies to the Http transport only. A Stdio upstream runs locally with no credentials.");
        }
    }
}
