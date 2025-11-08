using System.ComponentModel.DataAnnotations;

namespace EVServiceCenterMaintenanceAPI.Attributes
{
    public class MaxCurrentYearAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success;

            if (value is int year)
            {
                var currentYear = DateTime.Now.Year;
                if (year > currentYear)
                {
                    return new ValidationResult($"Manufacture year cannot exceed the current year ({currentYear}).");
                }
            }

            return ValidationResult.Success;
        }
    }
}
