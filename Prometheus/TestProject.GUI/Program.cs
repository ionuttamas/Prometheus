using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestProject.Common;
using TestProject.Services;
using TestProject._3rdParty;

namespace TestProject.GUI {
    public class Program
    {
        private static AtomicStack<int> atomicStack;
        private static NonAtomicUsedStack<int> nonAtomicUsedStack;
        private static AtomicQueue<int> atomicQueue;
        private static NonAtomicQueue<int> nonAtomicQueue;
        private static DeadlockedQueue<int> deadlockedQueue;
        private static Customer sharedCustomer;
        private static Customer sharedCustomer2;
        private static CustomerRepository customerRepository;
        private static List<Customer> customers;
        private static TransferService1 transferService1;
        private static PaymentProvider paymentProvider;

        static void Main(string[] args)
        {
            var queue = new AtomicQueue<Order>();
            var orderService = new OrderService(queue);
            var orderProcessor = new OrderProcessor(queue, 10);
            var thread = new Thread(Do);
            var transferService = new TransferService();
            customerRepository = new CustomerRepository(customers);
            var validator = new CustomerValidator(100, 20, 30);
            transferService1 = new TransferService1(customerRepository, customers, paymentProvider, null, validator);
            transferService1.If_NullCheck(sharedCustomer);
            transferService1.StringConstantTransfer(sharedCustomer, sharedCustomer, 100);
            transferService1.IntConstantTransfer(sharedCustomer, sharedCustomer2, 100); //TODO: for (sharedCustomer, sharedCustomer) arguments it fails
            transferService1.IfCheck_FieldReferenceCall(sharedCustomer);
            transferService1.IfCheck_LocalStaticCall(sharedCustomer);
            transferService1.IfCheck_ThisReferenceCall(sharedCustomer);
            transferService1.IfCheck_ExternalStaticCall(sharedCustomer);
            transferService1.If_3rdPartyCheck_StaticPureCall(sharedCustomer);
            transferService1.If_3rdPartyCheck_PureMethodStaticAssignment_DirectCheck(sharedCustomer);
            transferService1.If_3rdPartyCheck_ImpureStaticAssignment(sharedCustomer);
            transferService1.If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_PureMethodStaticAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_PureMethodReferenceAssignment_DirectCheck(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_PureReferenceCall(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_ImpureReferenceCall(sharedCustomer, sharedCustomer, 100);
            transferService1.If_3rdPartyCheck_StaticImpureCall(sharedCustomer);
            transferService1.MethodAssignment_IfTransfer(sharedCustomer, sharedCustomer, 100);
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
            proverTransferService.SimpleIfTransfer_NullParams(null, sharedCustomer, 100);
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

            var validator = new CustomerValidator(200, 25, 35);
            var transferService2 = new TransferService2(customerRepository, paymentProvider, null, validator);
            transferService2.StringConstantTransfer(sharedCustomer, sharedCustomer, 100);
            transferService2.Unsat_StringConstantTransfer(sharedCustomer, sharedCustomer, 100);
            transferService2.IntConstantTransfer(sharedCustomer, sharedCustomer, 100);
            transferService2.Unsat_IntConstantTransfer(sharedCustomer, sharedCustomer2, 100);
            transferService2.IfCheck_Sat_FieldReferenceCall(sharedCustomer);
            transferService2.IfCheck_Unsat_FieldReferenceCall(sharedCustomer);
            transferService2.IfCheck_Sat_LocalStaticCall(sharedCustomer);
            transferService2.IfCheck_Unsat_LocalStaticCall(sharedCustomer);
            transferService2.IfCheck_Sat_ThisReferenceCall(sharedCustomer);
            transferService2.IfCheck_Unsat_ThisReferenceCall(sharedCustomer);
            transferService2.IfCheck_Sat_ExternalStaticCall(sharedCustomer);
            transferService2.IfCheck_Unsat_ExternalStaticCall(sharedCustomer);
            transferService2.If_3rdPartyCheck_StaticPureCall(sharedCustomer);
            transferService2.If_3rdPartyCheck_PureReferenceCall(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_ImpureReferenceCall(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Negated_ImpureReferenceCall(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Unsat_StaticPureCall(sharedCustomer);
            transferService2.If_3rdPartyCheck_Sat_PureStaticAssignment_DirectCheck(sharedCustomer);
            transferService2.If_3rdPartyCheck_ImpureStaticAssignment_DirectCheck(sharedCustomer);
            transferService2.If_3rdPartyCheck_Negated_ImpureStaticAssignment(sharedCustomer);
            transferService2.If_3rdPartyCheck_Unsat_StaticAssignment_DirectCheck(sharedCustomer);
            transferService2.If_3rdPartyCheck_Unsat_PureReferenceCall(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_StaticImpureCall(sharedCustomer);
            transferService2.If_3rdPartyCheck_Negated_Sat_StaticImpureCall(sharedCustomer);
            transferService2.If_3rdPartyCheck_Sat_PureMethodStaticAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Unsat_PureMethodStaticAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_PureMethodReferenceAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Sat_PureMethodReferenceAssignment_DirectCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_ImpureMethodReferenceAssignment_DirectCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Unsat_PureMethodReferenceAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_ImpureMethodReferenceAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodStaticAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_ImpureMethodStaticAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Sat_Negated_PureMethodReferenceAssignment_DifferentArgs_MemberCheck(sharedCustomer, sharedCustomer, 34);
            transferService2.If_3rdPartyCheck_Unsat_Negated_PureMethodReferenceAssignment_DifferentArgs_MemberCheck(sharedCustomer, sharedCustomer, 34);
            transferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment_DirectCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Negated_Sat_ImpureMethodReferenceAssignment_MemberCheck(sharedCustomer, sharedCustomer, 100);
            transferService2.If_3rdPartyCheck_Negated_Sat_StaticPureCall_DifferentArgs(null, sharedCustomer);
            transferService2.MethodAssignment_SimpleAssign(customers[0]);
            transferService2.If_NullCheck_Satisfiable(sharedCustomer);
            transferService2.If_NullCheck_Unsatisfiable(sharedCustomer, sharedCustomer);
            transferService2.MethodAssignment_IfTransfer(sharedCustomer, sharedCustomer, 100);

            transferService2.MethodAssignment_WithIndexQuery_2(sharedCustomer, null, 100);
            transferService2.MethodAssignment_WithFirstQuery_2(sharedCustomer, null, 100);
            transferService2.MethodAssignment_WithWhereQuery_2(sharedCustomer, null, 100);

            var proverTransferService = new ProverTransferService();
            proverTransferService.SimpleIf_NegatedTransfer(sharedCustomer, null, 100);
            proverTransferService.SimpleIf_NegatedTransfer_NullParams(null, sharedCustomer, 100);
            proverTransferService.StringCondition_SimpleIf_NegatedTransfer(sharedCustomer, null, 100);
            proverTransferService.NestedCall_SimpleIfTransfer(sharedCustomer, null, 100);
        }
    }
}
