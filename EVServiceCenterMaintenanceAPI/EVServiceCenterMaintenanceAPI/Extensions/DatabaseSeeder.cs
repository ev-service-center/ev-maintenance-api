using EVServiceCenterMaintenanceAPI.DAO;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Helpers;
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
            if (!adminExists)
            {
                var adminUser = new User
                {
                    Username = "admin",
                    Email = "admin@evservicecenter.me",
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
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public static async Task SeedServiceCentersAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();

            var centersExist = await context.ServiceCenters.AnyAsync();
            if (centersExist)
            {
                Console.WriteLine("Service centers already exist. Skipping seeding.");
                return;
            }

            var serviceCenters = new List<ServiceCenter>
            {
                new() {
                    CenterName = "Trung Tâm Dịch Vụ Xe Điện TP.HCM - Quận 1",
                    Address = "Số 456 Đường Nguyễn Huệ, Quận 1, TP.HCM",
                    Phone = "028-9876-5432",
                    Email = "hcmc.q1@evservicecenter.me",
                    Status = ServiceCenterStatus.Open.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    CenterName = "Trung Tâm Dịch Vụ Xe Điện TP.HCM - Quận 3",
                    Address = "Số 123 Đường Lê Văn Sỹ, Quận 3, TP.HCM",
                    Phone = "028-1234-5678",
                    Email = "hcmc.q3@evservicecenter.me",
                    Status = ServiceCenterStatus.Open.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    CenterName = "Trung Tâm Dịch Vụ Xe Điện TP.HCM - Quận 7",
                    Address = "Số 789 Đường Nguyễn Thị Thập, Quận 7, TP.HCM",
                    Phone = "028-5678-9012",
                    Email = "hcmc.q7@evservicecenter.me",
                    Status = ServiceCenterStatus.Open.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    CenterName = "Trung Tâm Dịch Vụ Xe Điện TP.HCM - Quận 10",
                    Address = "Số 321 Đường 3 Tháng 2, Quận 10, TP.HCM",
                    Phone = "028-3456-7890",
                    Email = "hcmc.q10@evservicecenter.me",
                    Status = ServiceCenterStatus.Open.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    CenterName = "Trung Tâm Dịch Vụ Xe Điện TP.HCM - Quận Bình Thạnh",
                    Address = "Số 654 Đường Xô Viết Nghệ Tĩnh, Quận Bình Thạnh, TP.HCM",
                    Phone = "028-9012-3456",
                    Email = "hcmc.binhthanh@evservicecenter.me",
                    Status = ServiceCenterStatus.Open.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            try
            {
                await context.ServiceCenters.AddRangeAsync(serviceCenters);
                await context.SaveChangesAsync();
                Console.WriteLine($"Successfully seeded {serviceCenters.Count} service centers!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding service centers: {ex.Message}");
            }
        }

        public static async Task SeedServicesAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();

            var servicesExist = await context.Services.AnyAsync();
            if (servicesExist)
            {
                Console.WriteLine("Services already exist. Skipping seeding.");
                return;
            }

            var servicesToSeed = new List<Service>
            {
                // Dịch vụ bảo dưỡng cơ bản
                new() {
                    ServiceName = "Kiểm tra tổng thể",
                    Description = "Kiểm tra tổng thể xe điện, hệ thống điện, pin, phanh, lốp",
                    BasePrice = 1000,
                    EstimatedTime = 60,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 30,
                    ReminderMileage = 2000,
                    Notes = "Nên thực hiện định kỳ để đảm bảo an toàn",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Kiểm tra và bảo dưỡng pin",
                    Description = "Kiểm tra dung lượng pin, độ sạc, thay pin nếu cần",
                    BasePrice = 2000,
                    EstimatedTime = 90,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 90,
                    ReminderMileage = 5000,
                    Notes = "Pin là bộ phận quan trọng nhất của xe điện",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Thay pin",
                    Description = "Thay thế pin xe điện mới",
                    BasePrice = 5000,
                    EstimatedTime = 120,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 365,
                    ReminderMileage = 15000,
                    Notes = "Giá có thể thay đổi tùy loại xe và pin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Dịch vụ bảo dưỡng lốp
                new() {
                    ServiceName = "Kiểm tra áp suất lốp",
                    Description = "Kiểm tra và bơm áp suất lốp đúng tiêu chuẩn",
                    BasePrice = 1000,
                    EstimatedTime = 15,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 7,
                    ReminderMileage = 500,
                    Notes = "Miễn phí khi đi kèm dịch vụ khác",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Cân bằng lốp",
                    Description = "Cân bằng lốp xe để đảm bảo vận hành mượt mà",
                    BasePrice = 2500,
                    EstimatedTime = 30,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 30,
                    ReminderMileage = 3000,
                    Notes = "Nên cân bằng lốp sau khi thay lốp mới",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Thay lốp",
                    Description = "Thay lốp xe điện mới",
                    BasePrice = 4000,
                    EstimatedTime = 45,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 180,
                    ReminderMileage = 10000,
                    Notes = "Giá có thể thay đổi tùy loại lốp",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Dịch vụ bảo dưỡng hệ thống điện
                new() {
                    ServiceName = "Kiểm tra hệ thống sạc",
                    Description = "Kiểm tra cổng sạc, cáp sạc, bộ sạc",
                    BasePrice = 3000,
                    EstimatedTime = 45,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 60,
                    ReminderMileage = 4000,
                    Notes = "Đảm bảo an toàn khi sạc xe",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Sửa chữa hệ thống điện",
                    Description = "Sửa chữa các vấn đề về hệ thống điện, dây điện",
                    BasePrice = 5000,
                    EstimatedTime = 120,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = null,
                    ReminderMileage = null,
                    Notes = "Theo yêu cầu khi có sự cố",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Dịch vụ bảo dưỡng hệ thống phanh
                new() {
                    ServiceName = "Kiểm tra phanh",
                    Description = "Kiểm tra hệ thống phanh, má phanh, dầu phanh",
                    BasePrice = 1500,
                    EstimatedTime = 30,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 60,
                    ReminderMileage = 4000,
                    Notes = "An toàn là ưu tiên hàng đầu",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Thay má phanh",
                    Description = "Thay má phanh mới",
                    BasePrice = 1200,
                    EstimatedTime = 60,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 180,
                    ReminderMileage = 8000,
                    Notes = "Thay má phanh định kỳ để đảm bảo hiệu quả phanh",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Dịch vụ khác
                new() {
                    ServiceName = "Vệ sinh xe",
                    Description = "Vệ sinh ngoài và trong xe điện",
                    BasePrice = 2000,
                    EstimatedTime = 45,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 15,
                    ReminderMileage = 1000,
                    Notes = "Giữ xe luôn sạch đẹp",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Bảo dưỡng động cơ điện",
                    Description = "Kiểm tra và bảo dưỡng động cơ điện",
                    BasePrice = 4800,
                    EstimatedTime = 90,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 180,
                    ReminderMileage = 10000,
                    Notes = "Động cơ điện cần bảo dưỡng định kỳ",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new() {
                    ServiceName = "Kiểm tra và cập nhật phần mềm",
                    Description = "Cập nhật phần mềm điều khiển xe",
                    BasePrice = 1200,
                    EstimatedTime = 30,
                    Status = ServiceStatus.Active.ToString(),
                    ReminderIntervalDays = 90,
                    ReminderMileage = null,
                    Notes = "Cập nhật phần mềm để có trải nghiệm tốt nhất",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            try
            {
                await context.Services.AddRangeAsync(servicesToSeed);
                await context.SaveChangesAsync();
                Console.WriteLine($"Successfully seeded {servicesToSeed.Count} services!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding services: {ex.Message}");
            }
        }

        public static async Task SeedAppointmentSlotsAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();
            var slotsExist = await context.AppointmentSlots.AnyAsync();
            if (slotsExist)
            {
                Console.WriteLine("Appointment slots already exist. Skipping seeding.");
                return;
            }

            Console.WriteLine("Seeding appointment slots for 7 days...");

            var serviceCenters = await context.ServiceCenters.ToListAsync();
            if (serviceCenters.Count == 0)
            {
                Console.WriteLine("No service centers found. Cannot seed appointment slots.");
                return;
            }

            var slots = new List<AppointmentSlot>();
            var startDate = TimeZoneHelper.TodayInVietnam;
            var endDate = startDate.AddDays(6);

            foreach (var center in serviceCenters)
            {
                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    // Chỉ tạo slots cho thứ 2 - thứ 7 (Monday = 1, Saturday = 6)
                    if (currentDate.DayOfWeek >= DayOfWeek.Monday && currentDate.DayOfWeek <= DayOfWeek.Saturday)
                    {
                        var slotTimes = new[]
                        {
                            (startHour: 7, endHour: 9),
                            (startHour: 9, endHour: 11),
                            (startHour: 13, endHour: 15),
                            (startHour: 15, endHour: 17),
                            (startHour: 17, endHour: 19)
                        };

                        foreach (var (startHour, endHour) in slotTimes)
                        {
                            var startTime = DateTime.SpecifyKind(currentDate.AddHours(startHour), DateTimeKind.Unspecified);
                            var endTime = DateTime.SpecifyKind(currentDate.AddHours(endHour), DateTimeKind.Unspecified);

                            slots.Add(new AppointmentSlot
                            {
                                CenterId = center.CenterId,
                                StartTime = startTime,
                                EndTime = endTime,
                                IsAvailable = true,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    currentDate = currentDate.AddDays(1);
                }
            }

            try
            {
                await context.AppointmentSlots.AddRangeAsync(slots);
                await context.SaveChangesAsync();
                Console.WriteLine($"Successfully seeded {slots.Count} appointment slots for {serviceCenters.Count} centers over 7 days!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding appointment slots: {ex.Message}");
            }
        }

        public static async Task SeedPartsAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();

            var partsExist = await context.Parts.AnyAsync();
            if (partsExist)
            {
                Console.WriteLine("Parts already exist. Skipping seeding.");
                return;
            }

            // Lấy tất cả service centers
            var serviceCenters = await context.ServiceCenters.ToListAsync();
            if (serviceCenters.Count == 0)
            {
                Console.WriteLine("No service centers found. Cannot seed parts.");
                return;
            }

            var parts = new List<Part>();

            foreach (var center in serviceCenters)
            {
                var centerParts = new List<Part>
                {
                    new() {
                        PartName = "Pin xe điện Lithium",
                        Description = "Pin lithium-ion cho xe điện, dung lượng cao, tuổi thọ lâu",
                        CostPrice = 1050,
                        Price = 1500,
                        QuantityInStock = 50,
                        MinStock = 10,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Lốp xe điện (bộ 2)",
                        Description = "Lốp chuyên dụng cho xe điện, chống mòn tốt, độ bền cao",
                        CostPrice = 1400,
                        Price = 2000,
                        QuantityInStock = 80,
                        MinStock = 20,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Má phanh xe điện",
                        Description = "Má phanh chuyên dụng cho xe điện, hiệu quả phanh cao",
                        CostPrice = 1300,
                        Price = 2000,
                        QuantityInStock = 100,
                        MinStock = 30,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Cáp sạc xe điện",
                        Description = "Cáp sạc nhanh cho xe điện, dây dẫn chất lượng cao",
                        CostPrice = 1200,
                        Price = 2000,
                        QuantityInStock = 70,
                        MinStock = 20,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Bộ sạc xe điện",
                        Description = "Bộ sạc pin xe điện, sạc nhanh, an toàn",
                        CostPrice = 2450,
                        Price = 3500,
                        QuantityInStock = 60,
                        MinStock = 15,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Dây curoa động cơ điện",
                        Description = "Dây curoa truyền động cho động cơ điện",
                        CostPrice = 1200,
                        Price = 2000,
                        QuantityInStock = 75,
                        MinStock = 25,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Bộ điều khiển (Controller)",
                        Description = "Bộ điều khiển động cơ điện xe điện",
                        CostPrice = 1000,
                        Price = 1500,
                        QuantityInStock = 55,
                        MinStock = 15,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Cảm biến tốc độ",
                        Description = "Cảm biến đo tốc độ cho xe điện",
                        CostPrice = 900,
                        Price = 1500,
                        QuantityInStock = 65,
                        MinStock = 20,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Bình nước làm mát",
                        Description = "Bình chứa nước làm mát cho hệ thống động cơ điện",
                        CostPrice = 1300,
                        Price = 2000,
                        QuantityInStock = 70,
                        MinStock = 20,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new() {
                        PartName = "Phin lọc gió",
                        Description = "Phin lọc gió cho hệ thống làm mát động cơ điện",
                        CostPrice = 950,
                        Price = 1500,
                        QuantityInStock = 90,
                        MinStock = 30,
                        CenterId = center.CenterId,
                        Status = PartStatus.Active.ToString(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                parts.AddRange(centerParts);
            }

            try
            {
                await context.Parts.AddRangeAsync(parts);
                await context.SaveChangesAsync();
                Console.WriteLine($"Successfully seeded {parts.Count} parts for {serviceCenters.Count} service centers!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding parts: {ex.Message}");
            }
        }

        public static async Task SeedStaffAndTechniciansAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();
            var userDAO = services.GetRequiredService<UserDao>();

            // Kiểm tra xem đã có Staff hoặc Technician chưa
            var employeesExist = await context.Employees.AnyAsync();
            if (employeesExist)
            {
                Console.WriteLine("Staff and Technicians already exist. Skipping seeding.");
                return;
            }

            var serviceCenters = await context.ServiceCenters.ToListAsync();
            if (serviceCenters.Count == 0)
            {
                Console.WriteLine("No service centers found. Cannot seed staff and technicians.");
                return;
            }

            var usersWithCenter = new List<(User user, int centerId)>();
            var employees = new List<Employee>();

            foreach (var center in serviceCenters)
            {
                // Tạo Staff users
                for (int i = 1; i <= 2; i++)
                {
                    var staffUser = new User
                    {
                        Username = $"staff_{center.CenterId}_{i}",
                        Email = $"staff{i}@center{center.CenterId}.evservicecenter.me",
                        FullName = $"Nhân Viên {i} - {center.CenterName}",
                        Phone = $"028{center.CenterId:D2}{i:D2}0000",
                        Role = UserRole.Staff.ToString(),
                        Status = UserStatus.Active.ToString(),
                        Avatar = DefaultAvatar.Local,
                    };

                    usersWithCenter.Add((staffUser, center.CenterId));
                }

                // Tạo Technician users
                for (int i = 1; i <= 3; i++)
                {
                    var techUser = new User
                    {
                        Username = $"technician_{center.CenterId}_{i}",
                        Email = $"technician{i}@center{center.CenterId}.evservicecenter.me",
                        FullName = $"Kỹ Thuật Viên {i} - {center.CenterName}",
                        Phone = $"028{center.CenterId:D2}{i:D2}1111",
                        Role = UserRole.Technician.ToString(),
                        Status = UserStatus.Active.ToString(),
                        Avatar = DefaultAvatar.Local,
                    };

                    usersWithCenter.Add((techUser, center.CenterId));
                }
            }

            try
            {
                foreach (var (user, centerId) in usersWithCenter)
                {
                    // Default password: Password123! (cho Staff và Technician)
                    var createdUser = await userDAO.CreateUserAsync(user, "Password123!");

                    // Tạo Employee record ngay sau khi tạo User
                    var employee = new Employee
                    {
                        EmployeeId = createdUser.UserId,
                        CenterId = centerId,
                        Shift = null,
                        PerformanceScore = 85.00m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    employees.Add(employee);
                }

                // Thêm tất cả employees vào context
                await context.Employees.AddRangeAsync(employees);
                await context.SaveChangesAsync();

                Console.WriteLine($"Successfully seeded {usersWithCenter.Count} staff and technicians ({employees.Count} employees) for {serviceCenters.Count} service centers!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding staff and technicians: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static async Task SeedCustomersAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();
            var userDAO = services.GetRequiredService<UserDao>();

            var customersExist = await context.Users.AnyAsync(u => u.Role == UserRole.Customer.ToString());
            if (customersExist)
            {
                Console.WriteLine("Customers already exist. Skipping seeding.");
                return;
            }

            var customerNames = new[]
            {
                "Nguyễn Văn An", "Trần Thị Bình", "Lê Văn Cường", "Phạm Thị Dung", "Hoàng Văn Em",
                "Võ Thị Phượng", "Đặng Văn Giang", "Bùi Thị Hương", "Ngô Văn Hùng", "Đỗ Thị Lan",
                "Phan Văn Minh", "Vũ Thị Ngọc", "Đinh Văn Quang", "Dương Văn Sơn", "Nguyễn Thị Tâm",
                "Lý Văn Tuấn", "Chu Văn Việt", "Bạch Thị Yến", "Hồ Văn Đức", "Tạ Văn Hải"
            };

            var customers = new List<User>();

            for (int i = 0; i < customerNames.Length; i++)
            {
                var customer = new User
                {
                    Username = $"customer{i + 1}",
                    Email = $"customer{i + 1}@example.com",
                    FullName = customerNames[i],
                    Phone = $"090{1000000 + i:D7}",
                    Role = UserRole.Customer.ToString(),
                    Status = UserStatus.Active.ToString(),
                    Avatar = DefaultAvatar.Local,
                };

                customers.Add(customer);
            }

            try
            {
                foreach (var customer in customers)
                {
                    // Default password: Customer123!
                    await userDAO.CreateUserAsync(customer, "Customer123!");
                }

                Console.WriteLine($"Successfully seeded {customers.Count} customers!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding customers: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static async Task SeedVehiclesAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<EvserviceCenterDbContext>();

            var vehiclesExist = await context.Vehicles.AnyAsync();
            if (vehiclesExist)
            {
                Console.WriteLine("Vehicles already exist. Skipping seeding.");
                return;
            }

            var customers = await context.Users
                .Where(u => u.Role == UserRole.Customer.ToString())
                .ToListAsync();

            if (customers.Count == 0)
            {
                Console.WriteLine("No customers found. Cannot seed vehicles.");
                return;
            }

            // Các mẫu xe điện phổ biến ở Việt Nam
            var vehicleModels = new[]
            {
                ("Pega X", "PEG2024"),
                ("VinFast Klara", "VIN2023"),
                ("Pega S", "PEG2024"),
                ("Yadea G5", "YAD2023"),
                ("Aima A200", "AIM2024"),
                ("Pega E200", "PEG2023"),
                ("VinFast Ludo", "VIN2022"),
                ("Yadea X5", "YAD2024"),
                ("Aima X7", "AIM2023"),
                ("Pega Q", "PEG2024"),
                ("VinFast Impes", "VIN2023"),
                ("Yadea G6", "YAD2024"),
                ("Aima G300", "AIM2023"),
                ("Pega X1", "PEG2024"),
                ("VinFast Klara S", "VIN2023")
            };

            var colors = new[] { "Đen", "Trắng", "Xám", "Đỏ", "Xanh dương", "Xanh lá", "Vàng", "Bạc" };
            var random = new Random();

            var vehicles = new List<Vehicle>();
            var platePrefixes = new[] { "29", "30", "50", "51", "52", "53", "54", "55", "56", "57", "58", "59" }; // Biển số TP.HCM

            int vehicleIndex = 0;

            // Phân phối vehicles cho customers (mỗi customer có 0-2 vehicles)
            foreach (var customer in customers)
            {
                var numVehicles = random.Next(0, 3); // 0, 1, hoặc 2 vehicles per customer

                for (int i = 0; i < numVehicles && vehicleIndex < vehicleModels.Length; i++)
                {
                    var (model, vinPrefix) = vehicleModels[vehicleIndex % vehicleModels.Length];
                    var manufactureYear = random.Next(2020, 2025);
                    var currentMileage = random.Next(500, 15000);
                    var color = colors[random.Next(colors.Length)];
                    var platePrefix = platePrefixes[random.Next(platePrefixes.Length)];
                    var plateNumber = random.Next(1000, 9999);
                    var vin = $"{vinPrefix}{random.Next(100000, 999999)}";
                    var plate = $"{platePrefix}{plateNumber:D4}";

                    var lastMaintenanceDate = DateTime.UtcNow.AddDays(-random.Next(0, 180)); // Trong vòng 6 tháng gần đây

                    var vehicle = new Vehicle
                    {
                        CustomerId = customer.UserId,
                        Model = model,
                        Vin = vin,
                        ManufactureYear = manufactureYear,
                        CurrentMileage = currentMileage,
                        LastMaintenanceDate = lastMaintenanceDate,
                        Color = color,
                        Plate = plate,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    vehicles.Add(vehicle);
                    vehicleIndex++;
                }
            }

            try
            {
                await context.Vehicles.AddRangeAsync(vehicles);
                await context.SaveChangesAsync();
                Console.WriteLine($"Successfully seeded {vehicles.Count} vehicles for {customers.Count} customers!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding vehicles: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static async Task SeedAllAsync(IServiceProvider services)
        {
            Console.WriteLine("Starting database seeding...");
            await SeedAdminUserAsync(services);
            await SeedServiceCentersAsync(services);
            await SeedServicesAsync(services);
            await SeedStaffAndTechniciansAsync(services);
            await SeedCustomersAsync(services);
            await SeedAppointmentSlotsAsync(services);
            await SeedPartsAsync(services);
            await SeedVehiclesAsync(services);
            Console.WriteLine("Database seeding completed!");
        }
    }
}
