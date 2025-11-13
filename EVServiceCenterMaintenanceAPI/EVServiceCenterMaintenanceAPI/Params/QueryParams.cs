namespace EVServiceCenterMaintenanceAPI.Params
{
    public class QueryParams
    {
        private int _page = 1;
        private int _pageSize = 10;
        private string _sortOrder = "desc";

        //public virtual string? Type { get; set; }

        public virtual int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        public virtual int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 10 : value > 100 ? 100 : value;
        }
        public virtual string? SortBy { get; set; }
        public virtual string SortOrder
        {
            get => _sortOrder;
            set => _sortOrder = string.Equals(value, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        public virtual string? Search { get; set; }

        //public virtual string? Status { get; set; }

        public virtual DateTime? FromDate { get; set; }

        public virtual DateTime? ToDate { get; set; }

        public virtual (bool IsValid, string ErrorMessage) Validate()
        {
            if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
            {
                return (false, "FromDate cannot be later than ToDate.");
            }

            return (true, string.Empty);
        }
    }
}
