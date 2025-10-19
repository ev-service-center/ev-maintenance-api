using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.Extensions
{
    public class DatabaseSeeder
    {
        public static async Task SeedAdminUserAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();
            var userDAO = services.GetRequiredService<UserDao>();

            var adminExists = await context.Users.AnyAsync(u => u.Role == UserRole.Admin.ToString());
            if(!adminExists)
            {
                var adminUser = new User
                {
                    Username = "admin",
                    Email = "admin@evservicecenter.com",
                    FullName = "System Administrator",
                    Phone = "0000000000",
                    Role = UserRole.Admin.ToString(),
                    Status = UserStatus.Active.ToString(),
                    Avatar = DefaultAvatar.Local,
                };

                //Default password: Admin@123
                try
                {
                    await userDAO.CreateUserAsync(adminUser, "Admin@123");
                    Console.WriteLine("Admin user created successfully!");
                }
                catch (Exception ex) { 
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
