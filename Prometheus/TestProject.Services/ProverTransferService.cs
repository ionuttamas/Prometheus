namespace TestProject.Services
{
    public class ProverTransferService
    {
        public void Transfer(Customer from, Customer to, decimal amount) {
            from.AccountBalance -= amount;
            to.AccountBalance += amount;
        }

        public void SimpleIfTransfer(Customer from, Customer to, decimal amount) {
            if (from.AccountBalance > 0) {
                Customer customer = from;
                from.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
        }

        public void SimpleIf_NegatedTransfer(Customer from2, Customer to, decimal amount) {
            if (from2.AccountBalance < 0) {
                Customer referenceCustomer = from2;
                from2.AccountBalance -= amount;
                to.AccountBalance += amount;
            }
        }

        public void StringCondition_SimpleIfTransfer(Customer from, Customer to, decimal amount)
        {
            if (from.DeliveryAddress.City == "Berlin")
            {
                Customer customer = from;
            }
        }

        public void StringCondition_SimpleIf_NegatedTransfer(Customer from2, Customer to, decimal amount)
        {
            if (from2.DeliveryAddress.City == "Berlin")
            {
                Customer referenceCustomer = from2;
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
            if (!from.IsActive && from.AccountBalance < 50 && amount < 30)
            {
                Customer customer = from;
            }
        }

        public void NestedCall_SimpleIf_SimpleIfTransfer(Customer from2, Customer to, decimal amount) {
            if (from2.Age > 30)
            {
                Customer referenceCustomer = from2;
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
                } else if (from.Age == 40 && from.AccountBalance > 60)
                {
                    Customer exclusiveCustomer = from;
                }
            } else {
                if (!from.IsActive && from.DeliveryAddress.StreetAddress =="da") {
                    customer = from;
                }
            }
        }
    }
}