using AssessmentPlatform.Models;

using Microsoft.EntityFrameworkCore;

using PeaceEnablers.Dtos.CountryDto;
using PeaceEnablers.Models;

namespace PeaceEnablers.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = default!;
        public DbSet<Pillar> Pillars { get; set; } = default!;
        public DbSet<Question> Questions { get; set; } = default!;
        public DbSet<QuestionOption> QuestionOptions { get; set; } = default!;
        public DbSet<AssessmentResponse> AssessmentResponses { get; set; } = default!;
        public DbSet<Assessment> Assessments { get; set; } = default!;
        public DbSet<PillarAssessment> PillarAssessments { get; set; } = default!;
        public DbSet<Country> Countries { get; set; } = default!;
        public DbSet<UserCountryMapping> UserCountryMappings { get; set; } = default!;
        public DbSet<AppLogs> AppLogs { get; set; } = default!;
        public DbSet<PaymentRecord> PaymentRecords { get; set; } = default!;
        public DbSet<PublicUserCountryMapping> PublicUserCountryMappings { get; set; } = default!;
        public DbSet<AnalyticalLayer> AnalyticalLayers { get; set; } = default!;
        public DbSet<FiveLevelInterpretation> FiveLevelInterpretations { get; set; } = default!;
        public DbSet<AnalyticalLayerResult> AnalyticalLayerResults { get; set; } = default!;
        public DbSet<CountryUserPillarMapping> CountryUserPillarMappings { get; set; } = default!;
        public DbSet<AIDataSourceCitation> AIDataSourceCitations { get; set; } = default!;
        public DbSet<AICountryScore> AICountryScores { get; set; } = default!;
        public DbSet<AIEstimatedQuestionScore> AIEstimatedQuestionScores { get; set; } = default!;
        public DbSet<AIPillarScore> AIPillarScores { get; set; } = default!;
        public DbSet<AITrustLevel> AITrustLevels { get; set; } = default!;
        public DbSet<AnalyticalLayerPillarMapping> AnalyticalLayerPillarMappings { get; set; } = default!;
        public DbSet<EvaluationCountryProgressResultDto> CountryProgressResults { get; set; }
        public DbSet<GetCountriesProgressAdminDto> GetCountriesProgressAdminDto { get; set; }
        public DbSet<AIUserCountryMapping> AIUserCountryMappings { get; set; }
        public DbSet<CountryPeer> CountryPeers { get; set; } = default!;
        public DbSet<EvaluationCountryProgressHistoryResultDto> CountryProgressHistoryResults { get; set; }
        public DbSet<CountryDocument> CountryDocuments { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasKey(ur => ur.UserID);

            modelBuilder.Entity<Pillar>().HasKey(uc => uc.PillarID);
            modelBuilder.Entity<Pillar>()
                .HasMany(q => q.Questions)
                .WithOne(qo => qo.Pillar)
                .HasForeignKey(qo => qo.PillarID)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Question>().HasKey(uc => uc.QuestionID);
            modelBuilder.Entity<QuestionOption>().HasKey(qo => qo.OptionID);

            modelBuilder.Entity<Question>()
                .HasMany(q => q.QuestionOptions)
                .WithOne(qo => qo.Question)
                .HasForeignKey(qo => qo.QuestionID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Assessment>().HasKey(uc => uc.AssessmentID);
            modelBuilder.Entity<AssessmentResponse>().HasKey(uc => uc.ResponseID);
            modelBuilder.Entity<PillarAssessment>().HasKey(uc => uc.PillarAssessmentID);

            modelBuilder.Entity<Assessment>()
                .HasMany(r => r.PillarAssessments)
                .WithOne(a=>a.Assessment)
                .HasForeignKey(r => r.AssessmentID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PillarAssessment>()
            .HasMany(r => r.Responses)
            .WithOne(a => a.PillarAssessment)
            .HasForeignKey(r => r.PillarAssessmentID)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Country>().HasKey(uc => uc.CountryID);
            modelBuilder.Entity<PaymentRecord>(entity =>
            {
                entity.HasKey(p => p.PaymentRecordID);
                entity.Property(e => e.Tier)
                      .HasConversion<byte>();

                entity.Property(e => e.PaymentStatus)
                      .HasConversion<byte>();
            });

            modelBuilder.Entity<UserCountryMapping>().HasKey(uc => uc.UserCountryMappingID);
            modelBuilder.Entity<PublicUserCountryMapping>().HasKey(uc => uc.PublicUserCountryMappingID);

            modelBuilder.Entity<AnalyticalLayer>(entity =>
            {
                entity.HasKey(al => al.LayerID);

                entity.HasMany(al=>al.AnalyticalLayerResults)
                .WithOne(x=>x.AnalyticalLayer)
                .HasForeignKey(x=>x.LayerID);

                entity.HasMany(al => al.FiveLevelInterpretations)
               .WithOne(x => x.AnalyticalLayer)
               .HasForeignKey(x => x.LayerID);
            });
            modelBuilder.Entity<AnalyticalLayerResult>(entity =>
            {
                entity.HasKey(al => al.LayerResultID);
            });
            modelBuilder.Entity<FiveLevelInterpretation>(entity =>
            {
                entity.HasKey(al => al.InterpretationID);
            });
            modelBuilder.Entity<CountryUserPillarMapping>().HasKey(ur => ur.CountryUserPillarMappingID);

            modelBuilder.Entity<AIDataSourceCitation>().HasKey(ur => ur.CitationID);
            modelBuilder.Entity<AICountryScore>(entity =>
            {
                entity.HasKey(e => e.CountryScoreID);
            });
            modelBuilder.Entity<AIEstimatedQuestionScore>().HasKey(ur => ur.QuestionScoreID);
            modelBuilder.Entity<AIPillarScore>().HasKey(ur => ur.PillarScoreID);
            modelBuilder.Entity<AITrustLevel>().HasKey(ur => ur.TrustID);
            modelBuilder.Entity<AnalyticalLayerPillarMapping>().HasKey(ur => ur.AnalyticalLayerPillarMappingID);
            modelBuilder.Entity<AIUserCountryMapping>().HasKey(ur => ur.AIUserCountryMappingID);
            modelBuilder.Entity<EvaluationCountryProgressResultDto>().HasNoKey().ToView(null); 
            modelBuilder.Entity<GetCountriesProgressAdminDto>().HasNoKey().ToView(null);
            modelBuilder.Entity<EvaluationCountryProgressHistoryResultDto>().HasNoKey().ToView(null);
            modelBuilder.Entity<CountryPeer>(entity =>
            {
                entity.HasKey(e => e.CountryPeerID);
                entity.ToTable("CountryPeer");
            });

            modelBuilder.Entity<CountryDocument>(entity =>
            {
                entity.HasKey(e => e.CountryDocumentID);
                entity.ToTable("CountryDocuments");
            });
            base.OnModelCreating(modelBuilder);
        }

    }
}
