using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TimeSeries;
using Microsoft.ML.Transforms.TimeSeries;
using SmartStockAI.Core.Contracts.AI;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed partial class AiService
{
    private void EnsureAuthenticatedWhenAvailable()
    {
        if (currentUserAccessor.IsAuthenticated)
        {
            AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);
        }
    }

    private string EnsureModelDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, _options.ModelDirectoryName);
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "forecast"));
        Directory.CreateDirectory(Path.Combine(directory, "classification"));
        return directory;
    }

    private async Task<Dictionary<int, decimal>> RebuildExpectedInboundAsync(DateTime now, CancellationToken cancellationToken)
    {
        var draftInbound = await dbContext.StockDocuments
            .AsNoTracking()
            .Where(x => x.Type == Core.Enums.StockDocumentType.Receipt && x.Status == Core.Enums.StockDocumentStatus.Draft)
            .SelectMany(x => x.Lines)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToListAsync(cancellationToken);

        var existing = await dbContext.ExpectedInboundSnapshots.ToListAsync(cancellationToken);
        dbContext.ExpectedInboundSnapshots.RemoveRange(existing);
        foreach (var item in draftInbound)
        {
            dbContext.ExpectedInboundSnapshots.Add(new ExpectedInboundSnapshot
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                CalculatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return draftInbound.ToDictionary(x => x.ProductId, x => x.Quantity);
    }

    private async Task<Dictionary<int, decimal>> BuildCategoryForecastsAsync(
        IReadOnlyList<Category> categories,
        IReadOnlyList<Product> products,
        IReadOnlyList<StockMovement> issueMovements,
        IReadOnlyDictionary<int, decimal> expectedInboundByProduct,
        string modelRoot,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dbContext.ForecastSnapshots.RemoveRange(dbContext.ForecastSnapshots.Where(x => x.ScopeType == CategoryScope));
        dbContext.ModelTrainingInfos.RemoveRange(dbContext.ModelTrainingInfos.Where(x => x.ModelType == ForecastModelType && x.ScopeType == CategoryScope));
        await dbContext.SaveChangesAsync(cancellationToken);

        var productsByCategory = products
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var categoryForecasts = new Dictionary<int, decimal>();

        foreach (var category in categories)
        {
            productsByCategory.TryGetValue(category.Id, out var categoryProducts);
            categoryProducts ??= [];
            var productIds = categoryProducts.Select(x => x.Id).ToHashSet();
            var monthlySeries = BuildMonthlySeries(issueMovements.Where(x => productIds.Contains(x.ProductId)));
            var expectedInbound = categoryProducts.Sum(x => expectedInboundByProduct.GetValueOrDefault(x.Id));

            var forecastResult = TrainForecastModel(
                $"{CategoryScope.ToLowerInvariant()}-{category.Id}",
                monthlySeries,
                modelRoot);

            var availableStock = categoryProducts.Sum(x => x.CurrentStock - x.ReservedStock);
            var snapshot = CreateSnapshot(
                CategoryScope,
                category.Id,
                category.Name,
                now,
                monthlySeries.Count,
                forecastResult,
                availableStock,
                expectedInbound,
                null);

            dbContext.ForecastSnapshots.Add(snapshot);
            dbContext.ModelTrainingInfos.Add(new ModelTrainingInfo
            {
                ModelType = ForecastModelType,
                ScopeType = CategoryScope,
                ScopeId = category.Id,
                TrainedAtUtc = now,
                TrainingRowsCount = monthlySeries.Count,
                QualityMetric = forecastResult.Mape,
                ArtifactPath = forecastResult.ArtifactPath,
                Notes = $"Average monthly demand: {snapshot.AverageMonthlyDemand:0.###}"
            });

            categoryForecasts[category.Id] = snapshot.AverageMonthlyDemand;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return categoryForecasts;
    }

    private async Task<int> BuildProductForecastsAsync(
        IReadOnlyList<Product> products,
        IReadOnlyList<StockMovement> issueMovements,
        IReadOnlyDictionary<int, decimal> expectedInboundByProduct,
        IReadOnlyDictionary<int, decimal> categoryForecasts,
        string modelRoot,
        DateTime now,
        CancellationToken cancellationToken)
    {
        dbContext.ForecastSnapshots.RemoveRange(dbContext.ForecastSnapshots.Where(x => x.ScopeType == ProductScope));
        dbContext.ModelTrainingInfos.RemoveRange(dbContext.ModelTrainingInfos.Where(x => x.ModelType == ForecastModelType && x.ScopeType == ProductScope));
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var product in products)
        {
            var monthlySeries = BuildMonthlySeries(issueMovements.Where(x => x.ProductId == product.Id));
            ForecastComputationResult forecastResult;
            string? sourceScopeType = null;
            int? sourceScopeId = null;
            string? sourceScopeName = null;

            if (monthlySeries.Count < _options.MinimumHistoryMonthsForForecast)
            {
                sourceScopeType = CategoryScope;
                sourceScopeId = ResolveFallbackCategoryId(product);
                sourceScopeName = product.Category?.ParentCategory?.Name ?? product.Category?.Name;
                var fallbackMonthly = sourceScopeId.HasValue && categoryForecasts.TryGetValue(sourceScopeId.Value, out var categoryMonthly)
                    ? categoryMonthly
                    : monthlySeries.Values.DefaultIfEmpty(0m).Average();
                forecastResult = CreateFallbackForecast(
                    monthlySeries,
                    fallbackMonthly,
                    Path.Combine(modelRoot, "forecast", $"fallback-product-{product.Id}.json"));
            }
            else
            {
                forecastResult = TrainForecastModel(
                    $"{ProductScope.ToLowerInvariant()}-{product.Id}",
                    monthlySeries,
                    modelRoot);
            }

            var expectedInbound = expectedInboundByProduct.GetValueOrDefault(product.Id);
            var snapshot = CreateSnapshot(
                ProductScope,
                product.Id,
                product.Name,
                now,
                monthlySeries.Count,
                forecastResult,
                product.CurrentStock - product.ReservedStock,
                expectedInbound,
                sourceScopeType,
                sourceScopeId,
                sourceScopeName);

            dbContext.ForecastSnapshots.Add(snapshot);
            dbContext.ModelTrainingInfos.Add(new ModelTrainingInfo
            {
                ModelType = ForecastModelType,
                ScopeType = ProductScope,
                ScopeId = product.Id,
                TrainedAtUtc = now,
                TrainingRowsCount = monthlySeries.Count,
                QualityMetric = forecastResult.Mape,
                ArtifactPath = forecastResult.ArtifactPath,
                Notes = forecastResult.UsesFallback ? $"Fallback source: {sourceScopeName ?? "None"}" : null
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return products.Count;
    }

    private ForecastSnapshot CreateSnapshot(
        string scopeType,
        int scopeId,
        string scopeName,
        DateTime now,
        int historyMonthsCount,
        ForecastComputationResult forecastResult,
        decimal availableStock,
        decimal expectedInbound,
        string? sourceScopeType,
        int? sourceScopeId = null,
        string? sourceScopeName = null)
    {
        var monthlyForecast = forecastResult.Forecast.FirstOrDefault()?.Quantity ?? 0m;
        var forecastLeadTime = monthlyForecast * _options.LeadTimeMonths;
        var safetyStock = monthlyForecast * _options.SafetyStockMonths;
        var recommendedOrder = Math.Max(0m, (forecastLeadTime + safetyStock) - availableStock - expectedInbound);
        var projectedDeficit = Math.Max(0m, (forecastLeadTime + safetyStock) - availableStock - expectedInbound);

        var payload = new ForecastSnapshotPayload
        {
            History = forecastResult.History,
            Forecast = forecastResult.Forecast
        };

        return new ForecastSnapshot
        {
            ScopeType = scopeType,
            ScopeId = scopeId,
            ScopeName = scopeName,
            GeneratedAtUtc = now,
            HistoryMonthsCount = historyMonthsCount,
            UsesFallback = forecastResult.UsesFallback,
            SourceScopeType = sourceScopeType,
            SourceScopeId = sourceScopeId,
            SourceScopeName = sourceScopeName,
            AverageMonthlyDemand = monthlyForecast,
            ForecastLeadTime = forecastLeadTime,
            SafetyStock = safetyStock,
            ExpectedInbound = expectedInbound,
            RecommendedOrder = recommendedOrder,
            ProjectedDeficit = projectedDeficit,
            ModelQuality = forecastResult.Mape,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            ArtifactPath = forecastResult.ArtifactPath
        };
    }

    private ForecastComputationResult TrainForecastModel(
        string artifactKey,
        SortedDictionary<DateOnly, decimal> monthlySeries,
        string modelRoot)
    {
        if (monthlySeries.Count < _options.MinimumHistoryMonthsForForecast)
        {
            return CreateFallbackForecast(
                monthlySeries,
                monthlySeries.Values.DefaultIfEmpty(0m).Average(),
                Path.Combine(modelRoot, "forecast", $"{artifactKey}.json"));
        }

        const int horizon = 12;
        var seriesValues = monthlySeries.Values.Select(x => (float)x).ToArray();
        var observations = seriesValues.Select(x => new DemandObservation { Value = x }).ToList();
        var data = MlContext.Data.LoadFromEnumerable(observations);

        var windowSize = Math.Max(2, Math.Min(6, Math.Max(2, seriesValues.Length / 2)));
        var pipeline = Microsoft.ML.TimeSeriesCatalog.ForecastBySsa(
            MlContext.Forecasting,
            outputColumnName: nameof(DemandForecast.ForecastedValues),
            inputColumnName: nameof(DemandObservation.Value),
            windowSize: windowSize,
            seriesLength: seriesValues.Length,
            trainSize: seriesValues.Length,
            horizon: horizon,
            confidenceLevel: 0.9f,
            confidenceLowerBoundColumn: nameof(DemandForecast.LowerBound),
            confidenceUpperBoundColumn: nameof(DemandForecast.UpperBound));

        var model = pipeline.Fit(data);
        var artifactPath = Path.Combine(modelRoot, "forecast", $"{artifactKey}.zip");
        using (var stream = File.Create(artifactPath))
        {
            MlContext.Model.Save(model, data.Schema, stream);
        }

        var engine = PredictionFunctionExtensions.CreateTimeSeriesEngine<DemandObservation, DemandForecast>(model, MlContext);
        var prediction = engine.Predict();
        var lastPeriod = monthlySeries.Keys.LastOrDefault();
        var forecast = new List<ForecastPointPayload>(horizon);

        for (var i = 0; i < horizon; i++)
        {
            forecast.Add(new ForecastPointPayload
            {
                Period = lastPeriod.AddMonths(i + 1),
                Quantity = Sanitize(prediction.ForecastedValues.ElementAtOrDefault(i)),
                LowerBound = Sanitize(prediction.LowerBound.ElementAtOrDefault(i)),
                UpperBound = Sanitize(prediction.UpperBound.ElementAtOrDefault(i))
            });
        }

        var history = monthlySeries
            .Select(x => new ForecastPointPayload
            {
                Period = x.Key,
                Quantity = x.Value,
                LowerBound = x.Value,
                UpperBound = x.Value
            })
            .ToList();

        return new ForecastComputationResult
        {
            History = history,
            Forecast = forecast,
            UsesFallback = false,
            ArtifactPath = artifactPath,
            Mape = EstimateMape(monthlySeries)
        };
    }

    private ForecastComputationResult CreateFallbackForecast(
        SortedDictionary<DateOnly, decimal> monthlySeries,
        decimal fallbackMonthly,
        string artifactPath)
    {
        var history = monthlySeries
            .Select(x => new ForecastPointPayload
            {
                Period = x.Key,
                Quantity = x.Value,
                LowerBound = x.Value,
                UpperBound = x.Value
            })
            .ToList();

        var start = monthlySeries.Count == 0
            ? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1)
            : monthlySeries.Keys.Last();

        var forecast = Enumerable.Range(1, 12)
            .Select(index => new ForecastPointPayload
            {
                Period = start.AddMonths(index),
                Quantity = Math.Max(0m, fallbackMonthly),
                LowerBound = Math.Max(0m, fallbackMonthly * 0.85m),
                UpperBound = Math.Max(0m, fallbackMonthly * 1.15m)
            })
            .ToList();

        File.WriteAllText(artifactPath, JsonSerializer.Serialize(forecast, JsonOptions));

        return new ForecastComputationResult
        {
            History = history,
            Forecast = forecast,
            UsesFallback = true,
            ArtifactPath = artifactPath,
            Mape = null
        };
    }

    private static SortedDictionary<DateOnly, decimal> BuildMonthlySeries(IEnumerable<StockMovement> movements)
    {
        return movements
            .GroupBy(x => new DateOnly(x.CreatedAt.Year, x.CreatedAt.Month, 1))
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity))
            .ToSortedDictionary();
    }

    private int? ResolveFallbackCategoryId(Product product)
    {
        return product.Category?.ParentCategoryId ?? product.CategoryId;
    }

    private static decimal? EstimateMape(SortedDictionary<DateOnly, decimal> monthlySeries)
    {
        if (monthlySeries.Count < 4)
        {
            return null;
        }

        var values = monthlySeries.Values.ToList();
        var validationCount = Math.Min(3, values.Count - 1);
        var history = values.Take(values.Count - validationCount).ToList();
        if (history.Count == 0)
        {
            return null;
        }

        var baseline = history.Average();
        var validation = values.Skip(values.Count - validationCount).ToList();
        var errors = validation
            .Where(x => x > 0)
            .Select(x => Math.Abs((x - baseline) / x))
            .ToList();

        if (errors.Count == 0)
        {
            return 0m;
        }

        return errors.Average();
    }

    private static decimal Sanitize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
        {
            return 0m;
        }

        return decimal.Round((decimal)value, 3, MidpointRounding.AwayFromZero);
    }

    private static ForecastSnapshotPayload DeserializePayload(string json)
    {
        return JsonSerializer.Deserialize<ForecastSnapshotPayload>(json, JsonOptions) ?? new ForecastSnapshotPayload();
    }

    private static AiForecastPointDto MapPoint(ForecastPointPayload point)
    {
        return new AiForecastPointDto
        {
            Period = point.Period,
            Quantity = point.Quantity,
            LowerBound = point.LowerBound,
            UpperBound = point.UpperBound,
            IsForecast = point.LowerBound != point.UpperBound || point.Quantity != point.LowerBound
        };
    }

    private sealed class DemandObservation
    {
        public float Value { get; init; }
    }

    private sealed class DemandForecast
    {
        public float[] ForecastedValues { get; set; } = [];
        public float[] LowerBound { get; set; } = [];
        public float[] UpperBound { get; set; } = [];
    }

    private sealed class ForecastComputationResult
    {
        public List<ForecastPointPayload> History { get; init; } = [];
        public List<ForecastPointPayload> Forecast { get; init; } = [];
        public bool UsesFallback { get; init; }
        public string ArtifactPath { get; init; } = string.Empty;
        public decimal? Mape { get; init; }
    }

    private sealed class ForecastSnapshotPayload
    {
        public List<ForecastPointPayload> History { get; init; } = [];
        public List<ForecastPointPayload> Forecast { get; init; } = [];
    }

    private sealed class ForecastPointPayload
    {
        public DateOnly Period { get; init; }
        public decimal Quantity { get; init; }
        public decimal LowerBound { get; init; }
        public decimal UpperBound { get; init; }
    }
}

internal static class DictionaryExtensions
{
    public static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(this IDictionary<TKey, TValue> source)
        where TKey : notnull
    {
        return new(source);
    }
}
