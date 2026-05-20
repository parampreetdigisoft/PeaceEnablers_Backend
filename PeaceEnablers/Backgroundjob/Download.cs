using PeaceEnablers.Models;
using DocumentFormat.OpenXml.Office2010.Excel;

namespace PeaceEnablers.Backgroundjob
{
    public class Download
    {
        private readonly ChannelService channelService;
        public Download(ChannelService channelService) 
        {
            this.channelService = channelService;
        }
        public string Type { get; set; } = string.Empty;
        public int? UserID { get; set; }
        public int? CountryID { get; set; }
        public bool CountryEnable { get; set; }
        public bool PillarEnable { get; set; }
        public bool QuestionEnable { get; set; }
        public bool ImmediateSummaryEnable { get; set; }
        public bool RegenerateMissingQuestionsEnable { get; set; }
        public string InsertAnalyticalLayerResults(int countryID = 0)
        {
            CountryID = countryID;
            Type = "InsertAnalyticalLayerResults";
            channelService.Write(this);
            return "Execution has been started";
        }

        public Task AiResearchByCountryId(int countryID , bool countryEnable,bool pillarEnable, bool questionEnable,bool immediateSummaryEnable = false, bool regenerateMissingQuestionsEnable = false)
        {
            this.CountryID = countryID;
            this.CountryEnable = countryEnable;
            this.PillarEnable = pillarEnable;
            this.QuestionEnable = questionEnable;
            this.ImmediateSummaryEnable = immediateSummaryEnable;
            this.RegenerateMissingQuestionsEnable = regenerateMissingQuestionsEnable;
            Type = "AiResearchByCountryId";
            channelService.Write(this);
            return Task.CompletedTask;
        }
    }
}
