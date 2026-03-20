using PeaceEnablers.Backgroundjob;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.PillarDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PeaceEnablers.Services
{
    public class PillarService : IPillarService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;
        private readonly Download _download;
        public PillarService(ApplicationDbContext context, IAppLogger appLogger, Download download)
        {
            _context = context;
            _appLogger = appLogger;
            _download = download;
        }

        public async Task<List<Pillar>> GetAllAsync()
        {
            try
            {
                return await _context.Pillars.OrderBy(p => p.DisplayOrder).ToListAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetAllAsync", ex);
                return new List<Pillar>();
            }

        }

        public async Task<Pillar> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Pillars.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in GetByIdAsync", ex);
                return new Pillar();
            }

        }

        public async Task<Pillar> AddAsync(Pillar pillar)
        {
            try
            {
                _context.Pillars.Add(pillar);
                await _context.SaveChangesAsync();
                return pillar;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in AddAsync", ex);
                return new Pillar();
            }

        }

        public async Task<Pillar> UpdateAsync(int id, UpdatePillarDto pillar)
        {
            try
            {
                var existing = await _context.Pillars.FindAsync(id);
                if (existing == null) return null;
                existing.PillarName = pillar.PillarName ?? "";
                existing.Description = pillar.Description ?? "";
                existing.DisplayOrder = pillar.DisplayOrder;

                if (pillar.ImageFile != null)
                {
                    if (!string.IsNullOrEmpty(existing.ImagePath))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existing.ImagePath);
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/assets/pillars");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);
                    var fileName = Guid.NewGuid() + Path.GetExtension(pillar.ImageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await pillar.ImageFile.CopyToAsync(stream);
                    existing.ImagePath = $"assets/pillars/{fileName}";
                }

                if (existing.Weight != pillar.Weight || existing.Reliability != pillar.Reliability)
                {
                    existing.Weight = pillar.Weight;
                    existing.Reliability = pillar.Reliability;
                    _download.InsertAnalyticalLayerResults();
                }
                await _context.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occured", ex);
                return new Pillar();
            }
        }


        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var pillar = await _context.Pillars.FindAsync(id);
                if (pillar == null) return false;
                _context.Pillars.Remove(pillar);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure", ex);
                return false;
            }

        }

        public async Task<ResultResponseDto<List<PillarWithQuestionsDto>>> GetPillarsWithQuestions(GetCityPillarHistoryRequestDto request)
        {
            try
            {
                // 1. Validate user
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserID == request.UserID);

                if (user == null)
                    return ResultResponseDto<List<PillarWithQuestionsDto>>.Failure(new[] { "Invalid user" });

                // 2. Filter user-city mappings based on role
                Expression<Func<UserCityMapping, bool>> predicate = user.Role switch
                {
                    UserRole.Analyst => x => !x.IsDeleted && x.CityID == request.CityID &&
                                             (x.AssignedByUserId == request.UserID || x.UserID == request.UserID),
                    UserRole.Evaluator => x => !x.IsDeleted && x.CityID == request.CityID && x.UserID == request.UserID,
                    _ => x => !x.IsDeleted && x.CityID == request.CityID
                };

                var mappingIds = await _context.UserCityMappings
                    .Where(predicate)
                    .Select(x => x.UserCityMappingID)
                    .ToListAsync();

                // 3. Get assessments with pillar + responses
                var assessmentsQuery = _context.Assessments.Include(a => a.UserCityMapping).Include(a => a.PillarAssessments).ThenInclude(pa => pa.Responses)
                .Where(a => mappingIds.Contains(a.UserCityMappingID) && a.IsActive && a.UpdatedAt.Year == request.UpdatedAt.Year);

                if (request.ExportType?.ToLower() != "pdf")
                {
                    // Apply AssessmentPhase filter only if NOT PDF
                    assessmentsQuery = assessmentsQuery.Where(a =>
                        a.AssessmentPhase == AssessmentPhase.Completed
                        || a.AssessmentPhase == AssessmentPhase.EditRejected
                        || a.AssessmentPhase == AssessmentPhase.EditRequested);
                }

                var assessments = await assessmentsQuery.AsNoTracking().ToListAsync();

                // 4. Get pillar list with questions + options
                var pillars = await _context.Pillars
                    .Include(p => p.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .Where(p => !request.PillarID.HasValue || p.PillarID == request.PillarID)
                    .OrderBy(p => p.DisplayOrder)
                    .AsNoTracking()
                    .ToListAsync();

                // 5. Preload users dictionary
                var userIds = assessments.Select(a => a.UserCityMapping.UserID).Distinct().ToList();
                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);
                var aiAssessmentData = await _context.AIEstimatedQuestionScores
                    .Where(x => x.CityID == request.CityID && x.Year == request.UpdatedAt.Year).AsNoTracking().ToListAsync();
                // 6. Build response
                var result = pillars.Select(p => new PillarWithQuestionsDto
                {
                    PillarID = p.PillarID,
                    PillarName = p.PillarName,
                    DisplayOrder = p.DisplayOrder,
                    TotalQuestions = p.Questions.Count,
                    Questions = p.Questions
                        .OrderBy(q => q.DisplayOrder)
                        .Where(q => !q.IsDeleted)
                        .Select(q =>
                        {
                            var userAnswers = userIds.Select(uid =>
                            {
                                var paResponses = assessments
                                    .Where(a => a.UserCityMapping.UserID == uid)
                                    .SelectMany(a => a.PillarAssessments)
                                    .Where(pa => pa.PillarID == p.PillarID)
                                    .SelectMany(pa => pa.Responses)
                                    .ToList();

                                var response = paResponses.FirstOrDefault(r => r.QuestionID == q.QuestionID);
                                var option = q.QuestionOptions.FirstOrDefault(o => o.OptionID == response?.QuestionOptionID);

                                return new QuestionUserAnswerDto
                                {
                                    UserID = uid,
                                    FullName = usersDict.TryGetValue(uid, out var name) ? name : "",
                                    Score = (int?)response?.Score,
                                    Justification = response?.Justification ?? "",
                                    OptionText = option?.OptionText ?? ""
                                };
                            }).ToDictionary(x => x.UserID);


                            // ✅ ------------------ ADD AI RESPONSE ------------------

                            var aiData = aiAssessmentData
                                .FirstOrDefault(x => x.QuestionID == q.QuestionID && x.PillarID == p.PillarID);

                            if (aiData != null)
                            {
                                // Map AI score to option text (same like user)
                                var aiOption = q.QuestionOptions
                                    .FirstOrDefault(o => o.ScoreValue == aiData.AIScore);

                                userAnswers[-1] = new QuestionUserAnswerDto
                                {
                                    UserID = -1,
                                    FullName = "AI",

                                    // Score
                                    Score = aiData.AIScore != null ? (int?)Convert.ToInt32(aiData.AIScore) : null,

                                    // Justification
                                    Justification = (aiData.EvidenceSummary ?? "") +
                                                    (!string.IsNullOrEmpty(aiData.SourceDataExtract)
                                                        ? $" | {aiData.SourceDataExtract}"
                                                        : "") +
                                                    (!string.IsNullOrEmpty(aiData.SourceURL)
                                                        ? $" SourceURL : {aiData.SourceURL}"
                                                        : ""),

                                    // Option text from score
                                    OptionText = aiOption?.OptionText ?? ""
                                };
                            }
                            else
                            {
                                // ✅ Empty AI column if no data
                                userAnswers[-1] = new QuestionUserAnswerDto
                                {
                                    UserID = -1,
                                    FullName = "AI",
                                    Score = null,
                                    Justification = "",
                                    OptionText = ""
                                };
                            }

                            return new QuestionWithUserDto
                            {
                                QuestionID = q.QuestionID,
                                QuestionText = q.QuestionText,
                                DisplayOrder = q.DisplayOrder,
                                Users = userAnswers
                            };
                        }).ToList()
                }).ToList();
                var resu = result;
                return ResultResponseDto<List<PillarWithQuestionsDto>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPillarsWithQuestions", ex);
                return ResultResponseDto<List<PillarWithQuestionsDto>>.Failure(new[] { "There was an error, please try again later" });
            }
        }

        public async Task<Tuple<string, byte[]>> ExportPillarsHistoryByUserId(GetCityPillarHistoryRequestDto requestDto)
        {
            try
            {
                var response = await GetPillarsWithQuestions(requestDto);
                var city = await _context.Cities
                    .FirstOrDefaultAsync(x => x.CityID == requestDto.CityID);

                if (!response.Succeeded || response.Result == null)
                {
                    return new Tuple<string, byte[]>("", Array.Empty<byte>());
                }

                byte[] fileBytes;
                string fileName;

                if (requestDto.ExportType?.ToLower() == "pdf")
                {
                    // ✅ Use structured data directly (NO flattening)
                    fileBytes = GeneratePdf(response.Result, city, requestDto.UpdatedAt.Year);

                    fileName = $"ExportPillarsHistory_{requestDto.CityID}_{requestDto.PillarID}.pdf";
                }
                else
                {
                    // ✅ Excel (existing)
                    fileBytes = MakePillarSheet(response.Result, city);
                    fileName = $"ExportPillarsHistory_{requestDto.CityID}_{requestDto.PillarID}.xlsx";
                }

                return new Tuple<string, byte[]>(fileName, fileBytes);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error Occure in ExportPillarsHistoryByUserId", ex);
                return new Tuple<string, byte[]>("", Array.Empty<byte>());
            }
        }
        public byte[] GeneratePdf(List<PillarWithQuestionsDto> data, City city, int year)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);

                    page.Content().Column(col =>
                    {
                        int pillarIndex = 1;

                        foreach (var pillar in data)
                        {
                            // ================= HEADER =================
                            col.Item()
                                .Background("#1f4b3f")
                                .Padding(15)
                                .Row(row =>
                                {
                                    row.RelativeItem().Column(left =>
                                    {
                                        left.Item().Text($"{pillarIndex}. {pillar.PillarName}")
                                            .FontSize(18)
                                            .Bold()
                                            .FontColor("#ffffff");

                                        left.Item().Text($"{city?.CityName}, {city?.State}, USA | Data Year: {year}")
                                            .FontSize(10)
                                            .FontColor("#cfe7df");

                                        left.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy}")
                                            .FontSize(9)
                                            .FontColor("#cfe7df");
                                    });

                                    row.ConstantItem(70)
                                        .Height(50)
                                        .Background("#ffffff")
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Text("PEM")
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor("#1f4b3f");
                                });

                            col.Item().PaddingBottom(10);

                            int questionIndex = 1;

                            foreach (var question in pillar.Questions)
                            {
                                string questionNumber = $"{pillarIndex}.{questionIndex}";

                                // ================= QUESTION CARD =================
                                col.Item()
                                    .Background("#ffffff")
                                    .Border(1)
                                    .BorderColor("#e5e5e5")
                                    .Padding(12)
                                    .Column(qCol =>
                                    {
                                        // Question Title
                                        qCol.Item().Text($"{questionNumber} {question.QuestionText}")
                                            .FontSize(12)
                                            .Bold();

                                        qCol.Item().PaddingTop(10);

                                        // ================= CLEAN TABLE =================
                                        qCol.Item().Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.RelativeColumn(2); // Name
                                                columns.RelativeColumn(1); // Score
                                                columns.RelativeColumn(5); // Option
                                            });

                                            // HEADER
                                            table.Header(header =>
                                            {
                                                header.Cell().PaddingBottom(5)
                                                    .Text("Name").SemiBold().FontSize(10);

                                                header.Cell().PaddingBottom(5)
                                                    .Text("Score").SemiBold().FontSize(10);

                                                header.Cell().PaddingBottom(5)
                                                    .Text("Option").SemiBold().FontSize(10);
                                            });

                                            // ROWS
                                            foreach (var user in question.Users.Values
                                                         .OrderBy(x => x.UserID == -1 ? 1 : 0))
                                            {
                                                bool isAI = user.UserID == -1;
                                                string bgColor = isAI ? "#e6f4ef" : "#ffffff";

                                                // NAME
                                                var nameCell = table.Cell()
                                                    .Padding(8)
                                                    .Background(bgColor)
                                                    .Text(isAI ? "AI" : user.FullName)
                                                    .FontColor(isAI ? "#0a7d5e" : "#000");

                                                if (isAI)
                                                    nameCell.Bold();

                                                // SCORE
                                                table.Cell()
                                                    .Padding(8)
                                                    .Background(bgColor)
                                                    .Text(user.Score?.ToString() ?? "");

                                                // OPTION
                                                table.Cell()
                                                    .Padding(8)
                                                    .Background(bgColor)
                                                    .Text(user.OptionText ?? "");
                                            }
                                        });
                                    });

                                questionIndex++;
                                col.Item().PaddingBottom(10);
                            }

                            pillarIndex++;
                            col.Item().PaddingBottom(15);
                        }
                    });
                });
            }).GeneratePdf();
        }

        private byte[] MakePillarSheet(List<PillarWithQuestionsDto> pillars, Models.City? city)
        {
            using (var workbook = new XLWorkbook())
            {
                var name = city == null ? $"{pillars.Count}-Pillars-Result" : city?.CityName+"-"+city?.State+ $"-{pillars.Count}-Pillars-Result";
                var shortName = name.Length > 30 ? name.Substring(0, 30) : name;

                var ws = workbook.Worksheets.Add(shortName);
                ws.Columns().Width = 35;
                ws.Column(1).Width = 6;  // S.NO.
                ws.Column(2).Width = 100;  // Pillar/Question text

                var protection = ws.Protect();
                protection.AllowedElements =
                   XLSheetProtectionElements.FormatColumns |
                   XLSheetProtectionElements.SelectLockedCells |
                   XLSheetProtectionElements.SelectUnlockedCells;

                var names = pillars
                    .SelectMany(p => p.Questions)
                    .SelectMany(q => q.Users.Values)
                    .GroupBy(u => u.UserID)
                    .Select(g => g.First())
                    .ToList();

                int row = 1;
                int pillarCounter = 1;

                foreach (var pillar in pillars)
                {
                    int c = 1;

                    // Header row
                    ws.Cell(row, c++).Value = "S.NO.";
                    ws.Cell(row, c++).Value = "PillarName";
                    foreach (var user in names)
                        ws.Cell(row, c++).Value = user.FullName;

                    var headerRange = ws.Range(row, 1, row, names.Count + 2);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ++row;
                    c = 1;

                    // Pillar row
                    ws.Cell(row, c++).Value = pillarCounter++; // pillar serial number
                    ws.Cell(row, c++).Value = pillar.PillarName;
                    ws.Cell(row, 2).Style.Font.Bold = true;

                    foreach (var user in names)
                    {
                        var score = pillar.Questions
                            .SelectMany(x => x.Users)
                            .Where(x => x.Key == user.UserID)
                            .Sum(x => x.Value.Score) ?? 0;

                        var richText = ws.Cell(row, c++).GetRichText();

                        richText.AddText("Total Score:  ")
                            .SetBold().SetFontColor(XLColor.DarkGray);
                        richText.AddText($"{score}\n")
                            .SetFontColor(XLColor.Black);
                    }

                    row += 2;
                    c = 1;

                    // Question header row
                    ws.Cell(row, c++).Value = "S.NO.";
                    ws.Cell(row, c++).Value = "Questions";
                    foreach (var user in names)
                        ws.Cell(row, c++).Value = user.FullName;

                    var headerQRange = ws.Range(row, 1, row, names.Count + 2);
                    headerQRange.Style.Font.Bold = true;
                    headerQRange.Style.Fill.BackgroundColor = XLColor.TealBlue;
                    headerQRange.Style.Font.FontColor = XLColor.White;
                    headerQRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    var q = pillar.Questions;
                    int questionCounter = 1;

                    for (var i = 0; i < q.Count; i++)
                    {
                        ++row;
                        var question = q[i];
                        var usersData = question.Users;

                        c = 1;
                        ws.Cell(row, c++).Value = $"{pillarCounter - 1}.{questionCounter++}";
                        ws.Cell(row, 1).Style.Font.Bold = true;
                        ws.Cell(row, c++).Value = question.QuestionText;


                        foreach (var user in names)
                        {
                            usersData.TryGetValue(user.UserID, out var answerDto);
                            answerDto ??= new();

                            var richText = ws.Cell(row, c++).GetRichText();

                            richText.AddText("OptionText: ")
                               .SetBold().SetFontColor(XLColor.DarkRed);
                            richText.AddText($"{answerDto.OptionText ?? "-"}\n")
                                .SetFontColor(XLColor.Black);

                            richText.AddText("Score: ")
                                .SetBold().SetFontColor(XLColor.DarkBlue);
                            richText.AddText($"{answerDto.Score}\n")
                                .SetFontColor(XLColor.Black);

                            richText.AddText("Comment: ")
                                .SetBold().SetFontColor(XLColor.DarkGreen);
                            richText.AddText($"{answerDto.Justification ?? "-"}")
                                .SetFontColor(XLColor.Black);

                            ws.Cell(row, c - 1).Style.Alignment.WrapText = true;
                            ws.Row(row).Height = 60;
                        }
                    }

                    row += 2;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<PaginationResponse<PillarsHistroyResponseDto>> GetResponsesByUserId(GetPillarResponseHistoryRequestNewDto request, UserRole userRole)
        {
            try
            {
                var year = request.UpdatedAt.Year;
                var startDate = new DateTime(year, 1, 1);
                var endDate = new DateTime(year + 1, 1, 1);
                // Role based filter
                IQueryable<UserCityMapping> userCityMappings = _context.UserCityMappings
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.CityID == request.CityID);

                userCityMappings = userRole switch
                {
                    UserRole.Analyst => userCityMappings.Where(x => x.AssignedByUserId == request.UserId),
                    UserRole.Evaluator => userCityMappings.Where(x => x.UserID == request.UserId),
                    _ => userCityMappings
                };

                // Main query (single DB round-trip)
                var rawData = await (
                    from ucm in userCityMappings
                    join a in _context.Assessments on ucm.UserCityMappingID equals a.UserCityMappingID
                    where a.IsActive && (a.UpdatedAt >= startDate && a.UpdatedAt <= endDate && (a.AssessmentPhase == AssessmentPhase.Completed || a.AssessmentPhase == AssessmentPhase.EditRejected || a.AssessmentPhase == AssessmentPhase.EditRequested))
                    from pa in a.PillarAssessments
                    where !request.PillarID.HasValue || pa.PillarID == request.PillarID
                    join p in _context.Pillars on pa.PillarID equals p.PillarID
                    select new
                    {
                        p.PillarID,
                        p.PillarName,
                        p.DisplayOrder,
                        UserID = ucm.UserID,
                        TotalQuestion = p.Questions.Count(),
                        Responses = pa.Responses
                    }
                ).ToListAsync();

                if (!rawData.Any())
                    return new PaginationResponse<PillarsHistroyResponseDto>();

                // Preload users
                var userIds = rawData.Select(x => x.UserID).Distinct().ToList();
                var usersDict = await _context.Users
                    .Where(u => userIds.Contains(u.UserID))
                    .ToDictionaryAsync(u => u.UserID, u => u.FullName);

                var result = rawData
                .GroupBy(x => new { x.PillarID, x.PillarName, x.DisplayOrder })
                .Select(pillarGroup =>
                {
                    return new PillarsHistroyResponseDto
                    {
                        PillarID = pillarGroup.Key.PillarID,
                        PillarName = pillarGroup.Key.PillarName,
                        DisplayOrder = pillarGroup.Key.DisplayOrder,
                        Users = pillarGroup
                        .GroupBy(x => x.UserID)
                        .Select(userGroup =>
                        {
                            var responses = userGroup
                                .SelectMany(x => x.Responses)
                                .Where(r => r.Score.HasValue &&
                                            (int)r.Score.Value <= (int)ScoreValue.Four)
                                .ToList();

                            var score = responses.Sum(r => (int?)r.Score ?? 0);
                            var scoreCount = responses.Count;
                            var totalQuestions = userGroup.Max(x => x.TotalQuestion);

                            decimal progress = scoreCount > 0
                                ? score * 100m / (scoreCount * 4m)
                                : 0m;

                            return new PillarsUserHistroyResponseDto
                            {
                                UserID = userGroup.Key,
                                FullName = usersDict.GetValueOrDefault(userGroup.Key, ""),
                                Score = score,
                                ScoreProgress = progress,
                                TotalQuestion = totalQuestions,
                                AnsQuestion = responses.Count,
                                AnsPillar = responses.Any() ? 1 : 0
                            };

                        }).ToList()
                    };
                }).OrderBy(x => x.DisplayOrder);

                var count = 0;
                var valid = 0;
                var totalRecords = 0;

                foreach (var r in result)
                {
                    totalRecords += r.Users.Count;
                    if (count+ r.Users.Count <= request.PageSize)
                    {
                        count += r.Users.Count;
                        valid++;
                    }
                }
                var filterResult = result.Skip((request.PageNumber - 1) * valid);

                return new PaginationResponse<PillarsHistroyResponseDto>
                {
                    Data = filterResult.Take(valid),
                    TotalRecords = totalRecords,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync(
                    "Error occurred in GetPillarsHistoryByUserId", ex);

                return new PaginationResponse<PillarsHistroyResponseDto>();
            }
        }
        IContainer HeaderCell(IContainer container)
        {
            return container
                .Padding(6)
                .Background("#f1f5f4")
                .Border(1)
                .BorderColor("#dcdcdc");
        }

        IContainer BodyCell(IContainer container, bool isAI)
        {
            return container
                .Padding(6)
                .Background(isAI ? "#e6f4ef" : "#ffffff")
                .Border(1)
                .BorderColor("#e0e0e0");
        }
    }
}