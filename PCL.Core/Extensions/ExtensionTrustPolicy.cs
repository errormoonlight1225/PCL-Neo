using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Plugin.SDK;

namespace PCL.Core.Extensions;

/// <summary>Where an extension package comes from.</summary>
public enum ExtensionPackageOrigin
{
    /// <summary>The built-in official software source.</summary>
    Official,

    /// <summary>A software source the user added.</summary>
    ThirdParty,

    /// <summary>A file the user picked or dropped.</summary>
    LocalFile
}

/// <summary>Result of <see cref="ExtensionTrustPolicy.Evaluate" />.</summary>
public sealed record ExtensionTrustDecision(bool Allowed, bool IsUnsigned, string? Reason)
{
    public static ExtensionTrustDecision Deny(string reason) => new(false, false, reason);
}

/// <summary>
///     Official source: the package must be signed by one of the official signing certificates
///     compiled into this build. Third-party sources and local files: unsigned packages are allowed
///     but flagged so the install dialog shows a red warning; signed packages go through the normal
///     signer-change checks.
/// </summary>
public static class ExtensionTrustPolicy
{
    public static ExtensionTrustDecision Evaluate(PluginPackageCheck check, ExtensionPackageOrigin origin,
        IReadOnlyCollection<string> officialSignerThumbprints)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (check.Error is not null) return ExtensionTrustDecision.Deny(check.Error);
        if (check.IsSigned && !check.SignatureValid) return ExtensionTrustDecision.Deny("The package signature is invalid.");

        if (origin == ExtensionPackageOrigin.Official)
        {
            if (!check.IsSigned) return ExtensionTrustDecision.Deny("Extensions from the official source must be signed.");
            if (officialSignerThumbprints.Count == 0)
                return ExtensionTrustDecision.Deny("This build has no official extension signer configured.");
            var signer = NormalizeThumbprint(check.SignerThumbprint);
            if (signer.Length == 0 || !officialSignerThumbprints.Any(item => NormalizeThumbprint(item) == signer))
                return ExtensionTrustDecision.Deny("The package is not signed by the official extension certificate.");
            return new ExtensionTrustDecision(true, false, null);
        }

        return new ExtensionTrustDecision(true, !check.IsSigned, null);
    }

    /// <summary>Parses a comma, semicolon or whitespace separated thumbprint list.</summary>
    public static IReadOnlyList<string> ParseThumbprints(string? value) =>
        (value ?? "").Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeThumbprint)
            .Where(item => item.Length is 40 or 64)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>Upper-case hex without separators, so <c>ab:cd</c> and <c>ABCD</c> compare equal.</summary>
    public static string NormalizeThumbprint(string? value) =>
        new string((value ?? "").Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
}
