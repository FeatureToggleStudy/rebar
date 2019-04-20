using System.Collections.Generic;
using System.Linq;
using NationalInstruments;
using NationalInstruments.DataTypes;
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
            foreach (var wireTerminal in wire.Terminals)
            {
                VariableReference variable = wireTerminal.GetFacadeVariable();
                SetVariableTypeAndLifetimeFromTypeVariable(variable);
            }
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
            SetVariableTypeAndLifetimeFromTypeVariable(outputVariable);
            _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(inputTerminal.GetTrueVariable(), outputVariable.Lifetime);
            return true;
        }

        public bool VisitConstant(Constant constant)
        {
            SetVariableTypeAndLifetimeFromTypeVariable(constant.OutputTerminal.GetTrueVariable());
            return true;
        }

        public bool VisitDropNode(DropNode dropNode)
        {
            return true;
        }

        public bool VisitExplicitBorrowNode(ExplicitBorrowNode explicitBorrowNode)
        {
            foreach (Terminal outputTerminal in explicitBorrowNode.OutputTerminals)
            {
                SetVariableTypeAndLifetimeFromTypeVariable(outputTerminal.GetTrueVariable());
            }
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

            // SetTypeAndLifetime for any output parameters based on type parameter substitutions
            foreach (var outputPair in functionalNode.OutputTerminals.Zip(signature.Outputs))
            {
                if (outputPair.Value.IsPassthrough)
                {
                    continue;
                }
                SetVariableTypeAndLifetimeFromTypeVariable(outputPair.Key.GetTrueVariable());
            }
            return true;
        }

        public bool VisitIterateTunnel(IterateTunnel iterateTunnel)
        {
            SetVariableTypeAndLifetimeFromTypeVariable(iterateTunnel.OutputTerminals[0].GetTrueVariable());
            return true;
        }

        public bool VisitLockTunnel(LockTunnel lockTunnel)
        {
            Terminal inputTerminal = lockTunnel.Terminals.ElementAt(0),
                outputTerminal = lockTunnel.Terminals.ElementAt(1);

            VariableReference outputVariable = outputTerminal.GetTrueVariable();
            SetVariableTypeAndLifetimeFromTypeVariable(outputVariable);
            _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(inputTerminal.GetTrueVariable(), outputVariable.Lifetime);
            return true;
        }

        public bool VisitLoopConditionTunnel(LoopConditionTunnel loopConditionTunnel)
        {
            Terminal inputTerminal = loopConditionTunnel.Terminals.ElementAt(0),
                outputTerminal = loopConditionTunnel.Terminals.ElementAt(1);
            VariableReference inputVariable = inputTerminal.GetTrueVariable();
            SetVariableTypeAndLifetimeFromTypeVariable(inputVariable);

            VariableReference outputVariable = outputTerminal.GetTrueVariable();
            SetVariableTypeAndLifetimeFromTypeVariable(outputVariable);
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
            Terminal outputTerminal = tunnel.Direction == Direction.Input ? tunnel.GetInnerTerminal() : tunnel.GetOuterTerminal();
            SetVariableTypeAndLifetimeFromTypeVariable(outputTerminal.GetTrueVariable());
            return true;
        }

        public bool VisitTerminateLifetimeTunnel(TerminateLifetimeTunnel unborrowTunnel)
        {
            // Do nothing; the output terminal's variable is the same as the associated BorrowTunnel's input variable
            return true;
        }

        public bool VisitUnwrapOptionTunnel(UnwrapOptionTunnel unwrapOptionTunnel)
        {
            Terminal outputTerminal = unwrapOptionTunnel.Direction == Direction.Input ? unwrapOptionTunnel.GetInnerTerminal(0, 0) : unwrapOptionTunnel.GetOuterTerminal(0);
            VariableReference outputVariable = outputTerminal.GetTrueVariable();
            SetVariableTypeAndLifetimeFromTypeVariable(outputVariable);
            return true;
        }

        private void SetVariableTypeAndLifetimeFromTypeVariable(VariableReference variable)
        {
            NIType outputType = variable.TypeVariableReference.RenderNIType();
            Lifetime lifetime = variable.TypeVariableReference.Lifetime;
            variable.SetTypeAndLifetime(outputType, lifetime);
        }
    }
}
