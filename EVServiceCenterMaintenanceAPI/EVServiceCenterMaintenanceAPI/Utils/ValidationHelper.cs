using System.Text.RegularExpressions;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public static partial class ValidationHelper
    {
        [GeneratedRegex(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[!@#$%^&*])")]
        private static partial Regex PasswordComplexityRegex();

        [GeneratedRegex(@"[A-Z]")]
        private static partial Regex HasUppercaseRegex();

        [GeneratedRegex(@"[a-z]")]
        private static partial Regex HasLowercaseRegex();

        [GeneratedRegex(@"\d")]
        private static partial Regex HasDigitRegex();

        [GeneratedRegex(@"[!@#$%^&*]")]
        private static partial Regex HasSpecialCharRegex();

        public static (bool IsValid, string ErrorMessage) ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return (false, "Password cannot be empty.");

            if (!PasswordComplexityRegex().IsMatch(password))
            {
                if (!HasUppercaseRegex().IsMatch(password))
                    return (false, "Password must contain at least one uppercase letter.");
                if (!HasLowercaseRegex().IsMatch(password))
                    return (false, "Password must contain at least one lowercase letter.");
                if (!HasDigitRegex().IsMatch(password))
                    return (false, "Password must contain at least one digit.");
                if (!HasSpecialCharRegex().IsMatch(password))
                    return (false, "Password must contain at least one special character.");

                return (false, "Password does not meet complexity requirements.");
            }

            return (true, string.Empty);
        }
    }
}
