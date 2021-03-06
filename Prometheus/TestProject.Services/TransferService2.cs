using System.Linq;
using TestProject._3rdParty;

namespace TestProject.Services
{
    public class TransferService2 {
        //TODO: handle interface reference tracking
        private readonly CustomerRepository customerRepository2;
        private readonly PaymentProvider paymentProvider2;
        private readonly IField field2;
        private readonly CustomerValidator validator2;

        public TransferService2(CustomerRepository customerRepository, PaymentProvider paymentProvider, IField field, CustomerValidator validator)
        {
            this.customerRepository2 = customerRepository;
            this.paymentProvider2 = paymentProvider;
            this.field2 = field;
            this.validator2 = validator;
        }

        public void SimpleAlgebraicAssignment_Sat(Customer from2, Customer to2, decimal amount) {
            var ageExpression = 2 * from2.Age + to2.Age;

            if (ageExpression + 4 <= 30) {
                var customer2 = from2;
            }
        }

        public void SimpleAlgebraicAssignment_Unsat(Customer from2, Customer to2, decimal amount)
        {
            var ageExpression = 2 * from2.Age;

            if (ageExpression + 4 <= 19) {
                var customer2 = from2;
            }
        }


        public void Polymorphic_VariousFields_ReferenceCall(Customer from2) {
            var result = field2.Compute(100);
            Customer customer2;

            if (result > 0) {
                customer2 = from2;
            } else {
                customer2 = null;
            }
        }


