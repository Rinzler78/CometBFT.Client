using System.Globalization;
using System.Xml.Linq;

const decimal threshold = 90m;
var root = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "TestResults");
var excludedPrefixes = Array.Empty<string>();
var excludedSuffixes = Array.Empty<string>();

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

var fileMetrics = new Dictionary<string, CoverageBucket>(StringComparer.OrdinalIgnoreCase);
var methodMetrics = new Dictionary<string, CoverageBucket>(StringComparer.OrdinalIgnoreCase);

foreach (var report in reports)
{
    var document = XDocument.Load(report);
    foreach (var packageElement in document.Descendants("package"))
    {
        var packageName = packageElement.Attribute("name")?.Value;
        foreach (var classElement in packageElement.Descendants("class"))
        {
            var fileName = classElement.Attribute("filename")?.Value;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var normalizedFile = NormalizePath(fileName, packageName);
            if (normalizedFile is null)
            {
                continue;
            }

            var fileBucket = GetOrCreateBucket(fileMetrics, normalizedFile);
            MergeLines(fileBucket, classElement.Descendants("line"));

            foreach (var methodElement in classElement.Descendants("method"))
            {
                var className = classElement.Attribute("name")?.Value ?? "<unknown-class>";
                var methodName = methodElement.Attribute("name")?.Value ?? "<unknown>";
                var methodSignature = methodElement.Attribute("signature")?.Value ?? string.Empty;
                var firstLineNumber = methodElement.Descendants("line")
                    .Select(line => line.Attribute("number")?.Value)
                    .Where(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    .Select(value => int.Parse(value!, CultureInfo.InvariantCulture))
                    .DefaultIfEmpty(0)
                    .Min();
                var methodKey = $"{normalizedFile}::{className}::{methodName}{methodSignature}@L{firstLineNumber}";
                var methodBucket = GetOrCreateBucket(methodMetrics, methodKey);
                MergeLines(methodBucket, methodElement.Descendants("line"));
            }
        }
    }
}

if (fileMetrics.Count == 0)
{
    Console.Error.WriteLine("No source files were found in the merged coverage reports.");
    return 1;
}

var fileResults = fileMetrics
    .Select(kvp => CoverageResult.FromBucket(kvp.Key, kvp.Value))
    .OrderBy(result => result.Key, StringComparer.OrdinalIgnoreCase)
    .ToList();

var methodResults = methodMetrics
    .Select(kvp => CoverageResult.FromBucket(kvp.Key, kvp.Value))
    .OrderBy(result => result.Key, StringComparer.OrdinalIgnoreCase)
    .ToList();

var globalLinesCovered = fileResults.Sum(result => result.LinesCovered);
var globalLinesTotal = fileResults.Sum(result => result.LinesTotal);
var globalLinePercent = Percentage(globalLinesCovered, globalLinesTotal);

var globalBranchesCovered = fileResults.Sum(result => result.BranchesCovered);
var globalBranchesTotal = fileResults.Sum(result => result.BranchesTotal);
var globalBranchPercent = Percentage(globalBranchesCovered, globalBranchesTotal);

Console.WriteLine($"Coverage reports: {reports.Count}");
Console.WriteLine($"Global line coverage: {globalLinePercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({globalLinesCovered}/{globalLinesTotal})");
Console.WriteLine($"Global branch coverage: {globalBranchPercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({globalBranchesCovered}/{globalBranchesTotal})");

var failingFileLines = fileResults.Where(result => result.LinePercent < threshold).ToList();
var failingFileBranches = fileResults.Where(result => result.BranchesTotal > 0 && result.BranchPercent < threshold).ToList();
var failingMethodLines = methodResults.Where(result => result.LinesTotal > 0 && result.LinePercent < threshold).ToList();
var failingMethodBranches = methodResults.Where(result => result.BranchesTotal > 0 && result.BranchPercent < threshold).ToList();

PrintFailures("Files below 90% line coverage:", failingFileLines, result =>
    $" - {result.Key}: {result.LinePercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({result.LinesCovered}/{result.LinesTotal})");
PrintFailures("Files below 90% branch coverage:", failingFileBranches, result =>
    $" - {result.Key}: {result.BranchPercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({result.BranchesCovered}/{result.BranchesTotal})");
PrintFailures("Methods below 90% line coverage:", failingMethodLines, result =>
    $" - {result.Key}: {result.LinePercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({result.LinesCovered}/{result.LinesTotal})");
PrintFailures("Methods below 90% branch coverage:", failingMethodBranches, result =>
    $" - {result.Key}: {result.BranchPercent.ToString("0.00", CultureInfo.InvariantCulture)}% ({result.BranchesCovered}/{result.BranchesTotal})");

if (globalLinePercent < threshold)
{
    Console.Error.WriteLine($"Global line coverage gate failed: {globalLinePercent.ToString("0.00", CultureInfo.InvariantCulture)}% < 90.00%.");
}

if (globalBranchPercent < threshold)
{
    Console.Error.WriteLine($"Global branch coverage gate failed: {globalBranchPercent.ToString("0.00", CultureInfo.InvariantCulture)}% < 90.00%.");
}

var passed = globalLinePercent >= threshold
    && globalBranchPercent >= threshold
    && failingFileLines.Count == 0
    && failingFileBranches.Count == 0
    && failingMethodLines.Count == 0
    && failingMethodBranches.Count == 0;

return passed ? 0 : 1;

void MergeLines(CoverageBucket bucket, IEnumerable<XElement> lineElements)
{
    foreach (var line in lineElements)
    {
        var numberText = line.Attribute("number")?.Value;
        var hitsText = line.Attribute("hits")?.Value;
        if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            || !int.TryParse(hitsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hitCount))
        {
            continue;
        }

        bucket.Lines[number] = hitCount > 0 || bucket.Lines.GetValueOrDefault(number);

        if (line.Attribute("branch")?.Value != "True")
        {
            continue;
        }

        var conditionCoverage = line.Attribute("condition-coverage")?.Value;
        if (string.IsNullOrWhiteSpace(conditionCoverage)
            || !TryParseConditionCoverage(conditionCoverage, out var coveredBranches, out var totalBranches))
        {
            continue;
        }

        if (bucket.Branches.TryGetValue(number, out var existing))
        {
            bucket.Branches[number] = new BranchMetric(
                Covered: Math.Max(existing.Covered, coveredBranches),
                Total: Math.Max(existing.Total, totalBranches));
        }
        else
        {
            bucket.Branches[number] = new BranchMetric(coveredBranches, totalBranches);
        }
    }
}

