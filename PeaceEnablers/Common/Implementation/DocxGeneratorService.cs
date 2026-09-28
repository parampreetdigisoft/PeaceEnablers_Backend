// ═══════════════════════════════════════════════════════════════════════════
//  DocxGeneratorService.cs
//
//  NuGet package required (free):
//    DocumentFormat.OpenXml  >=  3.0.0   (MIT, by Microsoft)
//    SkiaSharp                            (already present via QuestPDF)
//
//  Architecture:
//   • Charts are rendered to PNG via SkiaSharp (reusing the same paint
//     methods used by PdfGeneratorService) and embedded as images.
//   • Text sections, progress bars, and data tables use native OpenXML.
//   • The IDocxGeneratorService interface mirrors IPdfGeneratorService so
//     the DocumentGeneratorService facade can swap them transparently.
// ═══════════════════════════════════════════════════════════════════════════


using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using PeaceEnablers.Services;
using SkiaSharp;
using System.Diagnostics.Metrics;
using static PeaceEnablers.Services.AIComputationService;


// Aliases to avoid clashes with System.Drawing / Wordprocessing
using A    = DocumentFormat.OpenXml.Drawing;
using DW   = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC  = DocumentFormat.OpenXml.Drawing.Pictures;
using QPDF = QuestPDF.Infrastructure;

namespace PeaceEnablers.Common.Implementation
{
    public sealed partial class DocxGeneratorService : IDocxGeneratorService
    {
        // ── Constants ────────────────────────────────────────────────────────
        // All DXA values are "twentieths of a point" (1 inch = 1440 DXA).
        // All EMU values are "English Metric Units"  (1 inch = 914 400 EMU).

        private const uint   PageWidthDxa    = 11906;   // A4 width
        private const uint   PageHeightDxa   = 16838;   // A4 height
        private const int   MarginDxa       = 720;     // 0.5 inch margins
        private const int    ContentDxa      = (int)(PageWidthDxa - 2 * MarginDxa); // 10 466 DXA
        private const long   ContentWidthEmu = 6_645_000L;   // ≈ 7.27 inch in EMU
        private const long   HalfWidthEmu    = 3_220_000L;   // ≈ 3.52 inch in EMU
        private const string DarkBlue       = ReportThemeColors.DarkBlueHex;
        private const string MedBlue        = ReportThemeColors.MedBlueHex;
        private const string White           = ReportThemeColors.WhiteHex;

        // Unique image ID counter — reset per document
        private uint _imgId;

        private readonly IAppLogger _appLogger;

        public DocxGeneratorService(IAppLogger appLogger)
            => _appLogger = appLogger;

        // ════════════════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ════════════════════════════════════════════════════════════════════

