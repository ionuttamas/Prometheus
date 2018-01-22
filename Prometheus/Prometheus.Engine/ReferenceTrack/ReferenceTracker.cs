using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Prometheus.Common;
using Prometheus.Engine.Thread;

namespace Prometheus.Engine.ReferenceTrack {
    //TODO: this will be used for checking that 2 lock object or 2 instances under concurrency modifications are the same or not
    internal class ReferenceTracker
    {
        private readonly Solution solution;
        private readonly ThreadSchedule threadSchedule;

        public ReferenceTracker(Solution solution, ThreadSchedule threadSchedule)
        {
            this.solution = solution;
            this.threadSchedule = threadSchedule;
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(IdentifierNameSyntax first, IdentifierNameSyntax second)
        {
            var firstAssignment = new ConditionalAssignment
            {
                Reference = first,
                AssignmentLocation = first.GetLocation()
            };
            var secondAssignment = new ConditionalAssignment {
                Reference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment);
        }

        private bool HaveCommonValueInternal(ConditionalAssignment first, ConditionalAssignment second) {
            if (threadSchedule.GetThreadPath(solution, first.Reference.GetLocation()) != null)
                return false;

            if (threadSchedule.GetThreadPath(solution, second.Reference.GetLocation()) != null)
                return false;

            //TODO: need to check scoping: if "first" is a local variable => it cannot match a variable from another function/thread
            var firstAssignments = GetAssignments((IdentifierNameSyntax)first.Reference); //todo: this needs checking
            var secondAssignments = GetAssignments((IdentifierNameSyntax)second.Reference);

            foreach (ConditionalAssignment assignment in firstAssignments) {
                assignment.Conditions.AddRange(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments) {
                assignment.Conditions.AddRange(second.Conditions);
            }

            foreach (ConditionalAssignment firstAssignment in firstAssignments) {
                foreach (ConditionalAssignment secondAssignment in secondAssignments) {
                    if (ValidateReachability(firstAssignment, secondAssignment))
                        return true;
                }
            }

            return false;
        }

        private bool ValidateReachability(ConditionalAssignment first, ConditionalAssignment second)
        {
            if (!IsSatisfiable(first, second))
                return false;

            //TODO: this is simplistic check
            if (first.Reference.ToString() != second.Reference.ToString() ||
                first.Reference.GetLocation() != second.Reference.GetLocation())
            {
                var firstReferenceAssignment = new ConditionalAssignment
                {
                    Reference = first.Reference,
                    AssignmentLocation = first.AssignmentLocation,
                    Conditions = first.Conditions
                };
                var secondReferenceAssignment = new ConditionalAssignment {
                    Reference = second.Reference,
                    AssignmentLocation = second.AssignmentLocation,
                    Conditions = second.Conditions
                };

                return HaveCommonValueInternal(firstReferenceAssignment, second) ||
                       HaveCommonValueInternal(first, secondReferenceAssignment);
            }

            return false;
        }

        private bool IsSatisfiable(ConditionalAssignment first, ConditionalAssignment second)
        {
            return false;
        }

        /// <summary>
        /// Returns a list of conditional assignments that the identifier referenced and based on what conditions.
        /// The assignments will be the last references that the identifier took its reference from.
        /// <example>
        /// In the example below calling the <see cref="GetAssignments(IdentifierNameSyntax)"/> for the <paramref name="instance"/> parameter
        /// will return a list of 2 <typeparamref name="ConditionalAssignment"/> with {Condition: "person.Age > 20", Reference: "person"} and {Condition: Not("person.Age > 20"), Reference: "new Person()"}.
        /// <code>
        /// class TestClass
        /// {
        ///     public Person DecrementAge(Person person)
        ///     {
        ///         Person instance = null;
        ///
        ///         if(person.Age > 20)
        ///         {
        ///             instance = person;
        ///         }
        ///         else
        ///         {
        ///             instance = new Person();
        ///         }
        ///
        ///         instance.Age--;
        ///
        ///         return instance;
        ///     }
        /// }
        /// </code>
        /// </example>
        /// </summary>
        private List<ConditionalAssignment> GetAssignments(IdentifierNameSyntax identifier)
        {
            //TODO: this does not take into account previous assignments to parameters
            var method = identifier.GetLocation().GetContainingMethod();
            var parameterIndex =
                method.ParameterList.Parameters.IndexOf(x => x.Identifier.Text == identifier.Identifier.Text);

            if (parameterIndex < 0)
                return GetMethodAssignments(identifier);

            var methodInvocations = solution
                .FindReferenceLocations(method)
                .Where(x => threadSchedule.GetThreadPath(solution, x.Location) != null)
                .Select(x => x.GetNode<InvocationExpressionSyntax>())
                .Select(x => GetConditionalAssignment(x, parameterIndex))
                .ToList();

            return methodInvocations;
        }

        /// <summary>
        /// Returns the list of assignments made for that identifier in the method in which the identifier is used.
        /// </summary>
        private List<ConditionalAssignment> GetMethodAssignments(IdentifierNameSyntax identifier)
        {
            var method = identifier.GetLocation().GetContainingMethod();
            var identifierName = identifier.Identifier.Text;
            var assignments = method
                .DescendantNodes<AssignmentExpressionSyntax>()
                .Where(x=>x.Kind()==SyntaxKind.SimpleAssignmentExpression)
                .Where(x=>x.Left is IdentifierNameSyntax)
                .Where(x=>x.Left.As<IdentifierNameSyntax>().Identifier.Text == identifierName);
            var conditionalAssignments = assignments.Select(GetConditionalAssignment).ToList();

            return conditionalAssignments;
        }

        /// <summary>
        /// Gets the conditional assignment for the given assignment within its method.
        /// </summary>
        private ConditionalAssignment GetConditionalAssignment(AssignmentExpressionSyntax assignment)
        {
            var elseClause = assignment.FirstAncestor<ElseClauseSyntax>();
            var ifClause = assignment.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment
            {
                Reference = assignment.Right,
                AssignmentLocation = assignment.GetLocation()
            };
            SyntaxNode currentNode = assignment;

            while (currentNode != null) {
                if (ifClause!=null)
                {
                    conditionalAssignment.Conditions.AddRange(ProcessIfStatement(currentNode, out currentNode).Conditions);
                }
                else if (elseClause != null)
                {
                    conditionalAssignment.Conditions.AddRange(ProcessElseStatement(currentNode, out currentNode).Conditions);
                }

                elseClause = currentNode.FirstAncestor<ElseClauseSyntax>();
                ifClause = currentNode.FirstAncestor<IfStatementSyntax>();
            }

            return conditionalAssignment;
        }

        /// <summary>
        /// Gets the conditional assignment for the given assignment within its method.
        /// </summary>
        private ConditionalAssignment GetConditionalAssignment(InvocationExpressionSyntax invocation, int argumentIndex) {
            var elseClause = invocation.FirstAncestor<ElseClauseSyntax>();
            var ifClause = invocation.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment {
                Reference = invocation.ArgumentList.Arguments[argumentIndex],
                AssignmentLocation = invocation.GetLocation()
            };
            SyntaxNode currentNode = invocation;

            while (currentNode != null) {
                if (ifClause != null) {
                    conditionalAssignment.Conditions.AddRange(ProcessIfStatement(currentNode, out currentNode).Conditions);
                } else if (elseClause != null) {
                    conditionalAssignment.Conditions.AddRange(ProcessElseStatement(currentNode, out currentNode).Conditions);
                }

                elseClause = currentNode.FirstAncestor<ElseClauseSyntax>();
                ifClause = currentNode.FirstAncestor<IfStatementSyntax>();
            }

            return conditionalAssignment;
        }

        private ConditionalAssignment ProcessIfStatement(SyntaxNode node, out SyntaxNode lastNode) {
            var ifClause = node.FirstAncestor<IfStatementSyntax>();
            var conditionalAssignment = new ConditionalAssignment();
            lastNode = ifClause;

            if (ifClause != null)
            {
                conditionalAssignment.AddCondition(ifClause.Condition.ToString(), ifClause.GetLocation());
            }

            return conditionalAssignment;
        }

        private ConditionalAssignment ProcessElseStatement(SyntaxNode node, out SyntaxNode lastNode)
        {
            var elseClause = node.FirstAncestor<ElseClauseSyntax>();
            var conditionalAssignment = new ConditionalAssignment();
            lastNode = elseClause;

            while (elseClause != null) {
                var ifStatement = elseClause.FirstAncestor<IfStatementSyntax>();
                conditionalAssignment.AddCondition("!" + ifStatement.Condition, ifStatement.GetLocation());
                lastNode = ifStatement;
                elseClause = ifStatement.FirstAncestor<ElseClauseSyntax>();
            }

            return conditionalAssignment;
        }

        public object DecrementAge(object person)
        {
            object instance;

            if (true)
            {
                if (person.ToString() == "ds")
                {
                    instance = person;
                }
                else if (person.ToString() == "dsd")
                {
                    instance = person;
                }
                else if (person.ToString() == "ddsd")
                {
                    instance = person;
                }
                else
                {
                    instance = new object();
                }
            }
            return instance;
        }

        private Person instance;

        private void Do(Person person)
        {
            if (person.ToString().Length > 2)
            {
                instance.Age--;
            }
        }

        private void Bar(Person person)
        {
            Person currentPerson;

            if (person.ToString().Length < 2)
            {
                currentPerson= instance;
            }
            else
            {
                currentPerson = person;
            }

            Foo(currentPerson);
        }

        private void Foo(Person person)
        {
            person.Age++;
        }

        private class Person
        {
             public string Name { get; set; }
             public int Age { get; set; }
        }
    }


}
