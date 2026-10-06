namespace FlexiSpace.Infrastructure.services;

/// <summary>
/// Expands ordinary emails and Entra guest UPNs so directory rows can match either form.
/// </summary>
public static class EmailLookup
{
    public static string Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().TrimStart(':').Trim().ToLowerInvariant();

    public static IReadOnlyList<string> Candidates(string? email)
    {
        var normalized = Normalize(email);
        if (string.IsNullOrEmpty(normalized))
            return [];

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalized };

        // guest UPN → home email: local_domain.com#ext#@tenant → local@domain.com
        var extIdx = normalized.IndexOf("#ext#@", StringComparison.OrdinalIgnoreCase);
        if (extIdx > 0)
        {
            var prefix = normalized[..extIdx];
            var under = prefix.LastIndexOf('_');
            if (under > 0 && under < prefix.Length - 1)
                set.Add($"{prefix[..under]}@{prefix[(under + 1)..]}");
        }
        else if (normalized.Contains('@'))
        {
            // home email → guest UPN prefix (tenant suffix matched loosely in DB query)
            var at = normalized.IndexOf('@');
            var local = normalized[..at];
            var domain = normalized[(at + 1)..];
            set.Add($"{local}_{domain}");
        }

        return set.ToList();
    }
}
