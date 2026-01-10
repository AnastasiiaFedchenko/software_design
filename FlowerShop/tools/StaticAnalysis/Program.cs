using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var argsList = args.ToList();
string rootPath = GetArg(argsList, "--path") ?? Directory.GetCurrentDirectory();
string outDir = GetArg(argsList, "--out-dir") ?? Path.Combine(rootPath, "analysis");
int maxCyclomatic = int.TryParse(GetArg(argsList, "--max-cyclomatic"), out var max) ? max : 10;

Directory.CreateDirectory(outDir);

var metrics = new List<MethodMetrics>();

foreach (var file in EnumerateSourceFiles(rootPath))
{
    var text = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(text);
    var root = tree.GetRoot();

    foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
    {
        if (method.Body == null && method.ExpressionBody == null)
        {
            continue;
        }

        string methodName = BuildMethodName(method);
        int cyclomatic = CalculateCyclomaticComplexity(method);
        var halstead = CalculateHalstead(method);

        metrics.Add(new MethodMetrics
        {
            File = Path.GetRelativePath(rootPath, file),
            Method = methodName,
            Cyclomatic = cyclomatic,
            HalsteadVolume = halstead.Volume,
            HalsteadDifficulty = halstead.Difficulty,
            HalsteadEffort = halstead.Effort,
            OperatorsDistinct = halstead.OperatorsDistinct,
            OperandsDistinct = halstead.OperandsDistinct,
            OperatorsTotal = halstead.OperatorsTotal,
            OperandsTotal = halstead.OperandsTotal
        });
    }
}

WriteCsv(Path.Combine(outDir, "metrics.csv"), metrics);
File.WriteAllText(
    Path.Combine(outDir, "metrics.json"),
    JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true })
);

var violations = metrics.Where(m => m.Cyclomatic > maxCyclomatic).ToList();
if (violations.Count > 0)
{
    Console.Error.WriteLine($"Cyclomatic complexity violations (max {maxCyclomatic}):");
    foreach (var v in violations.OrderByDescending(v => v.Cyclomatic))
    {
        Console.Error.WriteLine($"{v.File}: {v.Method} -> {v.Cyclomatic}");
    }
    Environment.Exit(1);
}

Console.WriteLine($"OK. Methods analyzed: {metrics.Count}");

static IEnumerable<string> EnumerateSourceFiles(string rootPath)
{
    var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "TestResults", "Migrations", ".git", "load-testing"
    };

    return Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(excluded.Contains));
}

static string BuildMethodName(BaseMethodDeclarationSyntax method)
{
    var parentType = method.Parent as TypeDeclarationSyntax;
    var typeName = parentType?.Identifier.Text ?? "UnknownType";

    return method switch
    {
        MethodDeclarationSyntax m => $"{typeName}.{m.Identifier.Text}",
        ConstructorDeclarationSyntax c => $"{typeName}.{c.Identifier.Text}",
        _ => $"{typeName}.(method)"
    };
}

static int CalculateCyclomaticComplexity(SyntaxNode method)
{
    int complexity = 1;

    complexity += method.DescendantNodes().OfType<IfStatementSyntax>().Count();
    complexity += method.DescendantNodes().OfType<ForStatementSyntax>().Count();
    complexity += method.DescendantNodes().OfType<ForEachStatementSyntax>().Count();
    complexity += method.DescendantNodes().OfType<WhileStatementSyntax>().Count();
    complexity += method.DescendantNodes().OfType<DoStatementSyntax>().Count();
    complexity += method.DescendantNodes().OfType<ConditionalExpressionSyntax>().Count();
    complexity += method.DescendantNodes().OfType<CatchClauseSyntax>().Count();
    complexity += method.DescendantNodes().OfType<CaseSwitchLabelSyntax>().Count();
    complexity += method.DescendantNodes()
        .OfType<BinaryExpressionSyntax>()
        .Count(expr => expr.IsKind(SyntaxKind.CoalesceExpression));

    complexity += method.DescendantTokens()
        .Count(token => token.IsKind(SyntaxKind.AmpersandAmpersandToken) ||
                        token.IsKind(SyntaxKind.BarBarToken));

    return complexity;
}

