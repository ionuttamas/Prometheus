namespace TestProject._3rdParty
{
    public class PaymentProvider
    {
        public bool ValidatePayment(string from, string to, decimal amount)
        {
            return true;
        }

        public PaymentResult ProcessPayment(string from, string to, decimal amount) {
            return new PaymentResult
            {
                Message = "OK"
            };
        }
    }
}