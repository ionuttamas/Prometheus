using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class CallContext
    {
        /// <summary>
        /// In the case when we are interested in the data of a specific instance.
        /// If the assignment is a simple reference assignment, this property will be null.
        /// Currently, this only supports one level instance method calls, like "instance.Method()" not "instance.Member.Method()".
        /// E.g. in the case below, we bind the customer to the return values of the calling method, but bounded to the specific "customerRepository" instance.
        ///
        /// class TransferService
        /// {
        ///     private readonly CustomerRepository customerRepository;
        ///
        ///     public void Transfer(int fromId, int toId, decimal amount)
        ///     {
        ///         Customer customer = customerRepository.GetCustomer(fromId);
        ///
        ///         return instance;
        ///     }
        /// }
        /// </summary>
        public SyntaxNode InstanceNode { get; set; }

        /// <summary>
        /// The arguments table given the call context.
        /// E.g. for "reference = instance.Get(x, y)" and the method "Get(int a, int b)",
        /// the ArgumentsTable = {(a, x), (b, y)}.
        /// </summary>
        public Dictionary<ParameterSyntax, ArgumentSyntax> ArgumentsTable { get; set; }

        /// <summary>
        /// The invocation expression for the assignment.
        /// E.g. for "reference = instance.Get(x, y)" => "instance.Get(x, y)" will be the invocation expression.
        /// </summary>
        public InvocationExpressionSyntax InvocationExpression { get; set; }
    }
}