using GamePort.Cashier.Domain.Entities;

namespace GamePort.Cashier.Application.Interfaces;

public interface IAttendanceRepository
{
    Task<AttendanceRecord?> GetOpenByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AttendanceRecord>> GetByEmployeeAsync(Guid employeeId, int limit);
    Task<IEnumerable<AttendanceRecord>> GetByDateAsync(DateTime date);
    Task<AttendanceRecord> CreateAsync(AttendanceRecord record);
    Task UpdateAsync(AttendanceRecord record);
}
