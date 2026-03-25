using PeaceEnablers.Backgroundjob;
using PeaceEnablers.Backgroundjob.logging;
using PeaceEnablers.Common.Implementation;
using PeaceEnablers.Common.Interface;
using PeaceEnablers.IServices;
using PeaceEnablers.Services;

namespace PeaceEnablers.Common.DI
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
        {
            services.AddHostedService<ChannelWorker>();
            //services.AddHostedService<AiJobService>();
            services.AddScoped<Download>();
            services.AddHostedService<LogWorker>();
            // Channels
            services.AddSingleton<ChannelService>();
            services.AddSingleton<LogChannelService>();
            services.AddScoped<IAppLogger, AppLogger>();


            services.AddScoped<IAIAnalyzeService, AIAnalyzeService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPillarService, PillarService>();
            services.AddScoped<IAssessmentResponseService, AssessmentResponseService>();
            services.AddScoped<ICityService, CityService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICityUserService, CityUserService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IPublicService, PublicService>();
            services.AddScoped<IKpiService, KpiService>();
            services.AddScoped<IAIComputationService, AIComputationService>();
            services.AddScoped<ICommonService, CommonService>();
            services.AddScoped<Interface.IPdfGeneratorService, Implementation.PdfGeneratorService>();
            services.AddScoped<IDocxGeneratorService, DocxGeneratorService>();
            services.AddScoped<IDocumentGeneratorService, DocumentGeneratorService>();
            return services;
        }
    }
}
