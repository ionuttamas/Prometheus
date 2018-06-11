using System.Collections.Generic;
using System.Linq;

namespace TestProject.Services
{
    public class TransferService1
    {
        private readonly CustomerRepository _customerRepository;
        private readonly List<Customer> customers;

        public TransferService1(CustomerRepository customerRepository, List<Customer> customers)
        {
            _customerRepository = customerRepository;
            this.customers = customers;
        }

        public void MethodAssignment_IfTransfer(Customer from1, Customer to1, decimal amount) {
            var customer = _customerRepository.Compute(from1.Age, to1.Age);

            if (from1.AccountBalance > 0) {
                Customer refCustomer = customer;
            }

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