using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using SmartStockAI.Core.Contracts.AI;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;
using SmartStockAI.Data.Services.Ai;

namespace SmartStockAI.Data.Services;

public sealed partial class AiService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IOptions<AiOptions> optionsAccessor) : IAiService
{
    private const string ProductScope = "Product";
    private const string CategoryScope = "Category";
    private const string GlobalScope = "Global";
    private const string CategoryModelType = "CategoryClassifier";
    private const string ForecastModelType = "SsaForecast";

    private static readonly MLContext MlContext = new(seed: 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiOptions _options = optionsAccessor.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var hasForecasts = await dbContext.ForecastSnapshots.AnyAsync(cancellationToken);
        var hasCategoryModel = await dbContext.ModelTrainingInfos.AnyAsync(
            x => x.ModelType == CategoryModelType && x.ScopeType == GlobalScope,
            cancellationToken);

        if (!hasForecasts || !hasCategoryModel)
        {
            await RefreshModelsAsync(cancellationToken);
        }
    }

    public async Task<AiModelRefreshResultDto> RefreshModelsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedWhenAvailable();

        var modelRoot = EnsureModelDirectory();
        var now = DateTime.UtcNow;

        var products = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
                .ThenInclude(x => x!.ParentCategory)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Include(x => x.ParentCategory)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var issueMovements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.Type == Core.Enums.StockMovementType.Issue)
            .ToListAsync(cancellationToken);

        var expectedInboundByProduct = await RebuildExpectedInboundAsync(now, cancellationToken);

        var categoryRefresh = await TrainCategoryModelAsync(products, modelRoot, now, cancellationToken);
        var categoryForecasts = await BuildCategoryForecastsAsync(
            categories,
            products,
            issueMovements,
            expectedInboundByProduct,
            modelRoot,
            now,
            cancellationToken);

        var productForecasts = await BuildProductForecastsAsync(
            products,
            issueMovements,
            expectedInboundByProduct,
            categoryForecasts,
            modelRoot,
            now,
            cancellationToken);

        var forecastScores = await dbContext.ForecastSnapshots
            .AsNoTracking()
            .Where(x => x.ModelQuality.HasValue)
            .Select(x => x.ModelQuality!.Value)
            .ToListAsync(cancellationToken);

        return new AiModelRefreshResultDto
        {
            RefreshedAtUtc = now,
            ProductForecastCount = productForecasts,
            CategoryForecastCount = categoryForecasts.Count,
            CategoryTrainingRowsCount = categoryRefresh.TrainingRowsCount,
            CategoryModelScore = categoryRefresh.ModelScore,
            AverageForecastError = forecastScores.Count == 0 ? null : forecastScores.Average()
        };
    }

    public async Task<AiDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        var products = await dbContext.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var productSnapshots = await dbContext.ForecastSnapshots
            .AsNoTracking()
            .Where(x => x.ScopeType == ProductScope)
            .OrderByDescending(x => x.GeneratedAtUtc)
            .ToListAsync(cancellationToken);

        var categorySnapshots = await dbContext.ForecastSnapshots
            .AsNoTracking()
            .Where(x => x.ScopeType == CategoryScope)
            .OrderBy(x => x.ScopeName)
            .ToListAsync(cancellationToken);

        var criticalItems = (from snapshot in productSnapshots
                             join product in products on snapshot.ScopeId equals product.Id
                             where snapshot.ProjectedDeficit > 0 || product.CurrentStock - product.ReservedStock <= product.MinStock
                             orderby snapshot.ProjectedDeficit descending, snapshot.RecommendedOrder descending
                             select new AiCriticalStockItemDto
                             {
                                 ProductId = product.Id,
                                 ProductName = product.Name,
                                 Sku = product.Sku,
                                 AvailableStock = product.CurrentStock - product.ReservedStock,
                                 MinStock = product.MinStock,
                                 MonthlyForecast = snapshot.AverageMonthlyDemand,
                                 ProjectedDeficit = snapshot.ProjectedDeficit,
                                 RecommendedOrder = snapshot.RecommendedOrder,
                                 UsesFallback = snapshot.UsesFallback,
                                 GeneratedAtUtc = snapshot.GeneratedAtUtc
                             })
            .Take(8)
            .ToList();

        var purchaseRecommendations = (from snapshot in productSnapshots
                                       join product in products on snapshot.ScopeId equals product.Id
                                       where snapshot.RecommendedOrder > 0
                                       orderby snapshot.RecommendedOrder descending
                                       select new AiPurchaseRecommendationDto
                                       {
                                           ProductId = product.Id,
                                           ProductName = product.Name,
                                           Sku = product.Sku,
                                           CurrentStock = product.CurrentStock - product.ReservedStock,
                                           ExpectedInbound = snapshot.ExpectedInbound,
                                           MonthlyForecast = snapshot.AverageMonthlyDemand,
                                           ForecastLeadTime = snapshot.ForecastLeadTime,
                                           SafetyStock = snapshot.SafetyStock,
                                           RecommendedOrder = snapshot.RecommendedOrder,
                                           UsesFallback = snapshot.UsesFallback
                                       })
            .Take(8)
            .ToList();

        return new AiDashboardDto
        {
            LastForecastCalculatedAtUtc = productSnapshots.Select(x => (DateTime?)x.GeneratedAtUtc).FirstOrDefault(),
            CriticalItems = criticalItems,
            PurchaseRecommendations = purchaseRecommendations,
            CategoryForecasts = categorySnapshots
                .Take(6)
                .Select(x => new AiCategoryForecastSummaryDto
                {
                    CategoryId = x.ScopeId,
                    CategoryName = x.ScopeName,
                    MonthlyForecast = x.AverageMonthlyDemand,
                    ForecastSixMonths = x.ForecastLeadTime + x.SafetyStock,
                    GeneratedAtUtc = x.GeneratedAtUtc
                })
                .ToList()
        };
    }

    public async Task<AiProductAnalyticsDto?> GetProductAnalyticsAsync(int productId, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var snapshot = await dbContext.ForecastSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ScopeType == ProductScope && x.ScopeId == productId, cancellationToken);

        if (snapshot is null)
        {
            return new AiProductAnalyticsDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name
            };
        }

        var payload = DeserializePayload(snapshot.PayloadJson);

        return new AiProductAnalyticsDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Sku = product.Sku,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            ForecastCalculatedAtUtc = snapshot.GeneratedAtUtc,
            AverageMonthlyDemand = snapshot.AverageMonthlyDemand,
            ForecastLeadTime = snapshot.ForecastLeadTime,
            SafetyStock = snapshot.SafetyStock,
            ExpectedInbound = snapshot.ExpectedInbound,
            RecommendedOrder = snapshot.RecommendedOrder,
            ProjectedDeficit = snapshot.ProjectedDeficit,
            UsesFallback = snapshot.UsesFallback,
            History = payload.History.Select(MapPoint).ToList(),
            Forecast = payload.Forecast.Select(MapPoint).ToList()
        };
    }

    public async Task<AiCategoryRecommendationDto?> SuggestCategoryAsync(string productName, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        var normalizedName = NormalizeText(productName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var trainingInfo = await dbContext.ModelTrainingInfos
            .AsNoTracking()
            .OrderByDescending(x => x.TrainedAtUtc)
            .FirstOrDefaultAsync(x => x.ModelType == CategoryModelType && x.ScopeType == GlobalScope, cancellationToken);

        if (trainingInfo is null || !File.Exists(trainingInfo.ArtifactPath))
        {
            return await SuggestCategoryByTokensAsync(normalizedName, cancellationToken);
        }

        await using var stream = File.OpenRead(trainingInfo.ArtifactPath);
        var model = MlContext.Model.Load(stream, out _);
        var engine = MlContext.Model.CreatePredictionEngine<CategoryTrainingRow, CategoryPrediction>(model);
        var prediction = engine.Predict(new CategoryTrainingRow { Name = normalizedName });

        if (!int.TryParse(prediction.PredictedLabel, out var categoryId))
        {
            return await SuggestCategoryByTokensAsync(normalizedName, cancellationToken);
        }

        var category = await dbContext.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return await SuggestCategoryByTokensAsync(normalizedName, cancellationToken);
        }

        var confidence = CalculateConfidence(prediction.Score);
        return new AiCategoryRecommendationDto
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            Confidence = confidence,
            IsStrongRecommendation = confidence >= _options.StrongCategoryRecommendationThreshold,
            TrainedAtUtc = trainingInfo.TrainedAtUtc
        };
    }
}
