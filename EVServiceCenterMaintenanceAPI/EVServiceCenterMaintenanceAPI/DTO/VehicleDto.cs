using System.ComponentModel.DataAnnotations;
using EVServiceCenterMaintenanceAPI.Attributes;
using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.DTO
{
        public class VehicleResponeDto
        {
                public int VehicleId { get; set; }
                public int CustomerId { get; set; }
                public CustomerInfoDto? Customer { get; set; }
                public string Model { get; set; } = null!;
                public string VIN { get; set; } = null!;
                public int? ManufactureYear { get; set; }
                public decimal CurrentMileage { get; set; }
                public DateTime? LastMaintenanceDate { get; set; }
                public string? Color { get; set; }
                public string Plate { get; set; } = null!;
                public VehicleStatus Status { get; set; }
                public string? ImageUrl { get; set; }
                public DateTime CreatedAt { get; set; }
                public DateTime UpdatedAt { get; set; }
        }

        public class CustomerInfoDto
        {
                public int UserId { get; set; }
                public string FullName { get; set; } = null!;
                public string? Email { get; set; }
                public string? Phone { get; set; }
        }

        public class VehicleCreateRequestDto
        {
                [Required, Range(1, int.MaxValue, ErrorMessage = "CustomerId must be positive.")] public int CustomerId { get; set; }
                [Required, StringLength(100)] public string Model { get; set; } = null!;
                [Required, StringLength(50), RegularExpression(@"^[A-HJ-NPR-Z0-9]{17}$", ErrorMessage = "VIN must be 17 characters.")]
                public string VIN { get; set; } = null!;
                [Range(1886, int.MaxValue, ErrorMessage = "Manufacture year must be a positive number and not less than 1886.")]
                [MaxCurrentYear]
                public int? ManufactureYear { get; set; }
                [Range(0, (double)decimal.MaxValue, ErrorMessage = "CurrentMileage must be non-negative.")] public decimal CurrentMileage { get; set; }
                [StringLength(50)] public string? Color { get; set; }
                [Required, StringLength(20)]
                [RegularExpression(@"^(?:\d{2}\p{Lu}{0,2}\d?)-?\d{4,5}$",
                    ErrorMessage = "Invalid plate format. 01X-12345 / 34CD-1234 / 01X1-12345 / 34B1-1234")]
                public string Plate { get; set; } = null!;
        }

        public class VehicleUpdateRequestDto
        {
                [StringLength(100)] public string? Model { get; set; }
                [StringLength(50), RegularExpression(@"^[A-HJ-NPR-Z0-9]{17}$", ErrorMessage = "VIN must be 17 characters.")]
                public string? VIN { get; set; }
                [Range(1886, int.MaxValue, ErrorMessage = "Manufacture year must be a positive number and not less than 1886.")]
                [MaxCurrentYear]
                public int? ManufactureYear { get; set; }
                [Range(0, (double)decimal.MaxValue, ErrorMessage = "CurrentMileage must be non-negative.")]
                public decimal? CurrentMileage { get; set; }
                [StringLength(50)] public string? Color { get; set; }
                [StringLength(20)]
                [RegularExpression(@"^(?:\d{2}\p{Lu}{0,2}\d?)-?\d{4,5}$",
                    ErrorMessage = "Invalid plate format. 01X-12345 / 34CD-1234 / 01X1-12345 / 34B1-1234")]
                public string? Plate { get; set; }
                public VehicleStatus? Status { get; set; }
        }
}
