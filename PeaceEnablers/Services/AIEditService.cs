using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Models;
using PeaceEnablers.Data;
using PeaceEnablers.Dtos.AiDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.IServices;
using PeaceEnablers.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace PeaceEnablers.Services
{
    public class AIEditService : IAIEditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppLogger _appLogger;

        public AIEditService(ApplicationDbContext context, IAppLogger appLogger)
        {
            _context = context;
            _appLogger = appLogger;
        }

        #region ai score manual edit

        private async Task<bool> CanUserEditAiDataAsync(int userID, UserRole userRole, int countryID, int year)
        {
            if (userRole == UserRole.Admin)
                return true;

            if (userRole == UserRole.Analyst)
            {
                var assigned = await _context.UserCountryMappings
                    .AnyAsync(x => !x.IsDeleted && x.UserID == userID && x.CountryID == countryID);
                if (!assigned)
                    return false;

                return await CanAnalystEditAsync(userID, countryID, year);
            }

            return false;
        }

        private static string? ToAuditValue(object? value) => value?.ToString();

        private static void AddChange(
            Dictionary<string, (string? OldValue, string? NewValue)> changes,
            string fieldName,
            object? oldValue,
            object? newValue)
        {
            changes[fieldName] = (ToAuditValue(oldValue), ToAuditValue(newValue));
        }

        public async Task<ResultResponseDto<bool>> UpdateAICountryScore(UpdateAICountryScoreDto dto, int userID, UserRole userRole)
        {
            try
            {
                if (!await CanUserEditAiDataAsync(userID, userRole, dto.CountryID, dto.Year))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this country data. Analysts need active edit authority from admin." });

                var entity = await _context.AICountryScores
                    .FirstOrDefaultAsync(x => x.CountryID == dto.CountryID && x.Year == dto.Year);

                if (entity == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Country score record not found." });

                var changes = new Dictionary<string, (string? OldValue, string? NewValue)>(StringComparer.OrdinalIgnoreCase);
                AddChange(changes, nameof(entity.ConfidenceLevel), entity.ConfidenceLevel, dto.ConfidenceLevel ?? entity.ConfidenceLevel);
                AddChange(changes, nameof(entity.EvidenceSummary), entity.EvidenceSummary, dto.EvidenceSummary ?? entity.EvidenceSummary);
                AddChange(changes, nameof(entity.ImmediateSituationSummary), entity.ImmediateSituationSummary, dto.ImmediateSituationSummary ?? entity.ImmediateSituationSummary);
                AddChange(changes, nameof(entity.KeyDevelopments), entity.KeyDevelopments, dto.KeyDevelopments);
                AddChange(changes, nameof(entity.CriticalRisks), entity.CriticalRisks, dto.CriticalRisks);
                AddChange(changes, nameof(entity.Gaps), entity.Gaps, dto.Gaps);
                AddChange(changes, nameof(entity.KeyFindings), entity.KeyFindings, dto.KeyFindings);
                AddChange(changes, nameof(entity.Recommendations), entity.Recommendations, dto.Recommendations);
                AddChange(changes, nameof(entity.StructuralEvidence), entity.StructuralEvidence, dto.StructuralEvidence);
                AddChange(changes, nameof(entity.OperationalEvidence), entity.OperationalEvidence, dto.OperationalEvidence);
                AddChange(changes, nameof(entity.OutcomeEvidence), entity.OutcomeEvidence, dto.OutcomeEvidence);
                AddChange(changes, nameof(entity.PerceptionEvidence), entity.PerceptionEvidence, dto.PerceptionEvidence);
                AddChange(changes, nameof(entity.TemporalScope), entity.TemporalScope, dto.TemporalScope);
                AddChange(changes, nameof(entity.DistortionScreening), entity.DistortionScreening, dto.DistortionScreening);
                AddChange(changes, nameof(entity.PoliticalShock), entity.PoliticalShock, dto.PoliticalShock);
                AddChange(changes, nameof(entity.EconomicShock), entity.EconomicShock, dto.EconomicShock);
                AddChange(changes, nameof(entity.NarrativeShock), entity.NarrativeShock, dto.NarrativeShock);
                AddChange(changes, nameof(entity.StressScoreAdjustment), entity.StressScoreAdjustment, dto.StressScoreAdjustment);
                AddChange(changes, nameof(entity.InequalityAdjustment), entity.InequalityAdjustment, dto.InequalityAdjustment);
                AddChange(changes, nameof(entity.OpacityRisk), entity.OpacityRisk, dto.OpacityRisk);
                AddChange(changes, nameof(entity.NonCompensationNote), entity.NonCompensationNote, dto.NonCompensationNote);
                AddChange(changes, nameof(entity.RelationalIntegrity), entity.RelationalIntegrity, dto.RelationalIntegrity);
                AddChange(changes, nameof(entity.InstitutionalCapacity), entity.InstitutionalCapacity, dto.InstitutionalCapacity);
                AddChange(changes, nameof(entity.PrimarySource), entity.PrimarySource, dto.PrimarySource);
                AddChange(changes, nameof(entity.CrossPillarPatterns), entity.CrossPillarPatterns, dto.CrossPillarPatterns);
                AddChange(changes, nameof(entity.EquityAssessment), entity.EquityAssessment, dto.EquityAssessment);
                AddChange(changes, nameof(entity.ConflictRiskOutlook), entity.ConflictRiskOutlook, dto.ConflictRiskOutlook);
                AddChange(changes, nameof(entity.StrategicRecommendation), entity.StrategicRecommendation, dto.StrategicRecommendation);
                AddChange(changes, nameof(entity.DataTransparencyNote), entity.DataTransparencyNote, dto.DataTransparencyNote);

                if (userRole == UserRole.Analyst)
                {
                    return await SaveAnalystDraftAsync(
                        userID, dto.CountryID, dto.Year,
                        AIEditEntityType.Country, entity.CountryScoreID,
                        null, null, changes);
                }

                entity.ConfidenceLevel = dto.ConfidenceLevel ?? entity.ConfidenceLevel;
                entity.EvidenceSummary = dto.EvidenceSummary ?? entity.EvidenceSummary;
                entity.ImmediateSituationSummary = dto.ImmediateSituationSummary ?? entity.ImmediateSituationSummary;
                entity.KeyDevelopments = dto.KeyDevelopments;
                entity.CriticalRisks = dto.CriticalRisks;
                entity.Gaps = dto.Gaps;
                entity.KeyFindings = dto.KeyFindings;
                entity.Recommendations = dto.Recommendations;
                entity.StructuralEvidence = dto.StructuralEvidence;
                entity.OperationalEvidence = dto.OperationalEvidence;
                entity.OutcomeEvidence = dto.OutcomeEvidence;
                entity.PerceptionEvidence = dto.PerceptionEvidence;
                entity.TemporalScope = dto.TemporalScope;
                entity.DistortionScreening = dto.DistortionScreening;
                entity.PoliticalShock = dto.PoliticalShock;
                entity.EconomicShock = dto.EconomicShock;
                entity.NarrativeShock = dto.NarrativeShock;
                entity.StressScoreAdjustment = dto.StressScoreAdjustment;
                entity.InequalityAdjustment = dto.InequalityAdjustment;
                entity.OpacityRisk = dto.OpacityRisk;
                entity.NonCompensationNote = dto.NonCompensationNote;
                entity.RelationalIntegrity = dto.RelationalIntegrity;
                entity.InstitutionalCapacity = dto.InstitutionalCapacity;
                entity.PrimarySource = dto.PrimarySource;
                entity.CrossPillarPatterns = dto.CrossPillarPatterns;
                entity.EquityAssessment = dto.EquityAssessment;
                entity.ConflictRiskOutlook = dto.ConflictRiskOutlook;
                entity.StrategicRecommendation = dto.StrategicRecommendation;
                entity.DataTransparencyNote = dto.DataTransparencyNote;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await LogAdminPublishAsync(
                    userID, dto.CountryID, dto.Year,
                    AIEditEntityType.Country, entity.CountryScoreID,
                    null, null, changes);

                return ResultResponseDto<bool>.Success(true, new[] { "Country AI data updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAICountryScore", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update country AI data." });
            }
        }

        public async Task<ResultResponseDto<bool>> UpdateAIPillarScore(UpdateAIPillarScoreDto dto, int userID, UserRole userRole)
        {
            try
            {
                var entity = await _context.AIPillarScores
                    .Include(x => x.DataSourceCitations)
                    .FirstOrDefaultAsync(x => x.PillarScoreID == dto.PillarScoreID);

                if (entity == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Domain score record not found." });

                if (!await CanUserEditAiDataAsync(userID, userRole, entity.CountryID, entity.Year))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this pillar data. Analysts need active edit authority from admin." });

                var changes = new Dictionary<string, (string? OldValue, string? NewValue)>(StringComparer.OrdinalIgnoreCase);
                AddChange(changes, nameof(entity.ConfidenceLevel), entity.ConfidenceLevel, dto.ConfidenceLevel);
                AddChange(changes, nameof(entity.EvidenceSummary), entity.EvidenceSummary, dto.EvidenceSummary);
                AddChange(changes, nameof(entity.StructuralEvidence), entity.StructuralEvidence, dto.StructuralEvidence);
                AddChange(changes, nameof(entity.OperationalEvidence), entity.OperationalEvidence, dto.OperationalEvidence);
                AddChange(changes, nameof(entity.OutcomeEvidence), entity.OutcomeEvidence, dto.OutcomeEvidence);
                AddChange(changes, nameof(entity.PerceptionEvidence), entity.PerceptionEvidence, dto.PerceptionEvidence);
                AddChange(changes, nameof(entity.TemporalScope), entity.TemporalScope, dto.TemporalScope);
                AddChange(changes, nameof(entity.DistortionScreening), entity.DistortionScreening, dto.DistortionScreening);
                AddChange(changes, nameof(entity.RelationalIntegrity), entity.RelationalIntegrity, dto.RelationalIntegrity);
                AddChange(changes, nameof(entity.StressPoliticalShock), entity.StressPoliticalShock, dto.StressPoliticalShock);
                AddChange(changes, nameof(entity.StressEconomicShock), entity.StressEconomicShock, dto.StressEconomicShock);
                AddChange(changes, nameof(entity.StressNarrativeShock), entity.StressNarrativeShock, dto.StressNarrativeShock);
                AddChange(changes, nameof(entity.StressScoreAdjustment), entity.StressScoreAdjustment, dto.StressScoreAdjustment);
                AddChange(changes, nameof(entity.InequalityAdjustment), entity.InequalityAdjustment, dto.InequalityAdjustment);
                AddChange(changes, nameof(entity.OpacityRisk), entity.OpacityRisk, dto.OpacityRisk);
                AddChange(changes, nameof(entity.NonCompensationNote), entity.NonCompensationNote, dto.NonCompensationNote);
                AddChange(changes, nameof(entity.GeographicEquityNote), entity.GeographicEquityNote, dto.GeographicEquityNote);
                AddChange(changes, nameof(entity.InstitutionalAssessment), entity.InstitutionalAssessment, dto.InstitutionalAssessment);
                AddChange(changes, nameof(entity.DataGapAnalysis), entity.DataGapAnalysis, dto.DataGapAnalysis);
                AddChange(changes, nameof(entity.RedFlag), entity.RedFlag, dto.RedFlag);

                if (userRole == UserRole.Analyst)
                {
                    var draftResult = await SaveAnalystDraftAsync(
                        userID, entity.CountryID, entity.Year,
                        AIEditEntityType.Pillar, entity.PillarScoreID,
                        entity.PillarID, null, changes);

                    if (!draftResult.Succeeded)
                        return draftResult;

                    if (dto.DataSourceCitations != null && entity.DataSourceCitations != null)
                    {
                        foreach (var citationDto in dto.DataSourceCitations)
                        {
                            var citation = entity.DataSourceCitations.FirstOrDefault(x => x.CitationID == citationDto.CitationID);
                            if (citation == null)
                                continue;

                            var citationChanges = new Dictionary<string, (string? OldValue, string? NewValue)>(StringComparer.OrdinalIgnoreCase);
                            AddChange(citationChanges, nameof(citation.SourceType), citation.SourceType, citationDto.SourceType ?? citation.SourceType);
                            AddChange(citationChanges, nameof(citation.SourceName), citation.SourceName, citationDto.SourceName ?? citation.SourceName);
                            AddChange(citationChanges, nameof(citation.SourceURL), citation.SourceURL, citationDto.SourceURL ?? citation.SourceURL);
                            AddChange(citationChanges, nameof(citation.DataYear), citation.DataYear, citationDto.DataYear);
                            AddChange(citationChanges, nameof(citation.DataExtract), citation.DataExtract, citationDto.DataExtract ?? citation.DataExtract);
                            AddChange(citationChanges, nameof(citation.TrustLevel), citation.TrustLevel, citationDto.TrustLevel);

                            await SaveAnalystDraftAsync(
                                userID, entity.CountryID, entity.Year,
                                AIEditEntityType.Citation, citation.CitationID,
                                entity.PillarID, null, citationChanges);
                        }
                    }

                    return draftResult;
                }

                entity.ConfidenceLevel = dto.ConfidenceLevel;
                entity.EvidenceSummary = dto.EvidenceSummary;
                entity.StructuralEvidence = dto.StructuralEvidence;
                entity.OperationalEvidence = dto.OperationalEvidence;
                entity.OutcomeEvidence = dto.OutcomeEvidence;
                entity.PerceptionEvidence = dto.PerceptionEvidence;
                entity.TemporalScope = dto.TemporalScope;
                entity.DistortionScreening = dto.DistortionScreening;
                entity.RelationalIntegrity = dto.RelationalIntegrity;
                entity.StressPoliticalShock = dto.StressPoliticalShock;
                entity.StressEconomicShock = dto.StressEconomicShock;
                entity.StressNarrativeShock = dto.StressNarrativeShock;
                entity.StressScoreAdjustment = dto.StressScoreAdjustment;
                entity.InequalityAdjustment = dto.InequalityAdjustment;
                entity.OpacityRisk = dto.OpacityRisk;
                entity.NonCompensationNote = dto.NonCompensationNote;
                entity.GeographicEquityNote = dto.GeographicEquityNote;
                entity.InstitutionalAssessment = dto.InstitutionalAssessment;
                entity.DataGapAnalysis = dto.DataGapAnalysis;
                entity.RedFlag = dto.RedFlag;
                entity.UpdatedAt = DateTime.UtcNow;

                if (dto.DataSourceCitations != null && entity.DataSourceCitations != null)
                {
                    foreach (var citationDto in dto.DataSourceCitations)
                    {
                        var citation = entity.DataSourceCitations.FirstOrDefault(x => x.CitationID == citationDto.CitationID);
                        if (citation == null)
                            continue;

                        var citationChanges = new Dictionary<string, (string? OldValue, string? NewValue)>(StringComparer.OrdinalIgnoreCase);
                        AddChange(citationChanges, nameof(citation.SourceType), citation.SourceType, citationDto.SourceType ?? citation.SourceType);
                        AddChange(citationChanges, nameof(citation.SourceName), citation.SourceName, citationDto.SourceName ?? citation.SourceName);
                        AddChange(citationChanges, nameof(citation.SourceURL), citation.SourceURL, citationDto.SourceURL ?? citation.SourceURL);
                        AddChange(citationChanges, nameof(citation.DataYear), citation.DataYear, citationDto.DataYear);
                        AddChange(citationChanges, nameof(citation.DataExtract), citation.DataExtract, citationDto.DataExtract ?? citation.DataExtract);
                        AddChange(citationChanges, nameof(citation.TrustLevel), citation.TrustLevel, citationDto.TrustLevel);

                        citation.SourceType = citationDto.SourceType ?? citation.SourceType;
                        citation.SourceName = citationDto.SourceName ?? citation.SourceName;
                        citation.SourceURL = citationDto.SourceURL ?? citation.SourceURL;
                        citation.DataYear = citationDto.DataYear;
                        citation.DataExtract = citationDto.DataExtract ?? citation.DataExtract;
                        citation.TrustLevel = citationDto.TrustLevel;

                        await LogAdminPublishAsync(
                            userID, entity.CountryID, entity.Year,
                            AIEditEntityType.Citation, citation.CitationID,
                            entity.PillarID, null, citationChanges);
                    }
                }

                await _context.SaveChangesAsync();
                await LogAdminPublishAsync(
                    userID, entity.CountryID, entity.Year,
                    AIEditEntityType.Pillar, entity.PillarScoreID,
                    entity.PillarID, null, changes);

                return ResultResponseDto<bool>.Success(true, new[] { "Domain AI data updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIPillarScore", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update pillar AI data." });
            }
        }

        public async Task<ResultResponseDto<bool>> UpdateAIDataSourceCitation(UpdateAIDataSourceCitationDto dto, int userID, UserRole userRole)
        {
            try
            {
                var entity = await _context.AIDataSourceCitations
                    .Include(x => x.PillarScore)
                    .FirstOrDefaultAsync(x => x.CitationID == dto.CitationID);

                if (entity?.PillarScore == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Citation record not found." });

                if (!await CanUserEditAiDataAsync(userID, userRole, entity.PillarScore.CountryID, entity.PillarScore.Year))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this citation." });

                var changes = new Dictionary<string, (string? OldValue, string? NewValue)>(StringComparer.OrdinalIgnoreCase);
                AddChange(changes, nameof(entity.SourceType), entity.SourceType, dto.SourceType ?? entity.SourceType);
                AddChange(changes, nameof(entity.SourceName), entity.SourceName, dto.SourceName ?? entity.SourceName);
                AddChange(changes, nameof(entity.SourceURL), entity.SourceURL, dto.SourceURL ?? entity.SourceURL);
                AddChange(changes, nameof(entity.DataYear), entity.DataYear, dto.DataYear);
                AddChange(changes, nameof(entity.DataExtract), entity.DataExtract, dto.DataExtract ?? entity.DataExtract);
                AddChange(changes, nameof(entity.TrustLevel), entity.TrustLevel, dto.TrustLevel);

                if (userRole == UserRole.Analyst)
                {
                    return await SaveAnalystDraftAsync(
                        userID, entity.PillarScore.CountryID, entity.PillarScore.Year,
                        AIEditEntityType.Citation, entity.CitationID,
                        entity.PillarScore.PillarID, null, changes);
                }

                entity.SourceType = dto.SourceType ?? entity.SourceType;
                entity.SourceName = dto.SourceName ?? entity.SourceName;
                entity.SourceURL = dto.SourceURL ?? entity.SourceURL;
                entity.DataYear = dto.DataYear;
                entity.DataExtract = dto.DataExtract ?? entity.DataExtract;
                entity.TrustLevel = dto.TrustLevel;

                await _context.SaveChangesAsync();
                await LogAdminPublishAsync(
                    userID, entity.PillarScore.CountryID, entity.PillarScore.Year,
                    AIEditEntityType.Citation, entity.CitationID,
                    entity.PillarScore.PillarID, null, changes);

                return ResultResponseDto<bool>.Success(true, new[] { "Citation updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIDataSourceCitation", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update citation." });
            }
        }

        public async Task<ResultResponseDto<bool>> UpdateAIEstimatedQuestionScore(UpdateAIEstimatedQuestionScoreDto dto, int userID, UserRole userRole)
        {
            try
            {
                if (!await CanUserEditAiDataAsync(userID, userRole, dto.CountryID, dto.Year))
                    return ResultResponseDto<bool>.Failure(new[] { "You do not have permission to edit this question data. Analysts need active edit authority from admin." });

                var entity = await _context.AIEstimatedQuestionScores
                    .FirstOrDefaultAsync(x =>
                        x.CountryID == dto.CountryID &&
                        x.PillarID == dto.PillarID &&
                        x.QuestionID == dto.QuestionID &&
                        x.Year == dto.Year);

                if (entity == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Question score record not found." });

                var changes = new Dictionary<string, (string? OldValue, string? NewValue)>(StringComparer.OrdinalIgnoreCase);
                AddChange(changes, nameof(entity.AIScore), entity.AIScore, dto.AIScore);
                AddChange(changes, nameof(entity.ConfidenceLevel), entity.ConfidenceLevel, dto.ConfidenceLevel);
                AddChange(changes, nameof(entity.SourcesConsulted), entity.SourcesConsulted, dto.SourcesConsulted);
                AddChange(changes, nameof(entity.EvidenceSummary), entity.EvidenceSummary, dto.EvidenceSummary);
                AddChange(changes, nameof(entity.StructuralEvidence), entity.StructuralEvidence, dto.StructuralEvidence);
                AddChange(changes, nameof(entity.OperationalEvidence), entity.OperationalEvidence, dto.OperationalEvidence);
                AddChange(changes, nameof(entity.OutcomeEvidence), entity.OutcomeEvidence, dto.OutcomeEvidence);
                AddChange(changes, nameof(entity.PerceptionEvidence), entity.PerceptionEvidence, dto.PerceptionEvidence);
                AddChange(changes, nameof(entity.TemporalScope), entity.TemporalScope, dto.TemporalScope);
                AddChange(changes, nameof(entity.DistortionScreening), entity.DistortionScreening, dto.DistortionScreening);
                AddChange(changes, nameof(entity.RelationalDependencies), entity.RelationalDependencies, dto.RelationalDependencies);
                AddChange(changes, nameof(entity.StressPoliticalShock), entity.StressPoliticalShock, dto.StressPoliticalShock);
                AddChange(changes, nameof(entity.StressEconomicShock), entity.StressEconomicShock, dto.StressEconomicShock);
                AddChange(changes, nameof(entity.StressNarrativeShock), entity.StressNarrativeShock, dto.StressNarrativeShock);
                AddChange(changes, nameof(entity.StressOverallResilienceShock), entity.StressOverallResilienceShock, dto.StressOverallResilienceShock);
                AddChange(changes, nameof(entity.InequalityAdjustment), entity.InequalityAdjustment, dto.InequalityAdjustment);
                AddChange(changes, nameof(entity.OpacityRisk), entity.OpacityRisk, dto.OpacityRisk);
                AddChange(changes, nameof(entity.RedFlag), entity.RedFlag, dto.RedFlag);
                AddChange(changes, nameof(entity.SourceType), entity.SourceType, dto.SourceType);
                AddChange(changes, nameof(entity.SourceName), entity.SourceName, dto.SourceName);
                AddChange(changes, nameof(entity.SourceURL), entity.SourceURL, dto.SourceURL);
                AddChange(changes, nameof(entity.SourceDataYear), entity.SourceDataYear, dto.SourceDataYear);
                AddChange(changes, nameof(entity.SourceHierarchyLevel), entity.SourceHierarchyLevel, dto.SourceHierarchyLevel);
                AddChange(changes, nameof(entity.SourceDataExtract), entity.SourceDataExtract, dto.SourceDataExtract);

                if (userRole == UserRole.Analyst)
                {
                    return await SaveAnalystDraftAsync(
                        userID, dto.CountryID, dto.Year,
                        AIEditEntityType.Question, entity.QuestionScoreID,
                        dto.PillarID, dto.QuestionID, changes);
                }

                entity.AIScore = dto.AIScore;
                entity.Discrepancy = CalculateDiscrepancy(entity.EvaluatorScore, dto.AIScore);
                entity.ConfidenceLevel = dto.ConfidenceLevel;
                entity.SourcesConsulted = dto.SourcesConsulted;
                entity.EvidenceSummary = dto.EvidenceSummary;
                entity.StructuralEvidence = dto.StructuralEvidence;
                entity.OperationalEvidence = dto.OperationalEvidence;
                entity.OutcomeEvidence = dto.OutcomeEvidence;
                entity.PerceptionEvidence = dto.PerceptionEvidence;
                entity.TemporalScope = dto.TemporalScope;
                entity.DistortionScreening = dto.DistortionScreening;
                entity.RelationalDependencies = dto.RelationalDependencies;
                entity.StressPoliticalShock = dto.StressPoliticalShock;
                entity.StressEconomicShock = dto.StressEconomicShock;
                entity.StressNarrativeShock = dto.StressNarrativeShock;
                entity.StressOverallResilienceShock = dto.StressOverallResilienceShock;
                entity.InequalityAdjustment = dto.InequalityAdjustment;
                entity.OpacityRisk = dto.OpacityRisk;
                entity.RedFlag = dto.RedFlag;
                entity.SourceType = dto.SourceType;
                entity.SourceName = dto.SourceName;
                entity.SourceURL = dto.SourceURL;
                entity.SourceDataYear = dto.SourceDataYear;
                entity.SourceHierarchyLevel = dto.SourceHierarchyLevel;
                entity.SourceDataExtract = dto.SourceDataExtract;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await LogAdminPublishAsync(
                    userID, dto.CountryID, dto.Year,
                    AIEditEntityType.Question, entity.QuestionScoreID,
                    dto.PillarID, dto.QuestionID, changes);

                return ResultResponseDto<bool>.Success(true, new[] { "Question AI data updated successfully." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in UpdateAIEstimatedQuestionScore", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to update question AI data." });
            }
        }

        #endregion ai score manual edit


        public async Task<bool> CanAnalystEditAsync(int userID, int countryID, int year)
        {
            return await _context.AIEditPermissions.AnyAsync(x =>
                x.UserID == userID &&
                x.CountryID == countryID &&
                x.Year == year &&
                x.Status == AIEditPermissionStatus.Active);
        }

        public async Task<ResultResponseDto<AIEditAccessDto>> GetEditAccess(int userID, UserRole userRole, int countryID, int year)
        {
            try
            {
                if (userRole == UserRole.Admin)
                {
                    return ResultResponseDto<AIEditAccessDto>.Success(new AIEditAccessDto
                    {
                        CanEdit = true,
                        Message = "Admin can edit and publish immediately."
                    });
                }

                if (userRole != UserRole.Analyst)
                {
                    return ResultResponseDto<AIEditAccessDto>.Success(new AIEditAccessDto
                    {
                        CanEdit = false,
                        Message = "Only Admin or Analyst can edit AI responses."
                    });
                }

                var assigned = await _context.UserCountryMappings
                    .AnyAsync(x => !x.IsDeleted && x.UserID == userID && x.CountryID == countryID);
                if (!assigned)
                {
                    return ResultResponseDto<AIEditAccessDto>.Success(new AIEditAccessDto
                    {
                        CanEdit = false,
                        Message = "Country is not assigned to you."
                    });
                }

                var permission = await _context.AIEditPermissions
                    .Where(x => x.UserID == userID && x.CountryID == countryID && x.Year == year)
                    .OrderByDescending(x => x.PermissionID)
                    .FirstOrDefaultAsync();

                var draftSession = await _context.AIEditSessions
                    .Where(x => x.UserID == userID && x.CountryID == countryID && x.Year == year)
                    .OrderByDescending(x => x.SessionID)
                    .FirstOrDefaultAsync();

                var canEdit = permission?.Status == AIEditPermissionStatus.Active;
                return ResultResponseDto<AIEditAccessDto>.Success(new AIEditAccessDto
                {
                    CanEdit = canEdit,
                    HasPendingDraft = draftSession != null && draftSession.Status == AIEditSessionStatus.Draft,
                    PermissionID = permission?.PermissionID,
                    SessionID = draftSession?.SessionID ?? permission?.ActiveSessionID,
                    PermissionStatus = permission?.Status.ToString(),
                    SessionStatus = draftSession?.Status.ToString(),
                    Message = canEdit
                        ? "You have active edit authority. Saves go to draft until you submit for admin approval. Live AI response stays unchanged."
                        : permission?.Status == AIEditPermissionStatus.PendingRequest
                            ? "Edit request is pending admin approval."
                            : "Request edit permission from admin, or ask admin to grant editable authority."
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetEditAccess", ex);
                return ResultResponseDto<AIEditAccessDto>.Failure(new[] { "Failed to get edit access." });
            }
        }

        public async Task<ResultResponseDto<AIEditPermissionDto>> RequestPermission(RequestAIEditPermissionDto dto, int userID)
        {
            try
            {
                var assigned = await _context.UserCountryMappings
                    .AnyAsync(x => !x.IsDeleted && x.UserID == userID && x.CountryID == dto.CountryID);
                if (!assigned)
                    return ResultResponseDto<AIEditPermissionDto>.Failure(new[] { "Country is not assigned to you." });

                var existingActive = await _context.AIEditPermissions.AnyAsync(x =>
                    x.UserID == userID && x.CountryID == dto.CountryID && x.Year == dto.Year &&
                    (x.Status == AIEditPermissionStatus.Active || x.Status == AIEditPermissionStatus.PendingRequest));

                if (existingActive)
                    return ResultResponseDto<AIEditPermissionDto>.Failure(new[] { "An active or pending permission already exists for this country/year." });

                var entity = new AIEditPermission
                {
                    UserID = userID,
                    CountryID = dto.CountryID,
                    Year = dto.Year,
                    Status = AIEditPermissionStatus.PendingRequest,
                    RequestedAt = DateTime.UtcNow,
                    Notes = dto.Notes
                };
                _context.AIEditPermissions.Add(entity);
                await _context.SaveChangesAsync();

                var mapped = await MapPermission(entity.PermissionID);
                return ResultResponseDto<AIEditPermissionDto>.Success(mapped!, new[] { "Edit permission requested. Waiting for admin approval." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RequestPermission", ex);
                return ResultResponseDto<AIEditPermissionDto>.Failure(new[] { "Failed to request edit permission." });
            }
        }

        public async Task<ResultResponseDto<AIEditPermissionDto>> GrantPermission(GrantAIEditPermissionDto dto, int adminUserID)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.UserID == dto.UserID && x.Role == UserRole.Analyst);
                if (user == null)
                    return ResultResponseDto<AIEditPermissionDto>.Failure(new[] { "Analyst user not found." });

                var count = await _context.UserCountryMappings
                    .Where(x => !x.IsDeleted
                             && x.UserID == dto.UserID
                             && dto.CountryIDs.Contains(x.CountryID))
                    .Select(x => x.CountryID)
                    .Distinct()
                    .CountAsync();

                if (count != dto.CountryIDs.Distinct().Count())
                {
                    return ResultResponseDto<AIEditPermissionDto>.Failure(
                        new[] { "Analyst doesn't have access to one or more selected countries." });
                }

                var blocking = await _context.AIEditPermissions
                    .Where(x => x.UserID == dto.UserID && dto.CountryIDs.Contains(x.CountryID) && x.Year == dto.Year &&
                                (x.Status == AIEditPermissionStatus.Active || x.Status == AIEditPermissionStatus.PendingRequest))
                    .ToListAsync();

                foreach (var item in blocking)
                {
                    item.Status = item.Status == AIEditPermissionStatus.PendingRequest
                        ? AIEditPermissionStatus.Rejected
                        : AIEditPermissionStatus.Revoked;
                }
                foreach(var countryID in dto.CountryIDs)
                {
                    var permission = new AIEditPermission
                    {
                        UserID = dto.UserID,
                        CountryID = countryID,
                        Year = dto.Year,
                        Status = AIEditPermissionStatus.Active,
                        RequestedAt = DateTime.UtcNow,
                        GrantedBy = adminUserID,
                        GrantedAt = DateTime.UtcNow,
                        Notes = dto.Notes
                    };
                    _context.AIEditPermissions.Add(permission);
                    await _context.SaveChangesAsync();

                    var session = new AIEditSession
                    {
                        PermissionID = permission.PermissionID,
                        UserID = dto.UserID,
                        CountryID = countryID,
                        Year = dto.Year,
                        Status = AIEditSessionStatus.Draft,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AIEditSessions.Add(session);
                    await _context.SaveChangesAsync();

                    permission.ActiveSessionID = session.SessionID;
                    await _context.SaveChangesAsync();                

                    var mapped = await MapPermission(permission.PermissionID);
                }
                return ResultResponseDto<AIEditPermissionDto>.Success(default!, new[] { "Edit authority granted. Analyst can edit country, pillar and question screens under one draft session." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GrantPermission", ex);
                return ResultResponseDto<AIEditPermissionDto>.Failure(new[] { "Failed to grant edit permission." });
            }
        }

        public async Task<ResultResponseDto<bool>> ReviewPermissionRequest(ReviewAIEditPermissionDto dto, int adminUserID)
        {
            try
            {
                var permission = await _context.AIEditPermissions.FirstOrDefaultAsync(x => x.PermissionID == dto.PermissionID);
                if (permission == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Permission request not found." });

                if (permission.Status != AIEditPermissionStatus.PendingRequest)
                    return ResultResponseDto<bool>.Failure(new[] { "Only pending requests can be reviewed." });

                if (!dto.Approve)
                {
                    permission.Status = AIEditPermissionStatus.Rejected;
                    permission.GrantedBy = adminUserID;
                    permission.GrantedAt = DateTime.UtcNow;
                    permission.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? permission.Notes : dto.Notes;
                    await _context.SaveChangesAsync();
                    return ResultResponseDto<bool>.Success(true, new[] { "Permission request rejected." });
                }

                permission.Status = AIEditPermissionStatus.Active;
                permission.GrantedBy = adminUserID;
                permission.GrantedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(dto.Notes))
                    permission.Notes = dto.Notes;

                var session = new AIEditSession
                {
                    PermissionID = permission.PermissionID,
                    UserID = permission.UserID,
                    CountryID = permission.CountryID,
                    Year = permission.Year,
                    Status = AIEditSessionStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AIEditSessions.Add(session);
                await _context.SaveChangesAsync();

                permission.ActiveSessionID = session.SessionID;
                await _context.SaveChangesAsync();

                return ResultResponseDto<bool>.Success(true, new[] { "Permission approved. Draft session opened for analyst." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ReviewPermissionRequest", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to review permission request." });
            }
        }

        public async Task<ResultResponseDto<bool>> RevokePermission(int permissionID, int adminUserID)
        {
            try
            {
                var permission = await _context.AIEditPermissions.FirstOrDefaultAsync(x => x.PermissionID == permissionID);
                if (permission == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Permission not found." });

                if (permission.Status != AIEditPermissionStatus.Active && permission.Status != AIEditPermissionStatus.PendingRequest)
                    return ResultResponseDto<bool>.Failure(new[] { "Permission is not active/pending." });

                permission.Status = permission.Status == AIEditPermissionStatus.PendingRequest
                    ? AIEditPermissionStatus.Rejected
                    : AIEditPermissionStatus.Revoked;
                permission.GrantedBy = adminUserID;
                permission.GrantedAt = DateTime.UtcNow;

                if (permission.ActiveSessionID.HasValue)
                {
                    var session = await _context.AIEditSessions.FirstOrDefaultAsync(x => x.SessionID == permission.ActiveSessionID.Value);
                    if (session != null && session.Status == AIEditSessionStatus.Draft)
                    {
                        session.Status = AIEditSessionStatus.Cancelled;
                        session.ReviewedBy = adminUserID;
                        session.ReviewedAt = DateTime.UtcNow;
                        session.ReviewComment = "Permission revoked by admin.";
                    }
                }

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Permission revoked." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in RevokePermission", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to revoke permission." });
            }
        }

        public async Task<PaginationResponse<AIEditPermissionDto>> GetPermissions(AIEditPermissionListRequestDto request)
        {
            try
            {
                var query = from p in _context.AIEditPermissions
                            join u in _context.Users on p.UserID equals u.UserID
                            join c in _context.Countries on p.CountryID equals c.CountryID
                            join g in _context.Users on p.GrantedBy equals g.UserID into gj
                            from g in gj.DefaultIfEmpty()
                            select new { p, u, c, GrantName = g != null ? g.FullName : null };

                if (request.CountryID.HasValue)
                    query = query.Where(x => x.p.CountryID == request.CountryID.Value);
                if (request.Year.HasValue)
                    query = query.Where(x => x.p.Year == request.Year.Value);
                if (request.UserId.HasValue && request.UserId > 0)
                    query = query.Where(x => x.p.UserID == request.UserId.Value);
                if (request.Status.HasValue)
                    query = query.Where(x => (byte)x.p.Status == request.Status.Value);


                var pagedResult  = await query.Select(x => new AIEditPermissionDto
                {
                    PermissionID = x.p.PermissionID,
                    UserID = x.p.UserID,
                    UserName = x.u.FullName,
                    UserEmail = x.u.Email,
                    CountryID = x.p.CountryID,
                    CountryName = x.c.CountryName,
                    Year = x.p.Year,
                    Status = x.p.Status,
                    RequestedAt = x.p.RequestedAt,
                    GrantedBy = x.p.GrantedBy,
                    GrantedByName = x.GrantName,
                    GrantedAt = x.p.GrantedAt,
                    Notes = x.p.Notes,
                    ActiveSessionID = x.p.ActiveSessionID
                }).ApplyPaginationAsync(request);


                return pagedResult;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetPermissions", ex);
                return new PaginationResponse<AIEditPermissionDto>();
            }
        }

        public async Task<ResultResponseDto<AIEditSessionDto>> SubmitSession(int sessionID, int userID, UserRole userRole)
        {
            try
            {
                var session = await _context.AIEditSessions.FirstOrDefaultAsync(x => x.SessionID == sessionID);
                if (session == null)
                    return ResultResponseDto<AIEditSessionDto>.Failure(new[] { "Session not found." });

                if (userRole != UserRole.Admin && session.UserID != userID)
                    return ResultResponseDto<AIEditSessionDto>.Failure(new[] { "You can only submit your own draft session." });

                if (session.Status != AIEditSessionStatus.Draft)
                    return ResultResponseDto<AIEditSessionDto>.Failure(new[] { "Only draft sessions can be submitted." });

                var hasChanges = await _context.AIEditChangeLogs.AnyAsync(x => x.SessionID == sessionID);
                if (!hasChanges)
                    return ResultResponseDto<AIEditSessionDto>.Failure(new[] { "No draft changes to submit. Save country/pillar/question edits first." });

                session.Status = AIEditSessionStatus.Submitted;
                session.SubmittedAt = DateTime.UtcNow;

                var permission = await _context.AIEditPermissions.FirstOrDefaultAsync(x => x.PermissionID == session.PermissionID);
                if (permission != null && permission.Status == AIEditPermissionStatus.Active)
                    permission.Status = AIEditPermissionStatus.Consumed;

                await _context.SaveChangesAsync();

                var mapped = await MapSession(session.SessionID);
                return ResultResponseDto<AIEditSessionDto>.Success(mapped!, new[] { "Draft submitted for admin approval. Live AI response is unchanged until approved. Edit authority is now consumed." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in SubmitSession", ex);
                return ResultResponseDto<AIEditSessionDto>.Failure(new[] { "Failed to submit draft session." });
            }
        }

        public async Task<ResultResponseDto<bool>> ReviewSession(ReviewAIEditSessionDto dto, int adminUserID)
        {
            try
            {
                var session = await _context.AIEditSessions.FirstOrDefaultAsync(x => x.SessionID == dto.SessionID);
                if (session == null)
                    return ResultResponseDto<bool>.Failure(new[] { "Session not found." });

                if (session.Status != AIEditSessionStatus.Submitted && session.Status != AIEditSessionStatus.Draft)
                    return ResultResponseDto<bool>.Failure(new[] { "Session is not awaiting review." });

                session.ReviewedBy = adminUserID;
                session.ReviewedAt = DateTime.UtcNow;
                session.ReviewComment = dto.ReviewComment;

                if (!dto.Approve)
                {
                    session.Status = AIEditSessionStatus.Rejected;
                    var permission = await _context.AIEditPermissions.FirstOrDefaultAsync(x => x.PermissionID == session.PermissionID);
                    if (permission != null && permission.Status == AIEditPermissionStatus.Active)
                        permission.Status = AIEditPermissionStatus.Consumed;
                    await _context.SaveChangesAsync();
                    return ResultResponseDto<bool>.Success(true, new[] { "Draft rejected. Live AI response was not changed." });
                }

                var applyResult = await ApplySessionToLiveAsync(session.SessionID, adminUserID);
                if (!applyResult.Succeeded)
                    return applyResult;

                session.Status = AIEditSessionStatus.Approved;
                var perm = await _context.AIEditPermissions.FirstOrDefaultAsync(x => x.PermissionID == session.PermissionID);
                if (perm != null)
                    perm.Status = AIEditPermissionStatus.Consumed;

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Changes approved and published to live AI response." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in ReviewSession", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to review session." });
            }
        }

        public async Task<PaginationResponse<AIEditSessionDto>> GetSessions(AIEditSessionListRequestDto request)
        {
            try
            {
                var query = from s in _context.AIEditSessions
                            join u in _context.Users on s.UserID equals u.UserID
                            join c in _context.Countries on s.CountryID equals c.CountryID
                            join r in _context.Users on s.ReviewedBy equals r.UserID into rj
                            from r in rj.DefaultIfEmpty()
                            select new { s, u, c, ReviewerName = r != null ? r.FullName : null };

                if (request.CountryID.HasValue)
                    query = query.Where(x => x.s.CountryID == request.CountryID.Value);
                if (request.Year.HasValue)
                    query = query.Where(x => x.s.Year == request.Year.Value);
                if (request.UserId.HasValue && request.UserId > 0)
                    query = query.Where(x => x.s.UserID == request.UserId.Value);
                if (request.Status.HasValue)
                    query = query.Where(x => (byte)x.s.Status == request.Status.Value);

                var page = Math.Max(1, request.PageNumber);
                var size = Math.Clamp(request.PageSize, 1, 100);

                var rows = await query
                    .Select(x =>                    
                        new AIEditSessionDto
                        {
                            SessionID = x.s.SessionID,
                            PermissionID = x.s.PermissionID,
                            UserID = x.s.UserID,
                            UserName = x.u.FullName,
                            CountryID = x.s.CountryID,
                            CountryName = x.c.CountryName,
                            Year = x.s.Year,
                            Status = x.s.Status,
                            CreatedAt = x.s.CreatedAt,
                            SubmittedAt = x.s.SubmittedAt,
                            ReviewedBy = x.s.ReviewedBy,
                            ReviewedByName = x.ReviewerName,
                            ReviewedAt = x.s.ReviewedAt,
                            ReviewComment = x.s.ReviewComment,
                            ChangeCount =  0,
                            FieldCount =  0
                        }
                    )
                    .OrderByDescending(x => x.SessionID)
                    .ApplyPaginationAsync(request);

                var sessionIds = rows.Data.Select(x => x.SessionID).ToList();
                var allLogs = await _context.AIEditChangeLogs
                    .Where(x => x.SessionID.HasValue && sessionIds.Contains(x.SessionID.Value))
                    .Select(x => new { x.SessionID, x.EntityType, x.EntityRecordID, x.FieldName })
                    .ToListAsync();

                var statsMap = allLogs
                    .GroupBy(x => x.SessionID!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g => new
                        {
                            ChangeCount = g.Count(),
                            FieldCount = g.Select(c => $"{c.EntityType}|{c.EntityRecordID}|{c.FieldName}").Distinct().Count()
                        });

                foreach(var x in rows.Data)
                {
                    statsMap.TryGetValue(x.SessionID, out var stats);
                    x.ChangeCount = stats?.ChangeCount ?? 0;
                    x.FieldCount = stats?.FieldCount ?? 0;
                };

                return rows;
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetSessions", ex);
                return new PaginationResponse<AIEditSessionDto>();
            }
        }

        public async Task<ResultResponseDto<AIEditSessionDetailDto>> GetSessionDetail(int sessionID)
        {
            try
            {
                var session = await MapSession(sessionID);
                if (session == null)
                    return ResultResponseDto<AIEditSessionDetailDto>.Failure(new[] { "Session not found." });

                var logs = await (
                    from l in _context.AIEditChangeLogs
                    where l.SessionID == sessionID
                    join u in _context.Users on l.ChangedBy equals u.UserID
                    join p in _context.Pillars on l.PillarID equals p.PillarID into pj
                    from p in pj.DefaultIfEmpty()
                    join q in _context.Questions on l.QuestionID equals q.QuestionID into qj
                    from q in qj.DefaultIfEmpty()
                    orderby l.ChangedAt
                    select new { l, ChangedByName = u.FullName, PillarName = p != null ? p.PillarName : null, QuestionText = q != null ? q.QuestionText : null }
                ).ToListAsync();

                var compares = logs
                    .GroupBy(x => new { x.l.EntityType, x.l.EntityRecordID, x.l.FieldName, x.l.CountryID, x.l.Year, x.l.PillarID, x.l.QuestionID })
                    .Select(g =>
                    {
                        var ordered = g.OrderBy(x => x.l.ChangedAt).ToList();
                        var first = ordered.First();
                        var last = ordered.Last();
                        return new AIEditChangeCompareDto
                        {
                            EntityType = g.Key.EntityType,
                            EntityRecordID = g.Key.EntityRecordID,
                            CountryID = g.Key.CountryID,
                            Year = g.Key.Year,
                            PillarID = g.Key.PillarID,
                            PillarName = first.PillarName,
                            QuestionID = g.Key.QuestionID,
                            QuestionText = first.QuestionText,
                            FieldName = g.Key.FieldName,
                            BaselineValue = first.l.OldValue,
                            ProposedValue = last.l.NewValue,
                            EditCount = ordered.Count,
                            FirstChangedAt = first.l.ChangedAt,
                            LastChangedAt = last.l.ChangedAt,
                            Trail = ordered.Select(x => new AIEditChangeLogDto
                            {
                                ChangeLogID = x.l.ChangeLogID,
                                SessionID = x.l.SessionID,
                                EntityType = x.l.EntityType,
                                EntityRecordID = x.l.EntityRecordID,
                                CountryID = x.l.CountryID,
                                Year = x.l.Year,
                                PillarID = x.l.PillarID,
                                QuestionID = x.l.QuestionID,
                                FieldName = x.l.FieldName,
                                OldValue = x.l.OldValue,
                                NewValue = x.l.NewValue,
                                ChangedBy = x.l.ChangedBy,
                                ChangedByName = x.ChangedByName,
                                ChangedAt = x.l.ChangedAt,
                                SaveBatchID = x.l.SaveBatchID,
                                IsPublished = x.l.IsPublished,
                                ChangeSource = x.l.ChangeSource
                            }).ToList()
                        };
                    })
                    .OrderBy(x => x.EntityType)
                    .ThenBy(x => x.PillarID)
                    .ThenBy(x => x.QuestionID)
                    .ThenBy(x => x.FieldName)
                    .ToList();

                return ResultResponseDto<AIEditSessionDetailDto>.Success(new AIEditSessionDetailDto
                {
                    Session = session,
                    Changes = compares
                });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetSessionDetail", ex);
                return ResultResponseDto<AIEditSessionDetailDto>.Failure(new[] { "Failed to load session detail." });
            }
        }

        public async Task<ResultResponseDto<List<AIEditChangeLogDto>>> GetChangeHistory(AIEditHistoryRequestDto request)
        {
            try
            {
                var query = from l in _context.AIEditChangeLogs
                            join u in _context.Users on l.ChangedBy equals u.UserID
                            select new { l, ChangedByName = u.FullName };

                if (request.CountryID.HasValue)
                    query = query.Where(x => x.l.CountryID == request.CountryID.Value);
                if (request.Year.HasValue)
                    query = query.Where(x => x.l.Year == request.Year.Value);
                if (request.SessionID.HasValue)
                    query = query.Where(x => x.l.SessionID == request.SessionID.Value);
                if (request.UserID.HasValue)
                    query = query.Where(x => x.l.ChangedBy == request.UserID.Value);
                if (request.EntityType.HasValue)
                    query = query.Where(x => (byte)x.l.EntityType == request.EntityType.Value);

                var page = Math.Max(1, request.PageNumber);
                var size = Math.Clamp(request.PageSize, 1, 200);

                var rows = await query
                    .OrderByDescending(x => x.l.ChangedAt)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToListAsync();

                var result = rows.Select(x => new AIEditChangeLogDto
                {
                    ChangeLogID = x.l.ChangeLogID,
                    SessionID = x.l.SessionID,
                    EntityType = x.l.EntityType,
                    EntityRecordID = x.l.EntityRecordID,
                    CountryID = x.l.CountryID,
                    Year = x.l.Year,
                    PillarID = x.l.PillarID,
                    QuestionID = x.l.QuestionID,
                    FieldName = x.l.FieldName,
                    OldValue = x.l.OldValue,
                    NewValue = x.l.NewValue,
                    ChangedBy = x.l.ChangedBy,
                    ChangedByName = x.ChangedByName,
                    ChangedAt = x.l.ChangedAt,
                    SaveBatchID = x.l.SaveBatchID,
                    IsPublished = x.l.IsPublished,
                    ChangeSource = x.l.ChangeSource
                }).ToList();

                return ResultResponseDto<List<AIEditChangeLogDto>>.Success(result);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in GetChangeHistory", ex);
                return ResultResponseDto<List<AIEditChangeLogDto>>.Failure(new[] { "Failed to load change history." });
            }
        }

        public async Task<ResultResponseDto<bool>> SaveAnalystDraftAsync(
            int userID,
            int countryID,
            int year,
            AIEditEntityType entityType,
            int entityRecordID,
            int? pillarID,
            int? questionID,
            IReadOnlyDictionary<string, (string? OldValue, string? NewValue)> fieldChanges)
        {
            try
            {
                var permission = await _context.AIEditPermissions
                    .Where(x => x.UserID == userID && x.CountryID == countryID && x.Year == year && x.Status == AIEditPermissionStatus.Active)
                    .OrderByDescending(x => x.PermissionID)
                    .FirstOrDefaultAsync();

                if (permission == null)
                    return ResultResponseDto<bool>.Failure(new[] { "No active edit permission. Request access or ask admin to grant editable authority." });

                AIEditSession? session = null;
                if (permission.ActiveSessionID.HasValue)
                {
                    session = await _context.AIEditSessions.FirstOrDefaultAsync(x =>
                        x.SessionID == permission.ActiveSessionID.Value &&
                        x.Status == AIEditSessionStatus.Draft);
                }

                if (session == null)
                {
                    session = new AIEditSession
                    {
                        PermissionID = permission.PermissionID,
                        UserID = userID,
                        CountryID = countryID,
                        Year = year,
                        Status = AIEditSessionStatus.Draft,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AIEditSessions.Add(session);
                    await _context.SaveChangesAsync();
                    permission.ActiveSessionID = session.SessionID;
                }

                var meaningful = fieldChanges
                    .Where(kv => !string.Equals(Normalize(kv.Value.OldValue), Normalize(kv.Value.NewValue), StringComparison.Ordinal))
                    .ToList();

                if (meaningful.Count == 0)
                    return ResultResponseDto<bool>.Success(true, new[] { "No field changes detected." });

                // If analyst re-saves the same field, chain OldValue from previous draft NewValue so trail is clear.
                var previousLogs = await _context.AIEditChangeLogs
                    .Where(x => x.SessionID == session.SessionID &&
                                x.EntityType == entityType &&
                                x.EntityRecordID == entityRecordID)
                    .OrderByDescending(x => x.ChangedAt)
                    .ToListAsync();

                var previousMap = previousLogs
                    .GroupBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().NewValue, StringComparer.OrdinalIgnoreCase);

                var batchId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                foreach (var change in meaningful)
                {
                    var oldValue = previousMap.TryGetValue(change.Key, out var prev)
                        ? prev
                        : change.Value.OldValue;

                    if (string.Equals(Normalize(oldValue), Normalize(change.Value.NewValue), StringComparison.Ordinal))
                        continue;

                    _context.AIEditChangeLogs.Add(new AIEditChangeLog
                    {
                        SessionID = session.SessionID,
                        EntityType = entityType,
                        EntityRecordID = entityRecordID,
                        CountryID = countryID,
                        Year = year,
                        PillarID = pillarID,
                        QuestionID = questionID,
                        FieldName = change.Key,
                        OldValue = oldValue,
                        NewValue = change.Value.NewValue,
                        ChangedBy = userID,
                        ChangedAt = now,
                        SaveBatchID = batchId,
                        IsPublished = false,
                        ChangeSource = "AnalystDraft"
                    });
                }

                await _context.SaveChangesAsync();
                return ResultResponseDto<bool>.Success(true, new[] { "Draft saved. Live AI response is unchanged until you submit and admin approves. You can continue editing pillar/question screens in the same session." });
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in SaveAnalystDraftAsync", ex);
                return ResultResponseDto<bool>.Failure(new[] { "Failed to save draft changes." });
            }
        }

        public async Task LogAdminPublishAsync(
            int adminUserID,
            int countryID,
            int year,
            AIEditEntityType entityType,
            int entityRecordID,
            int? pillarID,
            int? questionID,
            IReadOnlyDictionary<string, (string? OldValue, string? NewValue)> fieldChanges)
        {
            try
            {
                var meaningful = fieldChanges
                    .Where(kv => !string.Equals(Normalize(kv.Value.OldValue), Normalize(kv.Value.NewValue), StringComparison.Ordinal))
                    .ToList();
                if (meaningful.Count == 0)
                    return;

                var batchId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                foreach (var change in meaningful)
                {
                    _context.AIEditChangeLogs.Add(new AIEditChangeLog
                    {
                        SessionID = null,
                        EntityType = entityType,
                        EntityRecordID = entityRecordID,
                        CountryID = countryID,
                        Year = year,
                        PillarID = pillarID,
                        QuestionID = questionID,
                        FieldName = change.Key,
                        OldValue = change.Value.OldValue,
                        NewValue = change.Value.NewValue,
                        ChangedBy = adminUserID,
                        ChangedAt = now,
                        SaveBatchID = batchId,
                        IsPublished = true,
                        ChangeSource = "AdminDirect"
                    });
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error in LogAdminPublishAsync", ex);
            }
        }

        private async Task<ResultResponseDto<bool>> ApplySessionToLiveAsync(int sessionID, int adminUserID)
        {
            var logs = await _context.AIEditChangeLogs
                .Where(x => x.SessionID == sessionID && !x.IsPublished)
                .OrderBy(x => x.ChangedAt)
                .ToListAsync();

            if (logs.Count == 0)
                return ResultResponseDto<bool>.Failure(new[] { "No unpublished draft changes found." });

            var latestByField = logs
                .GroupBy(x => new { x.EntityType, x.EntityRecordID, x.FieldName })
                .Select(g => g.OrderByDescending(x => x.ChangedAt).First())
                .GroupBy(x => new { x.EntityType, x.EntityRecordID })
                .ToList();

            foreach (var entityGroup in latestByField)
            {
                var entityType = entityGroup.Key.EntityType;
                var entityId = entityGroup.Key.EntityRecordID;
                var fields = entityGroup.ToDictionary(x => x.FieldName, x => x.NewValue, StringComparer.OrdinalIgnoreCase);

                switch (entityType)
                {
                    case AIEditEntityType.Country:
                        {
                            var entity = await _context.AICountryScores.FirstOrDefaultAsync(x => x.CountryScoreID == entityId);
                            if (entity == null) break;
                            ApplyFields(entity, fields);
                            entity.UpdatedAt = DateTime.UtcNow;
                            break;
                        }
                    case AIEditEntityType.Pillar:
                        {
                            var entity = await _context.AIPillarScores.FirstOrDefaultAsync(x => x.PillarScoreID == entityId);
                            if (entity == null) break;
                            ApplyFields(entity, fields);
                            entity.UpdatedAt = DateTime.UtcNow;
                            break;
                        }
                    case AIEditEntityType.Question:
                        {
                            var entity = await _context.AIEstimatedQuestionScores.FirstOrDefaultAsync(x => x.QuestionScoreID == entityId);
                            if (entity == null) break;
                            ApplyFields(entity, fields);
                            if (fields.ContainsKey(nameof(AIEstimatedQuestionScore.AIScore)))
                                entity.Discrepancy = CalculateDiscrepancy(entity.EvaluatorScore, entity.AIScore);
                            entity.UpdatedAt = DateTime.UtcNow;
                            break;
                        }
                    case AIEditEntityType.Citation:
                        {
                            var entity = await _context.AIDataSourceCitations.FirstOrDefaultAsync(x => x.CitationID == entityId);
                            if (entity == null) break;
                            ApplyFields(entity, fields);
                            break;
                        }
                }
            }

            foreach (var log in logs)
            {
                log.IsPublished = true;
                log.ChangeSource = "PublishApply";
            }

            // Keep an apply marker for audit clarity
            _context.AIEditChangeLogs.Add(new AIEditChangeLog
            {
                SessionID = sessionID,
                EntityType = AIEditEntityType.Country,
                EntityRecordID = logs.First().EntityRecordID,
                CountryID = logs.First().CountryID,
                Year = logs.First().Year,
                FieldName = "__SessionApproved__",
                OldValue = "Draft",
                NewValue = "Published",
                ChangedBy = adminUserID,
                ChangedAt = DateTime.UtcNow,
                SaveBatchID = Guid.NewGuid(),
                IsPublished = true,
                ChangeSource = "PublishApply"
            });

            await _context.SaveChangesAsync();
            return ResultResponseDto<bool>.Success(true);
        }

        private static void ApplyFields(object entity, Dictionary<string, string?> fields)
        {
            var type = entity.GetType();
            foreach (var kv in fields)
            {
                if (kv.Key.StartsWith("__", StringComparison.Ordinal))
                    continue;

                var prop = type.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite)
                    continue;

                try
                {
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (kv.Value == null)
                    {
                        if (Nullable.GetUnderlyingType(prop.PropertyType) != null || !targetType.IsValueType)
                            prop.SetValue(entity, null);
                        continue;
                    }

                    object? converted;
                    if (targetType == typeof(string))
                        converted = kv.Value;
                    else if (targetType == typeof(decimal))
                        converted = decimal.Parse(kv.Value);
                    else if (targetType == typeof(int))
                        converted = int.Parse(kv.Value);
                    else if (targetType == typeof(bool))
                        converted = bool.Parse(kv.Value);
                    else if (targetType == typeof(DateTime))
                        converted = DateTime.Parse(kv.Value);
                    else
                        converted = Convert.ChangeType(kv.Value, targetType);

                    prop.SetValue(entity, converted);
                }
                catch
                {
                    // Skip invalid conversion for a single field rather than failing the whole publish.
                }
            }
        }

        private static decimal? CalculateDiscrepancy(decimal? evaluatorScore, decimal? aiProgress)
        {
            if (!evaluatorScore.HasValue && !aiProgress.HasValue)
                return null;
            return Math.Abs((evaluatorScore ?? 0) - (aiProgress ?? 0));
        }

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();

        private async Task<AIEditPermissionDto?> MapPermission(int permissionID)
        {
            var row = await (
                from p in _context.AIEditPermissions
                where p.PermissionID == permissionID
                join u in _context.Users on p.UserID equals u.UserID
                join c in _context.Countries on p.CountryID equals c.CountryID
                join g in _context.Users on p.GrantedBy equals g.UserID into gj
                from g in gj.DefaultIfEmpty()
                select new AIEditPermissionDto
                {
                    PermissionID = p.PermissionID,
                    UserID = p.UserID,
                    UserName = u.FullName,
                    UserEmail = u.Email,
                    CountryID = p.CountryID,
                    CountryName = c.CountryName,
                    Year = p.Year,
                    Status = p.Status,
                    RequestedAt = p.RequestedAt,
                    GrantedBy = p.GrantedBy,
                    GrantedByName = g != null ? g.FullName : null,
                    GrantedAt = p.GrantedAt,
                    Notes = p.Notes,
                    ActiveSessionID = p.ActiveSessionID
                }).FirstOrDefaultAsync();
            return row;
        }

        private async Task<AIEditSessionDto?> MapSession(int sessionID)
        {
            var row = await (
                from s in _context.AIEditSessions
                where s.SessionID == sessionID
                join u in _context.Users on s.UserID equals u.UserID
                join c in _context.Countries on s.CountryID equals c.CountryID
                join r in _context.Users on s.ReviewedBy equals r.UserID into rj
                from r in rj.DefaultIfEmpty()
                select new { s, UserName = u.FullName, CountryName = c.CountryName, ReviewerName = r != null ? r.FullName : null }
            ).FirstOrDefaultAsync();

            if (row == null)
                return null;

            var logsForSession = await _context.AIEditChangeLogs
                .Where(x => x.SessionID == sessionID)
                .Select(x => new { x.EntityType, x.EntityRecordID, x.FieldName })
                .ToListAsync();

            var changeCount = logsForSession.Count;
            var fieldCount = logsForSession
                .Select(x => $"{x.EntityType}|{x.EntityRecordID}|{x.FieldName}")
                .Distinct()
                .Count();

            return new AIEditSessionDto
            {
                SessionID = row.s.SessionID,
                PermissionID = row.s.PermissionID,
                UserID = row.s.UserID,
                UserName = row.UserName,
                CountryID = row.s.CountryID,
                CountryName = row.CountryName,
                Year = row.s.Year,
                Status = row.s.Status,
                CreatedAt = row.s.CreatedAt,
                SubmittedAt = row.s.SubmittedAt,
                ReviewedBy = row.s.ReviewedBy,
                ReviewedByName = row.ReviewerName,
                ReviewedAt = row.s.ReviewedAt,
                ReviewComment = row.s.ReviewComment,
                ChangeCount = changeCount,
                FieldCount = fieldCount
            };
        }
    }
}
