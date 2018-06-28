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
            var simpleImplicitCustomer = SimpleImplicitReturn(from, to, amount);
            var complexImplicitCustomer = ComplexImplicitReturn(from, to, amount);

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

        private Customer SimpleImplicitReturn(Customer from, Customer to, decimal amount) {
            if (amount < -10) {
                if (!from.IsActive && from.AccountBalance == 20) {
                    return null;
                } else if (from.Type == CustomerType.Gold && from.AccountBalance ==30) {
                    return new Customer();
                }
            }

            return to;
        }

        private Customer ComplexImplicitReturn(Customer from, Customer to, decimal amount) {
            if (amount > 0) {
                if (from.Type == CustomerType.Premium) {
                    return new Customer();
                } else if (from.Type == CustomerType.Gold) {
                    return from;
                } else {
                    return new Customer();
                }
            }

            if (amount < -10) {
                if (!from.IsActive && from.AccountBalance == 20) {
                    return null;
                } else if (from.Type == CustomerType.Gold && from.AccountBalance == 30) {
                    return new Customer();
                }
            }

            return to;
        }
    }
}