        public async Task<byte[]> GenerateCountryDetailsDocx(
            AiCountrySummeryDto countryDetails,
            List<AiCountryPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peerCountries,
            UserRole userRole)
        {
            try
            {
                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;
                    AddCountryDetailsSections(body, mainPart, countryDetails, pillars, kpis, peerCountries, userRole);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateCountryDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GeneratePillarDetailsDocx(AiCountryPillarResponse pillarData, UserRole userRole)
        {
            try
            {
                var countryDetails = new AiCountrySummeryDto
                {
                    CountryID = pillarData.CountryID,
                    CountryName = pillarData.CountryName,
                    Continent = pillarData.Continent,                   
                    Year = pillarData.AIDataYear,
                    AIProgress = pillarData.AIProgress

                };

                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;
                    AppendCountryHeader(mainPart, countryDetails, pillarData.PillarName);
                    AddPillarSection(body, mainPart, pillarData, userRole);
                    FinalizeLastSection(mainPart);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GeneratePillarDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        public async Task<byte[]> GenerateAllCountriesDetailsDocx(
            List<AiCountrySummeryDto> countries,
            Dictionary<int, List<AiCountryPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole)
        {
            try
            {
                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;
                    bool first = true;
                    foreach (var country in countries)
                    {
                        if (!pillarsDict.TryGetValue(country.CountryID, out var pillars) || !pillars.Any())
                            continue;

                        var countryKpis = kpis?.Where(k => k.CountryID == country.CountryID).ToList()
                                       ?? new List<KpiChartItem>();

                        if (!first) body.AppendChild(PageBreak());
                        first = false;

                        AddCountryDetailsSections(body, mainPart, country, pillars, countryKpis,
                                               new List<PeerCountryHistoryReportDto>(), userRole, isAllCountries: true);
                    }
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateAllCountriesDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }
        public async Task<byte[]> GenerateSelectedPillarsDetailsDocx(List<AiCountryPillarResponse> pillars, List<CountryPillarRankingResultDto> pillarRanks, UserRole userRole)
        {
            try
            {
                if (pillars == null || pillars.Count == 0)
                    return Array.Empty<byte>();

                var first = pillars[0];
                var countryDetails = new AiCountrySummeryDto
                {
                    CountryID = first.CountryID,
                    CountryName = first.CountryName,
                    Continent = first.Continent,
                    Year = first.AIDataYear,
                    AIProgress = pillars.Average(x => x.AIProgress ?? 0)
                };
                var pillarChartItems = pillars.Select(p => new PillarChartItem(
                    (p.PillarName?.Length > 20 ? p.PillarName[..20] : p.PillarName) ?? "-",
                    p.PillarName ?? "-",
                    p.AIProgress)).ToList();

                return BuildDocument(mainPart =>
                {
                    var body = mainPart.Document.Body!;
                    _imgId = 1;

                    AppendCountryHeader(mainPart, countryDetails, "Domain Performance Overview");
                    AddPillarOverviewSection(body, mainPart, pillarChartItems);

                    foreach (var pillarData in pillars)
                    {
                        AppendCountryHeader(mainPart, countryDetails, pillarData.PillarName);
                        var pillarRank = pillarRanks?.FirstOrDefault(p =>
                            p.PillarID == pillarData.PillarID && p.CountryID == pillarData.CountryID);
                        AddSelectedPillarMarkedSection(body, pillarData, pillarRank);
                    }

                    FinalizeLastSection(mainPart);
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GenerateSelectedPillarsDetailsDocx", ex);
                return Array.Empty<byte>();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DOCUMENT SHELL HELPER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Creates a blank A4 document, calls <paramref name="populate"/>, returns bytes.</summary>
        private byte[] BuildDocument(Action<MainDocumentPart> populate)
        {
            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());
                populate(mainPart);

                // A4 page size + narrow margins + page numbers in footer
                var body = mainPart.Document.Body!;
                body.AppendChild(new SectionProperties(
                    new PageSize  { Width = PageWidthDxa, Height = PageHeightDxa },
                    new PageMargin { Top = MarginDxa, Right = MarginDxa,
                                     Bottom = MarginDxa, Left = MarginDxa }));

                mainPart.Document.Save();
            }
            return ms.ToArray();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CITY REPORT  –  SECTION COMPOSITION
        // ════════════════════════════════════════════════════════════════════
        private void AddCountryDetailsSections(
            Body body, MainDocumentPart mainPart,
            AiCountrySummeryDto countryDetails,
            List<AiCountryPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peerCountries,
            UserRole userRole,
            bool isAllCountries = false)
        {
            // Reset pending header state for this document
            ResetSectionState();

            var kpiChartItems = kpis.OrderByDescending(x => x.Value).ToList();
            var pillarChartItems = pillars.Take(23)
                .Select(p => new PillarChartItem(
                    p.PillarName?.Length > 20 ? p.PillarName[..20] : p.PillarName ?? "—",
                    p.PillarName ?? "—",
                    p.AIProgress))
                .ToList();

            // ── 1. Global Dashboard ──────────────────────────────────────────────────
                AppendCountryHeader(mainPart, countryDetails, "Country Performance Dashboard");
                AddDashboardSection(body, mainPart, countryDetails, pillarChartItems, kpiChartItems);

            // ── 2. Country Summary ──────────────────────────────────────────────────────
            AppendCountryHeader(mainPart, countryDetails, null);          
            AddCountrySummarySection(body, mainPart, countryDetails, userRole, isAllCountries);

            // ── 3. Pillar Radial Overview ────────────────────────────────────────────
            if (pillars.Any())
            {
                AppendCountryHeader(mainPart, countryDetails, "Pillar Performance Overview");
                AddPillarOverviewSection(body, mainPart, pillarChartItems);
            }

            // ── 4. Peer Comparison & Trends ─────────────────────────────────────────
            if (!isAllCountries)
            {
                if (peerCountries.Any())
                {
                    AddPeerComparisonSections(body, mainPart, peerCountries, countryDetails, userRole);
                    AddPerformanceTrendSections(body, mainPart, peerCountries, countryDetails, userRole);
                }

                // ── 5. Per-Pillar Detail ─────────────────────────────────────────────────
                var accessiblePillars = pillars.Where(x =>
                    (x.IsAccess && userRole == UserRole.CountryUser) || userRole != UserRole.CountryUser).ToList();

                foreach (var pillar in accessiblePillars)
                {
                    AppendCountryHeader(mainPart, countryDetails, pillar.PillarName);
                    AddPillarSection(body, mainPart, pillar, userRole);
                }
            }

            // ── 6. KPI Dashboard (LAST section) ─────────────────────────────────────
            if (kpiChartItems.Any())
            {
                AppendCountryHeader(mainPart, countryDetails, "KPI Dashboard");
                AddKpiDashboardSection(body, mainPart, kpiChartItems, isAllCountries);
            }

            // ── 7. Findings and Recommendations (LAST, after KPI) ───────────────────
            if (!isAllCountries && !string.IsNullOrEmpty(countryDetails.Recommendations))
            {
                AppendCountryHeader(mainPart, countryDetails, "Recommendations");
                AppendContentSection(body, "Recommendations", countryDetails.Recommendations, ReportThemeColors.CB2DFDBHex);
            }

            FinalizeLastSection(mainPart);
        }

        // ════════════════════════════════════════════════════════════════════
        //  DASHBOARD SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddDashboardSection(
            Body body, MainDocumentPart mainPart,
            AiCountrySummeryDto country,
            List<PillarChartItem> pillars,
            List<KpiChartItem> kpis)
        {
            float overall = (float)country.AIProgress.GetValueOrDefault();
            var validPillars = pillars.ToList();
            // ── Call site ────────────────────────────────────────────────────────────────
            var donutPng = RenderPng((c, s) => PaintDonut(c, s, overall), 320, 220);
            var radarPng = RenderPng((c, s) => PaintSpiderChart(c, s, validPillars), 460, 280);

            var best = validPillars.OrderByDescending(x => x.Value).First();
            var worst = validPillars.OrderBy(x => x.Value).First();
            body.AppendChild(
                CreateScoreAndRadarRow(
                    mainPart,
                    donutPng, radarPng,
                    country,
                    pillars.Count, kpis.Count,
                    best, worst,
                    validPillars));

            body.AppendChild(Gap(20));

            // Row 2: KPI stats band
            int green = kpis.Count(k => k.Value >= 70);
            int amber = kpis.Count(k => k.Value is >= 40 and < 70);
            int red   = kpis.Count(k => k.Value < 40);
            foreach (var el in CreateKpiStatSection(kpis.Count, green, amber, red))
                body.Append(el);


            body.AppendChild(Gap(20));

            if (kpis.Any())
            {
                var avg = kpis.Average(x => x.Value).ToString("0.0") + "%";

                body.Append(CreateKpiOverviewHeader(avg));

                var sparkPng = RenderPng((c, s) => PaintKpiSparkline(c, s, kpis), 700, 130);
                body.AppendChild(CreateFullWidthImage(mainPart, sparkPng, 130));
            }


        }
        private static Table CreateKpiOverviewHeader(string avgText)
        {
            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }
                ),
                new TableRow(
                    // LEFT: Title
                    new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = (ContentDxa * 3 / 4).ToString(), Type = TableWidthUnitValues.Dxa },
                            new TableCellBorders(
                                new TopBorder { Val = BorderValues.None },
                                new BottomBorder { Val = BorderValues.None },
                                new LeftBorder { Val = BorderValues.None },
                                new RightBorder { Val = BorderValues.None }
                            )
                        ),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                            new Run(
                                new RunProperties(new Bold(), new FontSize { Val = "18" }),
                                new Text("KPI Overview — All Indicators (sorted high → low)")
                            )
                        )
                    ),

                    // RIGHT: Avg
                    new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = (ContentDxa / 4).ToString(), Type = TableWidthUnitValues.Dxa },
                            new TableCellBorders(
                                new TopBorder { Val = BorderValues.None },
                                new BottomBorder { Val = BorderValues.None },
                                new LeftBorder { Val = BorderValues.None },
                                new RightBorder { Val = BorderValues.None }
                            )
                        ),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                            new Run(
                                new RunProperties(new Bold(), new FontSize { Val = "18" }),
                                new Text($"Avg: {avgText}")
                            )
                        )
                    )
                )
            );
        }
        // ── Master row builder ────────────────────────────────────────────────────────

        private Table CreateScoreAndRadarRow(
            MainDocumentPart mainPart,
            byte[] donutPng,
            byte[] radarPng,
            AiCountrySummeryDto country,
            int pillarCount,
            int kpiCount,
            PillarChartItem? best,
            PillarChartItem? worst,
            List<PillarChartItem> pillars)
        {
            float overallScore = (float)country.AIProgress.GetValueOrDefault();

            var leftCell = BuildDonutCell(mainPart, donutPng, country, pillarCount, kpiCount, best, worst);
            var rightCell = BuildRadarCell(mainPart, radarPng, pillars);

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })),
                new TableRow(leftCell, rightCell));
        }

        // ── LEFT: Donut card (40 % width) ────────────────────────────────────────────
        private TableCell BuildDonutCell(
            MainDocumentPart mainPart,
            byte[] donutPng,
            AiCountrySummeryDto country,
            int pillarCount,
            int kpiCount,
            PillarChartItem? best,
            PillarChartItem? worst)
        {
            int leftDxa = (int)(ContentDxa * 0.30);   // 40 % of page content width
            long imgEmuW = (long)leftDxa * 914400L / 1440L;
            long imgEmuH = imgEmuW * 220 / 320;        // keep aspect of 320×220 render

            var cell = new TableCell();

            // ── Cell properties ──
            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = leftDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                CellNoBorder(),
                new TableCellMargin(
                    new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = ReportThemeColors.WhiteHex }));

            // ── Heading ──
            cell.Append(CenteredBoldPara("Overall Country Score", ReportThemeColors.TextInkHex, "20"));
            // ── Ranking Labels ──
            var globalRankLabel = country.Rank.HasValue && country.TotalCountry.HasValue && country.TotalCountry >=1
                ? $"Global Rank: {country.Rank} / {country.TotalCountry}"
                : "Global Rank: N/A";

            var regionName = string.IsNullOrEmpty(country.Region) ? "Region" : country.Region;

            var regionRankLabel = country.RegionRank.HasValue && country.RegionTotalCountry.HasValue && country.RegionTotalCountry >= 1
                ? $"{regionName}: {country.RegionRank} / {country.RegionTotalCountry}"
                : $"{regionName}: N/A";

           
            // ── Donut image ──
            cell.Append(EmbedImage(mainPart, donutPng, imgEmuW, imgEmuH));
 

            // ── Pillars | KPIs row ──
            cell.Append(BuildPillarKpiTable(pillarCount, kpiCount, leftDxa));


            if (best != null)
            {
                cell.Append(
                    BuildDualBadgeRow(
                        $"▲ {Shorten(best.Name, 16)} ({best.Value:F0})",
                        ReportThemeColors.SuccessGreenBgHex,
                        ReportThemeColors.SuccessGreenDarkHex,

                        globalRankLabel,
                        ReportThemeColors.SurfaceGreenHex,
                        ReportThemeColors.PdfDarkGreenHex
                    ));
            }

            // ─────────────────────────────────────────────
            // Worst Pillar + Region Rank
            // ─────────────────────────────────────────────
            if (worst != null)
            {
                cell.Append(
                    BuildDualBadgeRow(
                        $"▼ {Shorten(worst.Name, 16)} ({worst.Value:F0})",
                        ReportThemeColors.DangerRedBgHex,
                        ReportThemeColors.DangerRedDarkHex,

                        regionRankLabel,
                        ReportThemeColors.WarningOrangeBgHex,
                        ReportThemeColors.WarningOrangeTextHex
                    ));
            }
            return cell;
        }
        private Table BuildDualBadgeRow(
            string leftText,
            string leftBg,
            string leftColor,
            string rightText,
            string rightBg,
            string rightColor)
        {
            var table = new Table(
                new TableProperties(
                    new TableWidth
                    {
                        Width = "5000", // 100%
                        Type = TableWidthUnitValues.Pct
                    },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None }
                    )
                )
            );

            var row = new TableRow();

            // Left badge
            row.Append(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth
                        {
                            Width = "3250", // 65%
                            Type = TableWidthUnitValues.Pct
                        },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = leftBg
                        },
                        CellNoBorder()
                    ),
                    new Paragraph(
                        new ParagraphProperties(
                            new SpacingBetweenLines
                            {
                                Before = "40",
                                After = "40"
                            }),
                        new Run(
                            new RunProperties(
                                new Color { Val = leftColor },
                                new FontSize { Val = "14" }
                            ),
                            new Text(leftText)
                        )
                    )
                )
            );

            // Right badge
            row.Append(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth
                        {
                            Width = "1750", // 35%
                            Type = TableWidthUnitValues.Pct
                        },
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = rightBg
                        },
                        CellNoBorder()
                    ),
                    new Paragraph(
                        new ParagraphProperties(
                            new Justification
                            {
                                Val = JustificationValues.Center
                            },
                            new SpacingBetweenLines
                            {
                                Before = "40",
                                After = "40"
                            }),
                        new Run(
                            new RunProperties(
                                new Bold(),
                                new Color { Val = rightColor },
                                new FontSize { Val = "14" }
                            ),
                            new Text(rightText)
                        )
                    )
                )
            );

            table.Append(row);

            return table;
        }

        // ── RIGHT: Radar card (60 % width) ───────────────────────────────────────────
        private TableCell BuildRadarCell(
            MainDocumentPart mainPart,
            byte[] radarPng,
            List<PillarChartItem> pillars)
        {
            int rightDxa = (int)(ContentDxa * 0.60);   // 60 % of page content width
            long imgEmuW = (long)rightDxa * 914400L / 1440L;
            long imgEmuH = imgEmuW * 280 / 460;        // keep aspect of 460×280 render

            var cell = new TableCell();

            cell.Append(new TableCellProperties(
                new TableCellWidth { Width = rightDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                CellNoBorder(),
                new TableCellMargin(
                    new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = ReportThemeColors.WhiteHex }));

            // ── Heading ──
            cell.Append(CenteredBoldPara("Pillar Performance Radar", ReportThemeColors.PdfDarkGreenHex, "20"));

            // ── Radar image ──
            cell.Append(EmbedImage(mainPart, radarPng, imgEmuW, imgEmuH));

            return cell;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// Pillars | KPIs two-column mini-table
        private Table BuildPillarKpiTable(int pillarCount, int kpiCount, int parentDxa)
        {
            int half = parentDxa / 2;

            TableCell CountCell(string number, string label) =>
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = half.ToString(), Type = TableWidthUnitValues.Dxa },
                        CellNoBorder(),
                        new TableCellMargin(new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                    new Paragraph(new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                                new Bold(), new Color { Val = ReportThemeColors.PdfMediumGreenHex }, new FontSize { Val = "36" }),
                            new Text(number))),
                    new Paragraph(new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                                new Color { Val = ReportThemeColors.Gray757575Hex }, new FontSize { Val = "16" }),
                            new Text(label))));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = parentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.Single, Color = ReportThemeColors.GrayE0E0E0Hex, Size = 4 })),
                new TableRow(
                    CountCell(pillarCount.ToString(), "Pillars"),
                    CountCell(kpiCount.ToString(), "KPIs")));
        }

        /// Centered bold paragraph (headings)
        private static Paragraph CenteredBoldPara(string text, string hexColor, string halfPtSize) =>
            new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { After = "40" }),
                new Run(new RunProperties(
                        new Bold(),
                        new Color { Val = hexColor },
                        new FontSize { Val = halfPtSize }),
                    new Text(text)));


        /// Colored badge paragraph (best / worst pillars)
        private static Paragraph BadgePara(
            string text, string bgHex, string arrowHex, string textHex)
        {
            var shading = new Shading
            {
                Val = ShadingPatternValues.Clear,
                Color = "auto",
                Fill = bgHex
            };
            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "40", After = "0" },
                    new Indentation { Left = "80", Right = "80" },
                    shading),
                new Run(new RunProperties(
                        new Color { Val = arrowHex },
                        new FontSize { Val = "16" }),
                    new Text(text.Substring(0, 2))),
                new Run(new RunProperties(
                        new Color { Val = textHex },
                        new FontSize { Val = "16" }),
                    new Text(text.Substring(2)) { Space = SpaceProcessingModeValues.Preserve }));
        }

        /// TableCellBorders — all None
        private static TableCellBorders CellNoBorder()
        {
            var n = new EnumValue<BorderValues>(BorderValues.None);
            return new TableCellBorders(
                new TopBorder { Val = n },
                new BottomBorder { Val = n },
                new LeftBorder { Val = n },
                new RightBorder { Val = n });
        }

        // ════════════════════════════════════════════════════════════════════
        //  CITY SUMMARY SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddCountrySummarySection(Body body, MainDocumentPart mainPart, AiCountrySummeryDto data, UserRole userRole, bool isAllCountries= false)
        {
            // =========================
            // PROGRESS SECTION
            // =========================
            body.AppendChild(SectionHeading("Total Score", DarkBlue));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), MedBlue));
            // Rankings Section
            body.AppendChild(CreateRankingHeader("Rankings"));

            body.AppendChild(CreateRankRow("Global Rank",
                data.Rank, data.TotalCountry, ReportThemeColors.AccentGreenHex));

            body.AppendChild(CreateRankRow("Region Rank",
                data.RegionRank, data.RegionTotalCountry, ReportThemeColors.AccentBlueHex));

            body.AppendChild(Gap(160));

            // =========================
            // EXECUTIVE SUMMARY
            // =========================
            AppendContentSection(body, "Executive Summary", data.EvidenceSummary, ReportThemeColors.C163329Hex);

            if (!isAllCountries)
            {
                // =====================================================
                // current situation
                // =====================================================
                AppendContentSection(body, "Key Developments", data.KeyDevelopments, ReportThemeColors.CE6CCFFHex);
                AppendContentSection(body, "Critical Risks", data.CriticalRisks, ReportThemeColors.CC2F0F0Hex);
                AppendContentSection(body, "Gaps", data.Gaps, ReportThemeColors.CFFE6CCHex);

                // =====================================================
                // EVIDENCE SECTION
                // =====================================================
                AppendContentSection(body, "Structural Evidence", data.StructuralEvidence, ReportThemeColors.CE6CCFFHex);
                AppendContentSection(body, "Operational Evidence", data.OperationalEvidence, ReportThemeColors.CC2F0F0Hex);

                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Outcome Evidence", data.OutcomeEvidence, ReportThemeColors.CFFE6CCHex);
                AppendContentSection(body, "Perception Evidence", data.PerceptionEvidence, ReportThemeColors.CE6F7FFHex);

                // =====================================================
                // INTEGRITY CHECKS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Temporal Scope", data.TemporalScope, ReportThemeColors.CD9E6FFHex);
                AppendContentSection(body, "Distortion Screening", data.DistortionScreening, ReportThemeColors.CF2D9E6Hex);
                AppendContentSection(body, "Relational Integrity", data.RelationalIntegrity, ReportThemeColors.CF0FFE6Hex);

                // =====================================================
                // STRESS TESTS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Political Shock", data.PoliticalShock, ReportThemeColors.CFFD9CCHex);
                AppendContentSection(body, "Economic Shock", data.EconomicShock, ReportThemeColors.CFFF2CCHex);
                AppendContentSection(body, "Narrative Shock", data.NarrativeShock, ReportThemeColors.CE6F2FFHex);

                //body.AppendChild(PageBreak());

                //AppendContentSection(body, "Overall Stress Resilience", data.OverallStressResilience, ReportThemeColors.CE6FFE6Hex);
                AppendContentSection(body, "Stress Score Adjustment", data.StressScoreAdjustment, ReportThemeColors.CFFE6F2Hex);

                // =====================================================
                // GOVERNANCE ADJUSTMENTS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Inequality Adjustment", data.InequalityAdjustment, ReportThemeColors.CF9E6FFHex);
                AppendContentSection(body, "Opacity Risk", data.OpacityRisk, ReportThemeColors.CFFF0E6Hex);
                AppendContentSection(body, "Non Compensation Note", data.NonCompensationNote, ReportThemeColors.CE6FFF9Hex);

                // =====================================================
                // SYSTEM ANALYSIS
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Cross-Pillar System Dynamics", data.CrossPillarPatterns, ReportThemeColors.C6E9688Hex);
                AppendContentSection(body, "Institutional Capacity Assessment", data.InstitutionalCapacity, ReportThemeColors.C0D8057Hex);

                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Equity Assessment", data.EquityAssessment, ReportThemeColors.SuccessGreenBgHex);
                AppendContentSection(body, "Conflict Risk Outlook", data.ConflictRiskOutlook, ReportThemeColors.CFCE4ECHex);

                // =====================================================
                // STRATEGIC OUTPUT
                // =====================================================
                //body.AppendChild(PageBreak());

                AppendContentSection(body, "Strategic Policy Priorities", data.StrategicRecommendation, ReportThemeColors.C2E9975Hex);
                AppendContentSection(body, "Why This Assessment Matters", data.DataTransparencyNote, ReportThemeColors.C63A68FHex);
                AppendContentSection(body, "Key Findings", data.KeyFindings, ReportThemeColors.CBBDEFBHex);
            }     
        }

        private static Paragraph CreateRankingHeader(string text)
        {
            return new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "120" }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = ReportThemeColors.Gray374151Hex }, new FontSize { Val = "22" }),
                    new Text(text)));
        }
        private static Table CreateRankRow(string label, int? rank, int? total, string color)
        {
            var noBorder = new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None });

            var leftCell = new TableCell(
                new TableCellProperties(noBorder.CloneNode(true)),
                new Paragraph(
                    new Run(
                        new RunProperties(new Color { Val = ReportThemeColors.Gray4B5563Hex }, new FontSize { Val = "20" }),
                        new Text(label))));

            var rightPara = new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Right }));

            if (rank.HasValue && total.HasValue)
            {
                rightPara.Append(
                    new Run(new RunProperties(new Bold(), new Color { Val = color }),
                        new Text((rank ?? 0).ToString())),
                    new Run(new RunProperties(new Color { Val = ReportThemeColors.Gray6B7280Hex }),
                        new Text($" / {total}"))
                );
            }
            else
            {
                rightPara.Append(new Run(new Text("-")));
            }

            var rightCell = new TableCell(
                new TableCellProperties(noBorder.CloneNode(true)),
                rightPara);

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(leftCell, rightCell));
        }


        // ════════════════════════════════════════════════════════════════════
        //  PILLAR OVERVIEW SECTION  (radial + horizontal bars)
        // ════════════════════════════════════════════════════════════════════

        private void AddPillarOverviewSection(
            Body body, MainDocumentPart mainPart,
            List<PillarChartItem> pillars)
        {
            var data = pillars.Where(p => p.Value.HasValue).OrderByDescending(x => x.Value).ToList();
            if (!data.Any()) return;

            var radialPng = RenderPng((c, s) => PaintPillarRadialChart(c, s, data), 340, 340);
            var barPng    = RenderPng((c, s) => PaintPillarHorizontalBars(c, s, data), 400, 340);
            body.AppendChild(CreateSideBySideImages(mainPart, radialPng, barPng, 340));
            body.AppendChild(Gap(160));
            body.AppendChild(CreatePillarFooterTable(data));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PER-PILLAR SECTION
        // ════════════════════════════════════════════════════════════════════
        private void AddSelectedPillarMarkedSection(Body body, AiCountryPillarResponse data, CountryPillarRankingResultDto? pillarRank)
        {
            body.AppendChild(SectionHeading("Domain Score", DarkBlue));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), MedBlue));
            body.AppendChild(Gap(160));
            body.AppendChild(CreateRankTable(pillarRank));
            body.AppendChild(Gap(160));
            AppendContentSection(body, "Executive Summary", data.EvidenceSummary, ReportThemeColors.PdfDarkGreenHex);
        }

        private void AddPillarSection(
    Body body, MainDocumentPart mainPart,
    AiCountryPillarResponse data, UserRole userRole)
        {
            // =========================
            // PROGRESS SECTION
            // =========================
            body.AppendChild(SectionHeading("Pillar Score", DarkBlue));
            body.AppendChild(CreateProgressBar("Score", (float)(data.AIProgress ?? 0), MedBlue));
            body.AppendChild(Gap(160));

            // =========================
            // EVIDENCE SUMMARY
            // =========================
            AppendContentSection(body, "Executive Summary", data.EvidenceSummary, ReportThemeColors.C163329Hex);

            // =====================================================
            // EVIDENCE SECTION
            // =====================================================
            AppendContentSection(body, "Structural Evidence", data.StructuralEvidence, ReportThemeColors.HeaderBlueHex);
            AppendContentSection(body, "Operational Evidence", data.OperationalEvidence, ReportThemeColors.HeaderBlueMidHex);

            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Outcome Evidence", data.OutcomeEvidence, ReportThemeColors.HeaderBlueLightHex);
            AppendContentSection(body, "Perception Evidence", data.PerceptionEvidence, ReportThemeColors.HeaderBluePaleHex);

            // =====================================================
            // INTEGRITY CHECKS
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Temporal Scope", data.TemporalScope, ReportThemeColors.C5F497AHex);
            AppendContentSection(body, "Distortion Screening", data.DistortionScreening, ReportThemeColors.C8064A2Hex);
            AppendContentSection(body, "Relational Integrity", data.RelationalIntegrity, ReportThemeColors.CB1A0C7Hex);

            // =====================================================
            // STRESS TEST
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Stress Political Shock", data.StressPoliticalShock, ReportThemeColors.C7F6000Hex);
            AppendContentSection(body, "Stress Economic Shock", data.StressEconomicShock, ReportThemeColors.CBF9000Hex);
            AppendContentSection(body, "Stress Narrative Shock", data.StressNarrativeShock, ReportThemeColors.CFFD966Hex);

            //body.AppendChild(PageBreak());

            //AppendContentSection(body, "Stress Overall Resilience", data.StressOverallResilience, ReportThemeColors.CC55A11Hex);
            AppendContentSection(body, "Stress Score Adjustment", data.StressScoreAdjustment, ReportThemeColors.CE26B0AHex);

            // =====================================================
            // GOVERNANCE ADJUSTMENTS
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Inequality Adjustment", data.InequalityAdjustment, ReportThemeColors.C274E13Hex);
            AppendContentSection(body, "Opacity Risk", data.OpacityRisk, ReportThemeColors.C38761DHex);
            AppendContentSection(body, "Non-Compensation Note", data.NonCompensationNote, ReportThemeColors.C6AA84FHex);

            // =====================================================
            // ALERTS & EQUITY
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Red Flags", data.RedFlag, ReportThemeColors.AccentOrangeHex, ReportThemeColors.AccentRedHex);
            AppendContentSection(body, "Geographic Equity Note", data.GeographicEquityNote, ReportThemeColors.C0D8057Hex);

            // =====================================================
            // INSTITUTIONAL ANALYSIS
            // =====================================================
            //body.AppendChild(PageBreak());

            AppendContentSection(body, "Institutional Assessment", data.InstitutionalAssessment, ReportThemeColors.C2E9975Hex);

            AppendContentSection(
                body,
                "Analytical Foundations and Data Integration",
                data.DataGapAnalysis,
                ReportThemeColors.CA4BAB2Hex
            );

            // =====================================================
            // DATA SOURCES
            // =====================================================
            if (data.DataSourceCitations?.Any() == true)
            {
                body.AppendChild(PageBreak());
                AppendDataSourcesSection(body, data.DataSourceCitations.ToList());
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATA SOURCES SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AppendDataSourcesSection(Body body, List<AIDataSourceCitation> sources)
        {
            body.AppendChild(SectionHeading("Data Source Citations", ReportThemeColors.C396154Hex));
            foreach (var src in sources.Take(10))
            {
                body.AppendChild(BoldParagraph(src.SourceName ?? "", ReportThemeColors.C2C423BHex, 22));
                body.AppendChild(NormalParagraph(
                    $"Trust Level: {src.TrustLevel}/7  |  Year: {src.DataYear}  |  Type: {src.SourceType ?? "—"}",
                    ReportThemeColors.Gray757575Hex, 18));
                if (!string.IsNullOrEmpty(src.DataExtract))
                    body.AppendChild(NormalParagraph(TruncateText(src.DataExtract, 200), ReportThemeColors.Gray616161Hex, 18, italic: true));
                if (!string.IsNullOrEmpty(src.SourceURL))
                    body.AppendChild(NormalParagraph(src.SourceURL, ReportThemeColors.C305246Hex, 16));
                body.AppendChild(Gap(120));
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  KPI DASHBOARD SECTION
        // ════════════════════════════════════════════════════════════════════

        private void AddKpiDashboardSection(
            Body body, MainDocumentPart mainPart,
            List<KpiChartItem> kpis, bool isAllCountries = false)
        {
            if (!kpis.Any()) return;

            int total = kpis.Count;
            int green = kpis.Count(x => x.Value >= 70);
            int amber = kpis.Count(x => x.Value is >= 40 and < 70);
            int red   = kpis.Count(x => x.Value < 40);
            float avg = (float)kpis.Average(x => x.Value);

            body.AppendChild(CreateKpiSummaryBandTable(total, green, amber, red, avg));
            body.AppendChild(Gap(100));

            // Groups of 18 KPIs — bar chart + interpretation cards
            var groups = kpis
                .Select((k, i) => new { k, i })
                .GroupBy(x => x.i / 13)
                .Select(g => g.Select(x => x.k).ToList())
                .ToList();

            int offset = 0;
            foreach (var group in groups.Where(g => g.Any()))
            {
                int localOffset = offset;
                var barPng = RenderPng(
                    (c, s) => PaintKpiBarChart(c, s, group, localOffset),
                    700, 155);

                body.AppendChild(CreateFullWidthImage(mainPart, barPng, 155));
                body.AppendChild(Gap(80));
                if (!isAllCountries)
                {
                    body.AppendChild(CreateKpiCardTable(mainPart, group));
                    body.AppendChild(Gap(160));
                }
                offset += group.Count;
            }
        }

       

        /// <summary>
        /// Registers a repeating page header (appears on every page) that mirrors
        /// the QuestPDF CityComposeHeader layout:
        ///
        ///  ┌─────────────────────────────────────────┬────────────┐
        ///  │  [Title — bold white 21pt]              │ [  LOGO  ] │
        ///  │  City, State, Country | Data Year: YYYY │ [  white ] │
        ///  │  Generated: Mon DD, YYYY               │ [   box  ] │
        ///  └─────────────────────────────────────────┴────────────┘
        ///  ─────────── divider (#d9e2df) ───────────────────────────
        /// </summary>


        // ── Field: holds the pending header relId until the section is closed ──────
        private string? _pendingHeaderRelId = null;

        /// <summary>
        /// Call once before generating any country sections to reset state.
        /// </summary>
        private void ResetSectionState() => _pendingHeaderRelId = null;

        /// <summary>
        /// Must be called AFTER the last section's content has been appended.
        /// Attaches the last pending header to the document's final sectPr.
        /// </summary>
        private void FinalizeLastSection(MainDocumentPart mainPart)
        {
            if (_pendingHeaderRelId == null) return;

            var sectPr = mainPart.Document.Body!
                             .Elements<SectionProperties>().LastOrDefault()
                         ?? mainPart.Document.Body!.AppendChild(new SectionProperties());

            sectPr.RemoveAllChildren<HeaderReference>();
            sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Even, Id = _pendingHeaderRelId });
            sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.First, Id = _pendingHeaderRelId });
            sectPr.PrependChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = _pendingHeaderRelId });

            _pendingHeaderRelId = null;
        }


        /// <summary>
        /// Creates a header for the upcoming section.
        /// Automatically closes the PREVIOUS section with a next-page section break
        /// (which replaces the manual PageBreak() call between sections).
        /// </summary>
        private void AppendCountryHeader(
     MainDocumentPart mainPart,
     AiCountrySummeryDto data,
     string? sectionTitle = null)
        {
            var body = mainPart.Document.Body!;

            if (_pendingHeaderRelId != null)
            {
                var closingSectPr = BuildSectionProperties(_pendingHeaderRelId);
                body.AppendChild(new Paragraph(new ParagraphProperties(closingSectPr)));
            }

            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();

            string title = string.IsNullOrEmpty(sectionTitle) ? data.CountryName : sectionTitle;

            string logoPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot/assets/images/pem.png");

            int logoColW = 2600;
            int leftColW = ContentDxa - logoColW;

            const long logoWidthEmu = 900_000L;
            const long logoHeightEmu = 300_000L; // ✅ slightly increased

            // ✅ MAIN TABLE
            var layoutTable = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableLayout { Type = TableLayoutValues.Fixed },
                    new TableCellSpacing() { Width = "0", Type = TableWidthUnitValues.Dxa }
                )
            );

            var mainRow = new TableRow(
                new TableRowProperties(
                    new TableRowHeight
                    {
                        Val = 900, // ✅ prevent compression
                        HeightType = HeightRuleValues.AtLeast
                    }
                )
            );

            // ✅ LEFT CELL
            var leftCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = leftColW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Fill = ReportThemeColors.HeaderNavyHex },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "250", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "150", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            leftCell.Append(
                HeaderParagraph(title, "42", ReportThemeColors.WhiteHex, true, "40"),
                HeaderParagraph($"{data.CountryName}, {data.Continent} | Data Year: {data.Year}", "20", ReportThemeColors.SurfaceE8F3F0Hex, false, "20"),
                HeaderParagraph($"Generated: {DateTime.Now:MMM dd, yyyy}", "16", ReportThemeColors.SurfaceCFE3DDHex, false, "0")
            );

            mainRow.Append(leftCell);

            // ✅ RIGHT CELL (BLUE BACKGROUND)
            var rightCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = logoColW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Fill = ReportThemeColors.HeaderNavyHex },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            // ✅ INNER TABLE (WHITE BOX)
            var innerTable = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
                )
            );

            var innerRow = new TableRow();

            var innerCell = new TableCell(
                new TableCellProperties(
                    new Shading { Fill = ReportThemeColors.WhiteHex },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "120", Type = TableWidthUnitValues.Dxa },   // ✅ FIX
                        new BottomMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            if (File.Exists(logoPath))
            {
                var logoPara = EmbedImageInPart(
                    headerPart,
                    File.ReadAllBytes(logoPath),
                    logoWidthEmu,
                    logoHeightEmu
                );

                logoPara.ParagraphProperties = new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines
                    {
                        Before = "0",   // ✅ REMOVE extra space
                        After = "0",
                        Line = "240",
                        LineRule = LineSpacingRuleValues.Auto
                    }
                );

                innerCell.Append(logoPara);
            }
            else
            {
                innerCell.Append(new Paragraph());
            }

            innerRow.Append(innerCell);
            innerTable.Append(innerRow);

            rightCell.Append(innerTable);
            mainRow.Append(rightCell);

            layoutTable.Append(mainRow);

            // ✅ DIVIDER
            var divider = new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(
                        new BottomBorder
                        {
                            Val = BorderValues.Single,
                            Size = 6,
                            Color = ReportThemeColors.SurfaceD9E2DFHex
                        }
                    )
                )
            );

            header.Append(layoutTable, divider);

            headerPart.Header = header;
            header.Save();

            _pendingHeaderRelId = mainPart.GetIdOfPart(headerPart);
        }

        /// <summary>
        /// Builds a SectionProperties with header references and a next-page break.
        /// </summary>
        private static SectionProperties BuildSectionProperties(string headerRelId)
        {
            var sp = new SectionProperties();
            sp.AppendChild(new SectionType { Val = SectionMarkValues.NextPage });
            sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerRelId });
            sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.First, Id = headerRelId });
            sp.AppendChild(new HeaderReference { Type = HeaderFooterValues.Even, Id = headerRelId });
            return sp;
        }
        // ── Helper: single-line paragraph for the header ─────────────────────────────
        private static Paragraph HeaderParagraph(
            string text,
            string fontSize,
            string color,
            bool bold,
            string spacingAfter)
        {
            var rp = new RunProperties(
                new Color { Val = color },
                new FontSize { Val = fontSize },
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
            if (bold) rp.PrependChild(new Bold());

            return new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { Before = "0", After = spacingAfter }),
                new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }


        /// <summary>
        /// Mirrors EmbedImage() exactly, but targets a <see cref="HeaderPart"/> instead of
        /// <see cref="MainDocumentPart"/> so the relationship is resolved inside the header.
        /// </summary>
        private Paragraph EmbedImageInPart(HeaderPart headerPart, byte[] pngBytes,
      long widthEmu, long heightEmu)
        {
            var imgPart = headerPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imgPart.FeedData(ms);

            string relId = headerPart.GetIdOfPart(imgPart);
            uint id = _imgId++;

            // ✅ Build blip with white luminance recolor
            var blip = new A.Blip { Embed = relId };
            //blip.AppendChild(new A.LuminanceEffect { Brightness = 100000, Contrast = 0 });

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = $"img{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"img{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(           // ✅ uses the white-recolored blip
                                    blip,
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                    {
                        DistanceFromTop = 0U,
                        DistanceFromBottom = 0U,
                        DistanceFromLeft = 0U,
                        DistanceFromRight = 0U
                    });

            return new Paragraph(
                 new ParagraphProperties(
                     new Justification { Val = JustificationValues.Center },
                     new SpacingBetweenLines { Before = "0", After = "0" }
                 ),new Run(drawing)
             );
        }
        /// <summary>Coloured section heading with accent left-border effect.</summary>


        /// <summary>Horizontal progress bar implemented as a two-cell table.</summary>
        private static Table CreateProgressBar(string label, float percentage, string hexColor)
        {
            percentage = Math.Clamp(percentage, 0f, 100f);
            int filled  = (int)(ContentDxa * percentage / 100f);
            int empty   = ContentDxa - filled;

            var border = new EnumValue<BorderValues>(BorderValues.None);
            var noBorders = new TableCellBorders(
                new TopBorder    { Val = border },
                new BottomBorder { Val = border },
                new LeftBorder   { Val = border },
                new RightBorder  { Val = border });

            // Label row
            var labelRow = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                        noBorders.CloneNode(true)),
                    new Paragraph(
                        new Run(new RunProperties(
                            new Color { Val = ReportThemeColors.TextDarkHex }, new FontSize { Val = "22" }),
                            new Text(label)))));

            // Bar row
            TableCell filledCell = filled > 0
                ? new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = filled.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = hexColor },
                        noBorders.CloneNode(true)),
                    new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" })))
                : new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = "1", Type = TableWidthUnitValues.Dxa },
                    noBorders.CloneNode(true)),
                    new Paragraph());

            TableCell emptyCell = empty > 0
                ? new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = empty.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.ShadeLightHex },
                        noBorders.CloneNode(true)),
                    new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" })))
                : new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = "1", Type = TableWidthUnitValues.Dxa },
                    noBorders.CloneNode(true)),
                    new Paragraph());

            var barRow = new TableRow(filledCell, emptyCell);
            barRow.AppendChild(new TableRowProperties(new TableRowHeight { Val = 300, HeightType = HeightRuleValues.Exact }));

            // Score label row
            var scoreRow = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                        noBorders.CloneNode(true)),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                        new Run(new RunProperties(
                            new Bold(), new Color { Val = hexColor }, new FontSize { Val = "22" }),
                            new Text($"{percentage:F1}")))));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder   { Val = BorderValues.None })),
                labelRow, barRow, scoreRow);
        }

        /// <summary>Two-column content block: accent bar on left, title + body text on right.</summary>
        /// 
        private static void AppendContentSection(Body body, string title, string? content, string accentHex, string bgColor = ReportThemeColors.TextMutedHex)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var paragraphs = content
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // ─────────────────────────────────────────
            // TABLE (Single column now)
            // ─────────────────────────────────────────
            var table = new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderD9D9D9Hex, Size = 6 },
                        new BottomBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderD9D9D9Hex, Size = 6 },
                        new LeftBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderD9D9D9Hex, Size = 6 },
                        new RightBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderD9D9D9Hex, Size = 6 }
                    )
                )
            );

            // ─────────────────────────────────────────
            // TITLE ROW (with accent line)
            // ─────────────────────────────────────────
            var titleCell = new TableCell(
                new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.ShadeGrayHex },
                    new TableCellMargin(
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new TopMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
                    ),
                    // ✅ Accent line ONLY here (left border trick)
                    new TableCellBorders(
                        new LeftBorder
                        {
                            Val = BorderValues.Single,
                            Size = 18, // thickness of accent line
                            Color = accentHex
                        }
                    )
                ),
                new Paragraph(
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new Color { Val = ReportThemeColors.TextCharcoalHex },
                            new FontSize { Val = "28" }
                        ),
                        new Text(title)
                    )
                )
            );

            table.AppendChild(new TableRow(titleCell));

            // ─────────────────────────────────────────
            // CONTENT ROW
            // ─────────────────────────────────────────
            var contentCell = new TableCell(
                new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.RowAltHex },
                    new TableCellMargin(
                        new TopMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa }
                    )
                )
            );

            foreach (var para in paragraphs)
            {
                contentCell.AppendChild(new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Both },

                        // ✅ Force white background
                        new Shading
                        {
                            Val = ShadingPatternValues.Clear,
                            Fill = ReportThemeColors.WhiteHex
                        },

                        new SpacingBetweenLines
                        {
                            Line = "276", // 1.15
                            LineRule = LineSpacingRuleValues.Auto,
                            After = "120"
                        }
                    ),
                    new Run(
                        new RunProperties(
                            new Color { Val = bgColor },
                            new FontSize { Val = "22" }
                        ),
                        new Text(para) { Space = SpaceProcessingModeValues.Preserve }
                    )
                ));
            }

            table.AppendChild(new TableRow(contentCell));

            body.AppendChild(table);

            body.AppendChild(Gap(140));
        }

        // ── KPI stat band (4 colored boxes) ─────────────────────────────────

        private static IEnumerable<OpenXmlElement> CreateKpiStatSection(int total, int green, int amber, int red)
        {
            // Heading (Top - Left aligned)
            var heading = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = "20" } // space below heading
                ),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new FontSize { Val = "18" } // adjust if needed
                    ),
                    new Text("KPI Performance Distribution")
                )
            );

            // Existing table (UNCHANGED)
            var table = CreateKpiStatTable(total, green, amber, red);

            return new List<OpenXmlElement>
            {
                heading,
                table
            };
        }
        private static Table CreateKpiStatTable(int total, int green, int amber, int red)
        {
            int cellW = ContentDxa / 4;

            TableCell Stat(string val, string label, string bg, string fg)
            {
                var noBorder = new EnumValue<BorderValues>(BorderValues.None);

                return new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cellW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = bg },
                        new TableCellMargin(
                            new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
                        new TableCellBorders(
                            new TopBorder { Val = noBorder }, new BottomBorder { Val = noBorder },
                            new LeftBorder { Val = noBorder }, new RightBorder { Val = noBorder })
                    ),

                    // VALUE (Reduced size)
                    new Paragraph(
                        new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center },
                            new SpacingBetweenLines { Before = "0", After = "0", Line = "200", LineRule = LineSpacingRuleValues.Auto }
                        ),
                        new Run(
                            new RunProperties(
                                new Bold(),
                                new Color { Val = fg },
                                new FontSize { Val = "28" } // ↓ from 40
                            ),
                            new Text(val)
                        )
                    ),

                    // LABEL (Compact)
                    new Paragraph(
                        new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center },
                            new SpacingBetweenLines { Before = "0", After = "0", Line = "180", LineRule = LineSpacingRuleValues.Auto }
                        ),
                        new Run(
                            new RunProperties(
                                new Color { Val = fg },
                                new FontSize { Val = "18" } // ↑ slightly from 12 for readability
                            ),
                            new Text(label)
                        )
                    )
                );
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }
                ),
                new TableRow(
                    Stat(green.ToString(), "Performing ≥70%", ReportThemeColors.SuccessGreenBgHex, ReportThemeColors.SuccessGreenHex),
                    Stat(amber.ToString(), "Developing 40–69%", ReportThemeColors.WarningAmberBgHex, ReportThemeColors.CostOrangeHex),
                    Stat(red.ToString(), "Needs Improvement < 40 %", ReportThemeColors.DangerRedBgHex, ReportThemeColors.DangerRedHex),
                    Stat(total.ToString(), "Total KPIs", ReportThemeColors.SurfaceMintHex, ReportThemeColors.PdfDarkGreenHex)
                )
            );
        }

        // ── KPI summary band (dark green strip) ──────────────────────────────

        private static Table CreateKpiSummaryBandTable(
            int total, int green, int amber, int red, float avg)
        {
            int cellW = ContentDxa / 5;
            string avgColor = avg >= 70 ? ReportThemeColors.BarGreenHex : avg >= 40 ? ReportThemeColors.BarAmberHex : ReportThemeColors.BarRedHex;

            TableCell Pill(string val, string label, string fg)
            {
                var noBorder = new EnumValue<BorderValues>(BorderValues.None);
                return new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cellW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex },
                        new TableCellBorders(
                            new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                            new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Bold(), new Color { Val = fg }, new FontSize { Val = "30" }),
                            new Text(val))),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Color { Val = "FFFFFFBB" }, new FontSize { Val = "13" }),
                            new Text(label))));
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Pill(total.ToString(),  "Total KPIs",        ReportThemeColors.BarGreenHex),
                    Pill(green.ToString(),  "Performing ≥70%",   ReportThemeColors.BarGreenHex),
                    Pill(amber.ToString(),  "Developing 40–69%", ReportThemeColors.BarAmberHex),
                    Pill(red.ToString(), "Needs Improvement < 40 %", ReportThemeColors.BarRedHex),
                    Pill($"{avg:F1}%",      "Average Score",     avgColor)));
        }

        // ── KPI card grid (2 per row, with interpretation table) ─────────────

        private Table CreateKpiCardTable(MainDocumentPart mainPart, List<KpiChartItem> kpis)
        {
            int gap = 120;
            int cardW = (ContentDxa - gap) / 2;

            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var pairs = kpis
                .Select((k, i) => (k, i))
                .GroupBy(t => t.i / 2)
                .Select(g => g.ToList())
                .ToList();

            int globalIdx = 0;
            foreach (var pair in pairs)
            {
                var row = new TableRow();

                for (int pIdx = 0; pIdx < pair.Count; pIdx++)
                {
                    var (kpi, localI) = pair[pIdx];
                    int cardNum = globalIdx + localI + 1;

                    decimal v = kpi.Value;
                    v = v == 100 ? Math.Round(v, 0) : Math.Round(v, 1);
                    string accent = GetBarColor((float)v).TrimStart('#');

                    var interps = kpi.InterPretation ?? new List<FiveLevelInterpretationsDto>();
                    var matched = interps.FirstOrDefault(x =>
                        x.MinRange.HasValue && x.MaxRange.HasValue &&
                        v >= x.MinRange.Value && v <= x.MaxRange.Value);

                    if (matched == null && interps.Any())
                        matched = interps
                            .Where(x => x.MinRange.HasValue && x.MaxRange.HasValue)
                            .OrderBy(x => Math.Min(
                                Math.Abs(v - x.MinRange!.Value),
                                Math.Abs(v - x.MaxRange!.Value)))
                            .FirstOrDefault();

                    var cardTable = BuildKpiCardTable(kpi, cardNum, v, accent, interps, matched, cardW);

                    // Wrap card table in a cell
                    var cell = new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                            CellNoBorders()),
                        cardTable);
                    row.AppendChild(cell);

                    // Gap cell between the two cards
                    if (pair.Count > 1 && pIdx == 0)
                        row.AppendChild(new TableCell(
                            new TableCellProperties(
                                new TableCellWidth { Width = gap.ToString(), Type = TableWidthUnitValues.Dxa },
                                CellNoBorders()),
                            new Paragraph()));
                }

                // Pad to keep layout when row has only 1 card
                if (pair.Count == 1)
                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                            CellNoBorders()),
                        new Paragraph()));

                table.AppendChild(row);
                table.AppendChild(new TableRow(SpacerCell(ContentDxa, 80)));
                globalIdx += pair.Count;
            }

            return table;
        }

        /// <summary>
        /// Builds a single KPI card as a nested table, matching the QuestPDF card layout:
        ///   ┌─────────────────────────────────────────┐  ← accent border
        ///   │  [#N]  ShortName            score%       │  ← accent bg header
        ///   │         Name (subtitle)                  │
        ///   ├──────────┬──────────────────────────────┤  ← #F0F0F0 sub-header
        ///   │  Range   │ Condition                     │
        ///   ├──────────┼──────────────────────────────┤
        ///   │  0–20    │ Very Low                      │  ← stripe rows, matched = accent
        ///   │  …       │ …                             │
        ///   └──────────┴──────────────────────────────┘
        /// </summary>
        private Table BuildKpiCardTable(
            KpiChartItem kpi,
            int cardNum,
            decimal v,
            string accent,
            List<FiveLevelInterpretationsDto> interps,
            FiveLevelInterpretationsDto? matched,
            int cardW)
        {
            int rangeColW = 920;
            int condColW = cardW - rangeColW;

            var card = new Table(new TableProperties(
                new TableWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = accent },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            // ── ROW 1: Header band ───────────────────────────────────────────────────
            int bubbleW = 260;
            int scoreW = 720;
            int nameW = cardW - bubbleW - scoreW;

            var headerInner = new Table(new TableProperties(
                new TableWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var hRow = new TableRow();

            // Number bubble
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = bubbleW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "13" }),
                        new Text(cardNum.ToString())))));

            // ShortName + Name
            var nameCell = new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = nameW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "40", Type = TableWidthUnitValues.Dxa })));

            nameCell.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                    new Text(kpi.ShortName ?? "") { Space = SpaceProcessingModeValues.Preserve })));

            nameCell.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                new Run(
                    new RunProperties(new Color { Val = ReportThemeColors.DividerHex }, new FontSize { Val = "11" }),
                    new Text(kpi.Name ?? "") { Space = SpaceProcessingModeValues.Preserve })));

            hRow.AppendChild(nameCell);

            // Score
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = scoreW.ToString(), Type = TableWidthUnitValues.Dxa },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new TableCellMargin(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "20" }),
                        new Text($"{v}%") { Space = SpaceProcessingModeValues.Preserve }))));

            headerInner.AppendChild(hRow);

            var headerRow = new TableRow();
            headerRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new GridSpan { Val = 2 },
                    CellNoBorders(),
                    new Shading { Val = ShadingPatternValues.Clear, Fill = accent }),
                headerInner));
            card.AppendChild(headerRow);

            // ── ROW 2: Definition strip (only when Definition has content) ───────────
            if (!string.IsNullOrWhiteSpace(kpi.Definition))
            {
                // "DEF" label + definition text share one paragraph with a run each
                var defPara = new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { Before = "0", After = "0" }));

                // "DEF " label — small, bold, accent colour
                defPara.AppendChild(new Run(
                    new RunProperties(
                        new Bold(),
                        new Color { Val = accent },
                        new FontSize { Val = "9" },          // 4.5 pt
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
                    new Text("DEF :  ") { Space = SpaceProcessingModeValues.Preserve }));

                // Definition body — italic, dark grey, wraps naturally
                defPara.AppendChild(new Run(
                    new RunProperties(
                        new Italic(),
                        new Color { Val = ReportThemeColors.TextMutedHex },
                        new FontSize { Val = "11" },          // 5.5 pt
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
                    new Text(kpi.Definition) { Space = SpaceProcessingModeValues.Preserve }));

                var defCell = new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = cardW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new GridSpan { Val = 2 },               // spans Range + Condition columns
                        new TableCellBorders(
                            new TopBorder
                            {
                                Val = BorderValues.Single,
                                Size = 2,
                                Color = accent,
                                Space = 0
                            },
                            new BottomBorder
                            {
                                Val = BorderValues.Single,
                                Size = 2,
                                Color = ReportThemeColors.DividerHex,
                                Space = 0
                            },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None }),
                        new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.SurfacePaleHex },
                        new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                        new TableCellMargin(
                            new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                            new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa })),
                    defPara);

                var defRow = new TableRow();
                defRow.AppendChild(defCell);
                card.AppendChild(defRow);
            }

            // ── ROW 3: Sub-header (Range | Condition) ────────────────────────────────
            var subHdrRow = new TableRow();
            subHdrRow.AppendChild(MakeInterpCell("Range", rangeColW, ReportThemeColors.SubHeaderBgHex, ReportThemeColors.TextLabelHex, "11", bold: true, bottomDivider: false));
            subHdrRow.AppendChild(MakeInterpCell("Condition", condColW, ReportThemeColors.SubHeaderBgHex, ReportThemeColors.TextLabelHex, "11", bold: true, bottomDivider: false));
            card.AppendChild(subHdrRow);

            // ── ROWS 4+: Interpretation rows ─────────────────────────────────────────
            for (int i = 0; i < interps.Count; i++)
            {
                var interp = interps[i];
                bool isHit = interp == matched;
                bool isLast = i == interps.Count - 1;

                string rowBg = isHit ? accent : (i % 2 == 0 ? ReportThemeColors.WhiteHex : ReportThemeColors.RowAltHex);
                string rangeFg = isHit ? White : ReportThemeColors.TextFaintHex;
                string condFg = isHit ? White : ReportThemeColors.TextBodyHex;

                string rangeStr = interp.MinRange.HasValue && interp.MaxRange.HasValue
                    ? $"{Math.Round(interp.MinRange.Value, 0)}–{Math.Round(interp.MaxRange.Value, 0)}"
                    : "—";

                var interpRow = new TableRow();
                interpRow.AppendChild(MakeInterpCell(rangeStr, rangeColW, rowBg, rangeFg, "12", bold: false, bottomDivider: !isLast));
                interpRow.AppendChild(MakeInterpCell(interp.Condition ?? "—", condColW, rowBg, condFg, "13", bold: isHit, bottomDivider: !isLast));
                card.AppendChild(interpRow);
            }

            return card;
        }

        /// <summary>Creates a single interpretation table cell.</summary>
        private static TableCell MakeInterpCell(
            string text,
            int width,
            string bgColor,
            string fgColor,
            string fontSize,
            bool bold,
            bool bottomDivider)
        {
            var borders = new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new BottomBorder
                {
                    Val = bottomDivider ? BorderValues.Single : BorderValues.None,
                    Size = 2,
                    Color = ReportThemeColors.GrayE0E0E0Hex
                });

            var rp = new RunProperties(
                new Color { Val = fgColor },
                new FontSize { Val = fontSize });
            if (bold) rp.AppendChild(new Bold());

            return new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                    borders,
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bgColor },
                    new TableCellMargin(
                        new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                        new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                        new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }),
                    new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        }

        /// <summary>Returns a TableCellBorders with all sides set to None.</summary>
        private static TableCellBorders CellNoBorders() =>
            new TableCellBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None });

        // ── Pillar footer band (avg, best, worst) ────────────────────────────

        private static Table CreatePillarFooterTable(List<PillarChartItem> data)
        {
            float avg   = (float)data.Average(x => x.Value ?? 0);
            var   best  = data.OrderByDescending(x => x.Value).First();
            var   worst = data.OrderBy(x => x.Value).First();
            int   w3    = ContentDxa / 3;
            var noBorder = new EnumValue<BorderValues>(BorderValues.None);

            TableCell Cell(string[] lines, string[] fgs, string bg)
            {
                var noBord = new TableCellBorders(
                    new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                    new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder });
                var tc = new TableCell(new TableCellProperties(
                    new TableCellWidth { Width = w3.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bg },
                    noBord));
                for (int i = 0; i < lines.Length; i++)
                    tc.AppendChild(new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(
                            new Color { Val = fgs[i] },
                            new FontSize { Val = i == 0 ? "40" : "18" }),
                            new Text(lines[i]))));
                return tc;
            }

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(
                    Cell(new[] { $"{avg:F1}", "Average Score" },
                         new[] { GetBarColor(avg).TrimStart('#'), ReportThemeColors.SuccessGreenLightHex }, ReportThemeColors.PdfDarkGreenHex),
                    Cell(new[] { $"▲ {Shorten(best.Name ?? "—", 22)}", $"{best.Value:F1}%" },
                         new[] { ReportThemeColors.SuccessGreenDarkHex, ReportThemeColors.SuccessGreenHex }, ReportThemeColors.SuccessGreenBgHex),
                    Cell(new[] { $"▼ {Shorten(worst.Name ?? "—", 22)}", $"{worst.Value:F1}%" },
                         new[] { ReportThemeColors.DangerRedDarkHex, ReportThemeColors.DangerRedHex }, ReportThemeColors.DangerRedBgHex)));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PERFORMANCE TREND SECTIONS
        // ════════════════════════════════════════════════════════════════════


        private void AddPerformanceTrendSections(
           Body body, MainDocumentPart mainPart,
           List<PeerCountryHistoryReportDto> activeCountries,
           AiCountrySummeryDto countryDetails, UserRole userRole)
        {
            if (!activeCountries.Any()) return;

            var main = FindMainCountry(activeCountries, countryDetails);
            var peers = activeCountries.Where(p => !IsSameCountry(p.CountryName, countryDetails.CountryName)).ToList();
            var all = BuildAllCountries(main, peers);

            var allYears = all
                .SelectMany(c => c.CountryHistory ?? Enumerable.Empty<PeerCountryYearHistoryDto>())
                .Select(h => h.Year).Distinct().OrderBy(y => y).ToList();

            if (!allYears.Any()) return;

            // Historical trend
            AppendCountryHeader(mainPart, countryDetails, "Performance Trends Over Time");
            var peerAvg = allYears.Select(yr =>
            {
                var scores = peers.Select(p => p.CountryHistory?.FirstOrDefault(h => h.Year == yr))
                    .Where(h => h != null).Select(h => (float)h!.ScoreProgress).ToList();
                return (Year: yr, Avg: scores.Any() ? scores.Average() : 0f, HasData: scores.Any());
            }).ToList();

            var trendPng = RenderPng(
                (c, s) => PaintMultiLineTrend(c, s, allYears, peers, main, countryDetails, peerAvg),
                700, 200);
            body.AppendChild(CreateFullWidthImage(mainPart, trendPng, 200));
            body.AppendChild(Gap(100));

            if (main != null)
                body.AppendChild(CreateYoYTable(allYears.TakeLast(5).ToList(),
                    (main.CountryHistory ?? new()).OrderBy(h => h.Year).ToList(),
                    peerAvg.Select(p => (p.Year, p.Avg)).ToList()));

            body.AppendChild(PageBreak());

            // Pillar trend
            AppendCountryHeader(mainPart, countryDetails, "Pillar-Level Trend Analysis");
            if (main != null)
            {
                var pillars = (main.CountryHistory ?? new())
                    .SelectMany(h => h.Pillars ?? Enumerable.Empty<PeerCountryPillarHistoryReportDto>())
                    .GroupBy(p => p.PillarID)
                    .Select(g => g.OrderBy(p => p.DisplayOrder).First())
                    .OrderBy(p => p.DisplayOrder).Take(23).ToList();

                if (pillars.Any())
                {
                    var pillarTrendPng = RenderPng(
                        (c, s) => PaintPillarLineChart(c, s, allYears, main.CountryHistory ?? new(), pillars),
                        700, 200);
                    body.AppendChild(CreateFullWidthImage(mainPart, pillarTrendPng, 200));
                    body.AppendChild(Gap(100));
                    body.AppendChild(CreatePillarHeatmapTable(allYears, main.CountryHistory ?? new(), pillars));
                }
            }
        }



        // ════════════════════════════════════════════════════════════════════════
        //  PEER COMPARISON SECTIONS  –  mirrors PDF layout exactly
        // ════════════════════════════════════════════════════════════════════════

        // ════════════════════════════════════════════════════════════════════════
        //  PEER COMPARISON SECTIONS  –  mirrors PDF layout exactly
        // ════════════════════════════════════════════════════════════════════════

        private void AddPeerComparisonSections(
             Body body, MainDocumentPart mainPart,
             List<PeerCountryHistoryReportDto> activeCountries,
             AiCountrySummeryDto countryDetails, UserRole userRole)
        {
            if (!activeCountries.Any()) return;

            var main = FindMainCountry(activeCountries, countryDetails);
            var peers = activeCountries.Where(p => !IsSameCountry(p.CountryName, countryDetails.CountryName)).ToList();
            var all = BuildAllCountries(main, peers);

            // ── 5.1  Population-Based ────────────────────────────────────────────
            AppendCountryHeader(mainPart, countryDetails, "Population-Based Peer Comparison");

            var popSorted = all
                .Where(c => c.Population.HasValue)
                .OrderByDescending(c => c.Population)
                .ToList();

            if (popSorted.Any())
            {
                body.AppendChild(CreateInsightBand(
                    $"{popSorted.Count} countries compared  |  " +
                    $"Largest: {popSorted.First().CountryName} ({FormatPop(popSorted.First().Population)})  |  " +
                    $"Smallest: {popSorted.Last().CountryName} ({FormatPop(popSorted.Last().Population)})"));

                body.AppendChild(SectionHeading("Population Size by Country", DarkBlue));
                int popH = Math.Max(popSorted.Count * 40, 80);
                var popPng = RenderPng(
                    (c, s) => PdfGeneratorService.DrawPopulationBarsCanvas(c, s, popSorted, countryDetails),
                    700, popH);
                body.AppendChild(CreateFullWidthImage(mainPart, popPng, popH));
                body.AppendChild(Gap(80));
                body.AppendChild(CreateCountryLegendTable(popSorted, countryDetails));
                body.AppendChild(Gap(120));

                body.AppendChild(SectionHeading("Score vs Population  (each dot = one country)", DarkBlue));
                int scatterH = Math.Max(popSorted.Count * 30, 160);
                var scatterPng = RenderPng(
                    (c, s) => PdfGeneratorService.DrawScatterPlotCanvas(
                        c, s, popSorted, countryDetails,
                        country => (float)(country.Population ?? 0),
                        country => PdfGeneratorService.GetLatestScoreOrZeroForDocx(country),
                        "Population", "Score"),
                    700, scatterH);
                body.AppendChild(CreateFullWidthImage(mainPart, scatterPng, scatterH));
            }
            body.AppendChild(PageBreak());

            // ── 5.2  Regional ────────────────────────────────────────────────────
            AppendCountryHeader(mainPart, countryDetails, "Regional Peer Group Comparison");
            var regionPng = RenderPng((c, s) => PaintRegionalBars(c, s, all), 700, 220);
            body.AppendChild(CreateFullWidthImage(mainPart, regionPng, 220));
            body.AppendChild(PageBreak());

            // ── 5.3  Income-Level ────────────────────────────────────────────────
            AppendCountryHeader(mainPart, countryDetails, "Income-Level Peer Comparison");

            // ══════════════════════════════════════════════════════════════════
            // IncomePeerPage — DOCX (updated with PPP section)
            // ══════════════════════════════════════════════════════════════════

            var withIncome = all.Where(p => p.Income.HasValue).OrderBy(p => p.Income).ToList();
            if (withIncome.Any())
            {
                body.AppendChild(CreateInsightBand(
                    $"Income quartile analysis  |  {withIncome.Count} countries  |  " +
                    $"Range: {withIncome.Min(p => p.Income):C0} – {withIncome.Max(p => p.Income):C0}"));

                // ── Quartile bars ─────────────────────────────────────────────
                body.AppendChild(SectionHeading("Average Score by Income Quartile", DarkBlue));
                var quartilePng = RenderPng(
                    (c, s) => PdfGeneratorService.DrawIncomeQuartileBarsCanvas(c, s, all),
                    700, 145);
                body.AppendChild(CreateFullWidthImage(mainPart, quartilePng, 145));
                body.AppendChild(Gap(80));

                // ── Income vs Score scatter ───────────────────────────────────
                body.AppendChild(SectionHeading("Income vs Composite Score  (each dot = one country)", DarkBlue));
                var incScatterPng = RenderPng(
                    (c, s) => PdfGeneratorService.DrawScatterPlotCanvas(
                        c, s, withIncome, countryDetails,
                        country => (float)(country.Income ?? 0),
                        country => PdfGeneratorService.GetLatestScoreOrZeroForDocx(country),
                        "Income (USD)", "Score"),
                    700, 180);
                body.AppendChild(CreateFullWidthImage(mainPart, incScatterPng, 180));
                body.AppendChild(Gap(80));

                
                // ── Top performers by income group (PPP column added) ─────────
                body.AppendChild(SectionHeading("Top Performers by Income Group", DarkBlue));
                body.AppendChild(CreateIncomeGroupTable(all, countryDetails));
            }
            body.AppendChild(PageBreak());

            // ── 5.5  Relative Ranking ────────────────────────────────────────────
            AppendCountryHeader(mainPart, countryDetails, "Relative Ranking Among Peer Countries");
            AddRankingSection(body, mainPart, all, countryDetails);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  RANKING SECTION  –  hero banner + histogram + full table
        // ════════════════════════════════════════════════════════════════════════

        private void AddRankingSection(
            Body body, MainDocumentPart mainPart,
            List<PeerCountryHistoryReportDto> all,
            AiCountrySummeryDto countryDetails)
        {
            var ranked = all
                .Select(c => (Country: c, Score: GetLatestScoreOrZero(c)))
                .OrderByDescending(x => x.Score)
                .ToList();

            int mainRank = ranked.FindIndex(r => IsSameCountry(r.Country.CountryName, countryDetails.CountryName)) + 1;
            float mainScore = mainRank > 0 ? ranked[mainRank - 1].Score : 0f;
            float pctile = mainRank > 0 ? (1f - (float)mainRank / ranked.Count) * 100f : 0f;

            // Hero banner
            body.AppendChild(CreateHeroBanner(countryDetails, mainRank, ranked.Count, mainScore, pctile));
            body.AppendChild(Gap(120));

            // Score distribution histogram
            body.AppendChild(SectionHeading("Score Distribution Among All Countries", DarkBlue));
            var histPng = RenderPng(
                (c, s) => PdfGeneratorService.DrawHistogramCanvas(
                    c, s, ranked.Select(r => r.Score).ToList(), mainScore, 10),
                700, 160);
            body.AppendChild(CreateFullWidthImage(mainPart, histPng, 160));
            body.AppendChild(Gap(100));

            // Full ranking table
            body.AppendChild(SectionHeading("Full Country Ranking", DarkBlue));
            var rows = ranked.Select((r, i) => new[]
            {
        (i + 1).ToString(),
        r.Country.CountryName,
        r.Country.Continent    ?? "—",
        r.Country.Region     ?? "—",
        FormatPop(r.Country.Population),
        $"{r.Score:F1}"
    }).ToArray();

            body.AppendChild(CreateStyledTable(
                new[] { "#", "Country", "Continent", "Region", "Pop.", "Score" },
                new[] { 360, 2000, 1300, 1400, 1000, 900 },
                rows,
                highlightRow: i => IsSameCountry(ranked[i].Country.CountryName, countryDetails.CountryName)));
            body.AppendChild(PageBreak());
        }

        // ════════════════════════════════════════════════════════════════════════
        //  NEW ELEMENT BUILDERS
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Green insight band matching the PDF DrawInsightBand strip.</summary>
        private static Paragraph CreateInsightBand(string text) =>
            new(new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.SuccessGreenBgHex },
                    new SpacingBetweenLines { Before = "60", After = "80" }),
                new Run(
                    new RunProperties(
                        new Color { Val = ReportThemeColors.PdfDarkGreenHex },
                        new FontSize { Val = "17" },
                        new RunFonts { Ascii = "Arial" }),
                    new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        /// <summary>Bold section heading (matches PDF FontSize 11 Bold).</summary>
        private static Paragraph SectionHeading(string text, string hexColor) =>
            new(new ParagraphProperties(new SpacingBetweenLines { Before = "80", After = "60" }),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new Color { Val = hexColor.TrimStart('#') },
                        new FontSize { Val = "22" },
                        new RunFonts { Ascii = "Arial" }),
                    new Text(text)));

        /// <summary>
        /// Dark-green hero banner: rank left, score right — mirrors the PDF RelativeRankingPage banner.
        /// </summary>
        private static Table CreateHeroBanner(
            AiCountrySummeryDto countryDetails,
            int rank, int total, float score, float pctile)
        {
            var noBorder = new EnumValue<BorderValues>(BorderValues.None);
            TableCellBorders NoBorders() => new(
                new TopBorder { Val = noBorder }, new BottomBorder { Val = noBorder },
                new LeftBorder { Val = noBorder }, new RightBorder { Val = noBorder },
                new InsideHorizontalBorder { Val = noBorder }, new InsideVerticalBorder { Val = noBorder });

            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

            var row = new TableRow();

            // Left cell – rank + city line
            int leftW = ContentDxa - 1900;
            row.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = leftW.ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex },
                    NoBorders()),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "60", After = "20" }),
                    new Run(
                        new RunProperties(
                            new Bold(), new Color { Val = ReportThemeColors.ChartGoldHex },
                            new FontSize { Val = "64" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"#{rank} of {total}"))),
                new Paragraph(
                    new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "80" }),
                    new Run(
                        new RunProperties(
                            new Color { Val = ReportThemeColors.TrackGreenHex },
                            new FontSize { Val = "22" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"{countryDetails.CountryName}  ·  {countryDetails.Continent}")))));

            // Right cell – score + percentile
            row.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = "1900", Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex },
                    NoBorders()),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "60", After = "20" }),
                    new Run(
                        new RunProperties(
                            new Color { Val = ReportThemeColors.GrayA5A8ADHex },
                            new FontSize { Val = "18" }, new RunFonts { Ascii = "Arial" }),
                        new Text("Score"))),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "20" }),
                    new Run(
                        new RunProperties(
                            new Bold(), new Color { Val = ReportThemeColors.WhiteHex },
                            new FontSize { Val = "56" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"{score:F1}"))),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right },
                        new SpacingBetweenLines { Before = "0", After = "80" }),
                    new Run(
                        new RunProperties(
                            new Color { Val = ReportThemeColors.ChartTealHex },
                            new FontSize { Val = "18" }, new RunFonts { Ascii = "Arial" }),
                        new Text($"Top {100 - pctile:F0}% of peers")))));

            table.AppendChild(row);
            return table;
        }

        /// <summary>Income group table matching PDF IncomePeerPage top-performers table.</summary>
        private static Table CreateIncomeGroupTable(
            List<PeerCountryHistoryReportDto> all,
            AiCountrySummeryDto countryDetails)
        {
            string[] categoryOrder = { "Low Income", "Lower-Middle Income", "Upper-Middle Income", "High Income" };

            var segments = all
                .GroupBy(x => PdfGeneratorService.GetIncomeCategory(x.Income ?? 0))
                .ToDictionary(g => g.Key, g => g.ToList());

            var orderedCountries = new List<PeerCountryHistoryReportDto>();
            foreach (var label in categoryOrder)
                if (segments.TryGetValue(label, out var countries))
                    orderedCountries.AddRange(countries.OrderByDescending(c => GetLatestScoreOrZero(c)));

            var rows = orderedCountries.Select(country =>
            {
                float sc = GetLatestScoreOrZero(country);

                return new[]
                {
                    country.CountryName,
                    country.Continent ?? "—",
                    sc < 0 ? "—" : $"{sc:F1}",
                    PdfGeneratorService.GetIncomeCategory(country.Income ?? 0),
                    FormatPop(country.Income)         // ← NEW column
                };
            }).ToArray();

            return CreateStyledTableWithCellColors(
                headers: new[] { "Country", "Continent", "Score", "Income Group", "Income" },
                widths: new[] { 1800, 1000, 700, 2000, 1200 },
                rows: rows,
                highlightRow: i => IsSameCountry(orderedCountries[i].CountryName, countryDetails.CountryName)
                );
        }

        private static Table CreateCountryLegendTable(
            List<PeerCountryHistoryReportDto> allCountries, AiCountrySummeryDto countryDetails)
        {
            string[] palette = { ReportThemeColors.ChartGoldHex, ReportThemeColors.ChartTealHex, ReportThemeColors.ChartBlueHex, ReportThemeColors.ChartOrangeHex, ReportThemeColors.ChartPurpleHex, ReportThemeColors.ChartRedHex };
            var rows = new List<string[]>();
            for (int i = 0; i < allCountries.Count; i++)
            {
                bool isMain = IsSameCountry(allCountries[i].CountryName, countryDetails.CountryName);
                rows.Add(new[] { isMain ? "★" : "•", allCountries[i].CountryName, allCountries[i].Country ?? "—" });
            }
            return CreateStyledTable(
                new[] { "", "Country", "Country" },
                new[] { 300, 4000, 2000 },
                rows.ToArray());
        }

        // ── General styled data table ─────────────────────────────────────────

        private static Table CreateStyledTable(
            string[] headers, int[] colWidthsDxa, string[][] rows,
            Func<int, bool>? highlightRow = null)
        {
            var borderSingle = new EnumValue<BorderValues>(BorderValues.Single);
            TableCellBorders DataBorders() => new TableCellBorders(
                new BottomBorder { Val = borderSingle, Size = 4, Color = ReportThemeColors.GrayE0E0E0Hex });

            var table = new Table(new TableProperties(
                new TableWidth { Width = colWidthsDxa.Sum().ToString(), Type = TableWidthUnitValues.Dxa }));

            // Header row
            var hRow = new TableRow();
            for (int c = 0; c < headers.Length; c++)
            {
                hRow.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = colWidthsDxa[c].ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex }),
                    new Paragraph(
                        new Run(new RunProperties(
                            new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                            new Text(headers[c])))));
            }
            table.AppendChild(hRow);

            // Data rows
            for (int r = 0; r < rows.Length; r++)
            {
                bool highlight = highlightRow?.Invoke(r) ?? false;
                string rowBg = highlight ? ReportThemeColors.HighlightBgHex : (r % 2 == 0 ? ReportThemeColors.WhiteHex : ReportThemeColors.RowPaleHex);
                var dRow = new TableRow();
                for (int c = 0; c < rows[r].Length && c < headers.Length; c++)
                {
                    dRow.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = colWidthsDxa[c].ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = rowBg },
                            DataBorders()),
                        new Paragraph(
                            new Run(new RunProperties(
                                new Color { Val = highlight ? ReportThemeColors.PdfDarkGreenHex : ReportThemeColors.TextBodyHex },
                                new FontSize { Val = "16" }),
                                new Text(rows[r][c])))));
                }
                table.AppendChild(dRow);
            }
            return table;
        }

        // ── YoY performance table ─────────────────────────────────────────────

        private static Table CreateYoYTable(
            List<int> years,
            List<PeerCountryYearHistoryDto> mainHistory,
            List<(int Year, float Avg)> peerAvg)
        {
            int yearW = (ContentDxa - 1300) / Math.Max(years.Count, 1);
            var table = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

            TableCell HdrCell(string txt, bool first = false) =>
                new(new TableCellProperties(
                    new TableCellWidth { Width = (first ? 1300 : yearW).ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex }),
                    new Paragraph(new Run(
                        new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                        new Text(txt))));

            var hRow = new TableRow();
            hRow.AppendChild(HdrCell("Metric", first: true));
            foreach (var yr in years) hRow.AppendChild(HdrCell(yr.ToString()));
            table.AppendChild(hRow);

            void DataRow(string label, Func<int, string> valFn, Func<int, string> colorFn, string bg)
            {
                var row = new TableRow();
                row.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = "1300", Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                    new Paragraph(new Run(
                        new RunProperties(new Color { Val = ReportThemeColors.TextBodyHex }, new FontSize { Val = "16" }),
                        new Text(label)))));
                foreach (var yr in years)
                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = yearW.ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                            new Run(new RunProperties(
                                new Bold(), new Color { Val = colorFn(yr) }, new FontSize { Val = "16" }),
                                new Text(valFn(yr))))));
                table.AppendChild(row);
            }

            DataRow("Score",
                yr => { float s = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0); return $"{s:F1}"; },
                yr => { float s = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0); return GetBarColor(s).TrimStart('#'); },
                ReportThemeColors.PageBgHex);

            DataRow("YoY Δ",
                yr =>
                {
                    int idx = years.IndexOf(yr);
                    if (idx == 0) return "—";
                    float prev = (float)(mainHistory.FirstOrDefault(h => h.Year == years[idx - 1])?.ScoreProgress ?? 0);
                    float curr = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float d = curr - prev; return $"{(d >= 0 ? "+" : "")}{d:F1}";
                },
                yr =>
                {
                    int idx = years.IndexOf(yr);
                    if (idx == 0) return ReportThemeColors.TextFaintHex;
                    float prev = (float)(mainHistory.FirstOrDefault(h => h.Year == years[idx - 1])?.ScoreProgress ?? 0);
                    float curr = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    return curr >= prev ? ReportThemeColors.PdfMediumGreenHex : ReportThemeColors.ChartRedHex;
                },
                ReportThemeColors.WhiteHex);

            DataRow("vs Peers",
                yr =>
                {
                    float mine = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float peer = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                    float d = mine - peer; return $"{(d >= 0 ? "+" : "")}{d:F1}";
                },
                yr =>
                {
                    float mine = (float)(mainHistory.FirstOrDefault(h => h.Year == yr)?.ScoreProgress ?? 0);
                    float peer = peerAvg.FirstOrDefault(p => p.Year == yr).Avg;
                    return mine >= peer ? ReportThemeColors.PdfMediumGreenHex : ReportThemeColors.ChartRedHex;
                },
                ReportThemeColors.PageBgHex);

            return table;
        }

        // ── Pillar heatmap table ──────────────────────────────────────────────

        private static Table CreatePillarHeatmapTable(
            List<int> allYears,
            List<PeerCountryYearHistoryDto> history,
            List<PeerCountryPillarHistoryReportDto> pillars)
        {
            int yearW   = Math.Max(400, (ContentDxa - 1600) / Math.Max(allYears.Count, 1));
            var table   = new Table(new TableProperties(
                new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

            // Header
            var hRow = new TableRow();
            hRow.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = "1600", Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex }),
                new Paragraph(new Run(
                    new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "16" }),
                    new Text("Pillar")))));
            foreach (var yr in allYears)
                hRow.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = yearW.ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex }),
                    new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(new RunProperties(new Bold(), new Color { Val = White }, new FontSize { Val = "14" }),
                            new Text(yr.ToString())))));
            table.AppendChild(hRow);

            // Data rows
            foreach (var (pillar, pi) in pillars.Select((p, i) => (p, i)))
            {
                string rowBg = pi % 2 == 0 ? ReportThemeColors.PageBgHex : ReportThemeColors.WhiteHex;
                var row = new TableRow();
                row.AppendChild(new TableCell(
                    new TableCellProperties(
                        new TableCellWidth { Width = "1600", Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = rowBg }),
                    new Paragraph(new Run(
                        new RunProperties(new Bold(), new Color { Val = ReportThemeColors.PdfDarkGreenHex }, new FontSize { Val = "14" }),
                        new Text(pillar.PillarName)))));

                foreach (var yr in allYears)
                {
                    var h  = history.FirstOrDefault(h2 => h2.Year == yr);
                    var ps = h?.Pillars?.FirstOrDefault(p2 => p2.PillarID == pillar.PillarID);
                    bool hasData = ps != null;
                    float score  = hasData ? (float)ps!.ScoreProgress : -1f;
                    string cellBg = !hasData ? ReportThemeColors.SubHeaderBgHex
                        : InterpolateColor(ReportThemeColors.WhiteHex, ReportThemeColors.PdfDarkGreenHex, score / 100f).TrimStart('#');

                    row.AppendChild(new TableCell(
                        new TableCellProperties(
                            new TableCellWidth { Width = yearW.ToString(), Type = TableWidthUnitValues.Dxa },
                            new Shading { Val = ShadingPatternValues.Clear, Fill = cellBg }),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                            new Run(new RunProperties(
                                new Color { Val = score >= 50 ? White : ReportThemeColors.TextBodyHex },
                                new FontSize { Val = "14" }),
                                new Text(!hasData ? "—" : $"{score:F1}")))));
                }
                table.AppendChild(row);
            }
            return table;
        }

        // ════════════════════════════════════════════════════════════════════
        //  IMAGE EMBEDDING HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Embeds a PNG byte-array as a full-width image in the document.
        /// <paramref name="naturalHeightPx"/> is used to compute the aspect ratio.
        /// </summary>
        private Paragraph CreateFullWidthImage(
            MainDocumentPart mainPart, byte[] pngBytes, int naturalHeightPx,
            int naturalWidthPx = 700)
        {
            long widthEmu  = ContentWidthEmu;
            long heightEmu = ContentWidthEmu * naturalHeightPx / naturalWidthPx;
            return EmbedImage(mainPart, pngBytes, widthEmu, heightEmu);
        }

        /// <summary>Creates a two-cell table, each half containing one image.</summary>
        private Table CreateSideBySideImages(
            MainDocumentPart mainPart,
            byte[] leftPng, byte[] rightPng,
            int naturalHeightPx)
        {
            long hw     = HalfWidthEmu;
            long hh     = hw * naturalHeightPx / 320; // approx aspect

            var noBorder = new EnumValue<BorderValues>(BorderValues.None);
            TableCell ImgCell(byte[] png) => new(
                new TableCellProperties(
                    new TableCellWidth { Width = (ContentDxa / 2).ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableCellBorders(
                        new TopBorder    { Val = noBorder }, new BottomBorder { Val = noBorder },
                        new LeftBorder   { Val = noBorder }, new RightBorder  { Val = noBorder })),
                EmbedImage(mainPart, png, hw, hh));

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new TableRow(ImgCell(leftPng), ImgCell(rightPng)));
        }

        private Paragraph EmbedImage(
            MainDocumentPart mainPart, byte[] pngBytes, long widthEmu, long heightEmu)
        {
            var imgPart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(pngBytes))
                imgPart.FeedData(ms);

            string relId = mainPart.GetIdOfPart(imgPart);
            uint id = _imgId++;

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = id, Name = $"img{id}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"img{id}.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                        { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

            return new Paragraph(new Run(drawing));
        }

        // ════════════════════════════════════════════════════════════════════
        //  SKIA CHART RENDERERS  →  PNG bytes
        //  All Paint* methods below replicate the logic from PdfGeneratorService
        //  but operate on a SkiaSharp surface instead of a QuestPDF canvas.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Renders any SkiaSharp paint action to a PNG byte array.</summary>
        private static byte[] RenderPng(
            Action<SKCanvas, QPDF.Size> paintAction,
            int width, int height)
        {
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            paintAction(canvas, new QPDF.Size(width, height));
            canvas.Flush();
            using var snap    = surface.Snapshot();
            using var encoded = snap.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }

        // Forward declarations — these call the SAME static paint methods used by PdfGeneratorService.
        // If PdfGeneratorService makes them internal/protected, call them directly; otherwise just
        // redeclare the tiny wrappers below.

        private static void PaintDonut(SKCanvas c, QPDF.Size s, float score) =>
            PdfGeneratorService.PaintDonutPublic(c, s, score);

        private static void PaintSpiderChart(SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.PaintSpiderChartPublic(c, s, pillars);

        private static void PaintKpiSparkline(SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis) =>
            PdfGeneratorService.PaintKpiSparklinePublic(c, s, kpis);

        private static void PaintKpiBarChart(
            SKCanvas c, QPDF.Size s, List<KpiChartItem> kpis, int offset) =>
            PdfGeneratorService.DrawKpiBarChartCanvas(c, s, kpis, offset);

        private static void PaintPillarRadialChart(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.DrawPillarsRadialChartCanvas(c, s, pillars);

        private static void PaintPillarHorizontalBars(
            SKCanvas c, QPDF.Size s, List<PillarChartItem> pillars) =>
            PdfGeneratorService.DrawPillarHorizontalBarsCanvas(c, s, pillars);
        private static void PaintRegionalBars(
            SKCanvas c, QPDF.Size s, List<PeerCountryHistoryReportDto> all) =>
            PdfGeneratorService.DrawRegionalBarsCanvas(c, s, all);

        private static void PaintMultiLineTrend(
            SKCanvas c, QPDF.Size s,
            List<int> years, List<PeerCountryHistoryReportDto> peers,
            PeerCountryHistoryReportDto? main, AiCountrySummeryDto details,
            List<(int Year, float Avg, bool HasData)> avg) =>
            PdfGeneratorService.DrawMultiLineTrendChartCanvas(c, s, years, peers, main, details, avg);

        private static void PaintPillarLineChart(
            SKCanvas c, QPDF.Size s,
            List<int> years, List<PeerCountryYearHistoryDto> history,
            List<PeerCountryPillarHistoryReportDto> pillars) =>
            PdfGeneratorService.DrawPillarLineChartCanvas(c, s, years, history, pillars);

        // ════════════════════════════════════════════════════════════════════
        //  PARAGRAPH / ELEMENT UTILITIES
        // ════════════════════════════════════════════════════════════════════

        private static Paragraph NormalParagraph(
            string text, string hexColor, int halfPtSize,
            bool italic = false, string bg = ReportThemeColors.WhiteHex)
        {
            var rPr = new RunProperties(
                new Color { Val = hexColor },
                new FontSize { Val = halfPtSize.ToString() },
                new RunFonts { Ascii = "Arial" });
            if (italic) rPr.AppendChild(new Italic());

            return new Paragraph(
                new ParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = bg }),
                new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        private static Paragraph BoldParagraph(string text, string hexColor, int halfPtSize) =>
            new(new Run(
                new RunProperties(
                    new Bold(), new Color { Val = hexColor },
                    new FontSize { Val = halfPtSize.ToString() },
                    new RunFonts { Ascii = "Arial" }),
                new Text(text)));

        /// <summary>Empty paragraph with controlled spacing (spacing in twentieths of a point).</summary>
        private static Paragraph Gap(int spacingAfter) =>
            new(new ParagraphProperties(new SpacingBetweenLines { After = spacingAfter.ToString() }));

        private static Paragraph PageBreak() =>
            new(new Run(new Break { Type = BreakValues.Page }));


        private static TableCell SpacerCell(int widthDxa, uint heightTwips) =>
            new(new TableCellProperties(
                    new TableCellWidth { Width = widthDxa.ToString(), Type = TableWidthUnitValues.Dxa }),
                new Paragraph(new ParagraphProperties(
                    new SpacingBetweenLines { After = heightTwips.ToString() })));

        // ════════════════════════════════════════════════════════════════════
        //  COLOUR / FORMAT UTILITIES  (mirrors PdfGeneratorService statics)
        // ════════════════════════════════════════════════════════════════════

        static string GetBarColor(float value)
        {
            if (value >= 80) return ReportThemeColors.DangerRed;
            else if (value >= 60) return ReportThemeColors.WarningOrange;
            else if (value >= 40) return ReportThemeColors.WarningAmber;
            else if (value >= 20) return ReportThemeColors.SuccessGreenMid;
            return ReportThemeColors.SuccessGreen;
        }

        private static string Shorten(string text, int max) =>
            string.IsNullOrWhiteSpace(text) ? "" :
            text.Length <= max ? text : text[..max] + "…";

        private static string TruncateText(string text, int maxLength) =>
            string.IsNullOrEmpty(text) || text.Length <= maxLength
                ? text : text[..maxLength] + "...";

        private static string InterpolateColor(string from, string to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            var c1 = SKColor.Parse(from);
            var c2 = SKColor.Parse(to);
            byte r = (byte)(c1.Red   + (c2.Red   - c1.Red)   * t);
            byte g = (byte)(c1.Green + (c2.Green - c1.Green) * t);
            byte b = (byte)(c1.Blue  + (c2.Blue  - c1.Blue)  * t);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static float GetLatestScoreOrZero(PeerCountryHistoryReportDto country) =>
            country.CountryHistory?.OrderByDescending(h => h.Year).FirstOrDefault() is { } last
                ? (float)last.ScoreProgress : -1f;

        private static PeerCountryHistoryReportDto? FindMainCountry(
            List<PeerCountryHistoryReportDto> all, AiCountrySummeryDto country) =>
            all.FirstOrDefault(p => IsSameCountry(p.CountryName, country.CountryName));

        private static bool IsSameCountry(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static List<PeerCountryHistoryReportDto> BuildAllCountries(
            PeerCountryHistoryReportDto? main, List<PeerCountryHistoryReportDto> peers)
        {
            var list = new List<PeerCountryHistoryReportDto>();
            if (main != null) list.Add(main);
            list.AddRange(peers);
            return list;
        }

        private static string FormatPop(decimal? value)
        {
            if (!value.HasValue || value <= 0) return "N/A";
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000M:F1}B";
            if (value >= 1_000_000)     return $"{value / 1_000_000M:F1}M";
            if (value >= 1_000)         return $"{value / 1_000M:F0}K";
            return value.Value.ToString("N0");
        }
        // ══════════════════════════════════════════════════════════════════
        // NEW HELPERS — legend, italic note, divider, footnote
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Section divider with bottom border line — for PPP heading</summary>
        private static Paragraph CreateSectionDivider(string text, string hexColor)
        {
            var para = new Paragraph();
            var pPr = new ParagraphProperties();
            //pPr.AppendChild(new Paragraph(new BottomBorder
            //{
            //    Val = BorderValues.Single,
            //    Size = 6,
            //    Color = hexColor.Replace("#", "")
            //}));
            pPr.AppendChild(new SpacingBetweenLines { Before = "120", After = "60" });
            para.AppendChild(pPr);

            var run = new Run();
            run.AppendChild(new RunProperties
            {
                Bold = new Bold(),
                FontSize = new FontSize { Val = "22" },  // 11pt
                Color = new Color { Val = hexColor.Replace("#", "") }
            });
            run.AppendChild(new Text(text));
            para.AppendChild(run);
            return para;
        }

        /// <summary>Small italic grey explanatory note</summary>
        private static Paragraph CreateItalicNote(string text)
        {
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(
                new SpacingBetweenLines { Before = "40", After = "40" }));

            var run = new Run();
            run.AppendChild(new RunProperties
            {
                Italic = new Italic(),
                FontSize = new FontSize { Val = "16" },   // 8pt
                Color = new Color { Val = ReportThemeColors.TextMidHex }
            });
            run.AppendChild(new Text(text));
            para.AppendChild(run);
            return para;
        }

        /// <summary>Very small italic footnote (7pt)</summary>
        private static Paragraph CreateFootnote(string text)
        {
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(
                new SpacingBetweenLines { Before = "20", After = "20" }));

            var run = new Run();
            run.AppendChild(new RunProperties
            {
                Italic = new Italic(),
                FontSize = new FontSize { Val = "14" },   // 7pt
                Color = new Color { Val = ReportThemeColors.TextPaleHex }
            });
            run.AppendChild(new Text(text));
            para.AppendChild(run);
            return para;
        }

        /// <summary>PPP signal colour legend row</summary>
        private static Paragraph CreatePppLegend()
        {
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(
                new SpacingBetweenLines { Before = "40", After = "20" }));

            var signals = new[]
            {
        ("Strong PPP Advantage ≥2×",  ReportThemeColors.SuccessGreenHex),
        ("Moderate Advantage ≥1.3×",  ReportThemeColors.ModerateBlueHex),
        ("Near-Parity 0.9–1.3×",      ReportThemeColors.TextMidHex),
        ("Cost Pressure 0.7–0.9×",    ReportThemeColors.CostOrangeHex),
        ("High Cost Penalty <0.7×",   ReportThemeColors.PenaltyRedHex),
    };

            // Label prefix
            var prefix = new Run();
            prefix.AppendChild(new RunProperties
            {
                Bold = new Bold(),
                FontSize = new FontSize { Val = "14" },
                Color = new Color { Val = ReportThemeColors.TextSoftHex }
            });
            prefix.AppendChild(new Text("PPP Signals:  ") { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(prefix);

            foreach (var (label, color) in signals)
            {
                // Coloured bullet block
                var bullet = new Run();
                bullet.AppendChild(new RunProperties
                {
                    Bold = new Bold(),
                    FontSize = new FontSize { Val = "14" },
                    Color = new Color { Val = color }
                });
                bullet.AppendChild(new Text("■ ") { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(bullet);

                // Label text
                var lbl = new Run();
                lbl.AppendChild(new RunProperties
                {
                    FontSize = new FontSize { Val = "14" },
                    Color = new Color { Val = ReportThemeColors.TextMidHex }
                });
                lbl.AppendChild(new Text(label + "   ") { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(lbl);
            }

            return para;
        }
        private static Table CreateRankTable(CountryPillarRankingResultDto? data)
        {
            var border = new TableCellBorders(
                new TopBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 });

            TableCell Cell(string text, bool header, bool right, bool shade)
            {
                var props = new TableCellProperties(border.CloneNode(true));
                if (header || shade)
                    props.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = header ? ReportThemeColors.SurfaceWarmHex : ReportThemeColors.SurfaceWarmLightHex });

                var runProps = new RunProperties(new FontSize { Val = "22" }, new Color { Val = ReportThemeColors.TextSlateHex });
                if (header || right)
                    runProps.Append(new Bold());

                var paragraph = new Paragraph(
                    new ParagraphProperties(new Justification { Val = right ? JustificationValues.Right : JustificationValues.Left }),
                    new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

                return new TableCell(props, paragraph);
            }

            static string FormatRank(int rank, int total) =>
                rank > 0 && total > 0 ? $"{rank} / {total}" : "-";

            return new Table(
                new TableProperties(
                    new TableWidth { Width = ContentDxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableBorders(
                        new InsideHorizontalBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                        new TopBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Color = ReportThemeColors.BorderWarmHex, Size = 4 })),
                new TableRow(Cell("Ranking", true, false, false), Cell("Result", true, true, false)),
                new TableRow(Cell("Continent Rank", false, false, false), Cell(FormatRank(data?.GlobalPillarRank ?? 0, data?.TotalPillarsInAllCountries ?? 0), false, true, false)),
                new TableRow(Cell("Region Rank", false, false, true), Cell(FormatRank(data?.RegionPillarRank ?? 0, data?.TotalPillarInRegion ?? 0), false, true, true)),
                new TableRow(Cell("Country Level Rank", false, false, false), Cell(FormatRank(data?.CountryPillarRank ?? 0, data?.TotalPillars ?? 0), false, true, false)));
        }
        

        private static Table CreateStyledTableWithCellColors(
            string[] headers,
            int[] widths,
            string[][] rows,
            Func<int, bool> highlightRow,
            Func<int, int, string?> cellColor = null,
            Func<int, int, string?> cellFontColor = null)
        {
            var table = new Table();

            // Table properties
            var tblPr = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = ReportThemeColors.BorderD0D8D0Hex },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = ReportThemeColors.BorderD0D8D0Hex },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = ReportThemeColors.BorderD0D8D0Hex },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = ReportThemeColors.BorderD0D8D0Hex },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2, Color = ReportThemeColors.GrayE0E0E0Hex },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 2, Color = ReportThemeColors.GrayE0E0E0Hex }),
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });
            table.AppendChild(tblPr);

            // Column widths
            var tblGrid = new TableGrid();
            foreach (var w in widths)
                tblGrid.AppendChild(new GridColumn { Width = w.ToString() });
            table.AppendChild(tblGrid);

            // Header row
            var headerRow = new TableRow();
            headerRow.AppendChild(new TableRowProperties(
                new TableRowHeight { Val = 260, HeightType = HeightRuleValues.AtLeast }));

            for (int col = 0; col < headers.Length; col++)
            {
                var cell = new TableCell();
                cell.AppendChild(new TableCellProperties(
                    new TableCellWidth { Width = widths[col].ToString(), Type = TableWidthUnitValues.Dxa },
                    new Shading { Val = ShadingPatternValues.Clear, Fill = ReportThemeColors.PdfDarkGreenHex }));

                var p = new Paragraph();
                var pp = new ParagraphProperties();
                pp.AppendChild(new SpacingBetweenLines { Before = "0", After = "0" });
                p.AppendChild(pp);

                var run = new Run();
                run.AppendChild(new RunProperties
                {
                    Bold = new Bold(),
                    FontSize = new FontSize { Val = "16" },  // 8pt
                    Color = new Color { Val = ReportThemeColors.WhiteHex }
                });
                run.AppendChild(new Text(headers[col]));
                p.AppendChild(run);
                cell.AppendChild(p);
                headerRow.AppendChild(cell);
            }
            table.AppendChild(headerRow);

            // Data rows
            for (int rowIdx = 0; rowIdx < rows.Length; rowIdx++)
            {
                bool isHighlight = highlightRow(rowIdx);
                string defaultBg = isHighlight ? ReportThemeColors.HighlightBgHex : ReportThemeColors.WhiteHex;

                var tr = new TableRow();
                tr.AppendChild(new TableRowProperties(
                    new TableRowHeight { Val = 240, HeightType = HeightRuleValues.AtLeast }));

                for (int col = 0; col < rows[rowIdx].Length; col++)
                {
                    string? bg = cellColor?.Invoke(rowIdx, col) ?? defaultBg;
                    string? fontColor = cellFontColor?.Invoke(rowIdx, col);
                    bool isBold = (col == 2) || isHighlight && col == 0; // score col bold

                    var cell = new TableCell();
                    cell.AppendChild(new TableCellProperties(
                        new TableCellWidth { Width = widths[col].ToString(), Type = TableWidthUnitValues.Dxa },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = bg ?? defaultBg }));

                    var p = new Paragraph();
                    var pp = new ParagraphProperties();
                    pp.AppendChild(new SpacingBetweenLines { Before = "0", After = "0" });
                    p.AppendChild(pp);

                    var run = new Run();
                    var rPr = new RunProperties
                    {
                        FontSize = new FontSize { Val = "16" },  // 8pt
                        Color = new Color { Val = fontColor ?? (isHighlight && col == 0 ? ReportThemeColors.PdfDarkGreenHex : ReportThemeColors.TextBodyHex) }
                    };
                    if (isBold) rPr.AppendChild(new Bold());
                    run.AppendChild(rPr);
                    run.AppendChild(new Text(rows[rowIdx][col]));
                    p.AppendChild(run);
                    cell.AppendChild(p);
                    tr.AppendChild(cell);
                }

                table.AppendChild(tr);
            }

            return table;
        }
    }
}
