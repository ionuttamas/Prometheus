namespace TestProject.Services
{
    public class Order
    {
        public string Customer { get; set; }
        public string Product { get; set; }
    }

    class TestClass
    {
        public object DecrementAge(object person)
        {
            dynamic instance = null;

            if (instance.Age > 20)
            {
                instance = person;
            }
            else
            {
                instance = new object();
            }

            instance.Age--;

            return instance;
        }
    }
}