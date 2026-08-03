using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<Location>> GetAllLocationsAsync();

        Task<Location?> GetLocationByIdAsync(int id);


        Task<Location> CreateLocationAsync(Location location);

        Task<bool> UpdateLocationAsync(int id, Location location);

        Task<bool> DeleteLocationAsync(int id);


    }
}