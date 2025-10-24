using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EVServiceCenterMaintenanceAPI.Attributes
{
    public partial class EmailOrUsernameAttribute : ValidationAttribute
    {
        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
        private static partial Regex EmailRegex();

        [GeneratedRegex(@"^(?!.*[._-]{2})[a-z][\w.-]{2,19}$", RegexOptions.IgnoreCase)]
        private static partial Regex UsernameRegex();

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var input = value?.ToString();
            if (string.IsNullOrEmpty(input))
                return ValidationResult.Success;

            if (EmailRegex().IsMatch(input))
            {
                var emailAttribute = new EmailAddressAttribute();
                if (!emailAttribute.IsValid(input))
                {
                    return new ValidationResult("Invalid email format.");
                }
            }
            else
            {
                if (!UsernameRegex().IsMatch(input))
                {
                    return new ValidationResult("Invalid username format. Username must be 3-20 characters, start with a letter, and contain only letters, numbers, dots, underscores, or hyphens.");
                }
            }

            return ValidationResult.Success;
        }
    }
}

