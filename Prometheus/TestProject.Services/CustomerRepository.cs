using System.Collections.Generic;

namespace TestProject.Services
{
    public class CustomerRepository
    {
        private readonly List<Customer> customers;

        public CustomerRepository(List<Customer> customers)
        {
            this.customers = customers;
        }

        public Customer Get(int x, int y)
        {
            if (x > y)
                return customers[x];

            return customers[y];
        }

        public Customer Compute(int x, int y) {
            if (x>2)
                return customers[x];

            return customers[x + y];
        }
    }
}