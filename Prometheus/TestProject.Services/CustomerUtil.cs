using System.Collections.Generic;
using System.Linq;

namespace TestProject.Services
{
    public static class CustomerUtil
    {
        public static Customer GetFirstLinq(List<Customer> customers, Customer from, decimal amount)
        {
            if (from.AccountBalance > amount)
                return customers.First(x => x.Age > from.Age);

            if (from.Type == CustomerType.Gold)
                return from;

            return customers.First(x => x.DeliveryAddress == from.DeliveryAddress);
        }
    }
}