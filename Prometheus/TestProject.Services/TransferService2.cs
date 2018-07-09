using System.Linq;
using TestProject._3rdParty;

namespace TestProject.Services
{
    public class TransferService2 {
        //TODO: handle interface reference tracking
        private readonly CustomerRepository customerRepository;
        private readonly PaymentProvider paymentProvider;

        public TransferService2(CustomerRepository customerRepository, PaymentProvider paymentProvider)
        {
            this.customerRepository = customerRepository;
            this.paymentProvider = paymentProvider;
        }

        public void If_NullCheck_Satisfiable(Customer from2) {
            if (from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void If_NullCheck_Unsatisfiable(Customer from2, Customer to2) {
            if (from2 == null) {
                Customer customer2 = to2;
            }
        }

        public void If_3rdPartyCheck_StaticCall(Customer from2) {
            if (from2 != null && BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_StaticCall(Customer from2) {
            if (from2 != null && !BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Sat_PureStaticAssignment(Customer from2) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name);

            if (from2 != null && isSsnValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureStaticAssignment(Customer from1) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnImpure(from1.Ssn, from1.Name);

            if (from1 != null && isSsnValid) {
                Customer customer2 = from1;
            }
        }

        public void If_3rdPartyCheck_Negated_ImpureStaticAssignment(Customer from1) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnImpure(from1.Ssn, from1.Name);

            if (from1 != null && !isSsnValid) {
                Customer customer2 = from1;
            }
        }

        public void If_3rdPartyCheck_Unsat_StaticAssignment(Customer from2) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name);

            if (from2 != null && !isSsnValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_PureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && paymentProvider.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_PureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && !paymentProvider.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && paymentProvider.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_ImpureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && !paymentProvider.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_PureMethodReferenceAssignment(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider.ProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider.ProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodReferenceAssignment(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider.ProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider.ProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void MethodAssignment_SimpleAssign(Customer customer)
        {
            var refCustomer = customer;
        }

        public void MethodAssignment_IfTransfer(Customer from, Customer to, decimal amount)
        {
            var customer = customerRepository.Get(from.Age, to.Age);

            if (from.AccountBalance > 0) {
                var refCustomer = customer;
            }

            if (from.Type == CustomerType.Premium) {
                var enumCustomer2 = to;
            }

            if (from.Type == CustomerType.Gold) {
                var unsatEnumCustomer2 = to;
            }

            if (from.Type == CustomerType.Premium)
            {
                from = to;
            }
            var selfReferentialCustomer2 = from;
        }

        public void MethodAssignment_WithIndexQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = customerRepository.GetWhere(to2.Age);
            Customer indexCustomer2 = customers[to2.Age * to2.Age + from2.Age * to2.Age];
        }

        public void MethodAssignment_WithFirstQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = customerRepository.GetWhere(to2.Age);
            Customer firstCustomer2 = customers.FirstOrDefault(x => (x.AccountBalance == from2.AccountBalance && x.DeliveryAddress.City == to2.DeliveryAddress.City) || (x.Age == from2.Age && x.DeliveryAddress.City == to2.DeliveryAddress.City));
        }

        public void MethodAssignment_WithWhereQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = customerRepository.GetWhere(to2.Age);
            var whereCustomers2 = customers.Where(x => x.AccountBalance == from2.AccountBalance && x.DeliveryAddress.City == to2.DeliveryAddress.City || x.Age == from2.Age && x.DeliveryAddress.City == to2.DeliveryAddress.City);
        }
    }
}