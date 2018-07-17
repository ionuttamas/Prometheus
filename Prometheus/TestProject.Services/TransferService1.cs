using System.Collections.Generic;
using System.Linq;
using TestProject._3rdParty;

namespace TestProject.Services
{
    public class TransferService1
    {
        private readonly CustomerRepository _customerRepository;
        private readonly List<Customer> customers;
        private readonly PaymentProvider paymentProvider;

        public TransferService1(CustomerRepository customerRepository, List<Customer> customers, PaymentProvider paymentProvider)
        {
            _customerRepository = customerRepository;
            this.customers = customers;
            //TODO: this does not work paymentProvider = new PaymentProvider();
            this.paymentProvider = paymentProvider;
        }

        public void If_NullCheck(Customer from1) {
            if (from1 != null) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_StaticPureCall(Customer from1) {
            if (from1 != null && BackgroundCheckHelper.ValidateSsnPure(from1.Ssn, from1.Name)) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_PureStaticAssignment(Customer from1)
        {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnPure(from1.Ssn, from1.Name);

            if (from1 != null && isSsnValid) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_ImpureStaticAssignment(Customer from1) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnImpure(from1.Ssn, from1.Name);

            if (from1 != null && isSsnValid) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_PureReferenceCall(Customer from1, Customer to1, decimal amount) {
            if (from1 != null && paymentProvider.ValidatePaymentPure(from1.Name, to1.Name, amount)) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_ImpureReferenceCall(Customer from1, Customer to1, decimal amount) {
            if (from1 != null && paymentProvider.ValidatePaymentImpure(from1.Name, from1.Name, amount)) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck(Customer from1, Customer to1, decimal amount)
        {
            var result = paymentProvider.ProcessPaymentPure(from1.Name, to1.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_PureMethodStaticAssignment_MemberCheck(Customer from1, Customer to1, decimal amount) {
            var result = BackgroundCheckHelper.StaticProcessPaymentPure(from1.Name, to1.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_PureMethodReferenceAssignment_DirectCheck(Customer from1, Customer to1, decimal amount) {
            var isPaymentValid = paymentProvider.ValidatePaymentPure(from1.Name, to1.Name, amount);

            if (amount > 0 && isPaymentValid) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck(Customer from1, Customer to1, decimal amount) {
            var result = paymentProvider.ProcessPaymentImpure(from1.Name, to1.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck(Customer from1, Customer to1, decimal amount) {
            var result = BackgroundCheckHelper.StaticProcessPaymentImpure(from1.Name, to1.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer1 = from1;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck(Customer from1, Customer to1, decimal amount) {
            var isPaymentValidX = paymentProvider.ValidatePaymentImpure(from1.Name, to1.Name, amount);

            if (amount > 0 && isPaymentValidX) {
                Customer customer1 = from1;
            }
        }

        public void MethodAssignment_IfTransfer(Customer from1, Customer to1, decimal amount) {
            var customer = _customerRepository.Compute(from1.Age, to1.Age);

            if (from1.AccountBalance > 0) {
                Customer refCustomer = customer;
            }

            if (from1.Type == CustomerType.Premium) {
                var enumCustomer1 = to1;
            }

            if (from1.Type == CustomerType.Premium) {
                var unsatEnumCustomer1 = to1;
            }

            if (from1.Type == CustomerType.Premium)
            {
                from1 = to1;
            }

            var selfReferentialCustomer1 = from1;
            Customer firstIndexedCustomer = _customerRepository.GetFirstIndexed();
            Customer keyIndexedCustomer = _customerRepository.GetKeyIndexed();
            Customer firstCustomer = _customerRepository.GetFirst(from1.AccountBalance);
            List<Customer> whereCustomers = _customerRepository.GetWhere(from1.Age);
            Customer innerMethodCustomer = GetInternalCustomer(from1, to1, amount);
            Customer innerFirstLinqMethodCustomer = GetInternalCustomer_WithFirstLinq(from1, to1, amount);
            Customer staticFirstLinqMethodCustomer = CustomerUtil.GetFirstLinq(customers, from1, amount);
            Customer innerNestedReferenceMethodCustomer = GetNestedReferenceCallCustomer(from1, to1, amount);
        }

        public void MethodAssignment_WithIndexQuery_1(Customer from1, Customer to1, decimal amount) {
            var customers = _customerRepository.GetWhere(to1.Age);
            Customer indexCustomer1 = customers[(from1.Age + to1.Age)* to1.Age];
        }

        public void MethodAssignment_WithFirstQuery_1(Customer from1, Customer to1, decimal amount) {
            var customers = _customerRepository.GetWhere(to1.Age);
            //TODO: support reference type comparison as "x.DeliveryAddress == to1.DeliveryAddress"
            Customer firstCustomer1 = customers.First(x => (x.Age == from1.Age || x.AccountBalance==from1.AccountBalance) && x.DeliveryAddress.City == to1.DeliveryAddress.City);
        }

        public void MethodAssignment_WithWhereQuery_1(Customer from1, Customer to1, decimal amount) {
            var customers = _customerRepository.GetWhere(to1.Age);
            var whereCustomers1 = customers.Where(x => (x.Age == from1.Age || x.AccountBalance == from1.AccountBalance) && x.DeliveryAddress.City == to1.DeliveryAddress.City);
        }

        public void EnumAssignment1(Customer from, Customer to, decimal amount) {
            if (from.Type == CustomerType.Premium) {
                var customer = from;
            }
        }

        public void EnumAssignment(Customer from, Customer to, decimal amount) {
            if (from.Type == CustomerType.Gold) {
                var customer = from;
            }
        }

        private Customer GetInternalCustomer(Customer innerFrom, Customer innerTo, decimal amount) {
            var customer = innerFrom;

            return customer;
        }

        private Customer GetInternalCustomer_WithFirstLinq(Customer innerFrom, Customer innerTo, decimal amount) {
            var customer = customers.First(x => x.Age == innerFrom.Age);

            if (innerFrom.Age > innerTo.Age)
                return customers.First(x => x.Name == innerFrom.Name);

            return customer;
        }

        private Customer GetNestedReferenceCallCustomer(Customer innerFrom, Customer innerTo, decimal amount)
        {
            var customer = _customerRepository.Compute(innerFrom.Age, innerTo.Age);

            return customer;
        }
    }
}