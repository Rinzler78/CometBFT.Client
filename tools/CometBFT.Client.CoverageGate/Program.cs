using System.Globalization;
using System.Xml.Linq;

const decimal threshold = 90m;
var root = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "TestResults");
var excludedPrefixes = new[]
{
    "CometBFT.Client.Core/Domain/",
    "CometBFT.Client.Core/Events/",
    "CometBFT.Client.Core/Exceptions/",
    "CometBFT.Client.Core/Options/",
};
var excludedSuffixes = new[]
{
    "CometBFT.Client.Rest/Json/RpcModels.cs",
    "CometBFT.Client.Grpc/Internal/GrpcChannelBroadcastApiClient.cs",
    "CometBFT.Client.Grpc/Internal/LegacyBroadcastApiClient.cs",
    "CometBFT.Client.Grpc/Internal/BroadcastApiClientFactory.cs",
    "CometBFT.Client.WebSocket/TendermintWebSocketClient.cs",
};

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Coverage root '{root}' does not exist.");
    return 1;
}

var reports = Directory
    .EnumerateFiles(root, "coverage.cobertura.xml", SearchOption.AllDirectories)
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToList();

if (reports.Count == 0)
{
    Console.Error.WriteLine($"No coverage.cobertura.xml files found under '{root}'.");
    return 1;
}

var linesByFile = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
var hitsByFile = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

foreach (var report in reports)
{
    var document = XDocument.Load(report);
    foreach (var classElement in document.Descendants("class"))
    {
        var fileName = classElement.Attribute("filename")?.Value;
        var packageName = classElement.Ancestors("package").FirstOrDefault()?.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            continue;
        }

        var normalized = fileName.Replace('\\', '/').TrimStart('/');
        var srcIndex = normalized.IndexOf("src/", StringComparison.OrdinalIgnoreCase);
        if (srcIndex >= 0)
        {
            normalized = normalized[srcIndex..];
        }

        if (!normalized.StartsWith("CometBFT.Client.", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(packageName)
            && packageName.StartsWith("CometBFT.Client.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{packageName}/{normalized}";
        }

        if (normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/samples/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/tools/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (excludedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || excludedSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }

        if (!linesByFile.TryGetValue(normalized, out var lines))
        {
            lines = [];
            linesByFile[normalized] = lines;
        }

        if (!hitsByFile.TryGetValue(normalized, out var hits))
        {
            hits = [];
            hitsByFile[normalized] = hits;
        }

        foreach (var line in classElement.Descendants("line"))
        {
            var numberText = line.Attribute("number")?.Value;
            var hitsText = line.Attribute("hits")?.Value;
            if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                || !int.TryParse(hitsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hitCount))
            {
                continue;
            }

            lines.Add(number);
            if (hitCount > 0)
            {
                hits.Add(number);
            }
        }
    }
}

if (linesByFile.Count == 0)
{
    Console.Error.WriteLine("No source files were found in the merged coverage reports.");
    return 1;
}

var fileResults = linesByFile
    .Select(kvp =>
    {
        var total = kvp.Value.Count;
        var covered = hitsByFile.TryGetValue(kvp.Key, out var hits) ? hits.Count : 0;
        var percent = total == 0 ? 100m : Math.Round(covered * 100m / total, 2, MidpointRounding.AwayFromZero);
        return new FileCoverage(kvp.Key, covered, total, percent);
    })
    .OrderBy(result => result.Path, StringComparer.OrdinalIgnoreCase)
    .ToList();

var totalLines = fileResults.Sum(result => result.Total);
var coveredLines = fileResults.Sum(result => result.Covered);
var globalPercent = totalLines == 0 ? 100m : Math.Round(coveredLines * 100m / totalLines, 2, MidpointRounding.AwayFromZero);

Console.WriteLine($"Coverage reports: {reports.Count}");
Console.WriteLine($"Global line coverage: {globalPercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({coveredLines}/{totalLines})");

var failingFiles = fileResults.Where(result => result.Percent < threshold).ToList();
if (failingFiles.Count > 0)
{
    Console.Error.WriteLine("Files below 90% line coverage:");
    foreach (var failing in failingFiles)
    {
        Console.Error.WriteLine($" - {failing.Path}: {failing.Percent.ToString("0.00", CultureInfo.InvariantCulture)}% ({failing.Covered}/{failing.Total})");
    }
}

if (globalPercent < threshold)
{
    Console.Error.WriteLine($"Global line coverage gate failed: {globalPercent.ToString("0.00", CultureInfo.InvariantCulture)}% < 90.00%.");
}

return globalPercent >= threshold && failingFiles.Count == 0 ? 0 : 1;

internal sealed record FileCoverage(string Path, int Covered, int Total, decimal Percent);
