using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReferenceProver
{
    internal class ReferenceProver
    {
        private readonly ReferenceTracker referenceTracker;
        private readonly ConditionalProver conditionalProver;

        public ReferenceProver(ReferenceTracker referenceTracker, ConditionalProver conditionalProver)
        {
            this.referenceTracker = referenceTracker;
            this.conditionalProver = conditionalProver;
        }

        /// <summary>
        /// Checks whether two identifiers can possibly have the same value within a thread schedule.
        /// TODO: this does not consider the possible conditional cases for assigning a variable within loops ("for", "while" loops).
        /// TODO: this does not consider assignments like: "order = person.Orders.First()" and "order = instance" where "instance" and "person.Orders.First()" can be the same
        /// </summary>
        public bool HaveCommonValue(IdentifierNameSyntax first, IdentifierNameSyntax second, out SyntaxNode commonNode) {
            var firstAssignment = new ConditionalAssignment {
                Reference = first,
                AssignmentLocation = first.GetLocation()
            };
            var secondAssignment = new ConditionalAssignment {
                Reference = second,
                AssignmentLocation = second.GetLocation()
            };
            return HaveCommonValueInternal(firstAssignment, secondAssignment, out commonNode);
        }

        private bool HaveCommonValueInternal(ConditionalAssignment first, ConditionalAssignment second, out SyntaxNode commonNode) {
            commonNode = null;

            //TODO: need to check scoping: if "first" is a local variable => it cannot match a variable from another function/thread
            var firstAssignments = referenceTracker.GetAssignments((IdentifierNameSyntax)first.Reference); //todo: this needs checking
            var secondAssignments = referenceTracker.GetAssignments((IdentifierNameSyntax)second.Reference);

            foreach (ConditionalAssignment assignment in firstAssignments) {
                assignment.Conditions.AddRange(first.Conditions);
            }

            foreach (ConditionalAssignment assignment in secondAssignments) {
                assignment.Conditions.AddRange(second.Conditions);
            }

            foreach (ConditionalAssignment firstAssignment in firstAssignments) {
                foreach (ConditionalAssignment secondAssignment in secondAssignments) {
                    if (ValidateReachability(firstAssignment, secondAssignment, out commonNode))
                        return true;
                }
            }

            return false;
        }

        private bool ValidateReachability(ConditionalAssignment first, ConditionalAssignment second, out SyntaxNode commonNode) {
            commonNode = null;

            if (!conditionalProver.IsSatisfiable(first, second))
                return false;

            if (AreEquivalent(first.Reference, second.Reference)) {
                commonNode = first.Reference;
                return true;
            }

            var firstReferenceAssignment = new ConditionalAssignment {
                Reference = first.Reference,
                AssignmentLocation = first.AssignmentLocation,
                Conditions = first.Conditions
            };
            var secondReferenceAssignment = new ConditionalAssignment {
                Reference = second.Reference,
                AssignmentLocation = second.AssignmentLocation,
                Conditions = second.Conditions
            };

            if (HaveCommonValueInternal(firstReferenceAssignment, second, out commonNode))
                return true;

            return HaveCommonValueInternal(first, secondReferenceAssignment, out commonNode);
        }

        /// <summary>
        /// This checks whether two nodes are the same reference (the shared memory of two threads).
        /// This can be a class field/property used by both thread functions or parameters passed to threads that are the same
        /// TODO: currently this checks only for field equivalence
        /// </summary>
        private static bool AreEquivalent(SyntaxNode first, SyntaxNode second) {
            if (first.ToString() != second.ToString())
                return false;

            if (first.GetLocation() != second.GetLocation())
                return false;

            return true;
        }
    }
}