namespace TestProject._3rdParty
{
    public  class BackgroundCheckHelper
    {
        public static bool ValidateSsnPure(string ssn, string name)
        {
            return true;
        }

        public static bool ValidateSsnImpure(string ssn, string name) {
            return true;
        }

        public static PaymentResult StaticProcessPaymentPure(string from, string to, decimal amount) {
            return new PaymentResult {
                Message = "OK"
            };
        }

        public static PaymentResult StaticProcessPaymentImpure(string from, string to, decimal amount) {
            return new PaymentResult {
                Message = "OK"
            };
        }
    }
}