static HalsteadMetrics CalculateHalstead(SyntaxNode method)
{
    var operators = new HashSet<string>();
    var operands = new HashSet<string>();
    int operatorsTotal = 0;
    int operandsTotal = 0;

    foreach (var token in method.DescendantTokens())
    {
        var kind = token.Kind();

        if (kind is SyntaxKind.IdentifierToken ||
            kind is SyntaxKind.StringLiteralToken ||
            kind is SyntaxKind.NumericLiteralToken ||
            kind is SyntaxKind.CharacterLiteralToken ||
            kind is SyntaxKind.InterpolatedStringTextToken)
        {
            operands.Add(token.ValueText);
            operandsTotal += 1;
            continue;
        }

        if (IsExcludedToken(kind))
        {
            continue;
        }

        var text = token.Text;
        operators.Add(text);
        operatorsTotal += 1;
    }

    int n1 = operators.Count;
    int n2 = operands.Count;
    int N1 = operatorsTotal;
    int N2 = operandsTotal;

    double n = n1 + n2;
    double N = N1 + N2;
    double volume = n == 0 ? 0 : N * Math.Log2(n);
    double difficulty = n2 == 0 ? 0 : (n1 / 2.0) * (N2 / (double)n2);
    double effort = difficulty * volume;

    return new HalsteadMetrics(n1, n2, N1, N2, volume, difficulty, effort);
}

static bool IsExcludedToken(SyntaxKind kind)
{
    return kind is SyntaxKind.OpenParenToken
        or SyntaxKind.CloseParenToken
        or SyntaxKind.OpenBraceToken
        or SyntaxKind.CloseBraceToken
        or SyntaxKind.OpenBracketToken
        or SyntaxKind.CloseBracketToken
        or SyntaxKind.SemicolonToken
        or SyntaxKind.CommaToken
        or SyntaxKind.EndOfFileToken;
}

static string? GetArg(List<string> args, string name)
{
    var index = args.FindIndex(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (index == -1 || index == args.Count - 1)
    {
        return null;
    }
    return args[index + 1];
}

static void WriteCsv(string path, IEnumerable<MethodMetrics> metrics)
{
    using var writer = new StreamWriter(path);
    writer.WriteLine("file,method,cyclomatic,halstead_volume,halstead_difficulty,halstead_effort,operators_distinct,operands_distinct,operators_total,operands_total");
    foreach (var m in metrics)
    {
        writer.WriteLine(string.Join(",",
            Escape(m.File),
            Escape(m.Method),
            m.Cyclomatic.ToString(CultureInfo.InvariantCulture),
            m.HalsteadVolume.ToString(CultureInfo.InvariantCulture),
            m.HalsteadDifficulty.ToString(CultureInfo.InvariantCulture),
            m.HalsteadEffort.ToString(CultureInfo.InvariantCulture),
            m.OperatorsDistinct.ToString(CultureInfo.InvariantCulture),
            m.OperandsDistinct.ToString(CultureInfo.InvariantCulture),
            m.OperatorsTotal.ToString(CultureInfo.InvariantCulture),
            m.OperandsTotal.ToString(CultureInfo.InvariantCulture)
        ));
    }
}

static string Escape(string value)
{
    if (!value.Contains(',') && !value.Contains('"'))
    {
        return value;
    }
    return $"\"{value.Replace("\"", "\"\"")}\"";
}

record HalsteadMetrics(
    int OperatorsDistinct,
    int OperandsDistinct,
    int OperatorsTotal,
    int OperandsTotal,
    double Volume,
    double Difficulty,
    double Effort
);

record MethodMetrics
{
    public string File { get; init; } = "";
    public string Method { get; init; } = "";
    public int Cyclomatic { get; init; }
    public double HalsteadVolume { get; init; }
    public double HalsteadDifficulty { get; init; }
    public double HalsteadEffort { get; init; }
    public int OperatorsDistinct { get; init; }
    public int OperandsDistinct { get; init; }
    public int OperatorsTotal { get; init; }
    public int OperandsTotal { get; init; }
}
