namespace TestProject.Services
{
    public class TransferService {
        public void Transfer(Customer from, Customer to, decimal amount)
        {
            from.AccountBalance -= amount;
            to.AccountBalance += amount;
        }
    }
}