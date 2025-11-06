using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.DTO
{
    public class VehicleResponeDto
    {
        public int VehicleId { get; set; }
        public int CustomerId { get; set; }
        [Required, StringLength(100)] public string Model { get; set; } = null!;
        [Required, StringLength(50), RegularExpression(@"^[A-HJ-NPR-Z0-9]{17}$", ErrorMessage = "VIN must be 17 characters.")]
        public string VIN { get; set; } = null!;
        public int? ManufactureYear { get; set; }
        public decimal CurrentMileage { get; set; }
        public DateTime? LastMaintenanceDate { get; set; }
        [StringLength(50)] public string? Color { get; set; }
        [Required, StringLength(20)] public string Plate { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class VehicleCreateRequestDto
    {
        [Required] public int CustomerId { get; set; }
        [Required, StringLength(100)] public string Model { get; set; } = null!;
        [Required, StringLength(50)] public string VIN { get; set; } = null!;
        public int? ManufactureYear { get; set; }
        public decimal CurrentMileage { get; set; }
        [StringLength(50)] public string? Color { get; set; }
        [Required, StringLength(20)] public string Plate { get; set; } = null!;
    }

    public class VehicleUpdateRequestDto
    {
        [Required] public int VehicleId { get; set; }
        [Required] public int CustomerId { get; set; }
        [Required, StringLength(100)] public string Model { get; set; } = null!;
        [Required, StringLength(50)] public string VIN { get; set; } = null!;
        public int? ManufactureYear { get; set; }
        public decimal CurrentMileage { get; set; }
        [StringLength(50)] public string? Color { get; set; }
        [Required, StringLength(20)] public string Plate { get; set; } = null!;
    }
}
