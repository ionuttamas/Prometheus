namespace TestProject.Services
{
    public class TestCustomerFactory {
        public static Customer StaticGetCustomer() {
            return null;
        }

        public Customer InstanceGetCustomer() {
            return null;
        }

        public Customer PropertyCustomer { get; set; }
        public Customer FieldCustomer;
    }

    public class TestTypeService
    {
        private Customer customerField;
        public Customer CustomerProperty { get; set; }
        private TestCustomerFactory customerFactory;

        public void SimpleAssignment(Customer from) {
            Customer localVar = from;
        }

        public void SplitAssignment(Customer from) {
            Customer localVar;
            localVar = from;
        }

        public void VarAssignment(Customer from)
        {
            var localVar = from;
        }

        public void VarNestedAssignment(Customer from) {
            var localVar = from.DeliveryAddress.City;
        }

        public void FieldAssignment(Customer from)
        {
            var localVar = customerField;
        }

        public void FieldNestedAssignment(Customer from) {
            var localVar = customerField.DeliveryAddress.City;
        }

        public void PropertyAssignment(Customer from) {
            var localVar = CustomerProperty;
        }

        public void PropertyNestedAssignment(Customer from) {
            var localVar = CustomerProperty.DeliveryAddress.City;
        }

        public void ParameterInference(Customer from)
        {
            var localVar = from;
        }

        public void LocalStaticAssignment() {
            var localVar = StaticGetCustomer();
        }

        public void LocalInstanceAssignment() {
            var localVar = InstanceGetCustomer();
        }

        public void ExternalVarStaticAssignment() {
            var localVar = TestCustomerFactory.StaticGetCustomer();
        }

        public void ExternalVarMethodAssignment() {
            var localVar = customerFactory.InstanceGetCustomer();
        }

        public void ExternalVarFieldAssignment() {
            var localVar = customerFactory.FieldCustomer;
        }

        public void ExternalVarPropertyAssignment() {
            var localVar = customerFactory.PropertyCustomer;
        }

        private Customer InstanceGetCustomer() {
            return null;
        }

        private static Customer StaticGetCustomer()
        {
            return null;
        }
    }
}