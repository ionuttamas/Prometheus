namespace TestProject.Services
{
    public class RegistrationService
    {
        public void Register(Customer customer)
        {
            customer.IsActive = true;
            Customer instance;
            instance = customer;
        }

        public void SimpleIfRegister(Customer customer)
        {
            if (customer.Type == CustomerType.Gold)
            {
                customer.IsActive = true;
            }
        }
    }
}