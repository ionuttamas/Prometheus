using System.Collections.Generic;

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
    }
}