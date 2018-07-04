namespace TestProject.Services
{
    public class Customer
    {
        public bool IsActive { get; set; }
        public CustomerType Type { get; set; }
        public string Name { get; set; }
        public string Ssn { get; set; }
        public int Age { get; set; }
        public decimal AccountBalance { get; set; }
        public Address DeliveryAddress { get; set; }
    }

    public class Address
    {
        public string City { get; set; }
        public string StreetAddress { get; set; }
    }

    public enum CustomerType {
        None,
        Regular,
        Gold,
        Premium
    }
}