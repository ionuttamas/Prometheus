﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestProject.Common;
using TestProject.Services;

namespace TestProject.GUI {
    class Program {
        static void Main(string[] args)
        {
            var queue = new AtomicQueue<Order>();
            var orderService = new OrderService(queue);
            var orderProcessor = new OrderProcessor(queue, 10);

            var thread = new Thread(Do);
            thread.Start();

            orderProcessor.Start();

            for (int i = 0; i < 100; i++)
            {
                orderService.Add(new Order());
            }
        }

        private static void Do() {
            throw new NotImplementedException();
        }
    }
}
