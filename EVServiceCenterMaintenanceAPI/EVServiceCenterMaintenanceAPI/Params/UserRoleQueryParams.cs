using EVServiceCenterMaintenanceAPI.Enums;

namespace EVServiceCenterMaintenanceAPI.Params
{
    public class UserRoleQueryParams : QueryParams
    {
        public UserStatus? StatusUser { get; set; }

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
