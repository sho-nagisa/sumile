using System.Collections.Generic;
using System.Threading.Tasks;
using sumile.Models;

namespace sumile.Services
{
    public interface IShiftService
    {
        Task<IEnumerable<Shift>> GetAllShiftsAsync();
        Task<Shift> GetShiftByIdAsync(int id);
        Task CreateShiftAsync(Shift shift);
        Task UpdateShiftAsync(Shift shift);
        Task DeleteShiftAsync(int id);
    }
}
