using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class PayOSService
    {
        private readonly PayOS _payOS;
        private readonly string _environment;

        public PayOSService(IOptions<PayOSSettings> options, IConfiguration configuration)
        {
            var settings = options.Value;

            if (string.IsNullOrEmpty(settings.ClientId) || string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.ChecksumKey))
            {
                throw new ArgumentException("PayOS configuration is missing or invalid");
            }

            _payOS = new PayOS(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
            _environment = configuration["Environment"] ?? "Test";
        }

        public long GenerateOrderCode(int workOrderId)
        {
            string prefix = _environment == "Production" ? "2" : "1";

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");

            string workOrderIdStr = workOrderId.ToString();

            string orderCodeStr = $"{prefix}{timestamp}{workOrderIdStr}";

            return long.Parse(orderCodeStr);
        }

        public async Task<CreatePaymentResult> CreatePaymentLink(int workOrderId, decimal amount, string description,
            List<ItemData> items, string cancelUrl, string returnUrl, int? expirationMinutes = 15)
        {
            // Generate unique orderCode từ workOrderId
            long orderCode = GenerateOrderCode(workOrderId);

            // Set expiration time (default: 15 minutes from now)
            long? expiredAt = expirationMinutes.HasValue
                ? (long)DateTimeOffset.UtcNow.AddMinutes(expirationMinutes.Value).ToUnixTimeSeconds()
                : null;

            PaymentData paymentData = new PaymentData(
                orderCode,
                (int)amount,
                description,
                items,
                cancelUrl,
                returnUrl,
                expiredAt: expiredAt
            );

            return await _payOS.createPaymentLink(paymentData);
        }

        public async Task<PaymentLinkInformation> GetPaymentLinkInformation(long orderCode)
        {
            try
            {
                PaymentLinkInformation paymentLinkInfo = await _payOS.getPaymentLinkInformation(orderCode);
                return paymentLinkInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get payment link information for orderCode {orderCode}: {ex.Message}", ex);
            }
        }

        public void VerifyPaymentWebhookData(WebhookType webhookType)
        {
            _payOS.verifyPaymentWebhookData(webhookType);
        }
    }
}
