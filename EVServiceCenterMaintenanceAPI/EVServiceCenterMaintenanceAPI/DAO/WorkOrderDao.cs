using EVServiceCenterMaintenanceAPI.Models;
using Microsoft.EntityFrameworkCore;
using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Params;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class WorkOrderDao
    {
        private readonly EvserviceCenterDbContext _context;

        public WorkOrderDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder, List<int> serviceIds)
        {
            try
            {
                workOrder.CheckInAt = DateTime.UtcNow;

                if (string.IsNullOrEmpty(workOrder.Status))
                {
                    workOrder.Status = WorkOrderStatus.Pending.ToString();
                }

                _context.WorkOrders.Add(workOrder);
                await _context.SaveChangesAsync();

                if (serviceIds != null && serviceIds.Any())
                {
                    // Validate tất cả services tồn tại trước khi add
                    var existingServices = await _context.Services
                        .Where(s => serviceIds.Contains(s.ServiceId))
                        .Select(s => s.ServiceId)
                        .ToListAsync();

                    var invalidServiceIds = serviceIds.Except(existingServices).ToList();
                    if (invalidServiceIds.Any())
                    {
                        throw new InvalidOperationException(
                            $"Invalid service IDs: {string.Join(", ", invalidServiceIds)}");
                    }

                    // Get service prices và add appointment services
                    var services = await _context.Services
                        .Where(s => serviceIds.Contains(s.ServiceId))
                        .ToListAsync();

                    foreach (var service in services)
                    {
                        var appointmentService = new AppointmentService
                        {
                            WorkOrderId = workOrder.WorkOrderId,
                            ServiceId = service.ServiceId,
                            Price = service.BasePrice,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.AppointmentServices.Add(appointmentService);
                    }
                    await _context.SaveChangesAsync();
                }

                var result = await _context.WorkOrders
                    .Include(wo => wo.Center)
                    .Include(wo => wo.Customer)
                    .Include(wo => wo.Vehicle)
                    .Include(wo => wo.Appointment)
                    .Include(wo => wo.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                    .FirstOrDefaultAsync(wo => wo.WorkOrderId == workOrder.WorkOrderId);

                return result ?? throw new InvalidOperationException("Failed to retrieve created WorkOrder.");
            }
            catch
            {
                throw;
            }
        }

        public async Task<WorkOrder?> GetWorkOrderByIdAsync(int id)
        {
            return await _context.WorkOrders
                .Include(wo => wo.Center)
                .Include(wo => wo.Customer)
                .Include(wo => wo.Vehicle)
                .Include(wo => wo.Appointment)
                .Include(wo => wo.AppointmentServices)
                    .ThenInclude(aps => aps.Service)
                .Include(wo => wo.MaintenanceHistories)
                    .ThenInclude(mh => mh.PartUsages)
                        .ThenInclude(pu => pu.Part)
                .AsNoTracking()
                .FirstOrDefaultAsync(wo => wo.WorkOrderId == id);
        }

        public async Task<(List<WorkOrder>, int)> GetAllWorkOrdersAsync(WorkOrderQueryParams queryParams)
        {
            var query = _context.WorkOrders
                .Include(wo => wo.Center)
                .Include(wo => wo.Customer)
                .Include(wo => wo.Vehicle)
                .Include(wo => wo.Appointment)
                .Include(wo => wo.AppointmentServices)
                    .ThenInclude(aps => aps.Service)
                .Include(wo => wo.MaintenanceHistories)
                    .ThenInclude(mh => mh.PartUsages)
                        .ThenInclude(pu => pu.Part)
                .AsNoTracking()
                .AsQueryable();

            // Apply filters
            if (queryParams.CenterId.HasValue)
            {
                query = query.Where(wo => wo.CenterId == queryParams.CenterId.Value);
            }

            if (queryParams.CustomerId.HasValue)
            {
                query = query.Where(wo => wo.CustomerId == queryParams.CustomerId.Value);
            }

            if (queryParams.VehicleId.HasValue)
            {
                query = query.Where(wo => wo.VehicleId == queryParams.VehicleId.Value);
            }

            if (!string.IsNullOrEmpty(queryParams.Status))
            {
                query = query.Where(wo => wo.Status == queryParams.Status);
            }

            // Get total count for pagination
            var total = await query.CountAsync();

            // Apply sorting
            if (!string.IsNullOrEmpty(queryParams.SortBy))
            {
                switch (queryParams.SortBy.ToLower())
                {
                    case "createdat":
                        query = queryParams.SortOrder == "desc" ? query.OrderByDescending(wo => wo.CheckInAt) : query.OrderBy(wo => wo.CheckInAt);
                        break;
                    case "updatedat":
                        query = queryParams.SortOrder == "desc" ? query.OrderByDescending(wo => wo.CheckOutAt) : query.OrderBy(wo => wo.CheckOutAt);
                        break;
                    case "checkinat":
                        query = queryParams.SortOrder == "desc" ? query.OrderByDescending(wo => wo.CheckInAt) : query.OrderBy(wo => wo.CheckInAt);
                        break;
                    default:
                        query = queryParams.SortOrder == "desc" ? query.OrderByDescending(wo => wo.WorkOrderId) : query.OrderBy(wo => wo.WorkOrderId);
                        break;
                }
            }
            else
            {
                query = query.OrderByDescending(wo => wo.WorkOrderId);
            }

            query = query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize);

            var workOrders = await query.ToListAsync();
            return (workOrders, total);
        }

        public async Task<List<WorkOrder>> GetWorkOrdersByCustomerIdAsync(int customerId)
        {
            return await _context.WorkOrders
                .Include(wo => wo.Center)
                .Include(wo => wo.Customer)
                .Include(wo => wo.Vehicle)
                .Include(wo => wo.Appointment)
                .Include(wo => wo.AppointmentServices)
                    .ThenInclude(aps => aps.Service)
                .Include(wo => wo.MaintenanceHistories)
                    .ThenInclude(mh => mh.PartUsages)
                        .ThenInclude(pu => pu.Part)
                .AsNoTracking()
                .Where(wo => wo.CustomerId == customerId)
                .ToListAsync();
        }

        public async Task<WorkOrder> UpdateWorkOrderAsync(WorkOrder workOrder)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingWorkOrder = await _context.WorkOrders
                    .FirstOrDefaultAsync(wo => wo.WorkOrderId == workOrder.WorkOrderId);

                if (existingWorkOrder == null)
                {
                    throw new Exception($"WorkOrder with ID {workOrder.WorkOrderId} not found.");
                }

                existingWorkOrder.CenterId = workOrder.CenterId;
                existingWorkOrder.CustomerId = workOrder.CustomerId;
                existingWorkOrder.VehicleId = workOrder.VehicleId;
                existingWorkOrder.CreatedByStaffId = workOrder.CreatedByStaffId;
                existingWorkOrder.AppointmentId = workOrder.AppointmentId;
                existingWorkOrder.Status = workOrder.Status;
                existingWorkOrder.CheckInAt = workOrder.CheckInAt;
                existingWorkOrder.CheckOutAt = workOrder.CheckOutAt;
                existingWorkOrder.OdometerKm = workOrder.OdometerKm;
                existingWorkOrder.Notes = workOrder.Notes;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await _context.WorkOrders
                    .Include(wo => wo.Center)
                    .Include(wo => wo.Customer)
                    .Include(wo => wo.Vehicle)
                    .Include(wo => wo.Appointment)
                    .Include(wo => wo.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(wo => wo.WorkOrderId == workOrder.WorkOrderId);

                return result ?? throw new InvalidOperationException("Failed to retrieve updated WorkOrder.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to update WorkOrder with ID {workOrder.WorkOrderId}.", ex);
            }
        }

        public async Task<bool> DeleteWorkOrderAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var workOrder = await _context.WorkOrders
                    .Include(wo => wo.AppointmentServices)
                    .FirstOrDefaultAsync(wo => wo.WorkOrderId == id);

                if (workOrder == null)
                {
                    return false;
                }

                _context.AppointmentServices.RemoveRange(workOrder.AppointmentServices);
                _context.WorkOrders.Remove(workOrder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}