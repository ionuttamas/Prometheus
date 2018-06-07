using System.Linq;

namespace TestProject.Services
{
    public class TransferService2 {
        //TODO: handle interface reference tracking
        private readonly CustomerRepository customerRepository;

        public TransferService2(CustomerRepository customerRepository)
        {
            this.customerRepository = customerRepository;
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