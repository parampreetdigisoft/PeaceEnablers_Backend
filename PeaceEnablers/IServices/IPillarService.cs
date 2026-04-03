using PeaceEnablers.Common.Models;
using PeaceEnablers.Dtos.AssessmentDto;
using PeaceEnablers.Dtos.CommonDto;
using PeaceEnablers.Dtos.PillarDto;
using PeaceEnablers.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeaceEnablers.IServices
{
    public interface IPillarService
    {
        Task<List<Pillar>> GetAllAsync();
        Task<Pillar> GetByIdAsync(int id);
        Task<Pillar> AddAsync(Pillar pillar);
        Task<Pillar> UpdateAsync(int id, UpdatePillarDto pillar);
        Task<bool> DeleteAsync(int id);
        Task<Tuple<string, byte[]>> ExportPillarsHistoryByUserId(GetCountryPillarHistoryRequestDto requestDto);
        Task<PaginationResponse<PillarsHistroyResponseDto>> GetResponsesByUserId(GetPillarResponseHistoryRequestNewDto request, UserRole userRole);

    }
} 