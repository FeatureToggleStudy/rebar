using System.Collections.Generic;
using System.Linq;
using NationalInstruments;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler.Nodes;

namespace Rebar.Compiler
{
    /// <summary>
    /// Sets the initial <see cref="NIType"/> and <see cref="Lifetime"/> of any <see cref="VariableReference"/>s associated
    /// with non-passthrough output terminals on each node. Can assume that all <see cref="VariableReference"/>s associated 
    /// with input terminals (passthrough and non-passthrough) have initial types and lifetimes set.
    /// </summary>
    internal class SetVariableTypesAndLifetimesTransform : VisitorTransformBase, IDfirNodeVisitor<bool>
    {
        private readonly LifetimeVariableAssociation _lifetimeVariableAssociation;

        public SetVariableTypesAndLifetimesTransform(LifetimeVariableAssociation lifetimeVariableAssociation)
        {
            _lifetimeVariableAssociation = lifetimeVariableAssociation;
        }

        protected override void VisitNode(Node node)
        {
            AutoBorrowNodeFacade nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(node);
            nodeFacade.UpdateInputsFromFacadeTypes();

            this.VisitRebarNode(node);
        }

        protected override void VisitWire(Wire wire)
        {
        }

        protected override void VisitBorderNode(NationalInstruments.Dfir.BorderNode borderNode)
        {
            AutoBorrowNodeFacade nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(borderNode);
            nodeFacade.UpdateInputsFromFacadeTypes();

            this.VisitRebarNode(borderNode);
        }

        public bool VisitBorrowTunnel(BorrowTunnel borrowTunnel)
        {
            Terminal inputTerminal = borrowTunnel.Terminals.ElementAt(0),
                outputTerminal = borrowTunnel.Terminals.ElementAt(1);
            VariableReference outputVariable = outputTerminal.GetTrueVariable();
            _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(inputTerminal.GetTrueVariable(), outputVariable.Lifetime);
            return true;
        }

        public bool VisitConstant(Constant constant)
        {
            return true;
        }

        public bool VisitDropNode(DropNode dropNode)
        {
            return true;
        }

        public bool VisitExplicitBorrowNode(ExplicitBorrowNode explicitBorrowNode)
        {
            Lifetime outputLifetime = explicitBorrowNode.OutputTerminals[0].GetTrueVariable().Lifetime;
            IEnumerable<VariableReference> inputVariables = explicitBorrowNode.InputTerminals.Select(VariableExtensions.GetTrueVariable);
            inputVariables.ForEach(v => _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(v, outputLifetime));
            return true;
        }

        public bool VisitFunctionalNode(FunctionalNode functionalNode)
        {
            Signature signature = Signatures.GetSignatureForNIType(functionalNode.Signature);
            // For any input reference parameters that were auto-borrowed, set interrupted variables for their borrow lifetime
            AutoBorrowNodeFacade facade = AutoBorrowNodeFacade.GetNodeFacade(functionalNode);
            facade.SetLifetimeInterruptedVariables(_lifetimeVariableAssociation);
            return true;
        }

        public bool VisitIterateTunnel(IterateTunnel iterateTunnel)
        {
            return true;
        }

        public bool VisitLockTunnel(LockTunnel lockTunnel)
        {
            Terminal inputTerminal = lockTunnel.Terminals.ElementAt(0),
                outputTerminal = lockTunnel.Terminals.ElementAt(1);

            VariableReference outputVariable = outputTerminal.GetTrueVariable();
            _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(inputTerminal.GetTrueVariable(), outputVariable.Lifetime);
            return true;
        }

        public bool VisitLoopConditionTunnel(LoopConditionTunnel loopConditionTunnel)
        {
            Terminal inputTerminal = loopConditionTunnel.Terminals.ElementAt(0),
                outputTerminal = loopConditionTunnel.Terminals.ElementAt(1);
            VariableReference inputVariable = inputTerminal.GetTrueVariable();
            VariableReference outputVariable = outputTerminal.GetTrueVariable();
            _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(inputVariable, outputVariable.Lifetime);
            return true;
        }

