namespace TestProject.Services
{
    public class CustomerValidator
    {
        private readonly int threshold;
        private readonly int lowAge;
        private readonly int highAge;

        public CustomerValidator(int threshold, int lowAge, int highAge)
        {
            this.threshold = threshold;
            this.lowAge = lowAge;
            this.highAge = highAge;
        }

        public bool IsValid(Customer customer)
        {
            if (customer.AccountBalance > threshold && lowAge < customer.Age && customer.Age < highAge)
                return true;

            return false;
        }

        public static bool IsValidStatic(Customer customer, int threshold)
        {
            if (customer.AccountBalance > threshold)
                return true;

            return false;
        }
    }
}