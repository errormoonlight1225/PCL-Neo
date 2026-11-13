using System.Security.Cryptography.X509Certificates;
using PCL.Plugin.SDK;

if (args.Length == 0)
{
    PrintUsage();
    return 2;
}

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "cert" when args.Length == 3:
        {
            var password = ReadPassword("PFX password: ");
            if (!string.Equals(password, ReadPassword("Confirm PFX password: "), StringComparison.Ordinal))
                throw new InvalidOperationException("PFX passwords do not match.");
            var pfx = PluginAuthoring.CreateSigningCertificate(args[1], password);
            var outputPath = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await output.WriteAsync(pfx);
            Console.WriteLine($"Created plugin signing certificate: {outputPath}");
            Console.WriteLine("Keep this PFX private and backed up. It is not installed into the Windows certificate store.");
            return 0;
        }
        case "pack" when args.Length == 4:
        {
            var isExtension = CheckPackKind(args[1], args[3]);
            var password = ReadPassword("PFX password: ");
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(args[2], password,
                X509KeyStorageFlags.EphemeralKeySet);
            await PluginAuthoring.CreatePackageAsync(args[1], args[3], certificate);
            Console.WriteLine($"Created signed {(isExtension ? "extension" : "plugin")} package: {Path.GetFullPath(args[3])}");
            return 0;
        }
        case "entry" when args.Length == 3:
        {
            // Prints an index.json entry for a software source, including the SHA-256 the launcher checks.
            var check = await PluginPackage.InspectAsync(args[1]);
            if (check.Error is not null || !check.IsValid) throw new InvalidDataException(check.Error ?? "Package is invalid.");
            if (!Uri.TryCreate(args[2], UriKind.Absolute, out var packageUri) || packageUri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("The package URL must be an absolute HTTPS URL.");
            await using var stream = File.OpenRead(args[1]);
            var sha256 = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream)).ToLowerInvariant();
            var manifest = check.Manifest;
            var entry = new Dictionary<string, object?>
            {
                ["id"] = manifest.Id,
                ["name"] = manifest.Name,
                ["version"] = manifest.Version,
                ["author"] = manifest.Author,
                ["description"] = manifest.Description,
                ["packageUrl"] = packageUri.AbsoluteUri,
                ["sha256"] = sha256
            };
            if (manifest.Kind == PluginManifestKind.Extension) entry["extensionApi"] = manifest.ExtensionApi;
            if (manifest.Website is not null) entry["website"] = manifest.Website;
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(entry,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
            if (!check.IsSigned)
                Console.Error.WriteLine("Warning: the package is unsigned. The official source rejects it; third-party sources show a red warning.");
            return 0;
        }
        default:
            PrintUsage();
            return 2;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

// Returns whether the directory holds an extension, after checking the output extension matches.
static bool CheckPackKind(string sourceDirectory, string outputPath)
{
    var manifestPath = Path.Combine(sourceDirectory, PluginPackage.ManifestFileName);
    if (!File.Exists(manifestPath)) throw new FileNotFoundException("plugin.json is missing.", manifestPath);
    var manifest = PluginPackage.DeserializeManifest(File.ReadAllBytes(manifestPath));
    var isExtension = manifest?.Kind == PluginManifestKind.Extension;
    var expected = isExtension ? PluginPackage.ExtensionPackageExtension : PluginPackage.Extension;
    if (!outputPath.EndsWith(expected, StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException($"A {(isExtension ? "extension" : "plugin")} package must be written to a {expected} file.");
    if (isExtension && (!string.IsNullOrWhiteSpace(manifest!.MixinConfig) || manifest.MixinConfigs.Count > 0 ||
                        manifest.ExperimentalFeatures.Count > 0))
        throw new InvalidDataException("Extension packages may not contain Mixin configurations.");
    return isExtension;
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key is ConsoleKey.Enter) break;
        if (key.Key is ConsoleKey.Backspace && password.Length > 0) password.Length--;
        else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
    }
    Console.WriteLine();
    return password.ToString();
}

static void PrintUsage()
{
    Console.WriteLine("PCL Plugin and extension authoring tool");
    Console.WriteLine("  PCL.Plugin.Tools cert <subject> <output.pfx>");
    Console.WriteLine("  PCL.Plugin.Tools pack <plugin-directory> <certificate.pfx> <output.pclplugin>");
    Console.WriteLine("  PCL.Plugin.Tools pack <extension-directory> <certificate.pfx> <output.pclext>");
    Console.WriteLine("  PCL.Plugin.Tools entry <package> <https-package-url>   (prints an index.json entry)");
    Console.WriteLine("Passwords are prompted without echo; do not pass secrets as command-line arguments.");
}
