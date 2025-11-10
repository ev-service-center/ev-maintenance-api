using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;
using System.Security.Cryptography;
using System.Text;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class PayOSService
    {
        private readonly PayOS _payOS;
        private readonly string _checksumKey;
        private readonly string _environment;

        public PayOSService(IOptions<PayOSSettings> options, IConfiguration configuration)
        {
            var settings = options.Value;

            if (string.IsNullOrEmpty(settings.ClientId) || string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.ChecksumKey))
            {
                throw new ArgumentException("PayOS configuration is missing or invalid");
            }

            _payOS = new PayOS(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
            _checksumKey = settings.ChecksumKey;
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
            List<ItemData> items, string cancelUrl, string returnUrl)
        {
            // Generate unique orderCode từ workOrderId
            long orderCode = GenerateOrderCode(workOrderId);

            PaymentData paymentData = new PaymentData(
                orderCode,
                (int)amount,
                description,
                items,
                cancelUrl,
                returnUrl
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