        public void IfCheck_Sat_LocallyInitialized_FieldReferenceCall(Customer from2) {
            var localValidator2 = new CustomerValidator(200, 30, 50);

            if (localValidator2.IsValid(from2) && from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Unsat_LocallyInitialized_FieldReferenceCall(Customer from2) {
            var localValidator2 = new CustomerValidator(200, 50, 60);

            if (localValidator2.IsValid(from2) && from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Sat_FieldReferenceCall(Customer from2) {
            if (validator2.IsValid(from2) && from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Unsat_FieldReferenceCall(Customer from2) {
            if (validator2.IsValid(from2) && from2 == null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Sat_ThisReferenceCall(Customer from2) {
            if (IsValid(from2) && from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Unsat_ThisReferenceCall(Customer from2) {
            if (IsValid(from2) && from2 == null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Sat_LocalStaticCall(Customer from2) {
            if (IsValid(from2) && from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Unsat_LocalStaticCall(Customer from2) {
            if (from2 != null && from2.Age < 10 && from2.AccountBalance > 100) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Sat_ExternalStaticCall(Customer from2) {
            if (CustomerValidator.IsValidStatic(from2, 100) && from2 != null) {
                Customer customer2 = from2;
            }
        }

        public void IfCheck_Unsat_ExternalStaticCall(Customer from2) {
            if (from2 != null && from2.AccountBalance < 100) {
                Customer customer2 = from2;
            }
        }

        private bool IsValid(Customer customer) {
            if (customer.IsActive && customer.Age > 28)
                return true;

            if (customer.AccountBalance == 200)
                return true;

            return false;
        }

        public void StringConstantTransfer(Customer from2, Customer to2, decimal amount) {
            if (from2.DeliveryAddress.City == "abc" && to2.DeliveryAddress.City == Constants.STRING_CONST_XYZ) {
                var customer2 = from2;
            }
        }

        public void Unsat_StringConstantTransfer(Customer from2, Customer to2, decimal amount) {
            if (from2.DeliveryAddress.City == "abc" && to2.DeliveryAddress.City == "gef") {
                var customer2 = from2;
            }
        }

        public void IntConstantTransfer(Customer from1, Customer to1, decimal amount) {
            if (from1.Age == 100 && to1.Age == Constants.INT_CONST_200) {
                var customer2 = from1;
            }
        }

        public void Unsat_IntConstantTransfer(Customer from2, Customer to2, decimal amount) {
            if (from2.Age == Constants.INT_CONST_100 && to2.Age == Constants.INT_CONST_100) {
                var customer2 = from2;
            }
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

        public void If_3rdPartyCheck_StaticPureCall(Customer from2) {
            if (from2 != null && BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_StaticImpureCall(Customer from2) {
            if (from2 != null && BackgroundCheckHelper.ValidateSsnImpure(from2.Ssn, from2.Name)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Sat_StaticImpureCall(Customer from2) {
            if (from2 != null && !BackgroundCheckHelper.ValidateSsnImpure(from2.Ssn, from2.Name)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_StaticPureCall(Customer from2) {
            if (from2 != null && !BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Sat_StaticPureCall_DifferentArgs(Customer from2, Customer to2) {
            if (from2 != null && !BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name)) {
                Customer customer2 = to2;
            }
        }

        public void If_3rdPartyCheck_Sat_PureStaticAssignment_DirectCheck(Customer from2) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name);

            if (from2 != null && isSsnValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureStaticAssignment_DirectCheck(Customer from1) {
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

        public void If_3rdPartyCheck_Unsat_StaticAssignment_DirectCheck(Customer from2) {
            var isSsnValid = BackgroundCheckHelper.ValidateSsnPure(from2.Ssn, from2.Name);

            if (from2 != null && !isSsnValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_PureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && paymentProvider2.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_PureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && !paymentProvider2.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && paymentProvider2.ValidatePaymentPure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_ImpureReferenceCall(Customer from2, Customer to2, decimal amount) {
            if (from2 != null && !paymentProvider2.ValidatePaymentImpure(from2.Name, to2.Name, amount)) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider2.ProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Sat_Negated_PureMethodReferenceAssignment_DifferentArgs_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider2.ProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 5 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_Negated_PureMethodReferenceAssignment_DifferentArgs_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result2 = paymentProvider2.ProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && !result2.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Sat_PureMethodStaticAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = BackgroundCheckHelper.StaticProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_PureMethodStaticAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = BackgroundCheckHelper.StaticProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider2.ProcessPaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Sat_PureMethodReferenceAssignment_DirectCheck(Customer from2, Customer to2, decimal amount) {
            var isPaymentValid = paymentProvider2.ValidatePaymentPure(from2.Name, to2.Name, amount);

            if (amount > 10 && isPaymentValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment_DirectCheck(Customer from2, Customer to2, decimal amount) {
            var isPaymentValid = paymentProvider2.ValidatePaymentPure(from2.Name, to2.Name, amount);

            if (amount > 0 && !isPaymentValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider2.ProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = BackgroundCheckHelper.StaticProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Sat_ImpureMethodStaticAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = BackgroundCheckHelper.StaticProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck(Customer from2, Customer to2, decimal amount) {
            var isPaymentValid = paymentProvider2.ValidatePaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && isPaymentValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment_DirectCheck(Customer from2, Customer to2, decimal amount) {
            var isPaymentValid = paymentProvider2.ValidatePaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 0 && !isPaymentValid) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider2.ProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount > 50 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void If_3rdPartyCheck_Negated_Unsat_ImpureMethodReferenceAssignment_MemberCheck(Customer from2, Customer to2, decimal amount) {
            var result = paymentProvider2.ProcessPaymentImpure(from2.Name, to2.Name, amount);

            if (amount < 0 && !result.IsSuccessful) {
                Customer customer2 = from2;
            }
        }

        public void MethodAssignment_SimpleAssign(Customer customer)
        {
            var refCustomer = customer;
        }

        public void MethodAssignment_IfTransfer(Customer from, Customer to, decimal amount)
        {
            var customer = customerRepository2.Get(from.Age, to.Age);

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
            var customers = customerRepository2.GetWhere(to2.Age);
            Customer indexCustomer2 = customers[to2.Age * to2.Age + from2.Age * to2.Age];
        }

        public void MethodAssignment_WithFirstQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = customerRepository2.GetWhere(to2.Age);
            Customer firstCustomer2 = customers.FirstOrDefault(x => (x.AccountBalance == from2.AccountBalance && x.DeliveryAddress.City == to2.DeliveryAddress.City) || (x.Age == from2.Age && x.DeliveryAddress.City == to2.DeliveryAddress.City));
        }

        public void MethodAssignment_WithWhereQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = customerRepository2.GetWhere(to2.Age);
            var whereCustomers2 = customers.Where(x => x.AccountBalance == from2.AccountBalance && x.DeliveryAddress.City == to2.DeliveryAddress.City || x.Age == from2.Age && x.DeliveryAddress.City == to2.DeliveryAddress.City);
        }
    }
}