        public bool VisitTerminateLifetimeNode(TerminateLifetimeNode terminateLifetimeNode)
        {
            Diagram parentDiagram = terminateLifetimeNode.ParentDiagram;
            VariableSet variableSet = parentDiagram.GetVariableSet();
            IEnumerable<VariableReference> inputVariables = terminateLifetimeNode.InputTerminals.Select(VariableExtensions.GetTrueVariable);
            IEnumerable<Lifetime> inputLifetimes = inputVariables.Select(v => v.Lifetime).Distinct();
            Lifetime singleLifetime;

            IEnumerable<VariableReference> decomposedVariables = Enumerable.Empty<VariableReference>();
            TerminateLifetimeErrorState errorState = TerminateLifetimeErrorState.NoError;
            // TerminateLifetimeTerminalFacade:
            // to update each terminal:
            // If we are in NoError state and haven't seen a lifetime yet or have a common lifetime:
            //   If input lifetime is non-null, bounded, and does not outlast parent diagram
            //      If we have a common lifetime and it matches input lifetime
            //         Keep going
            //      If we don't have a common lifetime
            //         Set common lifetime to input lifetime
            //      Else set state to NonUniqueLifetime
            //   Else if we have a common lifetime
            //      Set state to NonUniqueLifetime
            //   Else
            //      Set state to CannotTerminateLifetime
            //
            if (inputLifetimes.HasMoreThan(1))
            {
                errorState = TerminateLifetimeErrorState.InputLifetimesNotUnique;
                singleLifetime = inputLifetimes.First();
            }
            else if ((singleLifetime = inputLifetimes.FirstOrDefault()) == null)
            {
                // this means no inputs were wired, which is an error, but we should report it as unwired inputs
                // in CheckVariableUsages below
                errorState = TerminateLifetimeErrorState.NoError;
            }
            else if (singleLifetime.DoesOutlastDiagram(parentDiagram) || !singleLifetime.IsBounded)
            {
                errorState = TerminateLifetimeErrorState.InputLifetimeCannotBeTerminated;
            }
            else
            {
                errorState = TerminateLifetimeErrorState.NoError;
                // TODO: this does not account for Variables in singleLifetime that have already been consumed
                IEnumerable<VariableReference> variablesMatchingLifetime = variableSet.GetUniqueVariableReferences().Where(v => v.Lifetime == singleLifetime);
                int requiredInputCount = variablesMatchingLifetime.Count();
                terminateLifetimeNode.RequiredInputCount = requiredInputCount;
                if (inputVariables.Count() != terminateLifetimeNode.RequiredInputCount)
                {
                    errorState = TerminateLifetimeErrorState.NotAllVariablesInLifetimeConnected;
                }
                decomposedVariables = _lifetimeVariableAssociation.GetVariablesInterruptedByLifetime(singleLifetime);
                int outputCount = decomposedVariables.Count();
                terminateLifetimeNode.RequiredOutputCount = outputCount;

                terminateLifetimeNode.UpdateTerminals(requiredInputCount, outputCount);
            }
            terminateLifetimeNode.ErrorState = errorState;

            if (terminateLifetimeNode.ErrorState != TerminateLifetimeErrorState.InputLifetimeCannotBeTerminated)
            {
                var decomposedVariablesConcat = decomposedVariables.Concat(Enumerable.Repeat<VariableReference>(new VariableReference(), int.MaxValue));
                foreach (var outputTerminalPair in terminateLifetimeNode.OutputTerminals.Zip(decomposedVariablesConcat))
                {
                    Terminal outputTerminal = outputTerminalPair.Key;
                    VariableReference decomposedVariable = outputTerminalPair.Value;
                    if (decomposedVariable.IsValid)
                    {
                        outputTerminal.GetFacadeVariable().MergeInto(decomposedVariable);
                    }
                }
            }
            return true;
        }

        public bool VisitTunnel(Tunnel tunnel)
        {
            return true;
        }

        public bool VisitTerminateLifetimeTunnel(TerminateLifetimeTunnel unborrowTunnel)
        {
            // Do nothing; the output terminal's variable is the same as the associated BorrowTunnel's input variable
            return true;
        }

        public bool VisitUnwrapOptionTunnel(UnwrapOptionTunnel unwrapOptionTunnel)
        {
            return true;
        }
    }
}
