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
        public string InsertAnalyticalLayerResults(int countryID = 0)
        {
            CountryID = countryID;
            Type = "InsertAnalyticalLayerResults";
            channelService.Write(this);
            return "Execution has been started";
        }

        public Task AiResearchByCountryId(int countryID , bool countryEnable,bool pillarEnable, bool questionEnable)
        {
            this.CountryID = countryID;
            this.CountryEnable = countryEnable;
            this.PillarEnable = pillarEnable;
            this.QuestionEnable = questionEnable;
            Type = "AiResearchByCountryId";
            channelService.Write(this);
            return Task.CompletedTask;
        }
    }
}
