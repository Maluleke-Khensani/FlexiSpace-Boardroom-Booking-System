using FlexiSpace.Core.DTOs.Location;
using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Services
{
    public interface ILocationCalendarAccountService
    {
        Task<LocationCalendarAccount> CreateAsync(LocationCalendarAccountCreateDto dto);
    }
}
