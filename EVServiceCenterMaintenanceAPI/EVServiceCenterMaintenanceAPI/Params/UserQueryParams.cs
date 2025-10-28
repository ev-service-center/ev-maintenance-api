using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class UserQueryParams : QueryParams
    {
        public UserRole? Role { get; set; }
        public UserStatus? StatusUser { get; set; }
        public bool? WithoutEmployee { get; set; }

        public override (bool IsValid, string ErrorMessage) Validate()
        {
            var baseValidation = base.Validate();
            if (!baseValidation.IsValid)
            {
                return baseValidation;
            }

            return (true, string.Empty);
        }
    }
}
