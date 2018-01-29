namespace TestProject.Services
{
    public class Customer
    {
        public bool IsActive { get; set; }
        public CustomerType Type { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public decimal AccountBalance { get; set; }
    }

    public enum CustomerType {
        None,
        Regular,
        Gold,
        Premium
    }
}