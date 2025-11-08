using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;

namespace EVServiceCenterMaintenanceAPI.Utils
{
    public class PayOSService
    {
        private readonly PayOS _payOS;

        public PayOSService(IOptions<PayOSSettings> options)
        {
            var settings = options.Value;

            if (string.IsNullOrEmpty(settings.ClientId) || string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.ChecksumKey))
            {
                throw new ArgumentException("PayOS configuration is missing or invalid");
            }

            _payOS = new PayOS(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        }

        public async Task<CreatePaymentResult> CreatePaymentLink(int orderCode, decimal amount, string description,
            List<ItemData> items, string cancelUrl, string returnUrl)
        {
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
    }
}
