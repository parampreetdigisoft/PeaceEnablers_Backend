using Microsoft.EntityFrameworkCore;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.CountryUserDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;

namespace PeaceEnablers.Services
{
    public class SignalDashboardService : ISignalDashboardService
    {
        private const int PeaceStressTestModeId = 1;
        private const int EarlyWarningModeId = 2;
        private const int ResilienceModeId = 3;
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        public SignalDashboardService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }

        public async Task<ResultResponseDto<PeaceStressTestDashboardDto>> GetPeaceStressTestDashboard(int countryID, int year, int userId)
        {
            try
            {
                if (!await ValidateCountryAccess(countryID, userId))
                {
                    return ResultResponseDto<PeaceStressTestDashboardDto>.Failure(new[] { "You don't have access to this country data." });
                }

                var mappings = await LoadActiveMappings(PeaceStressTestModeId);
                if (!mappings.Any())
                {
                    return ResultResponseDto<PeaceStressTestDashboardDto>.Failure(new[] { "Peace stress test dashboard configuration not found." });
                }

                var layerIds = mappings.Select(x => x.LayerID).Distinct().ToList();

                var layers = await LoadLayers(layerIds);

                var accessibleLayerIds = await GetAccessibleLayerIds(userId);
                var currentResults = await LoadLayerResultsByYear(countryID, year, layerIds);
                var previousResults = await LoadLayerResultsByYear(countryID, year - 1, layerIds);
                var pemScores = await LoadCountryPemScores(countryID, year);
                var primaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel == 1));
                var secondaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel != 1));
                var primarySignals = BuildSignalCards(primaryMappings, layers, currentResults, previousResults, accessibleLayerIds, pemScores.Current);

                primarySignals.Insert(0,new SignalCardDto
                {
                    LayerID = 0,
                    LayerCode = "PEM",
                    LayerName = "Country Score",
                    Description = "Score of the Country",
                    Code = "PEM Score",
                    Name = "Country Score",
                    Value = pemScores.Current,
                    Delta = pemScores.Delta,
                    Condition = CommonStaticMethods.GetConditionByScore(pemScores.Current)
                });

                var secondarySignals = BuildSignalCards(secondaryMappings, layers, currentResults, previousResults, accessibleLayerIds, pemScores.Current);
                ApplyPemToLayerCard(primarySignals, pemScores, layers);
                ApplyPemToLayerCard(secondarySignals, pemScores, layers);
                var pemLayer = layers.Values.FirstOrDefault(x => x.LayerCode.Equals("PEM", StringComparison.OrdinalIgnoreCase));

                var pemInterpretation = ResolveInterpretation(
                    pemLayer,
                    pemLayer != null && currentResults.TryGetValue(pemLayer.LayerID, out var pemResult)
                        ? pemResult.InterpretationId
                        : null);
                var pemCondition = CommonStaticMethods.GetConditionByScore(pemScores.Current);

                var narratives = primarySignals
                    .Where(x => !x.LayerCode.Equals("PEM", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.IsAlert)
                    .ThenByDescending(x => x.Value)
                    .Take(4)
                    .Select(x => new StressNarrativeDto
                    {
                        Headline = $"{x.LayerName} ({x.Condition})",
                        Detail = string.IsNullOrWhiteSpace(x.Descriptor) ? x.Narrative : x.Descriptor
                    })
                    .ToList();

                var allSignals = primarySignals.Concat(secondarySignals).ToList();
                var response = new PeaceStressTestDashboardDto
                {
                    CountryID = countryID,
                    Year = year,
                    Pem = pemScores.Current,
                    CountryScore = pemScores.Current,
                    PemDirectionalMovement = pemScores.Delta,
                    PemCondition = pemCondition,
                    PemDescriptor = pemInterpretation?.Descriptor ?? string.Empty,
                    PemStrategicAction = pemInterpretation?.StrategicAction ?? string.Empty,
                    PrimarySignals = primarySignals,
                    SecondarySignals = secondarySignals,
                    Signals = allSignals,
                    Narratives = narratives
                };

                return ResultResponseDto<PeaceStressTestDashboardDto>.Success(response, new[] { "Peace stress test dashboard generated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPeaceStressTestDashboard", ex);
                return ResultResponseDto<PeaceStressTestDashboardDto>.Failure(new[] { "There is an error, please try later" });
            }
        }



        public async Task<ResultResponseDto<EarlyWarningDashboardDto>> GetEarlyWarningDashboard(int countryID, int year, int userId)
        {
            try
            {
                if (!await ValidateCountryAccess(countryID, userId))
                {
                    return ResultResponseDto<EarlyWarningDashboardDto>.Failure(new[] { "You don't have access to this country data." });
                }

                var mappings = await LoadActiveMappings(EarlyWarningModeId);
                if (!mappings.Any())
                {
                    return ResultResponseDto<EarlyWarningDashboardDto>.Failure(new[] { "Early warning dashboard configuration not found." });
                }

                var layerIds = mappings.Select(x => x.LayerID).Distinct().ToList();
                var layers = await LoadLayers(layerIds);
                var accessibleLayerIds = await GetAccessibleLayerIds(userId);
                var orderedMappings = OrderMappings(mappings);

                var trendYears = new[] { year - 2, year - 1, year };
                var yearlyResults = new Dictionary<int, Dictionary<int, LayerYearResult>>();
                foreach (var trendYear in trendYears)
                {
                    yearlyResults[trendYear] = await LoadLayerResultsByYear(countryID, trendYear, layerIds);
                }

                var currentResults = yearlyResults[year];
                var previousResults = yearlyResults[year - 1];

                var alerts = BuildSignalCards(orderedMappings, layers, currentResults, previousResults, accessibleLayerIds)
                    .OrderByDescending(x => x.IsAlert)
                    .ThenByDescending(x => Math.Abs(x.Delta ?? 0))
                    .ThenByDescending(x => x.Value)
                    .ToList();

                var trends = orderedMappings
                .Select(mapping =>
                {
                    layers.TryGetValue(mapping.LayerID, out var layer);
                    var layerCode = layer?.LayerCode ?? mapping.LayerID.ToString();
                    var layerName = layer?.LayerName ?? layerCode;

                    return new SignalTrendDto
                    {
                        Code = layerCode,
                        Name = layerName,
                        Series = trendYears
                        .Select(trendYear =>
                        {
                            var value = yearlyResults.TryGetValue(trendYear, out var yearMap) &&
                                        yearMap.TryGetValue(mapping.LayerID, out var result)
                                ? result.Value
                                : 0m;

                            return new YearSignalPointDto
                            {
                                Year = trendYear,
                                Value = value
                            };
                        })
                        .ToList()
                    };
                })
                .ToList();

                var alertCount = alerts.Count(x => x.IsAlert);

                var dashboard = new EarlyWarningDashboardDto
                {
                    CountryID = countryID,
                    Year = year,
                    Alerts = alerts,
                    TrendSeries = trends,
                    Outlook = alertCount >= 4
                        ? "Escalation watch: multiple early-warning signals are rising."
                        : alertCount >= 2
                            ? "Cautionary watch: monitor highlighted warning signals."
                            : "Stable watch: no major warning escalation detected."
                };

                return ResultResponseDto<EarlyWarningDashboardDto>.Success(
                    dashboard,
                    new[] { "Early warning dashboard generated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetEarlyWarningDashboard", ex);
                return ResultResponseDto<EarlyWarningDashboardDto>.Failure(new[] { "There is an error, please try later" });
            }
        }



        public async Task<ResultResponseDto<ResilienceScorecardDto>> GetResilienceScorecard(int countryID, int year, int userId)
        {
            try
            {
                if (!await ValidateCountryAccess(countryID, userId))
                {
                    return ResultResponseDto<ResilienceScorecardDto>.Failure(new[] { "You don't have access to this country data." });
                }

                var mappings = await LoadActiveMappings(ResilienceModeId);
                if (!mappings.Any())
                {
                    return ResultResponseDto<ResilienceScorecardDto>.Failure(new[] { "Resilience scorecard configuration not found." });
                }

                var country = await _context.Countries
                    .AsNoTracking()
                    .Where(x => x.CountryID == countryID && x.IsActive && !x.IsDeleted)
                    .Select(x => new { x.CountryID, x.CountryName, x.Region })
                    .FirstOrDefaultAsync();

                if (country == null)
                {
                    return ResultResponseDto<ResilienceScorecardDto>.Failure(new[] { "Invalid country ID" });
                }


                var layerIds = mappings.Select(x => x.LayerID).Distinct().ToList();
                var layers = await LoadLayers(layerIds);
                var accessibleLayerIds = layerIds.ToHashSet();
                var currentResults = await LoadLayerResultsByYear(countryID, year, layerIds);
                var previousResults = await LoadLayerResultsByYear(countryID, year - 1, layerIds);

                var primaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel == 1));
                var secondaryMappings = OrderMappings(mappings.Where(x => x.PriorityLevel != 1));

                var primarySignals = BuildSignalCards(primaryMappings, layers, currentResults, previousResults, accessibleLayerIds);
                var secondarySignals = BuildSignalCards(secondaryMappings, layers, currentResults, previousResults, accessibleLayerIds);
                var resilienceSignals = primarySignals.Concat(secondarySignals).ToList();

                var scsLayerId = layers.Values
                    .FirstOrDefault(x => x.LayerCode.Equals("SCS", StringComparison.OrdinalIgnoreCase))
                    ?.LayerID;
                if (!scsLayerId.HasValue)
                {
                    scsLayerId = primaryMappings.FirstOrDefault()?.LayerID;
                }

                var scs = scsLayerId.HasValue && currentResults.TryGetValue(scsLayerId.Value, out var scsResult)
                    ? scsResult.Value
                    : primarySignals.FirstOrDefault()?.Value ?? 0m;

                var regionCountries = await _context.Countries
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.IsActive && x.Region == country.Region)
                    .Select(x => new { x.CountryID, x.CountryName })
                    .ToListAsync();

                var regionScores = new List<PeerResilienceDto>();

                foreach (var regionCountry in regionCountries)
                {
                    var peerResults = scsLayerId.HasValue
                        ? await LoadLayerResultsByYear(regionCountry.CountryID, year, new[] { scsLayerId.Value })
                        : new Dictionary<int, LayerYearResult>();

                    var peerScs = scsLayerId.HasValue &&
                                  peerResults.TryGetValue(scsLayerId.Value, out var peerResult)
                        ? peerResult.Value
                        : 0m;

                    regionScores.Add(new PeerResilienceDto
                    {
                        CountryID = regionCountry.CountryID,
                        CountryName = regionCountry.CountryName,
                        Scs = peerScs
                    });
                }

                regionScores = regionScores
                .OrderByDescending(x => x.Scs)
                .ToList();

                int rank = 1;
                decimal? previousScore = null;

                for (int i = 0; i < regionScores.Count; i++)
                {
                    if (previousScore != regionScores[i].Scs)
                    {
                        rank = i + 1;
                        previousScore = regionScores[i].Scs;
                    }

                    regionScores[i].ScsRank = rank;
                }

                var scsRank = regionScores.First(x => x.CountryID == countryID).ScsRank;

                var peerAverage = regionScores.Any()
                    ? Math.Round(regionScores.Average(x => x.Scs), 2)
                    : 0m;


                var belowAverageSignals = primarySignals
                    .Where(x => x.Delta.HasValue && x.Delta < 0)
                    .Select(x => x.LayerCode)
                    .Take(3)
                    .ToList();

                var implication = scs >= peerAverage
                    ? "Resilience posture is above regional baseline; suitable for proactive and catalytic investments."
                    : belowAverageSignals.Any()
                        ? $"Weak {string.Join(", ", belowAverageSignals)} indicate medium-term instability risk."
                        : "Resilience posture is below regional baseline; prioritize risk-mitigated and staged investments.";

                var dto = new ResilienceScorecardDto
                {
                    CountryID = countryID,
                    Year = year,
                    Region = country.Region ?? string.Empty,
                    Scs = scs,
                    RegionalRank = scsRank,
                    RegionSampleSize = regionScores.Count,
                    PeerAverageScs = peerAverage,
                    InvestmentImplication = implication,
                    PrimarySignals = primarySignals,
                    SecondarySignals = secondarySignals,
                    ResilienceSignals = resilienceSignals,
                    Peers = regionScores.Take(10).ToList()
                };

                return ResultResponseDto<ResilienceScorecardDto>.Success(
                    dto,
                    new[] { "Resilience scorecard generated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetResilienceScorecard", ex);
                return ResultResponseDto<ResilienceScorecardDto>.Failure(new[] { "There is an error, please try later" });
            }
        }

        private async Task<bool> ValidateCountryAccess(int countryID, int userId)
        {
            return await _context.PublicUserCountryMappings
                .AsNoTracking()
                .AnyAsync(x => x.UserID == userId && x.CountryID == countryID && x.IsActive);
        }

        private async Task<List<DashboardModeKPIMapping>> LoadActiveMappings(int dashboardModeId)
        {
            var dashboardMode = await _context.DashboardModes
                .AsNoTracking()
                .Include(x => x.DashboardModeKPIMappings.Where(m => m.IsActive))
                .FirstOrDefaultAsync(x => x.DashboardModeID == dashboardModeId);
            return dashboardMode?.DashboardModeKPIMappings.ToList() ?? new List<DashboardModeKPIMapping>();
        }

        private static List<DashboardModeKPIMapping> OrderMappings(IEnumerable<DashboardModeKPIMapping> mappings)
        {
            return mappings
                .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
                .ToList();
        }

        private async Task<Dictionary<int, AnalyticalLayer>> LoadLayers(IEnumerable<int> layerIds)
        {
            var ids = layerIds.Distinct().ToList();
            var layers = await _context.AnalyticalLayers
                .AsNoTracking()
                .Include(x => x.FiveLevelInterpretations)
                .Where(x => !x.IsDeleted && ids.Contains(x.LayerID))
                .ToListAsync();

            return layers.ToDictionary(x => x.LayerID);
        }

        private async Task<HashSet<int>> GetAccessibleLayerIds(int userId)
        {
            var layerIds = await _context.CountryUserPillarMappings
                .AsNoTracking()
                .Where(x => x.UserID == userId && x.IsActive)
                .Join(
                    _context.AnalyticalLayerPillarMappings.AsNoTracking(),
                    up => up.PillarID,
                    lp => lp.PillarID,
                    (up, lp) => lp.LayerID)
                .Distinct()
                .ToListAsync();

            return layerIds.ToHashSet();
        }

        private async Task<Dictionary<int, LayerYearResult>> LoadLayerResultsByYear(int countryID, int year, IEnumerable<int> layerIds)
        {
            var ids = layerIds.Distinct().ToList();
            if (!ids.Any())
            {
                return new Dictionary<int, LayerYearResult>();
            }

            var (startDate, endDate) = GetYearDateRange(year);
            var rows = await _context.AnalyticalLayerResults
                .AsNoTracking()
                .Where(x =>
                    x.CountryID == countryID &&
                    ids.Contains(x.LayerID) &&
                    x.AiLastUpdated.HasValue &&
                    x.AiLastUpdated.Value >= startDate &&
                    x.AiLastUpdated.Value < endDate)
                .Select(x => new
                {
                    x.LayerID,
                    x.AiCalValue5,
                    x.AiInterpretationID,
                    x.AiLastUpdated
                })
                .ToListAsync();
            return rows
            .GroupBy(x => x.LayerID)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g
                        .OrderByDescending(x => x.AiLastUpdated)
                        .First();
                    return new LayerYearResult
                    {
                        Value = Math.Round(latest.AiCalValue5 ?? 0m, 2),
                        InterpretationId = latest.AiInterpretationID 
                    };
                });
        }
        private async Task<CountryPemScores> LoadCountryPemScores(int countryID, int year)
        {
            var scores = await _context.AICountryScores
                .AsNoTracking()
                .Where(x =>
                    x.CountryID == countryID &&
                    x.IsVerified &&
                    (x.Year == year || x.Year == year - 1))
                .Select(x => new { x.Year, x.AIProgress })
                .ToListAsync();
            var current = scores.FirstOrDefault(x => x.Year == year)?.AIProgress;
            var previous = scores.FirstOrDefault(x => x.Year == year - 1)?.AIProgress;            

            return new CountryPemScores
            {
                Current = current ?? 0m,
                Previous = previous,
                Delta = previous.HasValue ? Math.Round(current!.Value - previous.Value, 2) : 0m
            };
        }
        private List<SignalCardDto> BuildSignalCards(
            IEnumerable<DashboardModeKPIMapping> mappings,
            IReadOnlyDictionary<int, AnalyticalLayer> layers,
            IReadOnlyDictionary<int, LayerYearResult> currentResults,
            IReadOnlyDictionary<int, LayerYearResult> previousResults,
            IReadOnlySet<int> accessibleLayerIds,
            decimal? pemOverride = null)
        {
            var cards = new List<SignalCardDto>();
            foreach (var mapping in mappings)
            {
                if (!layers.TryGetValue(mapping.LayerID, out var layer))
                {
                    continue;
                }

                currentResults.TryGetValue(mapping.LayerID, out var current);
                previousResults.TryGetValue(mapping.LayerID, out var previous);

                var value = current?.Value ?? 0m;

                if (pemOverride.HasValue &&
                    layer.LayerCode.Equals("PEM", StringComparison.OrdinalIgnoreCase))
                {
                    value = pemOverride.Value;
                }

                var interpretation = ResolveInterpretation(layer, current?.InterpretationId);
                var delta = previous != null
                    ? Math.Round(value - previous.Value, 2)
                    : (decimal?)null;

                var condition = interpretation?.Condition ?? ResolveConditionByValue(layer, value);

                var isAlert = IsAlertCondition(condition) ||
                              (delta.HasValue && delta.Value >= 5m);

                cards.Add(new SignalCardDto
                {
                    LayerID = layer.LayerID,
                    LayerCode = layer.LayerCode,
                    LayerName = layer.LayerName,
                    Description = "",
                    Code = layer.LayerCode,
                    Name = layer.LayerName,
                    Value = value,
                    Delta = delta,
                    Condition = condition,
                    Descriptor = interpretation?.Descriptor ?? string.Empty,
                    StrategicAction = interpretation?.StrategicAction ?? string.Empty,
                    Narrative = interpretation?.Descriptor ?? string.Empty,
                    InterpretationID = interpretation?.InterpretationID ?? 0,
                    IsAlert = isAlert,
                    IsAccessible = accessibleLayerIds.Contains(layer.LayerID),
                    Interpretations = MapInterpretations(layer)
                });
            }

            return cards;
        }



        private static void ApplyPemToLayerCard(
            List<SignalCardDto> cards,
            CountryPemScores pemScores,
            IReadOnlyDictionary<int, AnalyticalLayer> layers)
        {
            var pemCard = cards.FirstOrDefault(x => x.LayerCode.Equals("PEM", StringComparison.OrdinalIgnoreCase));

            if (pemCard == null)
            {
                return;
            }

            pemCard.Value = pemScores.Current;
            pemCard.Delta = pemScores.Previous.HasValue ? pemScores.Delta : null;

            if (layers.TryGetValue(pemCard.LayerID, out var pemLayer))
            {
                var interpretation = ResolveInterpretation(pemLayer, pemCard.InterpretationID > 0 ? pemCard.InterpretationID : null)
                                     ?? MatchInterpretationByValue(pemLayer, pemScores.Current);

                if (interpretation != null)
                {
                    pemCard.Condition = interpretation.Condition;
                    pemCard.Descriptor = interpretation.Descriptor;
                    pemCard.StrategicAction = interpretation.StrategicAction;
                    pemCard.Narrative = interpretation.Descriptor;
                    pemCard.InterpretationID = interpretation.InterpretationID;
                }
                else
                {
                    pemCard.Condition = ResolveConditionByValue(pemLayer, pemScores.Current);
                }
            }
        }

        private static List<FiveLevelInterpretationDto> MapInterpretations(AnalyticalLayer layer)
        {
            return layer.FiveLevelInterpretations
                .OrderByDescending(x => x.MaxRange)
                .Select(x => new FiveLevelInterpretationDto
                {
                    InterpretationID = x.InterpretationID,
                    LayerID = x.LayerID,
                    MinRange = x.MinRange,
                    MaxRange = x.MaxRange,
                    Condition = x.Condition ?? string.Empty,
                    Descriptor = x.Descriptor ?? string.Empty,
                    StrategicAction = x.StrategicAction ?? string.Empty
                })
                .ToList();
        }
        private static FiveLevelInterpretationDto? ResolveInterpretation(AnalyticalLayer? layer, int? interpretationId)
        {
            if (layer == null || !interpretationId.HasValue)
            {
                return null;
            }

            var match = layer.FiveLevelInterpretations
                .FirstOrDefault(x => x.InterpretationID == interpretationId.Value);

            return match == null ? null : ToInterpretationDto(match);
        }
        private static FiveLevelInterpretationDto? MatchInterpretationByValue(AnalyticalLayer layer, decimal value)
        {
            var match = layer.FiveLevelInterpretations.FirstOrDefault(x =>
                (!x.MinRange.HasValue || value >= x.MinRange.Value) &&
                (!x.MaxRange.HasValue || value <= x.MaxRange.Value));
            return match == null ? null : ToInterpretationDto(match);
        }

        private static FiveLevelInterpretationDto ToInterpretationDto(FiveLevelInterpretation interpretation)
        {
            return new FiveLevelInterpretationDto
            {
                InterpretationID = interpretation.InterpretationID,
                LayerID = interpretation.LayerID,
                MinRange = interpretation.MinRange,
                MaxRange = interpretation.MaxRange,
                Condition = interpretation.Condition ?? string.Empty,
                Descriptor = interpretation.Descriptor ?? string.Empty,
                StrategicAction = interpretation.StrategicAction ?? string.Empty
            };
        }
        private static string ResolveConditionByValue(AnalyticalLayer? layer, decimal value)
        {
            return MatchInterpretationByValue(layer ?? new AnalyticalLayer(), value)?.Condition ?? "";
        }

        private static bool IsAlertCondition(string condition)
        {
            var normalized = condition.ToLowerInvariant();
            return normalized.Contains("critical") ||
                   normalized.Contains("high") ||
                   normalized.Contains("elevated") ||
                   normalized.Contains("watch");
        }

        private static (DateTime StartDate, DateTime EndDate) GetYearDateRange(int year)
        {
            return (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
        }
        private sealed class LayerYearResult
        {
            public decimal Value { get; init; }
            public int? InterpretationId { get; init; }
        }

        private sealed class CountryPemScores
        {
            public decimal Current { get; init; }

            public decimal? Previous { get; init; }

            public decimal Delta { get; init; }

        }
    }
}


