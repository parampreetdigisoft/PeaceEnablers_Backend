
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System.Text;
using static PeaceEnablers.Services.AIComputationService;

namespace PeaceEnablers.Common.Implementation
{
    public partial class PdfGeneratorService : Interface.IPdfGeneratorService
    {
        #region constructor

        private readonly IAppLogger _appLogger;
        private readonly ICommonService _commonService;
        private int _pillarCount;
        public PdfGeneratorService(IAppLogger appLogger, ICommonService commonService)
        {
            _appLogger = appLogger;
            _commonService = commonService;
        }
        #endregion


        #region pdf pillars and country report

        public async Task<byte[]> GenerateAllCountriesDetailsPdf(List<AiCountrySummeryDto> countries, Dictionary<int, List<AiCountryPillarResponse>> pillarsDict, List<KpiChartItem> kpis, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;
                _pillarCount = (await _commonService.GetPillars()).Count;
                var document = Document.Create(container =>
                {
                    foreach(var countryDetails in countries)
                    {
                        if(pillarsDict.TryGetValue(countryDetails.CountryID, out var pillars) && pillars.Count > 0)
                        {
                            var kpiChartItems = kpis?
                            .Where(x => x.CountryID == countryDetails.CountryID)
                            .OrderByDescending(x => x.Value)
                            .ToList() ?? new List<KpiChartItem>();

                            AddCountryDetailsPdf(container, countryDetails, pillars, kpiChartItems,new(), userRole, true);
                        }
                    }
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateCountryDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GenerateCountryDetailsPdf(AiCountrySummeryDto countryDetails, List<AiCountryPillarResponse> pillars, List<KpiChartItem> kpis, List<PeerCountryHistoryReportDto> peerCountry, UserRole userRole)
        {
            try
            {

                QuestPDF.Settings.EnableDebugging = true;
                _pillarCount = (await _commonService.GetPillars()).Count;
                var document = Document.Create(container =>
                {
                    AddCountryDetailsPdf(container, countryDetails, pillars, kpis, peerCountry, userRole);
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateCountryDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsPdf(AiCountryPillarResponse pillarData, UserRole userRole)
        {
            try
            {
                QuestPDF.Settings.EnableDebugging = true;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(25);
                        page.PageColor(ReportThemeColors.RowPale);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));
                        page.Header().Element(header => PillarComposeHeader(header, pillarData));
                        page.Content().Element(content =>
                            PillarComposeContent(content, pillarData, userRole));
                        page.Footer().Element(PillarComposeFooter);
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GeneratePillarDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }
        public async Task<byte[]> GenerateSelectedPillarsDetailsPdf(List<AiCountryPillarResponse> pillars, List<CountryPillarRankingResultDto> pillarRankings, UserRole userRole)
        {
            try
            {
                if (pillars == null || pillars.Count == 0)
                    return Array.Empty<byte>();

                QuestPDF.Settings.EnableDebugging = true;
                _pillarCount = (await _commonService.GetPillars()).Count;

                var first = pillars[0];
                var countryDetails = new AiCountrySummeryDto
                {
                    CountryID = first.CountryID,
                    CountryName = first.CountryName,
                    Continent = first.Continent,
                    Year = first.AIDataYear,
                    AIProgress = pillars.Average(x => x.AIProgress ?? 0)
                };

                var pillarChartItems = pillars.Select(p => new PillarChartItem(SanitizeText(p.PillarName)?.Length > 20 ? SanitizeText(p.PillarName)[..20] : SanitizeText(p.PillarName) ?? "-", SanitizeText(p.PillarName) ?? "-", p.AIProgress)).ToList();

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        ApplyPageDefaults(page);
                        page.Header().Element(header =>
                            CountryComposeHeader(header, countryDetails, userRole, "Domain Performance Overview"));
                        page.Content().Element(content => PillarLineChartPage(content, pillarChartItems));
                        PageFooter(page);
                    });

                    foreach (var pillarData in pillars)
                    {
                        var pillarRank = pillarRankings?.FirstOrDefault(p =>
                            p.PillarID == pillarData.PillarID && p.CountryID == pillarData.CountryID);
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(25);
                            page.PageColor(ReportThemeColors.PageBg);
                            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));
                            page.Header().Element(header => PillarComposeHeader(header, pillarData));
                            page.Content().Element(content =>
                                SelectedPillarMarkedContent(content, pillarData, pillarRank, userRole));
                            page.Footer().Element(PillarComposeFooter);
                        });
                    }
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured in GenerateSelectedPillarsDetailsPdf", ex);
                return Array.Empty<byte>();
            }
        }
        public void AddCountryDetailsPdf(IDocumentContainer container, AiCountrySummeryDto countryDetails, List<AiCountryPillarResponse> pillars, List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peerCountries, UserRole userRole, bool isAllCountries = false)
        {
            var kpiChartItems = kpis.ToList();

            // Build pillar chart items (max 14)
            var pillarChartItems = pillars.Select(p => new PillarChartItem( SanitizeText(p.PillarName)?.Length > 20   ? SanitizeText(p.PillarName)[..20]
                  : SanitizeText(p.PillarName) ?? "-",  SanitizeText(p.PillarName) ?? "-", p.AIProgress)).ToList();
          
            AddGlobalDashboardPage(container, countryDetails, pillarChartItems, kpis, userRole);


            // ── Section 2 : Country Summary ─────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, null));
                page.Content().Element(content =>
                {
                    content.Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Element(x =>
                            CountrySummeryComposeContent(x, countryDetails, userRole, isAllCountries));
                    });
                });
                PageFooter(page);
            });


            // ── Section 3 : Pillar Radial Overview ───────────────────────────
            if (pillars.Any())
            {
                container.Page(page =>
                {
                    ApplyPageDefaults(page);
                    page.Header().Element(x =>
                        CountryComposeHeader(x, countryDetails, userRole, "Pillar Performance Overview"));
                    page.Content().Element(content =>
                        PillarLineChartPage(content, pillarChartItems));
                    PageFooter(page);
                });
            }

            // ── Section 1 : Global Dashboard ─────────────────────────────────
            if (!isAllCountries)
            {
                AddPeerCountryComparisonSection(container, peerCountries, countryDetails, userRole);
                AddPerformanceTrendsSection(container, peerCountries, countryDetails, userRole);

                // ── Section 4+ : Per-Pillar Detail ──────────────────────────────
                var accessiblePillars = pillars.Where(x => x.IsAccess && UserRole.CountryUser == userRole || UserRole.CountryUser != userRole).ToList();
                foreach (var p in accessiblePillars)
                {
                    container.Page(page =>
                    {
                        ApplyPageDefaults(page);
                        page.Header().Element(x =>
                            CountryComposeHeader(x, countryDetails, userRole, SanitizeText(p.PillarName)));
                        page.Content().Element(content =>
                        {
                            content.Column(column =>
                            {
                                column.Spacing(10);
                                column.Item().Element(x =>
                                    PillarComposeContent(x, p, userRole));
                            });
                        });
                        PageFooter(page);
                    });
                }
            }

            // ── Section 5 : KPI Dashboard ────────────────────────────────────
            if (kpiChartItems.Any())
            {
                container.Page(page =>
                {
                    ApplyPageDefaults(page);
                    page.Header().Element(x =>
                        CountryComposeHeader(x, countryDetails, userRole, "KPI Dashboard"));
                    page.Content().Element(content =>
                        KpiDashboardPage(content, kpiChartItems, isAllCountries));
                    PageFooter(page);
                });
            }

            if (!isAllCountries)
            {
                // -- Section 2 : Recommendations -------------------------------------
                container.Page(page =>
                {
                    ApplyPageDefaults(page);
                    page.Header().Element(x =>
                        CountryComposeHeader(x, countryDetails, userRole, null));
                    page.Content().Element(content =>
                    {
                        content.Column(column =>
                        {
                            column.Spacing(10);
                            column.Item().Element(x =>
                                AssessmentRecommendations(x, countryDetails, userRole));
                        });
                    });
                    PageFooter(page);
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  PAGE LAYOUT HELPERS  (reusable)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Applies standard A4 + font defaults to any page.</summary>
        static void ApplyPageDefaults(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(25);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));
        }

        /// <summary>Standard numeric footer for country pages.</summary>
        static void PageFooter(PageDescriptor page)
        {
            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
            });
        }        
        void AddGlobalDashboardPage(
            IDocumentContainer doc,
            AiCountrySummeryDto country,
            List<PillarChartItem> pillars,   // already filtered to max 14
            List<KpiChartItem> kpis,      // already filtered to max 107
            UserRole userRole)
        {
            var vPillars = pillars.ToList();

            doc.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, country, userRole, "Country Performance Dashboard"));
                page.Content().Element(x =>
                    RenderDashboardContent(x, vPillars, kpis, country));
                PageFooter(page);
            });
        }

        void RenderDashboardContent(
            IContainer container,
            List<PillarChartItem> pillars,
            List<KpiChartItem> kpis,
            AiCountrySummeryDto country)
        {
            //var vKpis = kpis.Where(k => k.Value.HasValue).ToList();

            float overall = (float)country.AIProgress.GetValueOrDefault();
            int kpiGreen = kpis.Count(k => k.Value >= 70);
            int kpiAmber = kpis.Count(k => k.Value >= 40 && k.Value < 70);
            int kpiRed = kpis.Count(k => k.Value == null || k.Value < 40);
            var best = pillars.OrderByDescending(p => p.Value).FirstOrDefault();
            var worst = pillars.OrderBy(p => p.Value).FirstOrDefault();

            container.PaddingTop(6).Column(col =>
            {
                col.Spacing(10);

                // ── Row 1 : Score Donut (left)  +  Pillar Radar (right) ──────────
                col.Item().Height(280).Row(row =>
                {
                    row.RelativeItem(5).Element(x =>
                        RenderScoreDonutCard(x, country, pillars.Count, kpis.Count, best, worst));

                    row.ConstantItem(10);

                    row.RelativeItem(5).Element(x =>
                        RenderPillarRadarCard(x, pillars));
                });

                // ── Row 2 : KPI distribution stat cards ──────────────────────────
                //var topKpis = kpis
                //    .Where(x =>
                //        string.Equals(x.ShortName, "UDRI", StringComparison.OrdinalIgnoreCase) ||
                //        string.Equals(x.ShortName, "PRUPS", StringComparison.OrdinalIgnoreCase))
                //    .ToList();

                //if (topKpis.Any())
                //    col.Item().Height(130).Element(x =>
                //    DrawTopKpiBand(x, topKpis));

                col.Item().Height(100).Element(x =>
                    RenderKpiDistributionBand(x, kpis.Count, kpiGreen, kpiAmber, kpiRed));

                // ── Row 3 : KPI sorted sparkline ─────────────────────────────────
                if (kpis.Any())
                    col.Item().Height(120).Element(x =>
                        RenderKpiSparklineCard(x, kpis));
            });
        }
        

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — Score Donut Card
        // ─────────────────────────────────────────────────────────────────────────────

        void RenderScoreDonutCard(
            IContainer container,
            AiCountrySummeryDto country,
            int pillarCount,
            int kpiCount,
            PillarChartItem? best,
            PillarChartItem? worst)
        {

            float score = (float)country.AIProgress.GetValueOrDefault();

            // Overall rank label
            var globalRankLabel = country.Rank.HasValue && country.TotalCountry.HasValue && country.TotalCountry > 1
                ? $"Global Rank: {country.Rank} / {country.TotalCountry}"
                : "Global Rank: N/A";

            // Regional rank label
            var regionRankLabel = country.RegionRank.HasValue && country.RegionTotalCountry.HasValue && country.RegionTotalCountry >= 1
                ? $"{country.Region} Region: {country.RegionRank} / {country.RegionTotalCountry}"
                : $"{country.Region} Region: N/A";


            container
                .Background(Colors.White)
                .Border(1)
                .BorderColor(ReportThemeColors.CD8E8E2)
                .Padding(8)
                .Column(col =>
                {
                    col.Spacing(0);

                    // ─────────────────────────────────────────────
                    // Title
                    // ─────────────────────────────────────────────
                    col.Item()
                        .AlignCenter()
                        .Text("Overall Country Score")
                        .FontSize(10)
                        .Bold()
                        .FontColor(ReportThemeColors.PdfDarkGreen);

                    // ─────────────────────────────────────────────
                    // Donut chart
                    // ─────────────────────────────────────────────
                    col.Item()
                        .Height(130)
                        .Canvas((canvas, size) =>
                            PaintDonut(canvas, size, score));

                    // Divider
                    col.Item()
                        .Height(1)
                        .Background(ReportThemeColors.SurfaceGreen);

                    // ─────────────────────────────────────────────
                    // Pillars + KPI Counts
                    // ─────────────────────────────────────────────
                    col.Item()
                        .PaddingTop(5)
                        .Row(row =>
                        {
                            // Pillars
                            row.RelativeItem()
                                .AlignCenter()
                                .Column(c =>
                                {
                                    c.Item()
                                        .AlignCenter()
                                        .Text(pillarCount.ToString())
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor(ReportThemeColors.PdfMediumGreen);

                                    c.Item()
                                        .AlignCenter()
                                        .Text("Pillars")
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.Gray757575);
                                });

                            // Divider
                            row.ConstantItem(1)
                                .Background(ReportThemeColors.GrayE0E0E0);

                            // KPIs
                            row.RelativeItem()
                                .AlignCenter()
                                .Column(c =>
                                {
                                    c.Item()
                                        .AlignCenter()
                                        .Text(kpiCount.ToString())
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor(ReportThemeColors.PdfMediumGreen);

                                    c.Item()
                                        .AlignCenter()
                                        .Text("KPIs")
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.Gray757575);
                                });
                        });

                    // ─────────────────────────────────────────────
                    // Best Pillar + Global Rank
                    // ─────────────────────────────────────────────
                    if (best != null)
                    {
                        col.Item()
                            .PaddingTop(6)
                            .Row(row =>
                            {
                                // Best pillar
                                row.RelativeItem()
                                    .Background(ReportThemeColors.SuccessGreenBg)
                                    .PaddingVertical(3)
                                    .PaddingHorizontal(5)
                                    .Text(
                                        $"▲ {Shorten(best.Name, 16)} ({best.Value:F0})")
                                    .FontSize(7)
                                    .FontColor(ReportThemeColors.SuccessGreenDark);

                                row.ConstantItem(4);

                                // Global rank
                                row.AutoItem()
                                    .Background(ReportThemeColors.SurfaceGreen)
                                    .PaddingVertical(3)
                                    .PaddingHorizontal(5)
                                    .Text(globalRankLabel)
                                    .FontSize(7)
                                    .Bold()
                                    .FontColor(ReportThemeColors.PdfDarkGreen);
                            });
                    }

                    // ─────────────────────────────────────────────
                    // Worst Pillar + Region Rank
                    // ─────────────────────────────────────────────
                    if (worst != null)
                    {
                        col.Item()
                            .PaddingTop(3)
                            .Row(row =>
                            {
                                // Worst pillar
                                row.RelativeItem()
                                    .Background(ReportThemeColors.DangerRedBg)
                                    .PaddingVertical(3)
                                    .PaddingHorizontal(5)
                                    .Text(
                                        $"▼ {Shorten(worst.Name, 16)} ({worst.Value:F0})")
                                    .FontSize(7)
                                    .FontColor(ReportThemeColors.DangerRedDark);

                                row.ConstantItem(4);

                                // Region rank
                                row.AutoItem()
                                    .Background(ReportThemeColors.WarningOrangeBg)
                                    .PaddingVertical(3)
                                    .PaddingHorizontal(5)
                                    .Text(regionRankLabel)
                                    .FontSize(7)
                                    .Bold()
                                    .FontColor(ReportThemeColors.WarningOrangeText);
                            });
                    }
                });
        }

        /// <summary>Renders the donut / gauge on an SKCanvas.</summary>
        static void PaintDonut(SKCanvas canvas, Size size, float score)
        {
            float cx = size.Width / 2f;
            float cy = size.Height / 2f;
            float outerR = Math.Min(cx, cy) - 8f;
            float thick = outerR * 0.30f;
            float mid = outerR - thick / 2f;

            var rect = new SKRect(cx - mid, cy - mid, cx + mid, cy + mid);

            // Background track
            using var bgPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thick,
                Color = SKColor.Parse(ReportThemeColors.SurfaceMint),
                IsAntialias = true
            };
            canvas.DrawOval(rect, bgPaint);

            // Score arc
            using var arcPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thick,
                Color = SKColor.Parse(ReportThemeColors.HeaderNavy),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawArc(rect, -90f, 360f * score / 100f, false, arcPaint);

            // Inner shadow ring
            using var shadowPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                Color = SKColor.Parse(ReportThemeColors.CD0E8DC),
                IsAntialias = true
            };
            canvas.DrawOval(
                new SKRect(cx - mid + thick / 2f + 2, cy - mid + thick / 2f + 2,
                           cx + mid - thick / 2f - 2, cy + mid - thick / 2f - 2),
                shadowPaint);

            // Center: score value
            using var bigTxt = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.HeaderNavy),
                TextSize = 26,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                FakeBoldText = true
            };
            canvas.DrawText($"{score:F1}", cx, cy + 9, bigTxt);

            // Center: sub-label
            using var subTxt = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.Gray9E9E9E),
                TextSize = 8,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText("country score", cx, cy + 21, subTxt);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — Pillar Radar / Spider Card
        // ─────────────────────────────────────────────────────────────────────────────

        void RenderPillarRadarCard(IContainer container, List<PillarChartItem> pillars)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.CAFC4DB)
                .Padding(4)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text("Pillar Performance Radar")
                        .FontSize(10).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                    col.Item().Height(230).Canvas((canvas, size) =>
                        PaintSpiderChart(canvas, size, pillars));
                });
        }

        /// <summary>Renders a filled spider/radar chart onto an SKCanvas.</summary>
        static void PaintSpiderChart(SKCanvas canvas, Size size, List<PillarChartItem> pillars)
        {
            int n = pillars.Count;
            if (n < 3) return;

            float cx = size.Width / 2f;
            float cy = size.Height / 2f;
            float radius = Math.Min(cx, cy) - 42f;

            // ── concentric grid rings ────────────────────────────────────────────
            using var ringPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColor.Parse(ReportThemeColors.HeaderBlueBg),
                StrokeWidth = 0.7f,
                IsAntialias = true
            };
            using var ringLblPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.GraySilver),
                TextSize = 7,
                IsAntialias = true,
                TextAlign = SKTextAlign.Left
            };

            for (int r = 1; r <= 4; r++)
            {
                float rr = radius * r / 4f;
                var pts = BuildRadarPoints(cx, cy, rr, n);
                var path = BuildPath(pts);
                canvas.DrawPath(path, ringPaint);
                canvas.DrawText($"{r * 25}", cx + rr + 2, cy - 2, ringLblPaint);
            }

            // ── spoke axes ──────────────────────────────────────────────────────
            using var axisPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.HeaderBlueWash),
                StrokeWidth = 0.7f,
                IsAntialias = true
            };
            for (int i = 0; i < n; i++)
            {
                var tip = RadarPt(cx, cy, radius, i, n);
                canvas.DrawLine(cx, cy, tip.X, tip.Y, axisPaint);
            }

            // ── data polygon ─────────────────────────────────────────────────────
            var dataPath = new SKPath();
            for (int i = 0; i < n; i++)
            {
                float v = (float)(pillars[i].Value ?? 0) / 100f;
                var pt = RadarPt(cx, cy, radius * v, i, n);
                if (i == 0) dataPath.MoveTo(pt);
                else dataPath.LineTo(pt);
            }
            dataPath.Close();

            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse(ReportThemeColors.MedBlue).WithAlpha(55),
                IsAntialias = true
            };
            using var edgePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                Color = SKColor.Parse(ReportThemeColors.HeaderBlue),
                IsAntialias = true
            };
            canvas.DrawPath(dataPath, fillPaint);
            canvas.DrawPath(dataPath, edgePaint);

            // ── data-point dots ──────────────────────────────────────────────────
            using var dotPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse(ReportThemeColors.HeaderBlue),
                IsAntialias = true
            };
            using var dotBorder = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.2f,
                Color = new SKColor(),
                IsAntialias = true
            };
            for (int i = 0; i < n; i++)
            {
                float v = (float)(pillars[i].Value ?? 0) / 100f;
                var pt = RadarPt(cx, cy, radius * v, i, n);
                canvas.DrawCircle(pt.X, pt.Y, 4f, dotPaint);
                canvas.DrawCircle(pt.X, pt.Y, 4f, dotBorder);
            }

            // ── axis labels ──────────────────────────────────────────────────────
            using var lblPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.DarkBlue),
                TextSize = 8f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            using var valPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.SteelBlue),
                TextSize = 7f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            for (int i = 0; i < n; i++)
            {
                var tip = RadarPt(cx, cy, radius + 26f, i, n);
                canvas.DrawText(
                    Shorten(pillars[i].ShortName ?? pillars[i].Name, 5),
                    tip.X, tip.Y + 3f, lblPaint);
            }
        }

        // ── Radar geometry helpers ───────────────────────────────────────────────────

        static SKPoint RadarPt(float cx, float cy, float r, int i, int n)
        {
            float angle = (-90f + 360f * i / n) * (float)Math.PI / 180f;
            return new SKPoint(cx + r * (float)Math.Cos(angle),
                               cy + r * (float)Math.Sin(angle));
        }

        static SKPoint[] BuildRadarPoints(float cx, float cy, float r, int n)
            => Enumerable.Range(0, n).Select(i => RadarPt(cx, cy, r, i, n)).ToArray();

        static SKPath BuildPath(SKPoint[] pts)
        {
            var p = new SKPath();
            if (pts.Length == 0) return p;
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.Close();
            return p;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — KPI Distribution Band  (4 stat cards)
        // ─────────────────────────────────────────────────────────────────────────────

        static void RenderKpiDistributionBand(
            IContainer container, int total, int green, int amber, int red)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.CAFC4DB)
                .Padding(10)
                .Column(col =>
                {
                    col.Item()
                        .Text("KPI Performance Distribution")
                        .FontSize(9).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                    col.Item().PaddingTop(7).Row(row =>
                    {
                        DashboardStatCard(row.RelativeItem(),
                            green.ToString(), "Performing ≥70%", ReportThemeColors.SuccessGreenBg, ReportThemeColors.SuccessGreen);
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            amber.ToString(), "Developing 40–69%", ReportThemeColors.WarningAmberBg, ReportThemeColors.CostOrange);
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            red.ToString(), "Needs Improvement < 40 %", ReportThemeColors.DangerRedBg, ReportThemeColors.DangerRed);
                        row.ConstantItem(8);
                        DashboardStatCard(row.RelativeItem(),
                            total.ToString(), "Total KPIs", ReportThemeColors.SurfaceMint, ReportThemeColors.PdfDarkGreen);
                    });
                });
        }

       

        /// <summary>Single coloured stat card used inside the distribution band.</summary>
        static void DashboardStatCard(IContainer container, string value, string label, string bg, string textColor)
        {
            container
                .Background(bg)
                .Padding(8)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text(value).FontSize(20).Bold().FontColor(textColor);
                    col.Item().AlignCenter()
                        .Text(label).FontSize(7).FontColor(textColor);
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  DASHBOARD WIDGET — KPI Sparkline (gradient area chart)
        // ─────────────────────────────────────────────────────────────────────────────

        void RenderKpiSparklineCard(IContainer container, List<KpiChartItem> kpis)
        {
            float avg = (float)kpis.Average(k => k.Value);

            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.CAFC4DB)
                .Padding(10)
                .Column(col =>
                {
                    col.Item().Row(hdr =>
                    {
                        hdr.RelativeItem()
                            .Text("KPI Overview — All Indicators (sorted high → low)")
                            .FontSize(9).Bold().FontColor(ReportThemeColors.PdfDarkGreen);
                        hdr.AutoItem()
                            .Text($"Avg: {avg:F1}%")
                            .FontSize(9).Bold().FontColor(GetBarColor(avg));
                    });

                    col.Item().PaddingTop(6).Height(78).Canvas((canvas, size) =>
                        PaintKpiSparkline(canvas, size, kpis));
                });
        }

        /// <summary>
        /// Gradient area sparkline for up to 107 KPIs, sorted descending.
        /// Includes dashed 70 % threshold line.
        /// </summary>
        static void PaintKpiSparkline(SKCanvas canvas, Size size, List<KpiChartItem> kpis)
        {
            var data = kpis.OrderByDescending(k => k.Value).ToList();
            int n = data.Count;
            if (n < 2) return;

            const float lp = 28f, bp = 12f, tp = 4f;
            float w = size.Width - lp;
            float h = size.Height - bp - tp;
            float sx = w / (n - 1);

            // Grid lines
            using var gp = new SKPaint { Color = SKColor.Parse(ReportThemeColors.SurfaceF0F4F1), StrokeWidth = 0.7f };
            using var gl = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.GraySilver),
                TextSize = 7,
                TextAlign = SKTextAlign.Right,
                IsAntialias = true
            };
            foreach (float m in new[] { 25f, 50f, 75f, 100f })
            {
                float y = tp + h - m / 100f * h;
                canvas.DrawLine(lp, y, size.Width, y, gp);
                canvas.DrawText($"{(int)m}", lp - 3, y + 3, gl);
            }

            // Gradient fill under line
            var fPath = new SKPath();
            fPath.MoveTo(lp, tp + h);
            fPath.LineTo(lp, tp + h - (float)(data[0].Value ) / 100f * h);
            for (int i = 1; i < n; i++)
                fPath.LineTo(lp + i * sx, tp + h - (float)(data[i].Value) / 100f * h);
            fPath.LineTo(lp + (n - 1) * sx, tp + h);
            fPath.Close();

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, tp), new SKPoint(0, tp + h),
                new[] { SKColor.Parse(ReportThemeColors.PdfMediumGreen).WithAlpha(95),
                SKColor.Parse(ReportThemeColors.PdfMediumGreen).WithAlpha(8) },
                null, SKShaderTileMode.Clamp);
            using var fp = new SKPaint { Shader = shader, Style = SKPaintStyle.Fill };
            canvas.DrawPath(fPath, fp);

            // Line
            var lPath = new SKPath();
            for (int i = 0; i < n; i++)
            {
                float x = lp + i * sx;
                float y = tp + h - (float)(data[i].Value) / 100f * h;
                if (i == 0) lPath.MoveTo(x, y); else lPath.LineTo(x, y);
            }
            using var lPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.6f,
                Color = SKColor.Parse(ReportThemeColors.PdfMediumGreen),
                IsAntialias = true
            };
            canvas.DrawPath(lPath, lPaint);

            // Dashed 70 % threshold
            float y70 = tp + h - 0.70f * h;
            using var thPaint = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.SuccessGreen).WithAlpha(140),
                StrokeWidth = 0.9f,
                PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
            };
            canvas.DrawLine(lp, y70, size.Width, y70, thPaint);

            using var thLbl = new SKPaint
            {
                Color = SKColor.Parse(ReportThemeColors.SuccessGreen),
                TextSize = 7,
                IsAntialias = true
            };
            canvas.DrawText("70%", size.Width - 24, y70 - 2, thLbl);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        //  REDESIGNED KPI DASHBOARD + PILLAR OVERVIEW
        //  Drop-in replacements for KpiDashboardPage / DrawKpiLineChart /
        //  PillarLineChartPage / DrawPillarsRadialChart
        // ═══════════════════════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────────────────────
        //  KPI DASHBOARD PAGE  ·  numbered bar chart + full-name reference tables
        // ─────────────────────────────────────────────────────────────────────────────

        void KpiDashboardPage(IContainer container, List<KpiChartItem> kpis, bool isAllCountries = false)
        {
            
            if (!kpis.Any()) return;

            int total = kpis.Count;
            int green = kpis.Count(x => x.Value >= 70);
            int amber = kpis.Count(x => x.Value is >= 40 and < 70);
            int red = kpis.Count(x =>  x.Value < 40);
            float avg = (float)kpis.Average(x => x.Value);

            // 13 bars per chart row — compact but legible
            var groups = kpis
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 13)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();


            container.Padding(14).Column(col =>
            {
                col.Spacing(12);

                // ── top summary strip ─────────────────────────────────────────────
                col.Item().Height(70).Element(x =>
                    DrawKpiSummaryBand(x, total, green, amber, red, avg));

                //if(topKpis.Any())
                //    col.Item().Height(130).Element(x =>
                //    DrawTopKpiBand(x,topKpis));

                // ── chart + reference-table sections ─────────────────────────────
                int offset = 0;
                foreach (var group in groups.Where(g => g.Any()))
                {
                    int localOffset = offset;          // capture for lambda
                    col.Item().Element(x => DrawKpiGroupSection(x, group, localOffset, isAllCountries));
                    offset += group.Count;
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  SUMMARY BAND  ·  five stat cards in a dark-green strip
        // ─────────────────────────────────────────────────────────────────────────────

        static void DrawKpiSummaryBand(
            IContainer container,
            int total, int green, int amber, int red, float avg)
        {
            container
                .Background(ReportThemeColors.PdfDarkGreen)
                .Padding(10)
                .Row(row =>
                {
                    KpiStatPill(row.RelativeItem(), total.ToString(), "Total KPIs", ReportThemeColors.BarGreen, ReportThemeColors.BarGreenAlpha);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), green.ToString(), "Performing ≥ 70 %", ReportThemeColors.BarGreen, ReportThemeColors.BarGreenAlpha);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), amber.ToString(), "Developing 40–69 %", ReportThemeColors.BarAmber, ReportThemeColors.BarAmberAlpha);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), red.ToString(), "Needs Improvement < 40 %", ReportThemeColors.BarRed, ReportThemeColors.BarRedAlpha);
                    row.ConstantItem(6);
                    KpiStatPill(row.RelativeItem(), $"{avg:F1}%", "Average Score",
                        avg >= 70 ? ReportThemeColors.BarGreen : avg >= 40 ? ReportThemeColors.BarAmber : ReportThemeColors.BarRed,
                        ReportThemeColors.BarGreenAlpha);
                });
        }


        // ─────────────────────────────────────────────────────────────────────────────
        //  GROUP SECTION  ·  bar chart on top, two-column legend table below
        // ─────────────────────────────────────────────────────────────────────────────

        void DrawKpiGroupSection(IContainer container, List<KpiChartItem> group, int offset, bool isAllCountries = false)
        {
            container
            .Border(1).BorderColor(ReportThemeColors.CC5D9D0)
            .Column(col =>
            {
                // bar chart — numbers printed below each bar
                col.Item().Height(148).Element(x => DrawKpiBarChart(x, group, offset));

                // hairline separator between chart and table
                col.Item().Height(1).Background(ReportThemeColors.CC5D9D0);

                // two-column reference table
                if(!isAllCountries)
                {
                    col.Item().Padding(6).Element(x => DrawKpiReferenceTable(x, group, offset));
                }
            });
        }


        // ─────────────────────────────────────────────────────────────────────────────
        //  BAR CHART  ·  sequential index numbers below each bar (not cryptic codes)
        // ─────────────────────────────────────────────────────────────────────────────
        void DrawKpiBarChart(IContainer container, List<KpiChartItem> data, int offset)
        {
            container
                .Background(Colors.White)
                .Canvas((canvas, size) =>
                {
                    if (!data.Any()) return;

                    const float lp = 8f;   // left pad
                    const float rp = 8f;   // right pad
                    const float tp = 22f;  // top pad  (value labels)
                    const float bp = 26f;  // bottom pad (index labels)

                    float chartW = size.Width - lp - rp;
                    float chartH = size.Height - tp - bp;
                    int n = data.Count;
                    float barW = chartW / n;
                    float innerW = barW * 0.62f;
                    float barGap = (barW - innerW) / 2f;

                    // ── background grid lines ─────────────────────────────────────
                    using var gridPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.SurfaceF2F7F4),
                        StrokeWidth = 0.6f,
                        IsAntialias = false
                    };
                    using var gridLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.BlueGrayLight),
                        TextSize = 7f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Left
                    };

                    foreach (float pct in new[] { 25f, 50f, 75f, 100f })
                    {
                        float gy = tp + chartH - pct / 100f * chartH;
                        canvas.DrawLine(lp, gy, lp + chartW, gy, gridPaint);
                        canvas.DrawText($"{(int)pct}", lp + 2, gy - 2, gridLblPaint);
                    }

                    // ── dashed 70 % performance threshold ────────────────────────
                    float y70 = tp + chartH - 0.70f * chartH;
                    using var threshPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.SuccessGreen).WithAlpha(100),
                        StrokeWidth = 0.9f,
                        PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0),
                        IsAntialias = true
                    };
                    canvas.DrawLine(lp, y70, lp + chartW, y70, threshPaint);

                    // ── paint reused across bars ──────────────────────────────────
                    using var valLblPaint = new SKPaint
                    { TextSize = 6.5f, IsAntialias = true, TextAlign = SKTextAlign.Center };
                    using var numLblPaint = new SKPaint
                    {
                        Color = SKColor.Parse(ReportThemeColors.BlueGray),
                        TextSize = 6.5f,
                        IsAntialias = true,
                        TextAlign = SKTextAlign.Center
                    };

                    // ── bars ──────────────────────────────────────────────────────
                    for (int i = 0; i < n; i++)
                    {
                        float v = (float)(data[i].Value);
                        var shortName = data[i].ShortName;
                        float bx = lp + i * barW + barGap;
                        float bh = v / 100f * chartH;
                        float by = tp + chartH - bh;
                        SKColor color = GetColor(v);
                        SKColor textcolor = v > 85 ? SKColors.White : SKColors.Black;


                        // ghost (full-height tinted background)
                        using var ghostPaint = new SKPaint
                        { Color = color.WithAlpha(35), IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, tp, bx + innerW, tp + chartH), 2, 2),
                            ghostPaint);

                        // filled bar with linear gradient
                        using var shader = SKShader.CreateLinearGradient(
                            new SKPoint(0, by), new SKPoint(0, tp + chartH),
                            new[] { color, color.WithAlpha(180) },
                            null, SKShaderTileMode.Clamp);
                        using var barPaint = new SKPaint { Shader = shader, IsAntialias = true };
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(bx, by, bx + innerW, tp + chartH), 2, 2),
                            barPaint);

                        // top cap accent line
                        using var capPaint = new SKPaint
                        {
                            Color = color,
                            StrokeWidth = 2.5f,
                            StrokeCap = SKStrokeCap.Round,
                            IsAntialias = true
                        };
                        canvas.DrawLine(bx + 1, by, bx + innerW - 1, by, capPaint);

                        // value label above bar
                        float vly = by - 3f;
                        if (vly < tp + 8f) vly = by + 10f;
                        valLblPaint.Color = textcolor;
                        canvas.DrawText($"{v:F1}%", bx + innerW / 2f, vly, valLblPaint);

                        // ── sequential index number below bar (e.g. "1", "2", …) ──
                        // Users cross-reference this with the legend table below.
                        canvas.DrawText(
                            $"{offset + i + 1}. " + shortName,
                            bx + innerW / 2f,
                            size.Height - 6f,
                            numLblPaint);
                    }
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  REFERENCE TABLE  ·  two-column layout, colored status bar, full KPI names
        // ─────────────────────────────────────────────────────────────────────────────
        void DrawKpiReferenceTable(IContainer container, List<KpiChartItem> group, int offset)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(x => DrawKpiInterpretationSection(x, group, offset));
            });
        }


        /// <summary>
        /// Renders all KPIs as paired cards — 2 per row.
        /// Each card: coloured header (code, name, score) + 5-row interpretation mini-table
        /// with the matching range row highlighted.
        /// </summary>
        void DrawKpiInterpretationSection(IContainer container, List<KpiChartItem> allItems,int offset)
        {
            // ── split into rows of 2 ─────────────────────────────────────────────
            var pairs = allItems
                .Select((item, idx) => (item, idx))
                .GroupBy(t => t.idx / 2)
                .Select(g => g.ToList())
                .ToList();

            container.Column(col =>
            {
                col.Spacing(4);

                foreach (var pair in pairs)
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(4);

                        foreach (var (kpi, idx) in pair)
                            row.RelativeItem().Column(card => DrawKpiCard(card, kpi, offset + idx + 1));

                        // pad last row if odd number of KPIs
                        if (pair.Count == 1)
                            row.RelativeItem().Element(_ => { });
                    });
                }
            });
        }
        void DrawKpiCard(ColumnDescriptor card, KpiChartItem kpi, int num)
        {
            var value = kpi.Value;
            var v = value == 100 ? Math.Round(value, 0) : Math.Round(value, 1);
            string accent = GetBarColor((float)v);

            var interps = kpi.InterPretation ?? new List<FiveLevelInterpretationsDto>();
            FiveLevelInterpretationsDto? matched = interps.FirstOrDefault(x =>
                x.MinRange.HasValue && x.MaxRange.HasValue &&
                value >= x.MinRange.Value && value <= x.MaxRange.Value);

            if (matched == null && interps.Any())
                matched = interps
                    .Where(x => x.MinRange.HasValue && x.MaxRange.HasValue)
                    .OrderBy(x => Math.Min(
                        Math.Abs(value - x.MinRange!.Value),
                        Math.Abs(value - x.MaxRange!.Value)))
                    .FirstOrDefault();

            card.Item()
                .Border(0.5f).BorderColor(accent)
                .Column(inner =>
                {
                    // ── 1. KPI header band ──────────────────────────────────────────
                    // Definition removed from here — gets its own strip below
                    inner.Item()
                         .Background(accent)
                         .PaddingHorizontal(5).PaddingVertical(3)
                         .Row(h =>
                         {
                             // Number bubble
                             h.ConstantItem(16)
                              .AlignMiddle()
                              .Background(ReportThemeColors.OverlayBlackAlpha)
                              .AlignCenter()
                              .Text($"{num}")
                              .FontSize(6f).Bold().FontColor(ReportThemeColors.White);

                             // Code + Name
                             h.RelativeItem()
                              .PaddingLeft(4)
                              .AlignMiddle()
                              .Column(nc =>
                              {
                                  nc.Item()
                                    .Text(kpi.ShortName ?? "")
                                    .FontSize(7.5f).Bold().FontColor(ReportThemeColors.White);
                                  nc.Item()
                                    .Text(kpi.Name ?? "")
                                    .FontSize(5f).FontColor(ReportThemeColors.WhiteAlpha73);
                              });

                             // Score
                             h.ConstantItem(34)
                              .AlignMiddle().AlignRight()
                              .Text($"{v}%")
                              .FontSize(9.5f).Bold().FontColor(ReportThemeColors.White);
                         });

                    // ── 2. Definition strip ─────────────────────────────────────────
                    // Shown only when definition exists; wraps gracefully for long text
                    if (!string.IsNullOrWhiteSpace(kpi.Definition))
                    {
                        inner.Item()
                             .Background(ReportThemeColors.SurfacePale)                      // very pale green-grey
                             .BorderTop(0.3f).BorderColor(accent)
                             .BorderBottom(0.3f).BorderColor(ReportThemeColors.Divider)
                             .PaddingHorizontal(5).PaddingVertical(3)
                             .Row(dr =>
                             {
                                 // Small label pill
                                 dr.ConstantItem(28)
                                   .AlignTop()
                                   .PaddingTop(0.5f)
                                   .Text("DEF")
                                   .FontSize(4.5f).Bold()
                                   .FontColor(accent);

                                 // Definition text — italic, wraps, keeps card compact
                                 dr.RelativeItem()
                                   .Text(kpi.Definition)
                                   .FontSize(5.5f).Italic()
                                   .FontColor(ReportThemeColors.TextMuted)
                                   .LineHeight(1.25f);
                             });
                    }

                    // ── 3. Interpretation column sub-header ─────────────────────────
                    inner.Item()
                         .Background(ReportThemeColors.ShadeEBEBEB)
                         .PaddingHorizontal(4).PaddingVertical(2)
                         .Row(sh =>
                         {
                             sh.ConstantItem(46)
                               .Text("Range")
                               .FontSize(5.5f).Bold().FontColor(ReportThemeColors.TextLabel);
                             sh.RelativeItem()
                               .Text("Condition")
                               .FontSize(5.5f).Bold().FontColor(ReportThemeColors.TextLabel);
                         });

                    // ── 4. Five interpretation rows ─────────────────────────────────
                    for (int i = 0; i < interps.Count; i++)
                    {
                        var interp = interps[i];
                        bool isHit = interp == matched;

                        string rowBg = isHit ? accent : (i % 2 == 0 ? ReportThemeColors.White : ReportThemeColors.RowAlt);
                        string rangeFg = isHit ? ReportThemeColors.White : ReportThemeColors.TextFaint;
                        string condFg = isHit ? ReportThemeColors.White : ReportThemeColors.TextBody;

                        string rangeStr = (interp.MinRange.HasValue && interp.MaxRange.HasValue)
                            ? $"{Math.Round(interp.MinRange.Value, 0)}–{Math.Round(interp.MaxRange.Value, 0)}"
                            : "—";

                        inner.Item()
                             .BorderBottom(0.3f).BorderColor(ReportThemeColors.GrayE0E0E0)
                             .Background(rowBg)
                             .PaddingHorizontal(4).PaddingVertical(2)
                             .Row(r =>
                             {
                                 r.ConstantItem(46)
                                  .Text(rangeStr)
                                  .FontSize(6f).FontColor(rangeFg);

                                 r.RelativeItem()
                                  .Text(interp.Condition ?? "—")
                                  .FontSize(6.5f)
                                  .Bold()
                                  .FontColor(condFg);
                             });
                    }
                });
        }

        static void KpiStatPill(
            IContainer container, string value, string label, string valueColor, string bg)
        {
            container
                .Background(bg)
                .Padding(6)
                .Column(c =>
                {
                    c.Item().AlignCenter()
                        .Text(value).FontSize(15).Bold().FontColor(valueColor);
                    c.Item().AlignCenter()
                        .Text(label).FontSize(6.5f).FontColor(ReportThemeColors.WhiteAlpha73);
                });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  PILLAR OVERVIEW PAGE  ·  redesigned horizontal bar layout + ring chart
        // ─────────────────────────────────────────────────────────────────────────────

        void PillarLineChartPage(IContainer container, List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).Take(23).ToList();
            if (!data.Any()) return;

            float avg = (float)data.Average(x => x.Value ?? 0);
            var best = data.OrderByDescending(x => x.Value).First();
            var worst = data.OrderBy(x => x.Value).First();

            container.Padding(16).Column(col =>
            {
                col.Spacing(10);

                // ── two-column layout: ring chart (left) + bar list (right) ──────
                col.Item().Height(420).Row(row =>
                {
                    // Left: radial ring chart
                    row.RelativeItem(5).Element(x => DrawPillarsRadialChart(x, data));

                    row.ConstantItem(12);

                    // Right: horizontal bar list
                    row.RelativeItem(6).Element(x =>
                        DrawPillarHorizontalBars(x, data));
                });

                // ── bottom: avg score + best/worst ───────────────────────────────
                col.Item().Element(x =>
                    DrawPillarFooterBand(x, avg, best, worst));
            });
        }

        // ── horizontal bar list for pillars ─────────────────────────────────────────

        static void DrawPillarHorizontalBars(IContainer container, List<PillarChartItem> data)
        {
            var sorted = data.OrderByDescending(x => x.Value).ToList();

            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.CDDE8E3)
                .Padding(14)
                .Column(col =>
                {
                    col.Item().PaddingBottom(8)
                        .Text("Pillar Overview").FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                    col.Spacing(6);
                    int index = 1;
                    foreach (var item in sorted)
                    {
                        float v = (float)(item.Value ?? 0);
                        var color = GetBarColor(v);

                        col.Item().Row(row =>
                        {
                            // Pillar label
                            row.ConstantItem(102).AlignMiddle()
                                .Text(Shorten(item.Name ?? item.ShortName ?? "—", 18))
                                .FontSize(8).FontColor(ReportThemeColors.BlueGrayDark);

                            // Bar track
                            row.RelativeItem().AlignMiddle().Height(13)
                                .Background(ReportThemeColors.SurfaceF0F4F1)
                                .Canvas((canvas, size) =>
                                {
                                    // filled portion with gradient
                                    float fillW = size.Width * v / 100f;
                                    SKColor barC = SKColor.Parse(color);

                                    using var shader = SKShader.CreateLinearGradient(
                                        new SKPoint(0, 0),
                                        new SKPoint(fillW, 0),
                                        new[] { barC.WithAlpha(210), barC },
                                        null,
                                        SKShaderTileMode.Clamp);
                                    using var fp = new SKPaint
                                    { Shader = shader, IsAntialias = true };
                                    canvas.DrawRoundRect(
                                        new SKRoundRect(
                                            new SKRect(0, 0, fillW, size.Height), 3, 3), fp);
                                });

                            // Score badge
                            row.ConstantItem(65).AlignMiddle().AlignRight()
                                .Text($"{v:F1}, Rank {index++}/{data.Count}")
                                .FontSize(8).Bold().FontColor(color);
                        });
                    }
                });
        }

        // ── footer band: avg + best + worst ─────────────────────────────────────────

        static void DrawPillarFooterBand(
            IContainer container, float avg, PillarChartItem best, PillarChartItem worst)
        {
            container.Row(row =>
            {
                // Average score
                row.RelativeItem(2)
                    .Background(ReportThemeColors.PdfDarkGreen)
                    .Padding(12)
                    .Column(c =>
                    {
                        c.Item().AlignCenter()
                            .Text("Average Score").FontSize(9).FontColor(ReportThemeColors.SuccessGreenLight);
                        c.Item().AlignCenter()
                            .Text($"{avg:F1}")
                            .FontSize(22).Bold()
                            .FontColor(GetBarColor(avg) == ReportThemeColors.SuccessGreen ? ReportThemeColors.SuccessGreenBright
                                     : GetBarColor(avg) == ReportThemeColors.WarningAmber ? ReportThemeColors.WarningAmberLight : ReportThemeColors.BarRed);
                    });

                row.ConstantItem(6);

                // Best pillar
                row.RelativeItem(3)
                    .Background(ReportThemeColors.SuccessGreenBg)
                    .Border(1).BorderColor(ReportThemeColors.SuccessGreenSoft)
                    .Padding(10)
                    .Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem()
                                .Background(ReportThemeColors.SuccessGreen).Padding(3)
                                .Text("▲ BEST").FontSize(7).Bold().FontColor(Colors.White);
                            r.ConstantItem(6);
                            r.RelativeItem()
                                .Text(Shorten(best.Name ?? "—", 26))
                                .FontSize(9).Bold().FontColor(ReportThemeColors.SuccessGreenDark);
                        });
                        c.Item().PaddingTop(4)
                            .Text($"{best.Value:F1}").FontSize(16).Bold().FontColor(ReportThemeColors.SuccessGreen);
                    });

                row.ConstantItem(6);

                // Worst pillar
                row.RelativeItem(3)
                    .Background(ReportThemeColors.DangerRedBg)
                    .Border(1).BorderColor(ReportThemeColors.DangerRedSoft)
                    .Padding(10)
                    .Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem()
                                .Background(ReportThemeColors.DangerRed).Padding(3)
                                .Text("▼ LOWEST").FontSize(7).Bold().FontColor(Colors.White);
                            r.ConstantItem(6);
                            r.RelativeItem()
                                .Text(Shorten(worst.Name ?? "—", 26))
                                .FontSize(9).Bold().FontColor(ReportThemeColors.DangerRedDark);
                        });
                        c.Item().PaddingTop(4)
                            .Text($"{worst.Value:F1}").FontSize(16).Bold().FontColor(ReportThemeColors.DangerRed);
                    });
            });
        }

        // ── radial ring chart (left panel) ──────────────────────────────────────────

        void DrawPillarsRadialChart(IContainer container, List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).OrderByDescending(x=>x.Value).ToList();
            if (!data.Any()) return;

            float avg = (float)data.Average(x => x.Value ?? 0);

            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.CDDE8E3)
                            .Canvas((canvas, size) => DrawPillarsRadialChartCanvas(canvas, size, data));
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  HEADERS / FOOTERS
        // ─────────────────────────────────────────────────────────────────────────────
        void CountryComposeHeader(
            IContainer container,
            AiCountrySummeryDto data,
            UserRole userRole,
            string? pillarName)
        {
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/pem.png");

            container.Column(column =>
            {
                column.Item().Background(ReportThemeColors.HeaderNavy).Padding(8).Row(row =>
                {
                    // Left content
                    row.RelativeItem().Column(col =>
                    {
                        col.Spacing(2);

                        string? title = string.IsNullOrEmpty(pillarName) ? data.CountryName : pillarName!;

                        col.Item().Text(title)
                            .FontSize(21)
                            .Bold()
                            .FontColor(Colors.White);

                        col.Item().Text($"{data.CountryName}, {data.Continent} | Data Year: {data.Year}")
                            .FontSize(10)
                            .FontColor(ReportThemeColors.SurfaceE8F3F0);

                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                            .FontSize(8)
                            .FontColor(ReportThemeColors.SurfaceCFE3DD);
                    });

                    // Right logo
                    row.ConstantItem(80)
                        .AlignRight()
                        .AlignMiddle()
                        .Background(Colors.White)
                        .Padding(4)
                        .Image(logoPath)
                        .FitArea();
                });

                // Divider
                column.Item().LineHorizontal(1).LineColor(ReportThemeColors.SurfaceD9E2DF);
            });
        }        

        void PillarComposeHeader(IContainer container, AiCountryPillarResponse data)
        {
            var logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/pem.png");

            container.Column(column =>
            {
                column.Item().Background(ReportThemeColors.HeaderNavy).Padding(12).Row(row =>
                {
                    // Left content
                    row.RelativeItem().Column(col =>
                    {
                        col.Spacing(2);

                        col.Item().Text(data.PillarName)
                            .FontSize(21)
                            .Bold()
                            .FontColor(Colors.White);

                        col.Item().Text($"{data.CountryName}, {data.Continent} | Data Year: {data.AIDataYear}")
                            .FontSize(10)
                            .FontColor(ReportThemeColors.SurfaceE8F3F0);

                        col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                            .FontSize(8)
                            .FontColor(ReportThemeColors.SurfaceCFE3DD);
                    });

                    // Logo
                    row.ConstantItem(90)
                        .AlignRight()
                        .AlignMiddle()
                        .Background(Colors.White)
                        .Padding(1)
                        .Image(logoPath)
                        .FitArea();
                });

                column.Item().LineHorizontal(1).LineColor(ReportThemeColors.SurfaceD9E2DF);
            });
        }

        static void PillarComposeFooter(IContainer container)
        {
            container.AlignCenter().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text(text =>
                    {
                        text.Span("Page "); text.CurrentPageNumber();
                        text.Span(" of "); text.TotalPages();
                    });
                    col.Item().PaddingTop(5).AlignCenter()
                        .Text("Country Assessment Platform").FontSize(8).FontColor(ReportThemeColors.Gray9E9E9E);
                });
            });
        }
        string SanitizeText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace('\u2011', '-') // non-breaking hyphen
                .Replace('\u2010', '-') // hyphen
                .Replace('\u2012', '-') // figure dash
                .Replace('\u2013', '-') // en dash
                .Replace('\u2014', '-') // em dash
                .Replace('\u2212', '-') // minus sign
                .Replace('\u00AD', ' ') // soft hyphen (invisible troublemaker)
                .Normalize(NormalizationForm.FormKC);
        }

        void CountrySummeryComposeContent(IContainer container, AiCountrySummeryDto data, UserRole userRole, bool isAllCountries=false)
        {
            container.PaddingTop(4).Column(column =>
            {
                // =========================
                // PROGRESS SECTION
                // =========================

                column.Item().PaddingTop(10)
                    .Element(c => CountryProgressSection(c, data, userRole));

                // =========================
                // EXECUTIVE SUMMARY
                // =========================
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", SanitizeText(data.EvidenceSummary), ReportThemeColors.C163329));

                if (!isAllCountries) {
                    // =====================================================
                    // Current situation
                    // =====================================================
                    if (!string.IsNullOrEmpty(data.KeyDevelopments))
                        column.Item().PaddingTop(8).Element(c =>
                            PillarContentSection(c, "Key Developments", SanitizeText(data.KeyDevelopments), ReportThemeColors.HeaderBlue));
                    if (!string.IsNullOrEmpty(data.CriticalRisks))
                        column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Critical Risks", SanitizeText(data.CriticalRisks), ReportThemeColors.HeaderBlueMid));
                    if (!string.IsNullOrEmpty(data.Gaps))
                        column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Gaps", SanitizeText(data.Gaps), ReportThemeColors.HeaderBlueLight));





                    // =====================================================
                    // EVIDENCE SECTION
                    // =====================================================              


                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Structural Evidence", SanitizeText(data.StructuralEvidence), ReportThemeColors.CE6CCFF));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Operational Evidence", SanitizeText(data.OperationalEvidence), ReportThemeColors.CC2F0F0));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Outcome Evidence", SanitizeText(data.OutcomeEvidence), ReportThemeColors.CFFE6CC));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Perception Evidence", SanitizeText(data.PerceptionEvidence), ReportThemeColors.CE6F7FF));

                    // =====================================================
                    // INTEGRITY CHECKS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("Integrity Checks")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Temporal Scope", SanitizeText(data.TemporalScope), ReportThemeColors.CD9E6FF));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Distortion Screening", SanitizeText(data.DistortionScreening), ReportThemeColors.CF2D9E6));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Relational Integrity", SanitizeText(data.RelationalIntegrity), ReportThemeColors.CF0FFE6));

                    // =====================================================
                    // STRESS TESTS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("Stress Tests")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Political Shock", SanitizeText(data.PoliticalShock), ReportThemeColors.CFFD9CC));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Economic Shock", SanitizeText(data.EconomicShock), ReportThemeColors.CFFF2CC));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Narrative Shock", SanitizeText(data.NarrativeShock), ReportThemeColors.CE6F2FF));
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(8).Element(c =>
                    //    PillarContentSection(c, "Overall Stress Resilience", SanitizeText(data.OverallStressResilience), ReportThemeColors.CE6FFE6));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Stress Score Adjustment", SanitizeText(data.StressScoreAdjustment), ReportThemeColors.CFFE6F2));

                    // =====================================================
                    // GOVERNANCE ADJUSTMENTS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("Governance Adjustments")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Inequality Adjustment", SanitizeText(data.InequalityAdjustment), ReportThemeColors.CF9E6FF));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Opacity Risk", SanitizeText(data.OpacityRisk), ReportThemeColors.CFFF0E6));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Non Compensation Note", SanitizeText(data.NonCompensationNote), ReportThemeColors.CE6FFF9));

                    // =====================================================
                    // SYSTEM ANALYSIS
                    // =====================================================
                    //column.Item().PageBreak();

                    //column.Item().PaddingTop(15).Text("System Analysis")
                    //    .FontSize(16).Bold();

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Cross-Pillar System Dynamics", SanitizeText(data.CrossPillarPatterns), ReportThemeColors.C6E9688));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Institutional Capacity Assessment", SanitizeText(data.InstitutionalCapacity), ReportThemeColors.C0D8057));

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Equity Assessment", SanitizeText(data.EquityAssessment), ReportThemeColors.SuccessGreenBg));

                    //column.Item().PageBreak();
                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Conflict Risk Outlook", SanitizeText(data.ConflictRiskOutlook), ReportThemeColors.CFCE4EC));

                    // =====================================================
                    // STRATEGIC OUTPUT
                    // =====================================================
                    //column.Item().PageBreak();                

                    column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Strategic Policy Priorities", SanitizeText(data.StrategicRecommendation), ReportThemeColors.C2E9975));

                    if (!string.IsNullOrEmpty(data.KeyFindings))
                        column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Key Findings", SanitizeText(data.KeyFindings), ReportThemeColors.C0D47A1));
                }                
            
            });
        }
        void AssessmentRecommendations(IContainer container, AiCountrySummeryDto data, UserRole userRole, bool isAllCountries = false)
        {
            container.PaddingTop(4).Column(column =>
            {
                if (!isAllCountries)
                {

                    if (!string.IsNullOrEmpty(data.Recommendations))
                        column.Item().PaddingTop(8).Element(c =>
                        PillarContentSection(c, "Recommendations", SanitizeText(data.Recommendations), ReportThemeColors.C00695C));
                }
            });
        }

        void PillarComposeContent(
     IContainer container, AiCountryPillarResponse data, UserRole userRole)
        {
            container.PaddingTop(8).Column(column =>
            {
                // =========================
                // PROGRESS SECTION
                // =========================
                column.Item().PaddingTop(10)
                    .Element(c => PillarProgressSection(c, data, userRole));

                // =========================
                // EXECUTIVE SUMMARY
                // =========================
                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", SanitizeText(data.EvidenceSummary), ReportThemeColors.C163329));

               
                // =====================================================
                // EVIDENCE SECTION
                // =====================================================
                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Structural Evidence", SanitizeText(data.StructuralEvidence), ReportThemeColors.HeaderBlue));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Operational Evidence", SanitizeText(data.OperationalEvidence), ReportThemeColors.HeaderBlueMid));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Outcome Evidence", SanitizeText(data.OutcomeEvidence), ReportThemeColors.HeaderBlueLight));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Perception Evidence", SanitizeText(data.PerceptionEvidence), ReportThemeColors.HeaderBluePale));

                // =====================================================
                // INTEGRITY CHECKS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Temporal Scope", SanitizeText(data.TemporalScope), ReportThemeColors.C5F497A));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Distortion Screening", SanitizeText(data.DistortionScreening), ReportThemeColors.C8064A2));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Relational Integrity", SanitizeText(data.RelationalIntegrity), ReportThemeColors.CB1A0C7));

                // =====================================================
                // STRESS TESTS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Political Shock", SanitizeText(data.StressPoliticalShock), ReportThemeColors.C7F6000));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Economic Shock", SanitizeText(data.StressEconomicShock), ReportThemeColors.CBF9000));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Narrative Shock", SanitizeText(data.StressNarrativeShock), ReportThemeColors.CFFD966));

                //column.Item().PageBreak();

                //column.Item().PaddingTop(8).Element(c =>
                //    PillarContentSection(c, "Overall Stress Resilience", SanitizeText(data.StressOverallResilience), ReportThemeColors.CC55A11));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Stress Score Adjustment", SanitizeText(data.StressScoreAdjustment), ReportThemeColors.CE26B0A));

                // =====================================================
                // GOVERNANCE ADJUSTMENTS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Inequality Adjustment", SanitizeText(data.InequalityAdjustment), ReportThemeColors.C274E13));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Opacity Risk", SanitizeText(data.OpacityRisk), ReportThemeColors.C38761D));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Non Compensation Note", SanitizeText(data.NonCompensationNote), ReportThemeColors.C6AA84F));

                // =====================================================
                // ALERTS & EQUITY
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Red Flags", SanitizeText(data.RedFlag), ReportThemeColors.AccentOrange, ReportThemeColors.AccentRed));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Geographic Equity Note", SanitizeText(data.GeographicEquityNote), ReportThemeColors.C0D8057));

                // =====================================================
                // SYSTEM / INSTITUTIONAL ANALYSIS
                // =====================================================
                //column.Item().PageBreak();

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Institutional Assessment", SanitizeText(data.InstitutionalAssessment), ReportThemeColors.C2E9975));

                column.Item().PaddingTop(8).Element(c =>
                    PillarContentSection(c, "Analytical Foundations and Data Integration", SanitizeText(data.DataGapAnalysis), ReportThemeColors.CA4BAB2));

                // =====================================================
                // DATA SOURCES
                // =====================================================
                if (data.DataSourceCitations?.Any() == true)
                {
                    column.Item().PageBreak();

                    column.Item().PaddingTop(8).Element(c =>
                        DataSourcesSection(c, data.DataSourceCitations.ToList()));
                }
            });
        }

        void CountryProgressSection(IContainer container, AiCountrySummeryDto data, UserRole userRole)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.GrayE5E7EB)
                .Padding(18)
                .Column(column =>
                {
                    // Header
                    column.Item().Text("Overview")
                        .FontSize(16)
                        .SemiBold()
                        .FontColor(ReportThemeColors.Gray1F2937);

                    column.Item().PaddingTop(8).Column(col =>
                    {
                        // Score Section
                        PillarProgressBar(col, "Total Score", data.AIProgress, ReportThemeColors.AccentTeal);

                        col.Item().PaddingVertical(12);

                        // Divider
                        col.Item().Height(1).Background(ReportThemeColors.SubHeaderBg);

                        col.Item().PaddingTop(12);

                        // Ranking Section Title
                        col.Item().Text("Rankings")
                            .FontSize(13)
                            .SemiBold()
                            .FontColor(ReportThemeColors.Gray374151);

                        col.Item().PaddingTop(8);

                        RankRowModern(col, "Global Rank", data.Rank, data.TotalCountry, ReportThemeColors.AccentGreen);

                        col.Item().PaddingTop(2);

                        RankRowModern(col, $"{data.Region} Region Rank", data.RegionRank, data.RegionTotalCountry, ReportThemeColors.AccentBlue);
                    });
                });
        }
        void RankRowModern(ColumnDescriptor column, string label, int? rank, int? total, string color)
        {
            column.Item().PaddingVertical(4).Row(row =>
            {
                row.RelativeItem()
                    .Text(label)
                    .FontSize(11)
                    .FontColor(ReportThemeColors.Gray4B5563);

                row.AutoItem().AlignRight().Element(e =>
                {
                    e.PaddingHorizontal(10)
                     .PaddingVertical(4)
                     .Padding(2)
                     .Background(ReportThemeColors.GrayF9FAFB)
                     .Border(1)
                     .BorderColor(ReportThemeColors.GrayE5E7EB)
                     .Text(txt =>
                     {
                         if (rank.HasValue && total.HasValue)
                         {
                             txt.Span($"{rank}")
                                .Bold()
                                .FontColor(color);

                             txt.Span($" / {total}")
                                .FontColor(ReportThemeColors.Gray6B7280);
                         }
                         else
                         {
                             txt.Span("-").FontColor(ReportThemeColors.Gray9CA3AF);
                         }
                     });
                });
            });
        }

        void PillarProgressSection(
            IContainer container, AiCountryPillarResponse data, UserRole userRole, bool isCity = false)
        {
            container
                .Background(Colors.White)
                .Border(1).BorderColor(ReportThemeColors.GrayE5E7EB)
                .Padding(18)
                .Column(column =>
                {
                    column.Item().Text(isCity ? "Total Overview" : "Pillar Score")
                        .FontSize(16)
                        .SemiBold()
                        .FontColor(ReportThemeColors.Gray1F2937);

                    column.Item().PaddingTop(15);

                    PillarProgressBar(column, "Score", data.AIProgress, ReportThemeColors.AccentTeal);
                });
        }
        void PillarProgressBar(ColumnDescriptor column, string label, decimal? percentage, string color)
        {
            float per = (float)Math.Min((double)(percentage ?? 0), 100);

            column.Item().Column(col =>
            {
                // Label + Value Row
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text(label)
                        .FontSize(11)
                        .FontColor(ReportThemeColors.Gray4B5563);

                    row.AutoItem()
                        .Text($"{percentage ?? 0:F1}")
                        .FontSize(11)
                        .Bold()
                        .FontColor(ReportThemeColors.Gray111827);
                });

                col.Item().PaddingTop(6);

                // Progress Bar
                col.Item().Height(8).Background(ReportThemeColors.GrayE5E7EB).Row(barRow =>
                {
                    barRow.RelativeItem(per)
                        .Background(color);

                    barRow.RelativeItem(100 - per);
                });
            });
        }

        /// <summary>Generic titled content block with accent bar.</summary>
        static void PillarContentSection(
            IContainer container, string title, string content, string accentColor, string textcolor = ReportThemeColors.TextDark)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background(accentColor);
                    row.RelativeItem().Background(ReportThemeColors.ShadeLight).Padding(12)
                        .Text(title).FontSize(15).Bold().FontColor(ReportThemeColors.TextNearBlack);
                });

                column.Item()
                    .Background(Colors.White)
                    .Border(1).BorderColor(ReportThemeColors.GrayE0E0E0)
                    .Padding(18)
                    .Text(content)
                    .FontSize(10).LineHeight(1.6f).FontColor(textcolor);
            });
        }

        void DataSourcesSection(IContainer container, List<AIDataSourceCitation> sources)
        {
            // 🔒 Sanitize entire list first (best practice)
            var safeSources = sources?
                .Select(s => new AIDataSourceCitation
                {
                    SourceName = SanitizeText(s.SourceName),
                    SourceType = SanitizeText(s.SourceType),
                    DataExtract = SanitizeText(s.DataExtract),
                    SourceURL = SanitizeText(s.SourceURL),
                    TrustLevel = s.TrustLevel,
                    DataYear = s.DataYear
                })
                .ToList() ?? new List<AIDataSourceCitation>();

            container.Column(column =>
            {
                // Header
                column.Item().Row(row =>
                {
                    row.ConstantItem(5).Background(ReportThemeColors.C396154);

                    row.RelativeItem()
                        .Background(ReportThemeColors.ShadeLight)
                        .Padding(12)
                        .Text(SanitizeText("Data Source Citations")) // 🔒 safe
                        .FontSize(15).Bold().FontColor(ReportThemeColors.TextNearBlack);
                });

                // Content Box
                column.Item().PaddingTop(10)
                    .Background(Colors.White)
                    .Border(1)
                    .BorderColor(ReportThemeColors.GrayE0E0E0)
                    .Padding(15)
                    .Column(col =>
                    {
                        foreach (var source in safeSources.Take(10))
                        {
                            col.Item().PaddingBottom(15).Column(sourceCol =>
                            {
                                // ── Row 1: Name + Type ─────────────────────
                                sourceCol.Item().Row(row =>
                                {
                                    row.RelativeItem()
                                        .Text(source.SourceName ?? "-")
                                        .FontSize(11).Bold().FontColor(ReportThemeColors.C2C423B);

                                    row.ConstantItem(100).AlignRight()
                                        .Background(GetSourceTypeBadgeColor(source.SourceType))
                                        .Padding(3)
                                        .Text(source.SourceType ?? "-")
                                        .FontSize(8)
                                        .FontColor(Colors.White);
                                });

                                // ── Row 2: Trust + Year ────────────────────
                                sourceCol.Item().PaddingTop(4).Row(row =>
                                {
                                    row.AutoItem()
                                        .Text(SanitizeText($"Trust Level: {source.TrustLevel}/7"))
                                        .FontSize(9).FontColor(ReportThemeColors.Gray757575);

                                    row.AutoItem()
                                        .PaddingLeft(15)
                                        .Text(SanitizeText($"Year: {source.DataYear}"))
                                        .FontSize(9).FontColor(ReportThemeColors.Gray757575);
                                });

                                // ── Data Extract ───────────────────────────
                                if (!string.IsNullOrWhiteSpace(source.DataExtract))
                                {
                                    sourceCol.Item().PaddingTop(6)
                                        .Text(TruncateText(source.DataExtract, 200))
                                        .FontSize(9)
                                        .FontColor(ReportThemeColors.Gray616161)
                                        .Italic();
                                }

                                // ── URL ────────────────────────────────────
                                if (!string.IsNullOrWhiteSpace(source.SourceURL))
                                {
                                    sourceCol.Item().PaddingTop(4)
                                        .Text(source.SourceURL)
                                        .FontSize(8)
                                        .FontColor(ReportThemeColors.C305246)
                                        .Underline();
                                }
                            });

                            // Divider
                            if (source != safeSources.Last())
                            {
                                col.Item()
                                    .PaddingBottom(10)
                                    .LineHorizontal(1)
                                    .LineColor(ReportThemeColors.DividerLight);
                            }
                        }
                    });
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        //  COLOR / FORMAT UTILITIES  (all static, reusable across pages)
        // ─────────────────────────────────────────────────────────────────────────────


        static SKColor GetColor(float value)
        {
            if (value >= 80) return SKColor.Parse(ReportThemeColors.DangerRed);
            else if (value >= 60) return SKColor.Parse(ReportThemeColors.WarningOrange);
            else if(value >= 40) return SKColor.Parse(ReportThemeColors.WarningAmber);
            else if(value >= 20) return SKColor.Parse(ReportThemeColors.SuccessGreenMid);
            return SKColor.Parse(ReportThemeColors.SuccessGreen);
        }
        static string GetBarColor(float value)
        {
            if (value >= 80) return ReportThemeColors.DangerRed;
            else if (value >= 60) return ReportThemeColors.WarningOrange;
            else if (value >= 40) return ReportThemeColors.WarningAmber;
            else if (value >= 20) return ReportThemeColors.SuccessGreenMid;
            return ReportThemeColors.SuccessGreen;
        }

        static string GetSourceTypeBadgeColor(string sourceType) => sourceType?.ToLower() switch
        {
            "government" => ReportThemeColors.SourceGovernment,
            "academic" => ReportThemeColors.SourceAcademic,
            "international" => ReportThemeColors.SourceInternational,
            "news/ngo" => ReportThemeColors.SourceNewsNgo,
            _ => ReportThemeColors.SourceDefault
        };

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Length <= max ? text : text[..max] + "…";
        }

        static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text[..maxLength] + "...";
        }



        #endregion pdf pillars and country report

    }

    public partial class PdfGeneratorService
    {   

        // Palette: index 0 = selected country (gold), 1-5 = peer countries
        private static readonly string[] CountryPalette =
        {
            ReportThemeColors.ChartGold,   // gold  – selected country
            ReportThemeColors.ChartTeal,   // teal
            ReportThemeColors.ChartBlue,   // blue
            ReportThemeColors.ChartOrange,   // orange
            ReportThemeColors.ChartPurple,   // purple
            ReportThemeColors.ChartRed,   // red
        };

        // Pillar palette (up to 14 distinct colours)
        private static readonly string[] PillarPalette =
        {
            ReportThemeColors.PdfDarkGreen,ReportThemeColors.PdfMediumGreen,ReportThemeColors.ChartTeal,ReportThemeColors.ChartGold,ReportThemeColors.ChartAmber,
            ReportThemeColors.ChartRed,ReportThemeColors.ChartPurple,ReportThemeColors.ChartBlue,ReportThemeColors.ChartLeaf,ReportThemeColors.ChartOrange,
            ReportThemeColors.ChartCyan,ReportThemeColors.ChartBrown,ReportThemeColors.ChartPink,ReportThemeColors.ChartSlate
        };

        // ══════════════════════════════════════════════════════════════════════════
        //  ENTRY POINTS  – called from AddCountryDetailsPdf
        // ══════════════════════════════════════════════════════════════════════════

        void AddPeerCountryComparisonSection(
            IDocumentContainer container,
            List<PeerCountryHistoryReportDto> peerCountries,
            AiCountrySummeryDto countryDetails,
            UserRole userRole)
        {
            if (peerCountries == null || !peerCountries.Any()) return;

            // Separate: main country entry + actual peer entries (cap at MaxpeerCountries)
            var main = FindMainCountry(peerCountries, countryDetails);
            var peers = peerCountries
                .Where(p => !IsSameCountry(p.CountryName, countryDetails.CountryName))
                .ToList();

            // ── 5.1  Population-Based ────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, "Population-Based Peer Comparison"));
                page.Content().Element(c =>
                    PopulationPeerPage(c, peers, main, countryDetails));
                PageFooter(page);
            });

            // ── 5.2  Regional ────────────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, "Regional Peer Group Comparison"));
                page.Content().Element(c =>
                    RegionalPeerPage(c, peers, main, countryDetails));
                PageFooter(page);
            });

            // ── 5.3  Income-Level ────────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, "Income-Level Peer Comparison"));
                page.Content().Element(c =>
                    IncomePeerPage(c, peers, main, countryDetails));
                PageFooter(page);
            });

           
            // ── 5.5  Relative Ranking ────────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, "Relative Ranking Among Peer countries"));
                page.Content().Element(c =>
                    RelativeRankingPage(c, peers, main, countryDetails));
                PageFooter(page);
            });
        }

        void AddPerformanceTrendsSection(
            IDocumentContainer container,
            List<PeerCountryHistoryReportDto> peerCountries,
            AiCountrySummeryDto countryDetails,
            UserRole userRole)
        {
            if (peerCountries == null || !peerCountries.Any()) return;

            var main = FindMainCountry(peerCountries, countryDetails);
            var peers = peerCountries
                .Where(p => !IsSameCountry(p.CountryName, countryDetails.CountryName))      
                .ToList();

            // ── 6.1 + 6.2  Historical & Five-Year Evolution ──────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, "Performance Trends Over Time"));
                page.Content().Element(c =>
                    HistoricalTrendsPage(c, peers, main, countryDetails));
                PageFooter(page);
            });

            // ── 6.3  Pillar-Level Trend ──────────────────────────────────────────
            container.Page(page =>
            {
                ApplyPageDefaults(page);
                page.Header().Element(x =>
                    CountryComposeHeader(x, countryDetails, userRole, "Pillar-Level Trend Analysis"));
                page.Content().Element(c =>
                    PillarTrendPage(c, main, countryDetails));
                PageFooter(page);
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  5.1  POPULATION-BASED PEER GROUP COMPARISON
        // ══════════════════════════════════════════════════════════════════════════

        void PopulationPeerPage(
            IContainer container,
            List<PeerCountryHistoryReportDto> peers,
            PeerCountryHistoryReportDto? main,
            AiCountrySummeryDto countryDetails)
        {
            // All countries (main + peers) sorted by population desc
            var all = BuildAllCountries(main, peers)
                .Where(c => c.Population.HasValue)
                .OrderByDescending(c => c.Population)
                .ToList();

            if (!all.Any()) { DrawNoDataPage(container); return; }

            long maxPop = (long)(all.Max(p => p.Population) ?? 1);

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"{all.Count} countries compared  |  " +
                    $"Largest: {all.First().CountryName} ({FormatPop(all.First().Population)})  |  " +
                    $"Smallest: {all.Last().CountryName} ({FormatPop(all.Last().Population)})"));

                // ── Population bar chart ──────────────────────────────────────
                col.Item().Text("Population Size by Country")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(all.Count * 40).Canvas((canvas, size) =>
                    DrawPopulationBars(canvas, size, all, countryDetails, maxPop));

                col.Item().Element(x => DrawCountryLegend(x, all, countryDetails));

                // ── Score vs Population scatter ───────────────────────────────
                col.Item().PaddingTop(8)
                    .Text("Score vs Population  (each dot = one country)")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(all.Count * 40).Canvas((canvas, size) =>
                    DrawScatterPlot(canvas, size, all, countryDetails,
                        c => (float)(c.Population ?? 0),
                        c => GetLatestScoreOrZero(c),
                        "Population", "Score"));
            });
        }

        void DrawPopulationBars(
            SKCanvas canvas, Size size,
            List<PeerCountryHistoryReportDto> countries,
            AiCountrySummeryDto countryDetails,
            long maxPop)
        {
            float rowH = 30f;
            float labelW = 130f;
            float barArea = size.Width - labelW - 72f;

            for (int i = 0; i < countries.Count; i++)
            {
                var country = countries[i];
                float y = i * rowH + 4f;
                float barW = (float)((country.Population ?? 0) / (double)maxPop * barArea);
                bool isMain = IsSameCountry(country.CountryName, countryDetails.CountryName);

                // Row background
                if (i % 2 == 0)
                    canvas.DrawRect(new SKRect(0, y - 2, size.Width, y + rowH - 4),
                        new SKPaint { Color = SKColor.Parse(ReportThemeColors.PageBg) });

                // Highlight selected country row
                if (isMain)
                    canvas.DrawRect(new SKRect(0, y - 2, size.Width, y + rowH - 4),
                        new SKPaint { Color = SKColor.Parse(ReportThemeColors.WarningAmberBg) });

                DrawCanvasText(canvas, country.CountryName, 4, y + 5, 9,
                    isMain ? ReportThemeColors.PdfDarkGreen : ReportThemeColors.TextMuted, bold: isMain);

                string barColor = isMain ? CountryPalette[0] : CountryPalette[1 + (i % (CountryPalette.Length - 1))];
                canvas.DrawRoundRect(
                    new SKRoundRect(new SKRect(labelW, y + 4, labelW + barW, y + rowH - 6), 3),
                    new SKPaint { Color = SKColor.Parse(barColor), IsAntialias = true });

                DrawCanvasText(canvas, FormatPop(country.Population),
                    labelW + barW + 5, y + 5, 9, ReportThemeColors.TextMid);
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  5.2  REGIONAL PEER GROUP COMPARISON
        // ══════════════════════════════════════════════════════════════════════════

        void RegionalPeerPage(
            IContainer container,
            List<PeerCountryHistoryReportDto> peers,
            PeerCountryHistoryReportDto? main,
            AiCountrySummeryDto countryDetails)
        {
            var all = BuildAllCountries(main, peers);

            var byRegion = all
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Region) ? p.Country ?? "Unknown" : p.Region)
                .OrderByDescending(g => g.Count())
                .ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"{byRegion.Count} region(s)  |  {all.Count} total countries analysed"));

                col.Item().Text("Country Distribution by Region")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(180).Canvas((canvas, size) =>
                    DrawDonutChart(canvas, size,
                        byRegion.Select(g => (g.Key, (float)g.Count())).ToList()));

                col.Item().PaddingTop(4)
                    .Text("Average Score per Region")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Canvas((canvas, size) =>
                {
                    var regionScores = byRegion
                        .Select(g => (
                            Region: g.Key,
                            Avg: g.Average(c => GetLatestScoreOrZero(c)),  // includes 0
                            Count: g.Count()
                        ))
                        .OrderByDescending(r => r.Avg)
                        .ToList();

                    float barH = 28f;
                    float labelW = 120f;
                    float barArea = size.Width - labelW - 65f;

                    for (int i = 0; i < regionScores.Count; i++)
                    {
                        var r = regionScores[i];
                        float y = i * barH + 4f;
                        float barW = r.Avg / 100f * barArea;

                        if (i % 2 == 0)
                            canvas.DrawRect(new SKRect(0, y - 2, size.Width, y + barH - 4),
                                new SKPaint { Color = SKColor.Parse(ReportThemeColors.PageBg) });

                        DrawCanvasText(canvas, r.Region, 4, y + 5, 9, ReportThemeColors.TextBody);
                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(labelW, y + 3, labelW + barW, y + barH - 6), 3),
                            new SKPaint { Color = SKColor.Parse(ScoreColor(r.Avg)), IsAntialias = true });
                        DrawCanvasText(canvas, $"{r.Avg:F1}  (n={r.Count})",
                            labelW + barW + 5, y + 5, 9, ReportThemeColors.TextMid);
                    }
                });
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  5.3  INCOME-LEVEL PEER COMPARISON
        // ══════════════════════════════════════════════════════════════════════════
        public static string GetIncomeCategory(decimal income)
        {
            if (income < 5000) return "Low Income";
            if (income <= 15000) return "Lower-Middle Income";
            if (income <= 40000) return "Upper-Middle Income";
            return "High Income";
        }
        private static string GetSegmentColor(string category)
        {
            return category switch
            {
                "Low Income" => ReportThemeColors.PenaltyRed,          // softer red
                "Lower-Middle Income" => ReportThemeColors.WarningGold, // amber
                "Upper-Middle Income" => ReportThemeColors.InfoBlue, // blue-green
                "High Income" => ReportThemeColors.SuccessGreen,         // strong green
                _ => ReportThemeColors.TextPale
            };
        }

        void IncomePeerPage(
              IContainer container,
              List<PeerCountryHistoryReportDto> peers,
              PeerCountryHistoryReportDto? main,
              AiCountrySummeryDto countryDetails)
        {
            var categoryOrder = new[]
            {
                "Low Income", "Lower-Middle Income", "Upper-Middle Income", "High Income"
            };

            var all = BuildAllCountries(main, peers);
            var withIncome = all.Where(p => p.Income.HasValue).OrderBy(p => p.Income).ToList();

            if (!withIncome.Any()) { DrawNoDataPage(container); return; }

            var segments = all
                .GroupBy(x => GetIncomeCategory(x.Income ?? 0))
                .ToDictionary(g => g.Key, g => g.ToList());

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                // ── Insight band ─────────────────────────────────────────────
                col.Item().Element(x => DrawInsightBand(x,
                    $"Income quartile analysis  |  {withIncome.Count} countries  |  " +
                    $"Range: {withIncome.Min(p => p.Income):C0} – {withIncome.Max(p => p.Income):C0}"));

                // ── Avg score per quartile bars (UNCHANGED) ──────────────────
                col.Item().Text("Average Score by Income Quartile")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(130).Canvas((canvas, size) =>
                {
                    float barAreaW = (size.Width - 40f) / 4f - 8f;
                    for (int i = 0; i < categoryOrder.Length; i++)
                    {
                        var label = categoryOrder[i];
                        if (!segments.TryGetValue(label, out var countries) || !countries.Any()) continue;

                        float avg = countries.Average(c => GetLatestScoreOrZero(c));
                        float barH = avg / 100f * 90f;
                        float x = 20 + i * ((size.Width - 40f) / 4f);

                        canvas.DrawRoundRect(
                            new SKRoundRect(new SKRect(x, 100 - barH, x + barAreaW, 100), 6),
                            new SKPaint { Color = SKColor.Parse(GetSegmentColor(label)), IsAntialias = true });

                        DrawCanvasText(canvas, $"{avg:F1}", x + barAreaW / 2 - 10, 100 - barH - 14, 9, ReportThemeColors.PdfDarkGreen, true);
                        DrawCanvasText(canvas, label, x, 108, 8, ReportThemeColors.TextMid);
                        DrawCanvasText(canvas, $"n={countries.Count}", x, 118, 8, ReportThemeColors.TextFaint);
                    }
                });

                // ── Scatter: Income vs Score (UNCHANGED) ─────────────────────
                col.Item().PaddingTop(4)
                    .Text("Income vs Composite Score  (each dot = one country)")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(160).Canvas((canvas, size) =>
                    DrawScatterPlot(canvas, size, withIncome, countryDetails,
                        c => (float)(c.Income ?? 0),
                        c => GetLatestScoreOrZero(c),
                        "Income (USD)", "Score"));


                // ── Top performers table (UPDATED — PPP column added) ────────
                col.Item().PaddingTop(8)
                    .Text("Top Performers by Income Group")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(100);  // Country
                        cols.ConstantColumn(55);   // Country
                        cols.ConstantColumn(40);   // Score
                        cols.RelativeColumn();     // Income Group
                        cols.ConstantColumn(55);   // Income
                        //cols.ConstantColumn(60);   // PPP  ← NEW
                    });

                    DrawTableHeader(table, new[]
                    {
                        "Country", "Continent", "Score", "Income Group", "Income"
                    });

                    foreach (var (label, countries) in segments)
                    {
                        foreach (var country in countries.OrderByDescending(c => GetLatestScoreOrZero(c)))
                        {
                            bool isMain = IsSameCountry(country.CountryName, countryDetails.Continent);
                            string rowBg = isMain ? ReportThemeColors.HighlightBg : Colors.White;
                            float score = GetLatestScoreOrZero(country);

                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                                .Padding(5).Text(country.CountryName).FontSize(8)
                                .FontColor(isMain ? ReportThemeColors.PdfDarkGreen : ReportThemeColors.TextBody);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                                .Padding(5).Text(country.Country ?? "—").FontSize(8).FontColor(ReportThemeColors.TextMid);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                                .Padding(5).Text($"{score:F1}").FontSize(8).Bold().FontColor(ScoreColor(score));
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                                .Padding(5).Text(label).FontSize(8).FontColor(ReportThemeColors.TextMid);
                            table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                                .Padding(5).Text(FormatPop(country.Income).ToString()).FontSize(8).FontColor(ReportThemeColors.TextMid);


                        }
                    }
                });
            });
        }


        // ══════════════════════════════════════════════════════════════════════════
        //  5.5  RELATIVE RANKING AMONG PEER countries
        // ══════════════════════════════════════════════════════════════════════════

        void RelativeRankingPage(
            IContainer container,
            List<PeerCountryHistoryReportDto> peers,
            PeerCountryHistoryReportDto? main,
            AiCountrySummeryDto countryDetails)
        {
            // Build ranked list including main country; include 0-score countries
            var all = BuildAllCountries(main, peers)
                .Select(c => (Country: c, Score: GetLatestScoreOrZero(c)))
                .OrderByDescending(x => x.Score)
                .ToList();

            int total = all.Count;
            int mainRank = all.FindIndex(r => IsSameCountry(r.Country.CountryName, countryDetails.CountryName)) + 1;
            float mainScore = mainRank > 0 ? all[mainRank - 1].Score : 0f;
            float pctile = mainRank > 0 ? (1f - (float)mainRank / total) * 100f : 0f;

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                // ── Hero rank banner ──────────────────────────────────────────
                col.Item().Background(ReportThemeColors.PdfDarkGreen).Padding(14).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"#{mainRank} of {total}")
                            .FontSize(32).Bold().FontColor(ReportThemeColors.ChartGold);
                        c.Item().Text($"{countryDetails.CountryName}  \u00b7  {countryDetails.Continent}")
                            .FontSize(12).FontColor(ReportThemeColors.TrackGreen);
                    });
                    row.ConstantItem(130).Column(c =>
                    {
                        c.Item().AlignRight().Text("Score").FontSize(9).FontColor(ReportThemeColors.GrayA5A8AD);
                        c.Item().AlignRight().Text($"{mainScore:F1}")
                            .FontSize(28).Bold().FontColor(Colors.White);
                        c.Item().AlignRight().Text($"Top {100 - pctile:F0}% of peers")
                            .FontSize(10).FontColor(ReportThemeColors.ChartTeal);
                    });
                });

                // ── Score distribution histogram ──────────────────────────────
                col.Item().Text("Score Distribution Among All countries")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(150).Canvas((canvas, size) =>
                    DrawHistogram(canvas, size,
                        all.Select(r => r.Score).ToList(), mainScore, 10));

                // ── Full ranking table ────────────────────────────────────────
                col.Item().Text("Full Country Ranking").FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(24);   // rank
                        cols.RelativeColumn();     // country name
                        cols.ConstantColumn(65);   // country
                        cols.ConstantColumn(55);   // region
                        cols.ConstantColumn(52);   // population
                        cols.ConstantColumn(70);   // score bar
                    });

                    DrawTableHeader(table, new[] { "#", "Country", "Continent", "Region", "Pop.", "Score" });


                    foreach (var (entry, idx) in all.Select((e, i) => (e, i)))
                    {
                        bool isMain = IsSameCountry(entry.Country.CountryName, countryDetails.CountryName);
                        string bg = isMain ? ReportThemeColors.HighlightBg : (idx % 2 == 0 ? Colors.White : ReportThemeColors.RowPale);
                        string rankColor = idx == 0 ? ReportThemeColors.ChartGold
                                         : idx == 1 ? ReportThemeColors.GrayA5A8AD
                                         : idx == 2 ? ReportThemeColors.Bronze : ReportThemeColors.TextMid;

                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.ShadeE8E8E8)
                            .Padding(4).Text($"{idx + 1}").FontSize(8).FontColor(rankColor);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.ShadeE8E8E8)
                            .Padding(4).Text(entry.Country.CountryName).FontSize(8)
                            .FontColor(isMain ? ReportThemeColors.PdfDarkGreen : ReportThemeColors.TextBody);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.ShadeE8E8E8)
                            .Padding(4).Text(entry.Country.Continent ?? "—").FontSize(8).FontColor(ReportThemeColors.TextMid);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.ShadeE8E8E8)
                            .Padding(4).Text(entry.Country.Region ?? "—").FontSize(8).FontColor(ReportThemeColors.TextMid);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(ReportThemeColors.ShadeE8E8E8)
                            .Padding(4).AlignRight()
                            .Text(FormatPop(entry.Country.Population)).FontSize(8).FontColor(ReportThemeColors.TextMid);

                        table.Cell()
                        .Background(bg)
                        .BorderBottom(0.5f)
                        .BorderColor(ReportThemeColors.ShadeE8E8E8)
                        .Padding(4)
                        .Row(r =>
                        {
                            var percent = entry.Score / 100f;

                            r.RelativeItem().Height(10).Background(ReportThemeColors.DividerLight).Layers(layer =>
                            {
                                layer.PrimaryLayer().Background(ReportThemeColors.DividerLight);

                                layer.Layer().Width((float)percent * 100)
                                    .Background(ScoreColor(entry.Score));
                            });

                            r.ConstantItem(24).AlignRight().Text($"{entry.Score:F1}")
                                .FontSize(8)
                                .FontColor(ReportThemeColors.TextBody);
                        });
                    }
                });
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  6.1 + 6.2  HISTORICAL PERFORMANCE TRENDS
        // ══════════════════════════════════════════════════════════════════════════

        void HistoricalTrendsPage(
            IContainer container,
            List<PeerCountryHistoryReportDto> peers,
            PeerCountryHistoryReportDto? main,
            AiCountrySummeryDto countryDetails)
        {
            var all = BuildAllCountries(main, peers);
            var mainCountry = main ?? peers.FirstOrDefault();

            if (mainCountry == null) { DrawNoDataPage(container); return; }

            // All distinct years across all countries
            var allYears = all
                .SelectMany(c => c.CountryHistory ?? Enumerable.Empty<PeerCountryYearHistoryDto>())
                .Select(h => h.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToList();

            if (!allYears.Any()) { DrawNoDataPage(container); return; }

            var mainHistory = (mainCountry.CountryHistory ?? new())
                .OrderBy(h => h.Year).ToList();

            // Peer average per year (include 0-score years)
            var peerAvg = allYears.Select(yr =>
            {
                var scores = peers
                    .Select(p => p.CountryHistory?.FirstOrDefault(h => h.Year == yr))
                    .Select(h => h != null ? (float?)h.ScoreProgress : null)
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .ToList();
                return (Year: yr, Avg: scores.Any() ? scores.Average() : 0f, HasData: scores.Any());
            }).ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(14);

                // Insight
                if (mainHistory.Count >= 2)
                {
                    float first = (float)mainHistory.First().ScoreProgress;
                    float last = (float)mainHistory.Last().ScoreProgress;
                    float delta = last - first;
                    col.Item().Element(x => DrawInsightBand(x,
                        $"Period: {allYears.First()} – {allYears.Last()}  |  " +
                        $"{mainCountry.CountryName}: {(delta >= 0 ? "+" : "")}{delta:F1} pts  |  " +
                        $"Latest score: {last:F1}  |  " +
                        $"{peers.Count} peer country(ies)"));
                }

                // ── 6.1  Multi-line trend ────────────────────────────────────
                col.Item().Text("6.1  Historical Score Trend")
                    .FontSize(12).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(190).Canvas((canvas, size) =>
                    DrawMultiLineTrendChart(canvas, size, allYears, peers, mainCountry, countryDetails, peerAvg));

                // Legend: one entry per country
                col.Item().Element(x => DrawCountryLineLegend(x, mainCountry, peers, countryDetails));

                col.Item().PaddingVertical(4).LineHorizontal(0.5f).LineColor(ReportThemeColors.GrayE0E0E0);

                // ── 6.2  Five-year area chart ────────────────────────────────
                col.Item().Text("6.2  Five-Year Composite Score Evolution")
                    .FontSize(12).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                var last5 = allYears.TakeLast(5).ToList();
                var mainLast5 = mainHistory.Where(h => last5.Contains(h.Year))
                                            .OrderBy(h => h.Year).ToList();
                var peerLast5 = peerAvg.Where(p => last5.Contains(p.Year))
                                        .OrderBy(p => p.Year).ToList();

                col.Item().Height(120).Canvas((canvas, size) =>
                    DrawAreaComparisonChart(canvas, size, last5, mainLast5,
                        peerLast5.Select(p => (p.Year, p.Avg)).ToList()));

                // YoY table
                if (mainLast5.Count > 1)
                    col.Item().Element(x =>
                        DrawYoYTable(x, last5, mainLast5,
                            peerLast5.Select(p => (p.Year, p.Avg)).ToList()));
            });
        }

        void DrawYoYTable(
            IContainer container,
            List<int> years,
            List<PeerCountryYearHistoryDto> mainHistory,
            List<(int Year, float Avg)> peerAvg)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(65);
                    for (int i = 0; i < years.Count; i++) cols.RelativeColumn();
                });

                // Header
                table.Cell().Background(ReportThemeColors.PdfDarkGreen).Padding(5)
                    .Text("Metric").FontSize(8).Bold().FontColor(Colors.White);
                foreach (var yr in years)
                    table.Cell().Background(ReportThemeColors.PdfDarkGreen).Padding(5).AlignRight()
                        .Text(yr.ToString()).FontSize(8).Bold().FontColor(Colors.White);

                // Score row
                table.Cell().Background(ReportThemeColors.PageBg).Padding(5)
                    .Text("Score").FontSize(8).FontColor(ReportThemeColors.TextBody);
                foreach (var yr in years)
                {
                    float s = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    table.Cell().Background(ReportThemeColors.PageBg).Padding(5).AlignRight()
                        .Text($"{s:F1}").FontSize(8).Bold().FontColor(ScoreColor(s));
                }

                // YoY delta
                table.Cell().Background(Colors.White).Padding(5)
                    .Text("YoY \u0394").FontSize(8).FontColor(ReportThemeColors.TextBody);
                for (int i = 0; i < years.Count; i++)
                {
                    if (i == 0)
                    {
                        table.Cell().Background(Colors.White).Padding(5)
                            .Text("—").FontSize(8).FontColor(ReportThemeColors.GrayAAAAAA);
                        continue;
                    }
                    float prev = (float)(mainHistory.FirstOrDefault(h => h.Year == years[i - 1])?.ScoreProgress ?? 0);
                    float curr = (float)(mainHistory.FirstOrDefault(h => h.Year == years[i])?.ScoreProgress ?? 0);
                    float d = curr - prev;
                    table.Cell().Background(Colors.White).Padding(5).AlignRight()
                        .Text($"{(d >= 0 ? "+" : "")}{d:F1}").FontSize(8)
                        .FontColor(d >= 0 ? ReportThemeColors.PdfMediumGreen : ReportThemeColors.ChartRed);
                }

                // vs Peers
                table.Cell().Background(ReportThemeColors.PageBg).Padding(5)
                    .Text("vs Peers").FontSize(8).FontColor(ReportThemeColors.TextBody);
                foreach (var yr in years)
                {
                    float myS = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float pAvg = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                    float d = myS - pAvg;
                    table.Cell().Background(ReportThemeColors.PageBg).Padding(5).AlignRight()
                        .Text($"{(d >= 0 ? "+" : "")}{d:F1}").FontSize(8)
                        .FontColor(d >= 0 ? ReportThemeColors.PdfMediumGreen : ReportThemeColors.ChartRed);
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  6.3  PILLAR-LEVEL TREND  (main country only; up to 14 pillars)
        // ══════════════════════════════════════════════════════════════════════════

        void PillarTrendPage(
            IContainer container,
            PeerCountryHistoryReportDto? mainCountry,
            AiCountrySummeryDto countryDetails)
        {
            if (mainCountry == null) { DrawNoDataPage(container); return; }

            var history = mainCountry.CountryHistory ?? new();            
            var allYears = history.Select(h => h.Year).OrderBy(y => y).ToList();

            if (!allYears.Any()) { DrawNoDataPage(container); return; }

            // Collect all unique pillars (cap at MaxPillars = 23)
            var pillars = history
                .SelectMany(h => h.Pillars ?? Enumerable.Empty<PeerCountryPillarHistoryReportDto>())
                .GroupBy(p => p.PillarID)
                .Select(g => g.OrderBy(p => p.DisplayOrder).First())
                .OrderBy(p => p.DisplayOrder)
                .Take(_pillarCount)
                .ToList();

            container.Padding(16).Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(x => DrawInsightBand(x,
                    $"{pillars.Count} pillar(s)  |  {allYears.Count} year(s)  |  Country: {mainCountry.CountryName}"));

                col.Item().Text("Pillar Score Trajectory Over Time")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Height(200).Canvas((canvas, size) =>
                    DrawPillarLineChart(canvas, size, allYears, history, pillars));

                //// Pillar colour legend
                col.Item().Element(x => DrawLegend(x,
                    pillars.Select((p, i) =>
                        (PillarPalette[i % PillarPalette.Length], p.PillarName)).ToArray(), 10));

                // ── Pillar heatmap table ──────────────────────────────────────
                col.Item().PaddingTop(4)
                    .Text("Pillar Score Heatmap  (darker = higher score)")
                    .FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(110);
                        foreach (var _ in allYears) cols.RelativeColumn();
                    });

                    table.Cell().Background(ReportThemeColors.PdfDarkGreen).Padding(5)
                        .Text("Pillar").FontSize(8).Bold().FontColor(Colors.White);
                    foreach (var yr in allYears)
                        table.Cell().Background(ReportThemeColors.PdfDarkGreen).Padding(5).AlignCenter()
                            .Text(yr.ToString()).FontSize(8).Bold().FontColor(Colors.White);

                    foreach (var (pillar, pi) in pillars.Select((p, i) => (p, i)))
                    {
                        string rowBg = pi % 2 == 0 ? ReportThemeColors.PageBg : Colors.White;

                        table.Cell().Background(rowBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                            .Padding(5).Text(pillar.PillarName).FontSize(8)
                            .Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                        foreach (var yr in allYears)
                        {
                            var h = history.FirstOrDefault(h2 => h2.Year == yr);
                            var ps = h?.Pillars?.FirstOrDefault(p2 => p2.PillarID == pillar.PillarID);
                            // treat null as "no data"; 0 is a valid score
                            bool hasData = ps != null;
                            float score = hasData ? (float)ps!.ScoreProgress : -1f;

                            string cellBg = !hasData ? ReportThemeColors.SubHeaderBg
                                : InterpolateColor(ReportThemeColors.White, ReportThemeColors.PdfDarkGreen, score / 100f);

                            table.Cell().Background(cellBg).BorderBottom(0.5f).BorderColor(ReportThemeColors.GrayE0E0E0)
                                .Padding(4).AlignCenter()
                                .Text(!hasData ? "—" : $"{score:F1}").FontSize(8)
                                .FontColor(score >= 50 ? Colors.White : ReportThemeColors.TextBody);
                        }
                    }
                });

                // ── Trend highlights ──────────────────────────────────────────
                if (allYears.Count >= 2 && pillars.Any())
                {
                    col.Item().PaddingTop(6)
                        .Text("Pillar Trend Highlights").FontSize(11).Bold().FontColor(ReportThemeColors.PdfDarkGreen);

                    var pillarDeltas = pillars.Select(p =>
                    {
                        var firstH = history.FirstOrDefault()?.Pillars?
                            .FirstOrDefault(pp => pp.PillarID == p.PillarID);
                        var lastH = history.LastOrDefault()?.Pillars?
                            .FirstOrDefault(pp => pp.PillarID == p.PillarID);
                        float d = (float)((lastH?.ScoreProgress ?? 0) - (firstH?.ScoreProgress ?? 0));
                        return (Name: p.PillarName, Delta: d);
                    }).OrderByDescending(x => x.Delta).ToList();

                    col.Item().Row(row =>
                    {
                        // Most improved  (replaced emoji with ASCII arrow)
                        row.RelativeItem().Background(ReportThemeColors.SuccessGreenBg).Padding(10).Column(c =>
                        {
                            c.Item().Text("(+) Most Improved").FontSize(9).Bold().FontColor(ReportThemeColors.PdfMediumGreen);
                            foreach (var pd in pillarDeltas.Take(3))
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(Shorten(pd.Name, 28)).FontSize(8).FontColor(ReportThemeColors.TextBody);
                                    r.ConstantItem(44).AlignRight()
                                        .Text($"+{pd.Delta:F1}").FontSize(8).Bold().FontColor(ReportThemeColors.PdfMediumGreen);
                                });
                        });

                        row.ConstantItem(10);

                        // Needs attention  (replaced emoji with ASCII)
                        row.RelativeItem().Background(ReportThemeColors.DangerRedBg).Padding(10).Column(c =>
                        {
                            c.Item().Text("(!) Needs Attention").FontSize(9).Bold().FontColor(ReportThemeColors.ChartRed);
                            foreach (var pd in pillarDeltas.TakeLast(3).Reverse())
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(Shorten(pd.Name, 28)).FontSize(8).FontColor(ReportThemeColors.TextBody);
                                    r.ConstantItem(44).AlignRight()
                                        .Text($"{pd.Delta:F1}").FontSize(8).Bold().FontColor(ReportThemeColors.ChartRed);
                                });
                        });
                    });
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  CANVAS CHART RENDERERS
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Multi-line trend chart: one coloured line per country (gold = main, palette = peers).
        /// Thin grey peer lines are no longer used; each peer gets its own distinct colour.
        /// </summary>
        void DrawMultiLineTrendChart(
            SKCanvas canvas, Size size,
            List<int> years,
            List<PeerCountryHistoryReportDto> peers,
            PeerCountryHistoryReportDto mainCountry,
            AiCountrySummeryDto countryDetails,
            List<(int Year, float Avg, bool HasData)> peerAvg)
        {
            if (years.Count < 2) return;

            const float padL = 36f, padR = 12f, padT = 10f, padB = 24f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            // Grid
            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse(ReportThemeColors.ShadeE8E8E8), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, ReportThemeColors.GrayAAAAAA);
            }
            foreach (int yr in years)
            {
                float x = Xp(yr);
                canvas.DrawLine(x, padT, x, padT + h,
                    new SKPaint { Color = SKColor.Parse(ReportThemeColors.SubHeaderBg), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, yr.ToString(), x - 14, padT + h + 7, 7, ReportThemeColors.TextFaint);
            }

            // Peer average (dashed green)
            var avgPts = peerAvg.Where(p => p.HasData)
                .Select(p => new SKPoint(Xp(p.Year), Yp(p.Avg))).ToList();
            DrawDashedPolyline(canvas, avgPts,
                new SKPaint
                {
                    Color = SKColor.Parse(ReportThemeColors.ChartTeal),
                    StrokeWidth = 1.5f,
                    IsAntialias = true,
                    IsStroke = true
                });

            // Individual peer lines
            for (int pi = 0; pi < peers.Count; pi++)
            {
                var peer = peers[pi];
                string clr = CountryPalette[1 + (pi % (CountryPalette.Length - 1))];

                var pts = (peer.CountryHistory ?? new())
                    .Where(h => years.Contains(h.Year))
                    .OrderBy(h => h.Year)
                    .Select(h => new SKPoint(Xp(h.Year), Yp((float)h.ScoreProgress)))
                    .ToList();

                DrawPolyline(canvas, pts,
                    new SKPaint
                    {
                        Color = SKColor.Parse(clr).WithAlpha(180),
                        StrokeWidth = 1.2f,
                        IsAntialias = true,
                        IsStroke = true
                    });
            }

            // Main country line (gold, bold)
            var mainPts = (mainCountry.CountryHistory ?? new())
                .Where(h => years.Contains(h.Year))
                .OrderBy(h => h.Year)
                .Select(h => new SKPoint(Xp(h.Year), Yp((float)h.ScoreProgress)))
                .ToList();

            DrawPolyline(canvas, mainPts,
                new SKPaint
                {
                    Color = SKColor.Parse(CountryPalette[0]),
                    StrokeWidth = 2.5f,
                    IsAntialias = true,
                    IsStroke = true
                });

            foreach (var pt in mainPts)
                canvas.DrawCircle(pt.X, pt.Y, 4f,
                    new SKPaint { Color = SKColor.Parse(CountryPalette[0]), IsAntialias = true });
        }

        void DrawAreaComparisonChart(
            SKCanvas canvas, Size size,
            List<int> years,
            List<PeerCountryYearHistoryDto> mainHistory,
            List<(int Year, float Avg)> peerAvg)
        {
            if (years.Count < 2) return;

            const float padL = 36f, padR = 10f, padT = 6f, padB = 20f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse(ReportThemeColors.ShadeE8E8E8), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, ReportThemeColors.GrayAAAAAA);
            }
            foreach (int yr in years)
                DrawCanvasText(canvas, yr.ToString(), Xp(yr) - 12, padT + h + 5, 7, ReportThemeColors.TextFaint);

            // Peer area
            var peerPath = new SKPath();
            peerPath.MoveTo(Xp(years.First()), Yp(0));
            foreach (int yr in years)
            {
                float v = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                peerPath.LineTo(Xp(yr), Yp(v));
            }
            peerPath.LineTo(Xp(years.Last()), Yp(0));
            peerPath.Close();
            canvas.DrawPath(peerPath,
                new SKPaint { Color = SKColor.Parse(ReportThemeColors.ChartTeal).WithAlpha(40), IsAntialias = true });

            // Main area
            var mainPath = new SKPath();
            mainPath.MoveTo(Xp(years.First()), Yp(0));
            foreach (int yr in years)
            {
                float v = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                mainPath.LineTo(Xp(yr), Yp(v));
            }
            mainPath.LineTo(Xp(years.Last()), Yp(0));
            mainPath.Close();
            canvas.DrawPath(mainPath,
                new SKPaint { Color = SKColor.Parse(CountryPalette[0]).WithAlpha(50), IsAntialias = true });

            // Outlines
            DrawPolyline(canvas,
                years.Select(yr => new SKPoint(Xp(yr),
                    Yp(peerAvg.FirstOrDefault(p => p.Year == yr).Avg))).ToList(),
                new SKPaint
                {
                    Color = SKColor.Parse(ReportThemeColors.ChartTeal),
                    StrokeWidth = 1.5f,
                    IsAntialias = true,
                    IsStroke = true
                });

            DrawPolyline(canvas,
                years.Select(yr => new SKPoint(Xp(yr),
                    Yp((float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0)))).ToList(),
                new SKPaint
                {
                    Color = SKColor.Parse(CountryPalette[0]),
                    StrokeWidth = 2f,
                    IsAntialias = true,
                    IsStroke = true
                });
        }

        /// <summary>Multi-pillar line chart — one coloured line per pillar (up to 14).</summary>
        void DrawPillarLineChart(
            SKCanvas canvas, Size size,
            List<int> years,
            List<PeerCountryYearHistoryDto> history,
            List<PeerCountryPillarHistoryReportDto> pillars)
        {
            if (years.Count < 2) return;

            const float padL = 36f, padR = 10f, padT = 8f, padB = 20f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float Xp(int yr) => padL + (yr - years.First()) / (float)(years.Last() - years.First()) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse(ReportThemeColors.ShadeE8E8E8), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, ReportThemeColors.GrayAAAAAA);
            }
            foreach (int yr in years)
                DrawCanvasText(canvas, yr.ToString(), Xp(yr) - 12, padT + h + 5, 7, ReportThemeColors.TextFaint);

            for (int pi = 0; pi < pillars.Count; pi++)
            {
                var pillar = pillars[pi];
                string color = PillarPalette[pi % PillarPalette.Length];

                var pts = years
                    .Select(yr =>
                    {
                        var hEntry = history.FirstOrDefault(h => h.Year == yr);
                        var ps = hEntry?.Pillars?.FirstOrDefault(p => p.PillarID == pillar.PillarID);
                        return ps != null ? (float?)ps.ScoreProgress : null;
                    })
                    .Select((s, i) => (Year: years[i], Score: s))
                    .Where(p => p.Score.HasValue)
                    .Select(p => new SKPoint(Xp(p.Year), Yp(p.Score!.Value)))
                    .ToList();

                DrawPolyline(canvas, pts,
                    new SKPaint
                    {
                        Color = SKColor.Parse(color),
                        StrokeWidth = 1.5f,
                        IsAntialias = true,
                        IsStroke = true
                    });

                foreach (var pt in pts)
                    canvas.DrawCircle(pt.X, pt.Y, 2.5f,
                        new SKPaint { Color = SKColor.Parse(color), IsAntialias = true });
            }
        }

        void DrawScatterPlot(
            SKCanvas canvas, Size size,
            List<PeerCountryHistoryReportDto> countries,
            AiCountrySummeryDto countryDetails,
            Func<PeerCountryHistoryReportDto, float> xVal,
            Func<PeerCountryHistoryReportDto, float> yVal,
            string xLabel, string yLabel)
        {
            const float padL = 42f, padR = 14f, padT = 8f, padB = 24f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;

            float xMin = countries.Any() ? countries.Min(xVal) : 0f;
            float xMax = countries.Any() ? countries.Max(xVal) : 1f;
            if (xMax <= xMin) xMax = xMin + 1;

            float Xp(float v) => padL + (v - xMin) / (xMax - xMin) * w;
            float Yp(float v) => padT + h - Math.Clamp(v, 0, 100) / 100f * h;

            // Axes
            canvas.DrawLine(padL, padT, padL, padT + h,
                new SKPaint { Color = SKColor.Parse(ReportThemeColors.GrayAAAAAA), StrokeWidth = 0.8f });
            canvas.DrawLine(padL, padT + h, padL + w, padT + h,
                new SKPaint { Color = SKColor.Parse(ReportThemeColors.GrayAAAAAA), StrokeWidth = 0.8f });

            foreach (int s in new[] { 0, 25, 50, 75, 100 })
            {
                float y = Yp(s);
                canvas.DrawLine(padL, y, padL + w, y,
                    new SKPaint { Color = SKColor.Parse(ReportThemeColors.DividerLight), StrokeWidth = 0.5f });
                DrawCanvasText(canvas, s.ToString(), 2, y - 5, 7, ReportThemeColors.TextPale);
            }

            for (int i = 0; i < countries.Count; i++)
            {
                var country = countries[i];
                bool isMain = IsSameCountry(country.CountryName, countryDetails.CountryName);
                float x = Xp(xVal(country));
                float y = Yp(yVal(country));
                string clr = isMain ? CountryPalette[0] : CountryPalette[1 + (i % (CountryPalette.Length - 1))];

                canvas.DrawCircle(x, y, isMain ? 6f : 4.5f,
                    new SKPaint { Color = SKColor.Parse(clr), IsAntialias = true });

                if (isMain)
                    canvas.DrawCircle(x, y, 6f,
                        new SKPaint
                        {
                            Color = SKColor.Parse(ReportThemeColors.PdfDarkGreen),
                            IsStroke = true,
                            StrokeWidth = 1.5f,
                            IsAntialias = true
                        });
            }

            DrawCanvasText(canvas, xLabel, padL + w / 2 - 22, padT + h + 14, 8, ReportThemeColors.TextLabel);
            DrawCanvasText(canvas, yLabel, 2, padT + h / 2, 8, ReportThemeColors.TextLabel);
        }

        void DrawDonutChart(SKCanvas canvas, Size size, List<(string Label, float Value)> segments)
        {
            float total = segments.Sum(s => s.Value);
            if (total <= 0) return;

            float cx = size.Height / 2f;
            float cy = size.Height / 2f;
            float outerR = size.Height / 2f - 6f;
            float innerR = outerR * 0.52f;

            float startAngle = -90f;
            for (int i = 0; i < segments.Count; i++)
            {
                float sweep = segments[i].Value / total * 360f;
                using var path = new SKPath();
                var outer = new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR);
                path.AddArc(outer, startAngle, sweep);
                path.ArcTo(new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR),
                    startAngle + sweep, -sweep, false);
                path.Close();

                canvas.DrawPath(path, new SKPaint
                { Color = SKColor.Parse(PillarPalette[i % PillarPalette.Length]), IsAntialias = true });

                startAngle += sweep;
            }

            // Centre count
            DrawCanvasText(canvas, $"{segments.Count}", cx - 8, cy - 8, 14, ReportThemeColors.PdfDarkGreen, bold: true);
            DrawCanvasText(canvas, "groups", cx - 16, cy + 6, 7, ReportThemeColors.TextMid);

            // Legend right of donut
            float lx = cx + outerR + 14f;
            for (int i = 0; i < segments.Count; i++)
            {
                float ly = 12 + i * 17f;
                canvas.DrawRoundRect(
                    new SKRoundRect(new SKRect(lx, ly, lx + 10, ly + 10), 2),
                    new SKPaint
                    {
                        Color = SKColor.Parse(PillarPalette[i % PillarPalette.Length]),
                        IsAntialias = true
                    });
                DrawCanvasText(canvas,
                    $"{Shorten(segments[i].Label, 18)}  ({segments[i].Value:F0})",
                    lx + 14, ly, 8, ReportThemeColors.TextBody);
            }
        }

        void DrawHistogram(
            SKCanvas canvas, Size size,
            List<float> scores, float markerValue, int bins)
        {
            if (!scores.Any()) return;

            const float padL = 30f, padR = 10f, padT = 6f, padB = 20f;
            float w = size.Width - padL - padR;
            float h = size.Height - padT - padB;
            float binW = w / bins;
            float bucketSz = 100f / bins;

            int[] counts = new int[bins];
            foreach (float s in scores)
            {
                int b = Math.Clamp((int)(s / bucketSz), 0, bins - 1);
                counts[b]++;
            }
            int maxCount = counts.Max() == 0 ? 1 : counts.Max();

            for (int b = 0; b < bins; b++)
            {
                float bH = (float)counts[b] / maxCount * h;
                float x = padL + b * binW;
                float midScr = b * bucketSz + bucketSz / 2f;

                canvas.DrawRoundRect(
                    new SKRoundRect(new SKRect(x + 2, padT + h - bH, x + binW - 2, padT + h), 2),
                    new SKPaint { Color = SKColor.Parse(ScoreColor(midScr)), IsAntialias = true });

                if (counts[b] > 0)
                    DrawCanvasText(canvas, counts[b].ToString(), x + 3, padT + h - bH - 12, 7, ReportThemeColors.TextMid);
            }

            for (int b = 0; b <= bins; b += 2)
                DrawCanvasText(canvas, (b * bucketSz).ToString("F0"),
                    padL + b * binW - 6, padT + h + 5, 7, ReportThemeColors.TextFaint);

            // Marker for selected country
            float mx = padL + Math.Clamp(markerValue, 0, 100) / 100f * w;
            canvas.DrawLine(mx, padT, mx, padT + h,
                new SKPaint
                {
                    Color = SKColor.Parse(CountryPalette[0]),
                    StrokeWidth = 2f,
                    PathEffect = SKPathEffect.CreateDash(new[] { 4f, 3f }, 0)
                });
            DrawCanvasText(canvas, $"^{markerValue:F1}", mx - 12, padT - 1, 7, CountryPalette[0], bold: true);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  SHARED LAYOUT HELPERS
        // ══════════════════════════════════════════════════════════════════════════

        void DrawInsightBand(IContainer container, string text)
        {
            container.Background(ReportThemeColors.SuccessGreenBg).Padding(8)
                .Text(text).FontSize(8.5f).FontColor(ReportThemeColors.PdfDarkGreen);
        }

        void DrawNoDataPage(IContainer container)
        {
            container.AlignCenter().AlignMiddle()
                .Text("No data available for this section.")
                .FontSize(12).FontColor(ReportThemeColors.GrayAAAAAA);
        }
        void DrawLegend(IContainer container, (string Color, string Label)[] items, int textsize = 20)
        {
            var groups = items
                .Select((x, i) => new { x, i })
                .GroupBy(x => x.i / 7)
                .Select(g => g.Select(v => v.x).ToList());

            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.AutoItem().AlignMiddle()
                        .Text("Legend:  ")
                        .FontSize(8)
                        .FontColor(ReportThemeColors.TextFaint);
                });

                foreach (var group in groups)
                {
                    col.Item().Row(row =>
                    {
                        foreach (var (color, label) in group)
                        {
                            row.ConstantItem(10).Height(10).Canvas((c, s) =>
                                c.DrawRoundRect(
                                    new SKRoundRect(new SKRect(0, 1, 10, 9), 2),
                                    new SKPaint { Color = SKColor.Parse(color) }));

                            row.AutoItem()
                                .PaddingLeft(3)
                                .PaddingRight(10)
                                .Text(Shorten(label, textsize))
                                .FontSize(8)
                                .FontColor(ReportThemeColors.TextMid);
                        }
                    });
                }
            });
        }

        /// <summary>Country-specific legend: gold dot for selected country, palette dots for peers.</summary>
        void DrawCountryLineLegend(
            IContainer container,
            PeerCountryHistoryReportDto mainCountry,
            List<PeerCountryHistoryReportDto> peers,
            AiCountrySummeryDto countryDetails)
        {
            var items = new List<(string Color, string Label)>
            {
                (CountryPalette[0],  $"{countryDetails.CountryName} (selected)"),
                (ReportThemeColors.ChartTeal,       "Peer Average")
            };
            for (int i = 0; i < peers.Count; i++)
                items.Add((CountryPalette[1 + (i % (CountryPalette.Length - 1))], peers[i].CountryName));

            DrawLegend(container, items.ToArray());
        }

        /// <summary>Legend row showing a coloured dot for every country in the chart.</summary>
        void DrawCountryLegend(
            IContainer container,
            List<PeerCountryHistoryReportDto> allcountries,
            AiCountrySummeryDto countryDetails)
        {
            var items = allcountries
                .Select((c, i) => (
                    Color: IsSameCountry(c.CountryName, countryDetails.CountryName)
                        ? CountryPalette[0]
                        : CountryPalette[1 + (i % (CountryPalette.Length - 1))],
                    Label: c.CountryName
                ))
                .ToArray();

            DrawLegend(container, items);
        }

        void SelectedPillarMarkedContent(
    IContainer container, AiCountryPillarResponse data, CountryPillarRankingResultDto? pillarRank, UserRole userRole)
        {
            container.PaddingTop(8).Column(column =>
            {
                column.Item().PaddingTop(10)
                    .Element(c => PillarProgressSection(c, data, userRole));

                column.Item().PaddingTop(10)
                    .Element(c => SelectedPillarRankTable(c, pillarRank));

                column.Item().PaddingTop(10).Element(c =>
                    PillarContentSection(c, "Executive Summary", SanitizeText(data.EvidenceSummary ?? ""), ReportThemeColors.AccentExecutiveSummary));

            });
        }
        void SelectedPillarRankTable(IContainer container, CountryPillarRankingResultDto? data)
        {
            static string FormatRank(int rank, int total) =>
                rank > 0 && total > 0 ? $"{rank} / {total}" : "-";

            container
                .Border(1).BorderColor(ReportThemeColors.Gray300)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(ReportThemeColors.Gray100).Padding(8)
                            .Text("Ranking").SemiBold().FontSize(11).FontColor(ReportThemeColors.GrayTailwind800);
                        header.Cell().Background(ReportThemeColors.Gray100).Padding(8).AlignRight()
                            .Text("Result").SemiBold().FontSize(11).FontColor(ReportThemeColors.GrayTailwind800);
                    });

                    void Row(string label, string value, bool shade)
                    {
                        var background = shade ? ReportThemeColors.Gray50 : ReportThemeColors.White;
                        table.Cell().Background(background).BorderTop(1).BorderColor(ReportThemeColors.Gray300).Padding(8)
                            .Text(label).FontSize(11).FontColor(ReportThemeColors.GrayTailwind700);
                        table.Cell().Background(background).BorderTop(1).BorderColor(ReportThemeColors.Gray300).Padding(8).AlignRight()
                            .Text(value).FontSize(11).Bold().FontColor(ReportThemeColors.GrayTailwind900);
                    }

                    Row("Pillar Global Rank", FormatRank(data?.GlobalPillarRank ?? 0, data?.TotalPillarsInAllCountries ?? 0), false);
                    Row($"{data?.Region} Region Rank", FormatRank(data?.RegionPillarRank ?? 0, data?.TotalPillarInRegion ?? 0), true);
                    Row("Pillar Country Level Rank", FormatRank(data?.CountryPillarRank ?? 0, data?.TotalPillars ?? 0), false);
                });
        }
        static void DrawTableHeader(TableDescriptor table, string[] headers)
        {
            foreach (string h in headers)
                table.Cell().Background(ReportThemeColors.PdfDarkGreen).Padding(5)
                    .Text(h).FontSize(8).Bold().FontColor(Colors.White);
        }

        static void DrawPolyline(SKCanvas canvas, List<SKPoint> pts, SKPaint paint)
        {
            for (int i = 0; i < pts.Count - 1; i++)
                canvas.DrawLine(pts[i], pts[i + 1], paint);
        }

        static void DrawDashedPolyline(SKCanvas canvas, List<SKPoint> pts, SKPaint paint)
        {
            var dashed = paint.Clone();
            dashed.PathEffect = SKPathEffect.CreateDash(new[] { 5f, 3f }, 0);
            DrawPolyline(canvas, pts, dashed);
        }

        static void DrawCanvasText(
            SKCanvas canvas, string text, float x, float y, float textSize,
            string hexColor, bool bold = false)
        {
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(hexColor),
                TextSize = textSize,
                IsAntialias = true,
                Typeface = bold
                    ? SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                    : SKTypeface.FromFamilyName("Arial")
            };
            canvas.DrawText(text, x, y + textSize, paint);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  UTILITY HELPERS
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the latest year's score including 0.
        /// Returns -1 only when there is genuinely NO history entry at all.
        /// </summary>
        static float GetLatestScoreOrZero(PeerCountryHistoryReportDto country)
        {
            var last = country.CountryHistory?
                .OrderByDescending(h => h.Year)
                .FirstOrDefault();
            return last != null ? (float)last.ScoreProgress : -1f;
        }

        /// <summary>Returns main-country entry from the combined list; null if not found.</summary>
        static PeerCountryHistoryReportDto? FindMainCountry(
            List<PeerCountryHistoryReportDto> all, AiCountrySummeryDto countryDetails) =>
            all.FirstOrDefault(p => IsSameCountry(p.CountryName, countryDetails.CountryName));

        /// <summary>Case-insensitive country name equality check.</summary>
        static bool IsSameCountry(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Builds a deduplicated list: main country first, then peers.</summary>
        static List<PeerCountryHistoryReportDto> BuildAllCountries(
            PeerCountryHistoryReportDto? main,
            List<PeerCountryHistoryReportDto> peers)
        {
            var list = new List<PeerCountryHistoryReportDto>();
            if (main != null) list.Add(main);
            list.AddRange(peers);
            return list;
        }

        static string ScoreColor(float score) =>
            score >= 70 ? ReportThemeColors.PdfMediumGreen : score >= 40 ? ReportThemeColors.ChartAmber : ReportThemeColors.ChartRed;

        static string DeriveRole(PeerCountryHistoryReportDto country)
        {
            if (country.Population >= 5_000_000) return "Metropolis";
            if (country.Population >= 1_000_000) return "Major Country";
            if (country.Population >= 300_000) return "Mid-Sized Country";
            if (country.Population >= 100_000) return "Large Town";
            return "Small Country";
        }

        static string FormatPop(decimal? value)
        {
            if (!value.HasValue || value <= 0) return "N/A";

            if (value >= 1_000_000_000) return $"{value / 1_000_000_000M:F1}B";
            if (value >= 1_000_000) return $"{value / 1_000_000M:F1}M";
            if (value >= 1_000) return $"{value / 1_000M:F0}K";

            return value.Value.ToString("N0");
        }

      

        static string InterpolateColor(string from, string to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            var c1 = SKColor.Parse(from);
            var c2 = SKColor.Parse(to);
            byte r = (byte)(c1.Red + (c2.Red - c1.Red) * t);
            byte g = (byte)(c1.Green + (c2.Green - c1.Green) * t);
            byte b = (byte)(c1.Blue + (c2.Blue - c1.Blue) * t);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }

}
