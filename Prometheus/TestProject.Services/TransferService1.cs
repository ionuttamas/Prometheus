using System.Collections.Generic;
using System.Linq;

namespace TestProject.Services
{
    public class TransferService1
    {
        private readonly CustomerRepository _customerRepository;

        public TransferService1(CustomerRepository customerRepository) {
            _customerRepository = customerRepository;
        }

        public void MethodAssignment_IfTransfer(Customer from, Customer to, decimal amount) {
            var customer = _customerRepository.Compute(from.Age, to.Age);

            if (from.AccountBalance > 0) {
                Customer refCustomer = customer;
            }

            Customer firstCustomer = _customerRepository.GetFirst(from.AccountBalance);
            List<Customer> whereCustomers = _customerRepository.GetWhere(from.Age);
        }

        public void MethodAssignment_WithIndexQuery_1(Customer from1, Customer to1, decimal amount) {
            var customers = _customerRepository.GetWhere(to1.Age);
            Customer indexCustomer1 = customers[(from1.Age + to1.Age)* to1.Age];
        }

        public void MethodAssignment_WithIndexQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = _customerRepository.GetWhere(to2.Age);
            Customer indexCustomer2 = customers[to2.Age * to2.Age+ from2.Age * to2.Age];
        }

        public void MethodAssignment_WithFirstQuery_1(Customer from1, Customer to1, decimal amount) {
            var customers = _customerRepository.GetWhere(to1.Age);
            //TODO: support reference type comparison as "x.DeliveryAddress == to1.DeliveryAddress"
            Customer firstCustomer1 = customers.First(x => (x.Age == from1.Age || x.AccountBalance==from1.AccountBalance) && x.DeliveryAddress.City == to1.DeliveryAddress.City);
        }

        public void MethodAssignment_WithFirstQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = _customerRepository.GetWhere(to2.Age);
            Customer firstCustomer2 = customers.FirstOrDefault(x => x.AccountBalance == from2.AccountBalance && x.DeliveryAddress.City == to2.DeliveryAddress.City || x.Age == from2.Age && x.DeliveryAddress.City == to2.DeliveryAddress.City);
        }

        public void MethodAssignment_WithWhereQuery_1(Customer from1, Customer to1, decimal amount) {
            var customers = _customerRepository.GetWhere(to1.Age);
            var whereCustomers1 = customers.Where(x => (x.Age == from1.Age || x.AccountBalance == from1.AccountBalance) && x.DeliveryAddress.City == to1.DeliveryAddress.City);
        }

        public void MethodAssignment_WithWhereQuery_2(Customer from2, Customer to2, decimal amount) {
            var customers = _customerRepository.GetWhere(to2.Age);
            var whereCustomers2 = customers.Where(x => x.AccountBalance == from2.AccountBalance && x.DeliveryAddress.City == to2.DeliveryAddress.City || x.Age == from2.Age && x.DeliveryAddress.City == to2.DeliveryAddress.City);
        }
    }
}