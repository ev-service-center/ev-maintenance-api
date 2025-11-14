using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.Services
{
    public class AppointmentSlotGeneratorService
    {
        private readonly EvserviceCenterDbContext _context;
        private readonly ILogger<AppointmentSlotGeneratorService> _logger;

        public AppointmentSlotGeneratorService(
            EvserviceCenterDbContext context,
            ILogger<AppointmentSlotGeneratorService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task GenerateSlotsForNext7DaysAsync()
        {
            try
            {
                var today = TimeZoneHelper.TodayInVietnam;

                // Tạo slots cho 7 ngày tới (bao gồm cả hôm nay)
                // Rolling window: Mỗi ngày tạo thêm 1 ngày mới vào cuối
                var candidateDates = Enumerable.Range(0, 7)
                    .Select(i => today.AddDays(i))
                    .ToArray();

                // Chỉ tạo slot cho các ngày từ Monday đến Saturday (bỏ qua Sunday)
                var datesToGenerate = candidateDates
                    .Where(ShouldGenerateSlotForDate)
                    .ToArray();

                var skippedDates = candidateDates.Except(datesToGenerate).ToArray();

                if (skippedDates.Length != 0)
                {
                    _logger.LogInformation("Bỏ qua các ngày Chủ nhật: {dates}",
                        string.Join(", ", skippedDates.Select(d => $"{d:dd/MM} ({d.DayOfWeek})")));
                }

                if (datesToGenerate.Length == 0)
                {
                    _logger.LogInformation("Không có ngày nào cần tạo slot (đã bỏ qua tất cả các ngày Chủ nhật)");
                    return;
                }

                _logger.LogInformation("Sẽ tạo slot cho 7 ngày tới: {dates}",
                    string.Join(", ", datesToGenerate.Select(d => $"{d:dd/MM} ({d.DayOfWeek})")));

                // Lấy danh sách trung tâm
                var centerIds = await _context.ServiceCenters
                    .Select(c => c.CenterId)
                    .ToListAsync();

                if (centerIds.Count == 0)
                {
                    _logger.LogWarning("Không tìm thấy trung tâm dịch vụ nào để tạo slot");
                    return;
                }

                _logger.LogInformation("Tìm thấy {count} trung tâm dịch vụ", centerIds.Count);

                var durationMinutes = (int)SlotTimeConfig.DefaultDuration;
                var allSlotsToCreate = new List<AppointmentSlot>();

                // Lấy tất cả các slot đã tồn tại để tránh query lặp lại
                var existingSlots = await _context.AppointmentSlots
                    .Where(s => datesToGenerate.Contains(s.StartTime.Date))
                    .Select(s => new { s.CenterId, s.StartTime.Date })
                    .ToListAsync();

                _logger.LogInformation("Tìm thấy {count} slot đã tồn tại cho các ngày cần tạo", existingSlots.Count);

                foreach (var centerId in centerIds)
                {
                    foreach (var date in datesToGenerate)
                    {
                        // Kiểm tra trong memory thay vì query database
                        if (existingSlots.Any(s => s.CenterId == centerId && s.Date == date.Date))
                        {
                            _logger.LogDebug("Bỏ qua trung tâm {centerId} ngày {date} - đã có slot",
                                centerId, date.ToString("dd/MM/yyyy"));
                            continue;
                        }

                        var slotsForDate = GenerateSlotsForDate(centerId, date, durationMinutes);
                        allSlotsToCreate.AddRange(slotsForDate);

                        _logger.LogInformation("Sẽ tạo {count} slot cho trung tâm {centerId} ngày {date} ({dayOfWeek})",
                            slotsForDate.Count, centerId, date.ToString("dd/MM/yyyy"), date.DayOfWeek);
                    }
                }

                if (allSlotsToCreate.Count != 0)
                {
                    _context.AppointmentSlots.AddRange(allSlotsToCreate);
                    var savedCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("Đã lưu thành công {count} slot đặt lịch vào database", savedCount);
                }
                else
                {
                    _logger.LogInformation("Không có slot mới nào cần tạo - tất cả đã tồn tại");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot đặt lịch");
                throw;
            }
        }

        private List<AppointmentSlot> GenerateSlotsForDate(int centerId, DateTime date, int durationMinutes)
        {
            var slots = new List<AppointmentSlot>();
            var dayStart = date.Add(SlotTimeConfig.StartTimeOfDay);
            var dayEnd = date.Add(SlotTimeConfig.GetLastSlotEndTime());
            var current = dayStart;
            var now = DateTime.UtcNow;

            while (current.AddMinutes(durationMinutes) <= dayEnd)
            {
                var slotStart = current.TimeOfDay;
                var slotEnd = current.AddMinutes(durationMinutes).TimeOfDay;

                // Bỏ qua slot nếu overlap với giờ nghỉ trưa (11h-13h)
                if (SlotTimeConfig.IsOverlapWithLunchBreak(slotStart, slotEnd))
                {
                    _logger.LogDebug("Bỏ qua slot {start}-{end} vì trùng giờ nghỉ trưa",
                        current.ToString("HH:mm"), current.AddMinutes(durationMinutes).ToString("HH:mm"));
                    current = current.AddMinutes(durationMinutes);
                    continue;
                }

                var startTime = DateTime.SpecifyKind(current, DateTimeKind.Unspecified);
                var endTime = DateTime.SpecifyKind(current.AddMinutes(durationMinutes), DateTimeKind.Unspecified);

                slots.Add(new AppointmentSlot
                {
                    CenterId = centerId,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsAvailable = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                current = current.AddMinutes(durationMinutes);
            }

            return slots;
        }

        private bool ShouldGenerateSlotForDate(DateTime date)
        {
            return date.DayOfWeek != DayOfWeek.Sunday;
        }

        public async Task GenerateSlotsForSpecificDateAsync(DateTime targetDate)
        {
            try
            {
                // Kiểm tra xem có nên tạo slot cho ngày này không
                if (!ShouldGenerateSlotForDate(targetDate))
                {
                    _logger.LogWarning("Không thể tạo slot cho ngày {date} vì là Chủ nhật",
                        targetDate.ToString("dd/MM/yyyy"));
                    return;
                }

                // Lấy danh sách trung tâm
                var centerIds = await _context.ServiceCenters
                    .Select(c => c.CenterId)
                    .ToListAsync();

                if (centerIds.Count == 0)
                {
                    _logger.LogWarning("Không tìm thấy trung tâm dịch vụ nào để tạo slot");
                    return;
                }

                _logger.LogInformation("Tìm thấy {count} trung tâm dịch vụ", centerIds.Count);

                var durationMinutes = (int)SlotTimeConfig.DefaultDuration;
                var allSlotsToCreate = new List<AppointmentSlot>();

                // Lấy các slot đã tồn tại cho ngày cụ thể
                var existingSlots = await _context.AppointmentSlots
                    .Where(s => s.StartTime.Date == targetDate.Date)
                    .Select(s => new { s.CenterId, Date = s.StartTime.Date })
                    .ToListAsync();

                _logger.LogInformation("Tìm thấy {count} slot đã tồn tại cho ngày {date}",
                    existingSlots.Count, targetDate.ToString("dd/MM/yyyy"));

                foreach (var centerId in centerIds)
                {
                    // Kiểm tra trong memory thay vì query database
                    if (existingSlots.Any(s => s.CenterId == centerId && s.Date == targetDate.Date))
                    {
                        _logger.LogDebug("Bỏ qua trung tâm {centerId} - đã có slot", centerId);
                        continue;
                    }

                    var slotsForDate = GenerateSlotsForDate(centerId, targetDate, durationMinutes);
                    allSlotsToCreate.AddRange(slotsForDate);

                    _logger.LogInformation("Sẽ tạo {count} slot cho trung tâm {centerId} ngày {date} ({dayOfWeek})",
                        slotsForDate.Count, centerId, targetDate.ToString("dd/MM/yyyy"), targetDate.DayOfWeek);
                }

                if (allSlotsToCreate.Count != 0)
                {
                    _context.AppointmentSlots.AddRange(allSlotsToCreate);
                    var savedCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("✓ Đã lưu thành công {count} slot cho ngày {date} ({dayOfWeek})",
                        savedCount, targetDate.ToString("dd/MM/yyyy"), targetDate.DayOfWeek);
                }
                else
                {
                    _logger.LogInformation("Không có slot mới nào cần tạo cho ngày {date} - tất cả đã tồn tại",
                        targetDate.ToString("dd/MM/yyyy"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho ngày {date}", targetDate.ToString("dd/MM/yyyy"));
                throw;
            }
        }

        public async Task GenerateSlotsForDateAndCenterAsync(DateTime targetDate, int centerId)
        {
            try
            {
                // Kiểm tra xem có nên tạo slot cho ngày này không
                if (!ShouldGenerateSlotForDate(targetDate))
                {
                    _logger.LogWarning("Không thể tạo slot cho ngày {date} vì là Chủ nhật",
                        targetDate.ToString("dd/MM/yyyy"));
                    return;
                }

                _logger.LogInformation("Bắt đầu tạo slot cho trung tâm {centerId} ngày {date}",
                    centerId, targetDate.ToString("dd/MM/yyyy"));

                var durationMinutes = (int)SlotTimeConfig.DefaultDuration;

                // Kiểm tra xem center này đã có slot cho ngày này chưa
                var existingSlots = await _context.AppointmentSlots
                    .Where(s => s.CenterId == centerId && s.StartTime.Date == targetDate.Date)
                    .AnyAsync();

                if (existingSlots)
                {
                    _logger.LogInformation("Trung tâm {centerId} đã có slot cho ngày {date} - bỏ qua",
                        centerId, targetDate.ToString("dd/MM/yyyy"));
                    return;
                }

                var slotsForDate = GenerateSlotsForDate(centerId, targetDate, durationMinutes);

                if (slotsForDate.Count != 0)
                {
                    _context.AppointmentSlots.AddRange(slotsForDate);
                    var savedCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("✓ Đã lưu thành công {count} slot cho trung tâm {centerId} ngày {date} ({dayOfWeek})",
                        savedCount, centerId, targetDate.ToString("dd/MM/yyyy"), targetDate.DayOfWeek);
                }
                else
                {
                    _logger.LogInformation("Không có slot nào được tạo cho trung tâm {centerId} ngày {date}",
                        centerId, targetDate.ToString("dd/MM/yyyy"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho trung tâm {centerId} ngày {date}",
                    centerId, targetDate.ToString("dd/MM/yyyy"));
                throw;
            }
        }

        public async Task GenerateSlotsForNext7DaysForCenterAsync(int centerId)
        {
            try
            {
                var today = TimeZoneHelper.TodayInVietnam;

                // Tạo slots cho 7 ngày tới (bao gồm cả hôm nay)
                var candidateDates = Enumerable.Range(0, 7)
                    .Select(i => today.AddDays(i))
                    .ToArray();

                // Chỉ tạo slot cho các ngày từ Monday đến Saturday (bỏ qua Sunday)
                var datesToGenerate = candidateDates
                    .Where(ShouldGenerateSlotForDate)
                    .ToArray();

                var skippedDates = candidateDates.Except(datesToGenerate).ToArray();

                if (skippedDates.Length != 0)
                {
                    _logger.LogInformation("Bỏ qua các ngày Chủ nhật: {dates}",
                        string.Join(", ", skippedDates.Select(d => $"{d:dd/MM} ({d.DayOfWeek})")));
                }

                if (datesToGenerate.Length == 0)
                {
                    _logger.LogInformation("Không có ngày nào cần tạo slot cho trung tâm {centerId} (đã bỏ qua tất cả các ngày Chủ nhật)",
                        centerId);
                    return;
                }

                _logger.LogInformation("Sẽ tạo slot cho trung tâm {centerId} trong 7 ngày tới: {dates}",
                    centerId, string.Join(", ", datesToGenerate.Select(d => $"{d:dd/MM} ({d.DayOfWeek})")));

                var durationMinutes = (int)SlotTimeConfig.DefaultDuration;
                var allSlotsToCreate = new List<AppointmentSlot>();

                // Lấy tất cả các slot đã tồn tại cho center này
                var existingSlots = await _context.AppointmentSlots
                    .Where(s => s.CenterId == centerId && datesToGenerate.Contains(s.StartTime.Date))
                    .Select(s => s.StartTime.Date)
                    .ToListAsync();

                _logger.LogInformation("Tìm thấy {count} ngày đã có slot cho trung tâm {centerId}",
                    existingSlots.Count, centerId);

                foreach (var date in datesToGenerate)
                {
                    // Kiểm tra trong memory thay vì query database
                    if (existingSlots.Contains(date.Date))
                    {
                        _logger.LogDebug("Bỏ qua ngày {date} - trung tâm {centerId} đã có slot",
                            date.ToString("dd/MM/yyyy"), centerId);
                        continue;
                    }

                    var slotsForDate = GenerateSlotsForDate(centerId, date, durationMinutes);
                    allSlotsToCreate.AddRange(slotsForDate);

                    _logger.LogInformation("Sẽ tạo {count} slot cho trung tâm {centerId} ngày {date} ({dayOfWeek})",
                        slotsForDate.Count, centerId, date.ToString("dd/MM/yyyy"), date.DayOfWeek);
                }

                if (allSlotsToCreate.Count != 0)
                {
                    _context.AppointmentSlots.AddRange(allSlotsToCreate);
                    var savedCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("Đã lưu thành công {count} slot cho trung tâm {centerId} trong 7 ngày tới",
                        savedCount, centerId);
                }
                else
                {
                    _logger.LogInformation("Không có slot mới nào cần tạo cho trung tâm {centerId} - tất cả đã tồn tại",
                        centerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho trung tâm {centerId}", centerId);
                throw;
            }
        }

        public async Task<int> GenerateSlotsForWeekForCenterAsync(DateTime startDate, int centerId)
        {
            try
            {
                // Tạo slots cho 7 ngày bắt đầu từ startDate
                var candidateDates = Enumerable.Range(0, 7)
                    .Select(i => startDate.AddDays(i))
                    .ToArray();

                // Chỉ tạo slot cho các ngày từ Monday đến Saturday (bỏ qua Sunday)
                var datesToGenerate = candidateDates
                    .Where(ShouldGenerateSlotForDate)
                    .ToArray();

                var skippedDates = candidateDates.Except(datesToGenerate).ToArray();

                if (skippedDates.Length != 0)
                {
                    _logger.LogInformation("Bỏ qua các ngày Chủ nhật: {dates}",
                        string.Join(", ", skippedDates.Select(d => $"{d:dd/MM} ({d.DayOfWeek})")));
                }

                if (datesToGenerate.Length == 0)
                {
                    _logger.LogInformation("Không có ngày nào cần tạo slot cho trung tâm {centerId} bắt đầu từ {startDate} (đã bỏ qua tất cả các ngày Chủ nhật)",
                        centerId, startDate.ToString("dd/MM/yyyy"));
                    return 0;
                }

                _logger.LogInformation("Sẽ tạo slot cho trung tâm {centerId} bắt đầu từ {startDate} trong 7 ngày: {dates}",
                    centerId, startDate.ToString("dd/MM/yyyy"), string.Join(", ", datesToGenerate.Select(d => $"{d:dd/MM} ({d.DayOfWeek})")));

                var durationMinutes = (int)SlotTimeConfig.DefaultDuration;
                var allSlotsToCreate = new List<AppointmentSlot>();

                // Lấy tất cả các slot đã tồn tại cho center này
                var existingSlots = await _context.AppointmentSlots
                    .Where(s => s.CenterId == centerId && datesToGenerate.Contains(s.StartTime.Date))
                    .Select(s => s.StartTime.Date)
                    .ToListAsync();

                _logger.LogInformation("Tìm thấy {count} ngày đã có slot cho trung tâm {centerId}",
                    existingSlots.Count, centerId);

                foreach (var date in datesToGenerate)
                {
                    // Kiểm tra trong memory thay vì query database
                    if (existingSlots.Contains(date.Date))
                    {
                        _logger.LogDebug("Bỏ qua ngày {date} - trung tâm {centerId} đã có slot",
                            date.ToString("dd/MM/yyyy"), centerId);
                        continue;
                    }

                    var slotsForDate = GenerateSlotsForDate(centerId, date, durationMinutes);
                    allSlotsToCreate.AddRange(slotsForDate);

                    _logger.LogInformation("Sẽ tạo {count} slot cho trung tâm {centerId} ngày {date} ({dayOfWeek})",
                        slotsForDate.Count, centerId, date.ToString("dd/MM/yyyy"), date.DayOfWeek);
                }

                if (allSlotsToCreate.Count != 0)
                {
                    _context.AppointmentSlots.AddRange(allSlotsToCreate);
                    var savedCount = await _context.SaveChangesAsync();
                    _logger.LogInformation("Đã lưu thành công {count} slot cho trung tâm {centerId} bắt đầu từ {startDate}",
                        savedCount, centerId, startDate.ToString("dd/MM/yyyy"));
                    return savedCount;
                }
                else
                {
                    _logger.LogInformation("Không có slot mới nào cần tạo cho trung tâm {centerId} bắt đầu từ {startDate} - tất cả đã tồn tại",
                        centerId, startDate.ToString("dd/MM/yyyy"));
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo slot cho trung tâm {centerId} bắt đầu từ {startDate}", centerId, startDate.ToString("dd/MM/yyyy"));
                throw;
            }
        }
    }
}