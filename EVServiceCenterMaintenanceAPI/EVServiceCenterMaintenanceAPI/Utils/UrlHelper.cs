namespace EVServiceCenterMaintenanceAPI.Utils
{
    public static class UrlHelper
    {
        public static string? ToAbsoluteUrl(this HttpContext context, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            //Base64
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            //Http, https
            if (IsAbsoluteUrl(path)) return path;

            // relative path
            string cleanPath = path.TrimStart('/');
            return $"{context.Request.Scheme}://{context.Request.Host}/{cleanPath}";
        }

        private static bool IsAbsoluteUrl(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
