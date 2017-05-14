using TestProject.Common;

namespace TestProject.Services
{
    public class OrderService
    {
        private readonly AtomicQueue<Order> orderQueue;

        public OrderService(AtomicQueue<Order> orderQueue)
        {
            this.orderQueue = orderQueue;
        }

        public void Add(Order order)
        {
            orderQueue.Enqueue(order);
        }
    }
}
