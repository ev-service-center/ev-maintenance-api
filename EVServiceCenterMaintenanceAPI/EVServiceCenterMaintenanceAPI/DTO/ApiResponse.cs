namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public string Status { get; set; } = null!;
        public string Message { get; set; } = null!;
        public IDictionary<string, string[]>? Errors { get; set; }
        public T Data { get; set; } = default!;

        public ApiResponse(int statusCode, string status, string message, IDictionary<string, string[]>? errors = null, T data = default)
        {
            StatusCode = statusCode;
            Status = status;
            Message = message;
            Errors = errors;
            Data = data;
        }
    }
}
