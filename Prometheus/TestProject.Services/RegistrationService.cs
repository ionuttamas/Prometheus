namespace TestProject.Services
{
    public class RegistrationService
    {
        public void Register(Customer customer)
        {
            customer.IsActive = true;
        }
    }
}