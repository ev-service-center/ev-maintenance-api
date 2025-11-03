using EVServiceCenterMaintenanceAPI.Helpers;
using EVServiceCenterMaintenanceAPI.Services;

namespace EVServiceCenterMaintenanceAPI.Background
{
    public class SlotGenerationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SlotGenerationBackgroundService> _logger;
        private readonly TimeSpan _runTime = TimeSpan.FromHours(4);

        public SlotGenerationBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<SlotGenerationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Slot Generation Background Service đang khởi động... (Chạy hàng ngày lúc {runTime})", _runTime);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = TimeZoneHelper.NowInVietnam;
                    var nextRun = CalculateNextRunTime(now);

                    var delay = nextRun - now;
                    _logger.LogInformation("Chạy tiếp theo vào: {nextRun:yyyy-MM-dd HH:mm:ss} ({dayOfWeek}) - Còn {hours}h {minutes}m",
                        nextRun, nextRun.DayOfWeek, (int)delay.TotalHours, delay.Minutes);

                    await Task.Delay(delay, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Service đang dừng do cancellation token được yêu cầu");
                        break;
                    }

                    await RunSlotGeneration();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Service đang dừng do operation bị hủy");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong quá trình tạo slot tự động");

                    // Đợi 1 giờ trước khi thử lại để tránh lặp lỗi liên tục
                    var retryDelay = TimeSpan.FromHours(1);
                    _logger.LogWarning("Sẽ thử lại sau {minutes} phút", retryDelay.TotalMinutes);

                    try
                    {
                        await Task.Delay(retryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Retry bị hủy do service đang dừng");
                        break;
                    }
                }
            }

            _logger.LogInformation("Slot Generation Background Service đã dừng");
        }

        private async Task RunSlotGeneration()
        {
            using var scope = _scopeFactory.CreateScope();
            var slotGenerator = scope.ServiceProvider.GetRequiredService<AppointmentSlotGeneratorService>();

            var currentTime = TimeZoneHelper.NowInVietnam;
            _logger.LogInformation("=== Bắt đầu tạo slot lúc {time} ({dayOfWeek}) ===",
                currentTime.ToString("yyyy-MM-dd HH:mm:ss"), currentTime.DayOfWeek);

            var startTime = DateTime.UtcNow;
            await slotGenerator.GenerateSlotsForTodayAndTomorrowAsync();
            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("=== Tạo slot hoàn thành trong {duration}ms ===",
                duration.TotalMilliseconds);
        }

        private DateTime CalculateNextRunTime(DateTime now)
        {
            var nextRun = now.Date.Add(_runTime);

            // Nếu đã qua giờ chạy hôm nay, chuyển sang ngày mai
            if (now > nextRun)
                nextRun = nextRun.AddDays(1);

            return nextRun;
        }
    }
}