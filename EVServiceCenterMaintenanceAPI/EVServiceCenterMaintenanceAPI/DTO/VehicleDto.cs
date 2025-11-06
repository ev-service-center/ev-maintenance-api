using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class VehicleResponeDto
    {
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        public string Model { get; set; } = null!;
        public string VIN { get; set; } = null!;
        public int? ManufactureYear { get; set; }
        public decimal CurrentMileage { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        public string? Color { get; set; }
        public string Plate { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class VehicleCreateRequestDto
    {
        [Required, Range(1, int.MaxValue, ErrorMessage = "CustomerId must be positive.")] public int CustomerId { get; set; }
        [Required, StringLength(100)]public string Model { get; set; } = null!;
        [Required, StringLength(50)] public string VIN { get; set; } = null!;
        public int? ManufactureYear { get; set; }
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "CurrentMileage must be non-negative.")] public decimal CurrentMileage { get; set; }
        [StringLength(50)] public string? Color { get; set; }
        [Required, StringLength(20)] public string Plate { get; set; } = null!;
    }

    public class VehicleUpdateRequestDto
    {
        [Required] public int VehicleId { get; set; }
        [Required, Range(1, int.MaxValue, ErrorMessage = "CustomerId must be positive.")] public int CustomerId { get; set; }
        [Required, StringLength(100)] public string Model { get; set; } = null!;
        [Required, StringLength(50)] public string VIN { get; set; } = null!;
        public int? ManufactureYear { get; set; }
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "CurrentMileage must be non-negative.")] public decimal CurrentMileage { get; set; }
        [StringLength(50)] public string? Color { get; set; }
        [Required, StringLength(20)] public string Plate { get; set; } = null!;
    }
}
