using EVServiceCenterMaintenanceAPI.Enums;
using EVServiceCenterMaintenanceAPI.Models;
using EVServiceCenterMaintenanceAPI.Params;
using Microsoft.EntityFrameworkCore;

namespace EVServiceCenterMaintenanceAPI.DAO
{
    public class InvoiceDao
    {
        private readonly EvserviceCenterDbContext _context;

        public InvoiceDao(EvserviceCenterDbContext context)
        {
            _context = context;
        }

        public async Task<Invoice> CreateInvoiceAsync(Invoice invoice)
        {
            // Set default values
            invoice.IssueDate = DateTime.UtcNow;
            invoice.Status ??= InvoiceStatus.Unpaid.ToString();
            invoice.CreatedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return invoice;
        }

        public async Task<Invoice?> GetInvoiceByIdAsync(int invoiceId)
        {
            return await _context.Invoices
                .Include(i => i.Payments)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.Customer)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.Vehicle)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.MaintenanceHistories)
                        .ThenInclude(mh => mh.PartUsages)
                            .ThenInclude(pu => pu.Part)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        }

        public async Task<Invoice?> GetInvoiceByWorkOrderIdAsync(int workOrderId)
        {
            return await _context.Invoices
                .Include(i => i.Payments)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.Customer)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.Vehicle)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.AppointmentServices)
                        .ThenInclude(aps => aps.Service)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.MaintenanceHistories)
                        .ThenInclude(mh => mh.PartUsages)
                            .ThenInclude(pu => pu.Part)
                .AsSplitQuery()
                .FirstOrDefaultAsync(i => i.WorkOrderId == workOrderId);
        }

        public async Task<(List<Invoice> invoices, int total)> GetAllInvoicesAsync(InvoiceQueryParams queryParams)
        {
            var query = _context.Invoices
                .Include(i => i.Payments)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.Customer)
                .Include(i => i.WorkOrder)
                    .ThenInclude(w => w.Vehicle)
                .AsSplitQuery()
                .AsQueryable();

            // Apply filters
            if (queryParams.Status.HasValue)
            {
                query = query.Where(i => i.Status == queryParams.Status.Value.ToString());
            }

            if (queryParams.CustomerId.HasValue)
            {
                query = query.Where(i => i.WorkOrder.CustomerId == queryParams.CustomerId.Value);
            }

            if (queryParams.WorkOrderId.HasValue)
            {
                query = query.Where(i => i.WorkOrderId == queryParams.WorkOrderId.Value);
            }

            if (queryParams.FromDate.HasValue)
            {
                query = query.Where(i => i.IssueDate >= queryParams.FromDate.Value);
            }

            if (queryParams.ToDate.HasValue)
            {
                query = query.Where(i => i.IssueDate <= queryParams.ToDate.Value);
            }

            // Apply search if provided
            if (!string.IsNullOrWhiteSpace(queryParams.Search))
            {
                var search = queryParams.Search;
                query = query.Where(i =>
                    (i.WorkOrder.Customer != null && (
                        i.WorkOrder.Customer.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        i.WorkOrder.Customer.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                    )) ||
                    (i.WorkOrder.Vehicle != null && (
                        i.WorkOrder.Vehicle.Plate.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        i.WorkOrder.Vehicle.Model.Contains(search, StringComparison.OrdinalIgnoreCase)
                    )));
            }

            var total = await query.CountAsync();

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .ToListAsync();

            return (invoices, total);
        }

        public async Task<Invoice> UpdateInvoiceAsync(Invoice invoice)
        {
            invoice.UpdatedAt = DateTime.UtcNow;
            _context.Invoices.Update(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        public async Task DeleteInvoiceAsync(int invoiceId)
        {
            var invoice = await _context.Invoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                throw new KeyNotFoundException($"Invoice with ID {invoiceId} not found.");
            }

            var hasPayments = await _context.Payments
                .AnyAsync(p => p.InvoiceId == invoiceId);
            if (hasPayments)
            {
                throw new InvalidOperationException("Cannot delete invoice that has payment records.");
            }

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> CalculateWorkOrderTotalAsync(int workOrderId)
        {
            // Get service costs
            var serviceCost = await _context.AppointmentServices
                .Where(aps => aps.WorkOrderId == workOrderId)
                .SumAsync(aps => aps.Price);

            // Get part costs from MaintenanceHistory
            var partCost = await _context.MaintenanceHistories
                .Where(mh => mh.WorkOrderId == workOrderId)
                .SelectMany(mh => mh.PartUsages)
                .SumAsync(pu => pu.QuantityUsed * pu.UnitPrice);

            return serviceCost + partCost;
        }
    }
}

