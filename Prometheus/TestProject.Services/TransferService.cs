namespace TestProject.Services
{
    public class TransferService {
        public void Transfer(Customer from, Customer to, decimal amount)
        {
            from.AccountBalance -= amount;
            to.AccountBalance += amount;
        }

        public void SimpleIfTransfer(Customer from, Customer to, decimal amount) {
            Customer customer;

            if (from.Type == CustomerType.Premium)
            {
                customer = from;
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
        }

        public void SimpleIfSingleElseTransfer(Customer from, Customer to, decimal amount)
        {
            Customer customer;

            if (from.Type == CustomerType.Premium)
            {
                customer = from;
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
            else
            {
                customer = from;
                from.AccountBalance -= 1.1m * amount;
                to.AccountBalance += 1.1m * amount;
            }
        }

        public void SimpleIfMultipleElseTransfer(Customer from, Customer to, decimal amount) {
            Customer customer;

            if (from.Type == CustomerType.Premium) {
                customer = from;
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            } else if (from.Type == CustomerType.Gold) {
                customer = from;
                from.AccountBalance -= 0.9m * amount;
                to.AccountBalance += amount;
            } else {
                customer = from;
                from.AccountBalance -= 1.1m * amount;
                to.AccountBalance += amount;
            }
        }

        public void NestedIfElseTransfer(Customer from, Customer to, decimal amount)
        {
            Customer customer;

            if (amount > 0)
            {
                if (from.Type == CustomerType.Premium)
                {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                }
                else if (from.Type == CustomerType.Gold)
                {
                    customer = from;
                    from.AccountBalance -= 0.9m*amount;
                    to.AccountBalance += amount;
                }
                else
                {
                    customer = from;
                    from.AccountBalance -= 1.1m*amount;
                    to.AccountBalance += amount;
                }
            }
        }

        public void NestedIfElse_With_IfElseTransfer(Customer from, Customer to, decimal amount) {
            Customer customer;

            if (amount > 0) {
                if (from.Type == CustomerType.Premium) {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                } else if (from.Type == CustomerType.Gold) {
                    customer = from;
                    from.AccountBalance -= 0.9m * amount;
                    to.AccountBalance += amount;
                } else {
                    customer = from;
                    from.AccountBalance -= 1.1m * amount;
                    to.AccountBalance += amount;
                }
            } else if (amount < 0)
            {
                if (!from.IsActive && from.AccountBalance < 0)
                {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                }
                else if (from.Type == CustomerType.Gold && from.AccountBalance < 0)
                {
                    customer = from;
                    from.AccountBalance -= 0.9m*amount;
                    to.AccountBalance += amount;
                }
            }
            else
            {
                if (!from.IsActive && from.AccountBalance > 0) {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                }
            }
        }

        public void NestedCall_SimpleIf_SimpleIfTransfer(Customer from, Customer to, decimal amount)
        {
            Customer referenceCustomer;

            if (from.Age > 30) {
                referenceCustomer = from;
                TransferInternal(referenceCustomer, to, amount);
            }
        }

        private void TransferInternal(Customer from, Customer to, decimal amount)
        {
            Customer customer;

            if (amount > 0) {
                if (from.Type == CustomerType.Premium) {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                } else if (from.Type == CustomerType.Gold) {
                    customer = from;
                    from.AccountBalance -= 0.9m * amount;
                    to.AccountBalance += amount;
                } else {
                    customer = from;
                    from.AccountBalance -= 1.1m * amount;
                    to.AccountBalance += amount;
                }
            } else if (amount < 0) {
                if (!from.IsActive && from.AccountBalance < 0) {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                } else if (from.Type == CustomerType.Gold && from.AccountBalance < 0) {
                    customer = from;
                    from.AccountBalance -= 0.9m * amount;
                    to.AccountBalance += amount;
                }
            } else {
                if (!from.IsActive && from.AccountBalance > 0) {
                    customer = from;
                    from.AccountBalance -= amount;
                    to.AccountBalance += amount;
                }
            }
        }
    }

    public class TransferService2
    {
        public void SimpleIfTransfer(Customer from, Customer to, decimal amount) {
            if (amount>0)
            {
                Customer customer = @from;
                customer.AccountBalance -= 100;
            }
        }

        public void SimpleIfTransfer2(Customer from, Customer to, decimal amount)
        {
            Customer customer;
            if (amount < 0)
            {
                customer = @from;
                customer.AccountBalance += 100;
            }
        }

        public void SimpleIfTransfer3(Customer from, Customer to, decimal amount)
        {
            Customer customer;

            if (amount < 2 && amount>-1) {
                customer = @from;
                customer.AccountBalance += 100;
            }
        }
    }
}