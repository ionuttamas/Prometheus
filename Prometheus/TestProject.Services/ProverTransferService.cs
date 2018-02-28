namespace TestProject.Services
{
    public class ProverTransferService
    {
        public void Transfer(Customer from, Customer to, decimal amount) {
            from.AccountBalance -= amount;
            to.AccountBalance += amount;
        }

        public void SimpleIfTransfer(Customer from, Customer to, decimal amount) {
            Customer customer;

            if (from.Type == CustomerType.Premium) {
                customer = from;
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
        }

        public void SimpleIf_NegatedTransfer(Customer from, Customer to, decimal amount) {
            if (from.Type != CustomerType.Premium) {
                Customer customer = from;
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
        }

        public void SimpleIfSingleElseTransfer(Customer from, Customer to, decimal amount) {
            Customer customer;

            if (from.Type == CustomerType.Premium) {
                customer = from;
            } else {
                customer = from;
            }
        }

        public void NestedCall_SimpleIf_SimpleIfTransfer_SatisfiableCounterpart(Customer from, Customer to, decimal amount)
        {
            Customer customer;

            if (!from.IsActive && from.AccountBalance < 50 && amount < 30)
            {
                customer = from;
            }
        }

        public void NestedCall_SimpleIf_SimpleIfTransfer(Customer from2, Customer to, decimal amount) {
            Customer referenceCustomer;

            if (from2.Age > 30) {
                referenceCustomer = from2;
                TransferInternal(referenceCustomer, to, amount);
            }
        }

        private void TransferInternal(Customer from, Customer to, decimal amount) {
            Customer customer;

            if (amount > 0) {
                if (from.Age == 30) {
                    customer = from;
                } else if (from.Age == 40) {
                    customer = from;
                } else {
                    customer = from;
                }
            } else if (amount < 0) {
                if (!from.IsActive && from.AccountBalance < 0) {
                    customer = from;
                } else if (from.Age == 40 && from.AccountBalance < 0) {
                    customer = from;
                }
            } else {
                if (!from.IsActive && from.DeliveryAddress.StreetAddress =="da") {
                    customer = from;
                }
            }
        }
    }
}