CoverageBucket GetOrCreateBucket(IDictionary<string, CoverageBucket> dictionary, string key)
{
    if (!dictionary.TryGetValue(key, out var bucket))
    {
        bucket = new CoverageBucket();
        dictionary[key] = bucket;
    }

    return bucket;
}

string? NormalizePath(string fileName, string? packageName)
{
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
        return null;
    }

    if (excludedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        || excludedSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
    {
        return null;
    }

    return normalized;
}

bool TryParseConditionCoverage(string text, out int covered, out int total)
{
    covered = 0;
    total = 0;

    var openParen = text.IndexOf('(');
    var slash = text.IndexOf('/');
    var closeParen = text.IndexOf(')');
    if (openParen < 0 || slash < 0 || closeParen < 0 || slash < openParen || closeParen < slash)
    {
        return false;
    }

    var coveredText = text[(openParen + 1)..slash];
    var totalText = text[(slash + 1)..closeParen];
    return int.TryParse(coveredText, NumberStyles.Integer, CultureInfo.InvariantCulture, out covered)
        && int.TryParse(totalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out total);
}

decimal Percentage(int covered, int total) => total == 0 ? 100m : Math.Round(covered * 100m / total, 2, MidpointRounding.AwayFromZero);

void PrintFailures(string heading, IReadOnlyCollection<CoverageResult> failures, Func<CoverageResult, string> formatter)
{
    if (failures.Count == 0)
    {
        return;
    }

    Console.Error.WriteLine(heading);
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(formatter(failure));
    }
}

internal sealed class CoverageBucket
{
    public Dictionary<int, bool> Lines { get; } = new();

    public Dictionary<int, BranchMetric> Branches { get; } = new();
}

internal sealed record BranchMetric(int Covered, int Total);

internal sealed record CoverageResult(
    string Key,
    int LinesCovered,
    int LinesTotal,
    decimal LinePercent,
    int BranchesCovered,
    int BranchesTotal,
    decimal BranchPercent)
{
    public static CoverageResult FromBucket(string key, CoverageBucket bucket)
    {
        var linesTotal = bucket.Lines.Count;
        var linesCovered = bucket.Lines.Values.Count(hit => hit);
        var branchesCovered = bucket.Branches.Values.Sum(metric => metric.Covered);
        var branchesTotal = bucket.Branches.Values.Sum(metric => metric.Total);
        return new CoverageResult(
            key,
            linesCovered,
            linesTotal,
            linesTotal == 0 ? 100m : Math.Round(linesCovered * 100m / linesTotal, 2, MidpointRounding.AwayFromZero),
            branchesCovered,
            branchesTotal,
            branchesTotal == 0 ? 100m : Math.Round(branchesCovered * 100m / branchesTotal, 2, MidpointRounding.AwayFromZero));
    }
}
