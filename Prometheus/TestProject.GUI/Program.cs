using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestProject.Common;
using TestProject.Services;

namespace TestProject.GUI {
    public class Program
    {
        private static AtomicStack<int> atomicStack;
        private static NonAtomicUsedStack<int> nonAtomicUsedStack;
        private static AtomicQueue<int> atomicQueue;
        private static NonAtomicQueue<int> nonAtomicQueue;
        private static DeadlockedQueue<int> deadlockedQueue;
        private static Customer sharedCustomer;
        private static CustomerRepository customerRepository;
        private static List<Customer> customers;
        private static TransferService1 transferService1;

        static void Main(string[] args)
        {
            var queue = new AtomicQueue<Order>();
            var orderService = new OrderService(queue);
            var orderProcessor = new OrderProcessor(queue, 10);
            var thread = new Thread(Do);
            var transferService = new TransferService();
            customerRepository = new CustomerRepository(customers);

            transferService1 = new TransferService1(customerRepository);
            transferService1.MethodAssignment_IfTransfer(sharedCustomer, null, 100);
            transferService1.MethodAssignment_WithIndexQuery_1(sharedCustomer, null, 100);
            transferService1.MethodAssignment_WithFirstQuery_1(sharedCustomer, null, 100);
            transferService1.MethodAssignment_WithWhereQuery_1(sharedCustomer, null, 100);

            transferService.Transfer(sharedCustomer, null, 200);
            transferService.SimpleIfTransfer(sharedCustomer, null, 200);
            transferService.SimpleIfSingleElseTransfer(sharedCustomer, null, 200);
            transferService.SimpleIfMultipleElseTransfer(sharedCustomer, null, 200);
            transferService.NestedIfElseTransfer(sharedCustomer, null, 200);
            transferService.NestedIfElse_With_IfElseTransfer(sharedCustomer, null, 200);
            transferService.NestedCall_SimpleIf_SimpleIfTransfer(sharedCustomer, null, 200);

            var proverTransferService = new ProverTransferService();
            proverTransferService.SimpleIfTransfer(sharedCustomer, null, 100);
            proverTransferService.StringCondition_SimpleIfTransfer(sharedCustomer, null, 100);
            proverTransferService.NestedCall_SimpleIfTransfer_SatisfiableCounterpart(sharedCustomer, null, 100);

            atomicStack.Pop();
            nonAtomicUsedStack.Pop();
            nonAtomicUsedStack.List.AddLast(2);
            atomicQueue.Dequeue();
            nonAtomicQueue.Dequeue();
            deadlockedQueue.Dequeue();
            thread.Start();
            orderProcessor.Start();

            for (int i = 0; i < 100; i++)
            {
                orderService.Add(new Order());
            }
        }

        private static void Do() {
            atomicStack.Push(3);
            nonAtomicUsedStack.Push(4);
            atomicQueue.Enqueue(1);
            nonAtomicQueue.Enqueue(1);
            deadlockedQueue.Enqueue(1);
            var registrationService = new RegistrationService();
            registrationService.Register(sharedCustomer);
            registrationService.SimpleIfRegister(sharedCustomer);

            var transferService2 = new TransferService2(customerRepository);
            transferService2.MethodAssignment_IfTransfer(sharedCustomer, null, 100);

            transferService1.MethodAssignment_WithIndexQuery_2(sharedCustomer, null, 100);
            transferService1.MethodAssignment_WithFirstQuery_2(sharedCustomer, null, 100);
            transferService1.MethodAssignment_WithWhereQuery_2(sharedCustomer, null, 100);

            var proverTransferService = new ProverTransferService();
            proverTransferService.SimpleIf_NegatedTransfer(sharedCustomer, null, 100);
            proverTransferService.StringCondition_SimpleIf_NegatedTransfer(sharedCustomer, null, 100);
            proverTransferService.NestedCall_SimpleIfTransfer(sharedCustomer, null, 100);
        }
    }
}
