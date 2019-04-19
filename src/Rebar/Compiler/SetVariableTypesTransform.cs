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
                TypeVariableReference typeReference = variable.TypeVariableReference;
                variable.SetTypeAndLifetime(typeReference.RenderNIType(), typeReference.Lifetime);
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
            constant.OutputTerminal.GetTrueVariable().SetTypeAndLifetime(constant.DataType, Lifetime.Unbounded);
            return true;
        }

        public bool VisitDropNode(DropNode dropNode)
        {
            return true;
        }

        public bool VisitExplicitBorrowNode(ExplicitBorrowNode explicitBorrowNode)
        {
            int inputCount = explicitBorrowNode.InputTerminals.Count;
            IEnumerable<VariableReference> inputVariables = explicitBorrowNode.InputTerminals.Select(VariableExtensions.GetTrueVariable);
            IEnumerable<NIType> outputTypes = inputVariables
                .Select(inputVariable => GetBorrowedOutputType(inputVariable, explicitBorrowNode.BorrowMode, explicitBorrowNode.AlwaysCreateReference));

            Lifetime firstLifetime = inputVariables.First().Lifetime;
            Lifetime outputLifetime;
            if (explicitBorrowNode.AlwaysBeginLifetime
                || !((firstLifetime?.IsBounded ?? false) && inputVariables.All(inputVariable => inputVariable.Lifetime == firstLifetime)))
            {
                outputLifetime = explicitBorrowNode.OutputTerminals.First().DefineLifetimeThatIsBoundedByDiagram();
                inputVariables.ForEach(v => _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(v, outputLifetime));
            }
            else
            {
                outputLifetime = firstLifetime;
            }

            // TODO: when necessary, mark the output lifetime as being a supertype of any of the bounded input lifetimes
            foreach (var pair in explicitBorrowNode.OutputTerminals.Zip(outputTypes))
            {
                Terminal outputTerminal = pair.Key;
                NIType outputType = pair.Value;
                outputTerminal.GetTrueVariable().SetTypeAndLifetime(outputType, outputLifetime);
            }
            return true;
        }

        private NIType GetBorrowedOutputType(VariableReference inputVariable, BorrowMode borrowMode, bool alwaysCreateReference)
        {
            NIType outputUnderlyingType = alwaysCreateReference
                ? inputVariable.Type
                : (inputVariable.Type.GetTypeOrReferentType());
            return borrowMode == BorrowMode.Immutable
                ? outputUnderlyingType.CreateImmutableReference()
                : outputUnderlyingType.CreateMutableReference();
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
            VariableReference inputVariable = iterateTunnel.Terminals.ElementAt(0).GetTrueVariable();
            NIType outputType;
            NIType inputType = inputVariable.Type.GetReferentType();
            if (!inputType.TryDestructureIteratorType(out outputType))
            {
                outputType = PFTypes.Void;
            }
            Terminal outputTerminal = iterateTunnel.Terminals.ElementAt(1);
            Lifetime outputLifetime;
            if (outputType.IsRebarReferenceType())
            {
                outputLifetime = outputTerminal.DefineLifetimeThatIsBoundedByDiagram();
                _lifetimeVariableAssociation.AddVariableInterruptedByLifetime(inputVariable, outputLifetime);
            }
            else
            {
                outputLifetime = Lifetime.Unbounded;
            }
            outputTerminal.GetTrueVariable().SetTypeAndLifetime(outputType, outputLifetime);
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

            var decomposedVariablesConcat = decomposedVariables.Concat(Enumerable.Repeat<VariableReference>(new VariableReference(), int.MaxValue));
            foreach (var outputTerminalPair in terminateLifetimeNode.OutputTerminals.Zip(decomposedVariablesConcat))
            {
                Terminal outputTerminal = outputTerminalPair.Key;
                VariableReference decomposedVariable = outputTerminalPair.Value;
                outputTerminal.GetFacadeVariable().MergeInto(decomposedVariable);
            }
            return true;
        }

        public bool VisitTunnel(Tunnel tunnel)
        {
            Terminal inputTerminal = tunnel.Direction == Direction.Input ? tunnel.GetOuterTerminal() : tunnel.GetInnerTerminal();
            Terminal outputTerminal = tunnel.Direction == Direction.Input ? tunnel.GetInnerTerminal() : tunnel.GetOuterTerminal();
            VariableReference inputVariable = inputTerminal.GetTrueVariable(),
                outputVariable = outputTerminal.GetTrueVariable();

            if (tunnel.Direction == Direction.Input)
            {
                SetVariableTypeAndLifetimeFromTypeVariable(outputVariable);
                return true;
            }

            var parentFrame = tunnel.ParentStructure as Frame;
            bool executesConditionally = parentFrame != null && DoesFrameExecuteConditionally(parentFrame);
            bool wrapOutputInOption = tunnel.Direction == Direction.Output && executesConditionally;

            Lifetime outputLifetime = Lifetime.Unbounded;
            NIType outputType = PFTypes.Void;
            // if input is unbounded/static, then output is unbounded/static
            // if input is from outer diagram, then output is a lifetime that outlasts the inner diagram
            // if input is from inner diagram and outlasts the inner diagram, we should be able to determine 
            //    which outer diagram lifetime it came from
            // otherwise, output is empty/error
            Lifetime inputLifetime = inputVariable.Lifetime;
            if (!inputLifetime.IsBounded)
            {
                outputLifetime = inputLifetime;
            }
            else if (tunnel.Direction == Direction.Input)
            {
                outputLifetime = inputLifetime;
            }
            // else if (inputLifetime outlasts inner diagram) { outputLifetime = outer diagram origin of inputLifetime; }
            else
            {
                outputLifetime = Lifetime.Empty;
            }
            outputType = inputVariable.Type;

            // If outputType is already an Option value type, then don't re-wrap it.
            if (wrapOutputInOption && !outputType.IsOptionType())
            {
                outputType = outputType.CreateOption();
            }
            outputVariable.SetTypeAndLifetime(
                outputType,
                outputLifetime);
            return true;
        }

        private bool DoesFrameExecuteConditionally(Frame frame)
        {
            // TODO: handle multi-frame flat sequence structures
            return frame.BorderNodes.OfType<UnwrapOptionTunnel>().Any();
        }

        public bool VisitTerminateLifetimeTunnel(TerminateLifetimeTunnel unborrowTunnel)
        {
            // Do nothing; the output terminal's variable is the same as the associated BorrowTunnel's input variable
            return true;
        }

        public bool VisitUnwrapOptionTunnel(UnwrapOptionTunnel unwrapOptionTunnel)
        {
            Terminal inputTerminal = unwrapOptionTunnel.Direction == Direction.Input ? unwrapOptionTunnel.GetOuterTerminal(0) : unwrapOptionTunnel.GetInnerTerminal(0, 0);
            Terminal outputTerminal = unwrapOptionTunnel.Direction == Direction.Input ? unwrapOptionTunnel.GetInnerTerminal(0, 0) : unwrapOptionTunnel.GetOuterTerminal(0);
            VariableReference inputVariable = inputTerminal.GetTrueVariable(),
                outputVariable = outputTerminal.GetTrueVariable();
            NIType optionType = inputVariable.Type;
            NIType optionValueType;
            if (optionType.TryDestructureOptionType(out optionValueType))
            {
                Lifetime outputLifetime = inputVariable.Lifetime;
                outputVariable.SetTypeAndLifetime(
                    optionValueType,
                    outputLifetime);
                return true;
            }

            outputVariable.SetTypeAndLifetime(
                PFTypes.Void,
                Lifetime.Unbounded);
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
