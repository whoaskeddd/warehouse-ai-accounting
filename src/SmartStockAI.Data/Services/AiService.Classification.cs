using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Transforms.Text;
using Microsoft.ML.Transforms;
using Microsoft.ML.Trainers;
using SmartStockAI.Core.Contracts.AI;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Services;

public sealed partial class AiService
{
    private async Task<(int TrainingRowsCount, decimal? ModelScore)> TrainCategoryModelAsync(
        IReadOnlyList<Product> products,
        string modelRoot,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rows = products
            .Where(x => x.CategoryId.HasValue)
            .Select(x => new CategoryTrainingRow
            {
                Label = x.CategoryId!.Value.ToString(),
                Name = NormalizeText(x.Name)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();

        var distinctLabels = rows.Select(x => x.Label).Distinct().Count();
        var artifactPath = Path.Combine(modelRoot, "classification", "categories.zip");

        dbContext.ModelTrainingInfos.RemoveRange(dbContext.ModelTrainingInfos.Where(x => x.ModelType == CategoryModelType && x.ScopeType == GlobalScope));

        if (rows.Count < 4 || distinctLabels < 2)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return (rows.Count, null);
        }

        var data = MlContext.Data.LoadFromEnumerable(rows);
        var split = MlContext.Data.TrainTestSplit(data, testFraction: rows.Count >= 10 ? 0.2 : 0.01);

        var pipeline = Microsoft.ML.ConversionsExtensionsCatalog.MapValueToKey(
                MlContext.Transforms.Conversion,
                outputColumnName: "Label",
                inputColumnName: nameof(CategoryTrainingRow.Label))
            .Append(Microsoft.ML.TextCatalog.FeaturizeText(
                MlContext.Transforms.Text,
                outputColumnName: "Features",
                inputColumnName: nameof(CategoryTrainingRow.Name)))
            .Append(Microsoft.ML.StandardTrainersCatalog.SdcaMaximumEntropy(
                MlContext.MulticlassClassification.Trainers,
                labelColumnName: "Label",
                featureColumnName: "Features"))
            .Append(Microsoft.ML.ConversionsExtensionsCatalog.MapKeyToValue(
                MlContext.Transforms.Conversion,
                outputColumnName: "PredictedLabel",
                inputColumnName: "PredictedLabel"));

        var model = pipeline.Fit(split.TrainSet);
        var transformed = model.Transform(split.TestSet);
        var metrics = MlContext.MulticlassClassification.Evaluate(transformed, labelColumnName: "Label");

        await using (var stream = File.Create(artifactPath))
        {
            MlContext.Model.Save(model, data.Schema, stream);
        }

        dbContext.ModelTrainingInfos.Add(new ModelTrainingInfo
        {
            ModelType = CategoryModelType,
            ScopeType = GlobalScope,
            TrainedAtUtc = now,
            TrainingRowsCount = rows.Count,
            QualityMetric = (decimal)metrics.MicroAccuracy,
            ArtifactPath = artifactPath,
            Notes = $"Distinct categories: {distinctLabels}"
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return (rows.Count, (decimal)metrics.MicroAccuracy);
    }

    private async Task<AiCategoryRecommendationDto?> SuggestCategoryByTokensAsync(string normalizedName, CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var productGroups = await dbContext.Products
            .AsNoTracking()
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(x => new
            {
                CategoryId = x.Key,
                Names = x.Select(y => y.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        var inputTokens = Tokenize(normalizedName);
        if (inputTokens.Count == 0)
        {
            return null;
        }

        var scored = productGroups
            .Select(group =>
            {
                var tokens = group.Names.SelectMany(Tokenize).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var overlap = inputTokens.Count(token => tokens.Contains(token));
                var score = tokens.Count == 0 ? 0m : (decimal)overlap / Math.Max(tokens.Count, inputTokens.Count);
                return new { group.CategoryId, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (scored is null || scored.Score <= 0)
        {
            return null;
        }

        var category = categories.FirstOrDefault(x => x.Id == scored.CategoryId);
        if (category is null)
        {
            return null;
        }

        return new AiCategoryRecommendationDto
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            Confidence = Math.Min(0.74m, scored.Score),
            IsStrongRecommendation = false
        };
    }

    private static string NormalizeText(string value)
    {
        return string.Join(' ', Tokenize(value));
    }

    private static List<string> Tokenize(string value)
    {
        return value
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '/', '\\', '-', '_', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 1)
            .ToList();
    }

    private static decimal CalculateConfidence(float[]? scores)
    {
        if (scores is null || scores.Length == 0)
        {
            return 0m;
        }

        var exps = scores.Select(MathF.Exp).ToArray();
        var total = exps.Sum();
        if (total <= 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)(exps.Max() / total), 4, MidpointRounding.AwayFromZero);
    }

    private sealed class CategoryTrainingRow
    {
        public string Label { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    private sealed class CategoryPrediction
    {
        [Microsoft.ML.Data.ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        public float[] Score { get; set; } = [];
    }
}
