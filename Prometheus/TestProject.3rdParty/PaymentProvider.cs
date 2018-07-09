namespace TestProject._3rdParty
{
    public class PaymentProvider
    {
        public bool ValidatePaymentPure(string from, string to, decimal amount)
        {
            return true;
        }

        public bool ValidatePaymentImpure(string from, string to, decimal amount) {
            return true;
        }

        public PaymentResult ProcessPaymentPure(string from, string to, decimal amount) {
            return new PaymentResult
            {
                Message = "OK"
            };
        }

        public PaymentResult ProcessPaymentImpure(string from, string to, decimal amount) {
            return new PaymentResult {
                Message = "OK"
            };
        }
    }
}