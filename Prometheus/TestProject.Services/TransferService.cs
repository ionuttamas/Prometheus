namespace TestProject.Services
{
    public class TransferService {
        public void Transfer(Customer from, Customer to, decimal amount)
        {
            from.AccountBalance -= amount;
            to.AccountBalance += amount;
        }

        public void SimpleIfTransfer(Customer from, Customer to, decimal amount) {
            if (from.Type == CustomerType.Premium)
            {
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
        }
    }
}