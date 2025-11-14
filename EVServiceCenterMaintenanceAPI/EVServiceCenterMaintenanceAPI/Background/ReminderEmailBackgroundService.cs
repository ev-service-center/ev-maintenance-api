using System;
using System.Threading;
using System.Threading.Tasks;
using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EVServiceCenterMaintenanceAPI.BackgroundServices
{
    public class ReminderEmailBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

        public ReminderEmailBackgroundService(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAppointmentsNeedingRemindersAsync();
                }
                catch (Exception ex)
                {
                }

                await Task.Delay(_interval, stoppingToken);
            }

        }

        private async Task ProcessAppointmentsNeedingRemindersAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var reminderDao = scope.ServiceProvider.GetRequiredService<ReminderDao>();
            var userDao = scope.ServiceProvider.GetRequiredService<UserDao>();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
            var _configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var reminders = await reminderDao.GetRemindersNeedToSendAsync();


            foreach (var reminder in reminders)
            {
                try
                {
                    switch (reminder.ReminderType)
                    {
                        case nameof(ReminderType.Rating):
                            var user = await userDao.GetUserByIdAsync(reminder.UserId);
                            if (user == null || string.IsNullOrWhiteSpace(user.Email)) continue;
                            var appRaringUrl = _configuration["AppRaringUrl"] ?? "https://localhost:5001/completed/rating";
                            await reminderDao.UpdateMarkReminderAsync(reminder);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }
    }
}