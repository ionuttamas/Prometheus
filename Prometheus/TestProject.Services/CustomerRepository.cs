using System.Collections.Generic;
using System.Linq;

namespace TestProject.Services
{
    public class CustomerRepository
    {
        private readonly List<Customer> customers;
        private readonly Dictionary<string, Customer> customersTable;

        public CustomerRepository(List<Customer> customers)
        {
            this.customers = customers;
            customersTable = new Dictionary<string, Customer>();
        }

        public Customer Get(int x, int y)
        {
            if (x > y)
                return customers[x];

            return customers[y];
        }

        public Customer Compute(int x, int y)
        {
            if (x > 2)
                return customers[x];

            return customers[x + y];
        }

        public Customer GetFirstIndexed()
        {
            return customers[0];
        }

        public Customer GetKeyIndexed() {
            return customersTable["key"];
        }

        public Customer GetFirst(decimal accountBalance)
        {
            return customers.First(x => x.AccountBalance == accountBalance);
        }

        public List<Customer> GetWhere(int age)
        {
            return customers.Where(x => x.Age == age).ToList();
        }
    }
}