using System;
using System.Collections.Generic;
using System.Linq;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler.Nodes;

namespace Rebar.Compiler
{
    internal class CreateNodeFacadesTransform : VisitorTransformBase, IDfirNodeVisitor<bool>
    {
        private AutoBorrowNodeFacade _nodeFacade;
        private TypeVariableSet _typeVariableSet;

        protected override void VisitDiagram(Diagram diagram)
        {
            _typeVariableSet = _typeVariableSet ?? diagram.DfirRoot.GetTypeVariableSet();
            diagram.SetVariableSet(new VariableSet(_typeVariableSet));
        }

        protected override void VisitWire(Wire wire)
        {
            AutoBorrowNodeFacade wireFacade = AutoBorrowNodeFacade.GetNodeFacade(wire);
            foreach (var terminal in wire.Terminals)
            {
                wireFacade[terminal] = new SimpleTerminalFacade(terminal);
            }

            Terminal firstSinkWireTerminal = wire.SinkTerminals.FirstOrDefault(),
                sourceWireTerminal = null;
            if (wire.TryGetSourceTerminal(out sourceWireTerminal) && firstSinkWireTerminal != null)
            {
                firstSinkWireTerminal.GetFacadeVariable().MergeInto(sourceWireTerminal.GetFacadeVariable());
            }
        }

        protected override void VisitNode(Node node)
        {
            _nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(node);
            this.VisitRebarNode(node);
            _nodeFacade = null;
        }

        protected override void VisitBorderNode(NationalInstruments.Dfir.BorderNode borderNode)
        {
            _nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(borderNode);
            this.VisitRebarNode(borderNode);
            _nodeFacade = null;
        }

        private class LifetimeTypeVariableGroup
        {
            private readonly VariableSet _variableSet;
            private readonly TypeVariableSet _typeVariableSet;
            private readonly List<VariableReference> _interruptedVariables = new List<VariableReference>();
            private readonly Lazy<Lifetime> _lazyNewLifetime;
            private readonly TypeVariableReference _lifetimeTypeReference;

            public LifetimeTypeVariableGroup(VariableSet variableSet)
            {
                _variableSet = variableSet;
                _typeVariableSet = variableSet.TypeVariableSet;
                _lazyNewLifetime = new Lazy<Lifetime>(() => _variableSet.DefineLifetimeThatIsBoundedByDiagram(_interruptedVariables));
                _lifetimeTypeReference = _typeVariableSet.CreateReferenceToLifetimeType(_lazyNewLifetime);
            }

            public void CreateReferenceAndPossibleBorrowTypesForFacade(TerminalFacade terminalFacade, bool mutable, TypeVariableReference underlyingTypeReference)
            {
                if (_lazyNewLifetime.IsValueCreated)
                {
                    throw new InvalidOperationException("Cannot add borrowed variables after creating new lifetime.");
                }
                TypeVariableReference referenceType = _typeVariableSet.CreateReferenceToReferenceType(
                    mutable, 
                    underlyingTypeReference, 
                    _lifetimeTypeReference);
                terminalFacade.TrueVariable.AdoptTypeVariableReference(referenceType);
                terminalFacade.FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToPossibleBorrowType(
                    mutable, 
                    terminalFacade.FacadeVariable, 
                    terminalFacade.TrueVariable,
                    _lazyNewLifetime));
            }
        }

        bool IDfirNodeVisitor<bool>.VisitAssignNode(AssignNode assignNode)
        {
            Terminal assigneeInput = assignNode.InputTerminals.ElementAt(0),
                newValueInput = assignNode.InputTerminals.ElementAt(1),
                assigneeOutput = assignNode.OutputTerminals.ElementAt(0);
            _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.RequireMutable).AddTerminalFacade(assigneeInput, assigneeOutput);
            _nodeFacade[newValueInput] = new SimpleTerminalFacade(newValueInput);

            TypeVariableReference dataTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            var lifetimeGroup = new LifetimeTypeVariableGroup(assigneeInput.GetVariableSet());
            lifetimeGroup.CreateReferenceAndPossibleBorrowTypesForFacade(_nodeFacade[assigneeInput], true, dataTypeVariable);
            _nodeFacade[newValueInput].FacadeVariable.AdoptTypeVariableReference(dataTypeVariable);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitConstant(Constant constant)
        {
            Terminal valueOutput = constant.OutputTerminals.ElementAt(0);
            _nodeFacade[valueOutput] = new SimpleTerminalFacade(valueOutput);

            _nodeFacade[valueOutput].FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToLiteralType(constant.DataType));

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitCreateCellNode(CreateCellNode createCellNode)
        {
            Terminal valueInput = createCellNode.InputTerminals.ElementAt(0),
                cellOutput = createCellNode.OutputTerminals.ElementAt(0);
            _nodeFacade[valueInput] = new SimpleTerminalFacade(valueInput);
            _nodeFacade[cellOutput] = new SimpleTerminalFacade(cellOutput);

            // TODO: this is technically polymorphic in the input mutability, except it takes non-reference types.
            // Implementing type inference for this would require a type variable that could resolve to NonLockingCell or LockingCell
            // depending on the mutability variable.

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitDropNode(DropNode dropNode)
        {
            Terminal valueInput = dropNode.InputTerminals.ElementAt(0);
            _nodeFacade[valueInput] = new SimpleTerminalFacade(valueInput);

            TypeVariableReference dataTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            _nodeFacade[valueInput].FacadeVariable.AdoptTypeVariableReference(dataTypeVariable);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitExchangeValuesNode(ExchangeValuesNode exchangeValuesNode)
        {
            Terminal input1Terminal = exchangeValuesNode.InputTerminals.ElementAt(0),
                input2Terminal = exchangeValuesNode.InputTerminals.ElementAt(1),
                output1Terminal = exchangeValuesNode.OutputTerminals.ElementAt(0),
                output2Terminal = exchangeValuesNode.OutputTerminals.ElementAt(1);
            ReferenceInputTerminalLifetimeGroup lifetimeGroup = _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.RequireMutable);
            lifetimeGroup.AddTerminalFacade(input1Terminal, output1Terminal);
            lifetimeGroup.AddTerminalFacade(input2Terminal, output2Terminal);

            TypeVariableReference dataTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            var lifetimeTypeVariableGroup = new LifetimeTypeVariableGroup(input1Terminal.GetVariableSet());
            lifetimeTypeVariableGroup.CreateReferenceAndPossibleBorrowTypesForFacade(_nodeFacade[input1Terminal], true, dataTypeVariable);
            lifetimeTypeVariableGroup.CreateReferenceAndPossibleBorrowTypesForFacade(_nodeFacade[input2Terminal], true, dataTypeVariable);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitExplicitBorrowNode(ExplicitBorrowNode explicitBorrowNode)
        {
            foreach (var terminal in explicitBorrowNode.Terminals)
            {
                _nodeFacade[terminal] = new SimpleTerminalFacade(terminal);
            }

            // ...uh
            // If AlwaysCreateReference is false, then I guess we want PossiblyBorrow variables for each input, and reference
            // variables for each output that use the common lifetime variable
            // If AlwaysCreateReference is true, then 

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitFunctionalNode(FunctionalNode functionalNode)
        {
            int inputIndex = 0, outputIndex = 0;
            var genericTypeParameters = new Dictionary<NIType, TypeVariableReference>();
            if (functionalNode.Signature.IsOpenGeneric())
            {
                foreach (NIType genericParameterNIType in functionalNode.Signature.GetGenericParameters())
                {
                    if (genericParameterNIType.IsGenericParameter())
                    {
                        // TODO: handle generic lifetime parameters differently
                        genericTypeParameters[genericParameterNIType] = _typeVariableSet.CreateReferenceToNewTypeVariable();
                    }
                }
            }

            foreach (NIType parameter in functionalNode.Signature.GetParameters())
            {
                NIType parameterDataType = parameter.GetDataType();
                bool isInput = parameter.GetInputParameterPassingRule() != NIParameterPassingRule.NotAllowed,
                    isOutput = parameter.GetOutputParameterPassingRule() != NIParameterPassingRule.NotAllowed;
                Terminal inputTerminal = null, outputTerminal = null;
                if (isInput)
                {
                    inputTerminal = functionalNode.InputTerminals[inputIndex];
                    ++inputIndex;
                }
                if (isOutput)
                {
                    outputTerminal = functionalNode.OutputTerminals[outputIndex];
                    ++outputIndex;
                }
                if (isInput && isOutput)
                {
                    if (parameterDataType.IsImmutableReferenceType())
                    {
                        // TODO: sharing lifetime groups
                        _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.AllowImmutable)
                            .AddTerminalFacade(inputTerminal, outputTerminal);

                        var lifetimeGroup = new LifetimeTypeVariableGroup(inputTerminal.GetVariableSet());
                        TypeVariableReference referentTypeVariableReference = CreateTypeVariableReferenceFromNIType(parameterDataType.GetReferentType(), genericTypeParameters);
                        lifetimeGroup.CreateReferenceAndPossibleBorrowTypesForFacade(_nodeFacade[inputTerminal], false, referentTypeVariableReference);
                    }
                    else if (parameterDataType.IsMutableReferenceType())
                    {
                        // TODO: sharing lifetime groups
                        _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.RequireMutable)
                            .AddTerminalFacade(inputTerminal, outputTerminal);

                        var lifetimeGroup = new LifetimeTypeVariableGroup(inputTerminal.GetVariableSet());
                        TypeVariableReference referentTypeVariableReference = CreateTypeVariableReferenceFromNIType(parameterDataType.GetReferentType(), genericTypeParameters);
                        lifetimeGroup.CreateReferenceAndPossibleBorrowTypesForFacade(_nodeFacade[inputTerminal], true, referentTypeVariableReference);
                    }
                    else
                    {
                        throw new NotSupportedException("Inout parameters must be reference types.");
                    }
                }
                else if (isOutput)
                {
                    _nodeFacade[outputTerminal] = new SimpleTerminalFacade(outputTerminal);

                    TypeVariableReference typeVariableReference = CreateTypeVariableReferenceFromNIType(parameterDataType, genericTypeParameters);
                    _nodeFacade[outputTerminal].TrueVariable.AdoptTypeVariableReference(typeVariableReference);
                }
                else if (isInput)
                {
                    _nodeFacade[inputTerminal] = new SimpleTerminalFacade(inputTerminal);

                    // TODO: should adopt a TypeVariableReference for the TrueVariable here as in the output case,
                    // but I need a test case for this.
                }
                else
                {
                    throw new NotSupportedException("Parameter is neither input nor output");
                }
            }
            return true;
        }

        private TypeVariableReference CreateTypeVariableReferenceFromNIType(NIType type, Dictionary<NIType, TypeVariableReference> genericTypeParameters)
        {
            if (type.IsGenericParameter())
            {
                return genericTypeParameters[type];
            }
            else if (!type.IsGeneric())
            {
                return _typeVariableSet.CreateReferenceToLiteralType(type);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        bool IDfirNodeVisitor<bool>.VisitSelectReferenceNode(SelectReferenceNode selectReferenceNode)
        {
            Terminal selectorInput = selectReferenceNode.InputTerminals.ElementAt(0),
                trueInput = selectReferenceNode.InputTerminals.ElementAt(1),
                falseInput = selectReferenceNode.InputTerminals.ElementAt(2),
                selectorOutput = selectReferenceNode.OutputTerminals.ElementAt(0),
                resultOutput = selectReferenceNode.OutputTerminals.ElementAt(1);
            _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.AllowImmutable).AddTerminalFacade(selectorInput, selectorOutput);
            ReferenceInputTerminalLifetimeGroup lifetimeGroup = _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.Polymorphic);
            lifetimeGroup.AddTerminalFacade(trueInput);
            lifetimeGroup.AddTerminalFacade(falseInput);
            _nodeFacade[resultOutput] = new SimpleTerminalFacade(resultOutput);
            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitSomeConstructorNode(SomeConstructorNode someConstructorNode)
        {
            Terminal valueInput = someConstructorNode.InputTerminals.ElementAt(0),
                optionOutput = someConstructorNode.OutputTerminals.ElementAt(0);

            SimpleTerminalFacade inputFacade = new SimpleTerminalFacade(valueInput),
                outputFacade = new SimpleTerminalFacade(optionOutput);
            _nodeFacade[valueInput] = inputFacade;
            _nodeFacade[optionOutput] = outputFacade;

            TypeVariableReference dataTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            inputFacade.FacadeVariable.AdoptTypeVariableReference(dataTypeVariable);
            outputFacade.FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToConstructorType("Option", dataTypeVariable));

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitTerminateLifetimeNode(TerminateLifetimeNode terminateLifetimeNode)
        {
            foreach (var terminal in terminateLifetimeNode.Terminals)
            {
                // TODO: when updating terminals during SA, also update the TerminalFacades
                _nodeFacade[terminal] = new SimpleTerminalFacade(terminal);
            }
            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitBorrowTunnel(BorrowTunnel borrowTunnel)
        {
            Terminal valueInput = borrowTunnel.InputTerminals.ElementAt(0),
                borrowOutput = borrowTunnel.OutputTerminals.ElementAt(0);
            _nodeFacade[valueInput] = new SimpleTerminalFacade(valueInput);
            _nodeFacade[borrowOutput] = new SimpleTerminalFacade(borrowOutput);

            // T -> &'a (mode) T
            TypeVariableReference dataTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            Lifetime innerLifetime = borrowOutput.GetVariableSet().DefineLifetimeThatOutlastsDiagram();
            TypeVariableReference referenceType = _typeVariableSet.CreateReferenceToReferenceType(
                borrowTunnel.BorrowMode == BorrowMode.Mutable,
                dataTypeVariable,
                _typeVariableSet.CreateReferenceToLifetimeType(innerLifetime));
            _nodeFacade[valueInput].FacadeVariable.AdoptTypeVariableReference(dataTypeVariable);
            _nodeFacade[borrowOutput].FacadeVariable.AdoptTypeVariableReference(referenceType);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitIterateTunnel(IterateTunnel iterateTunnel)
        {
            Terminal iteratorInput = iterateTunnel.InputTerminals.ElementAt(0),
                itemOutput = iterateTunnel.OutputTerminals.ElementAt(0);
            _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.RequireMutable).AddTerminalFacade(iteratorInput);
            _nodeFacade[itemOutput] = new SimpleTerminalFacade(itemOutput);

            // TODO: this is going to mess up pretty hard on iterators with reference Item types--like slices
            // the Item type in the inner diagram will need to have an inner diagram lifetime
            // &'a mut Iterator -> Item
            // TODO: itemType should be a variable
            TypeVariableReference itemType = _typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32);
            TypeVariableReference iteratorType = _typeVariableSet.CreateReferenceToConstructorType("Iterator", itemType);
            LifetimeTypeVariableGroup lifetimeTypeVariableGroup = new LifetimeTypeVariableGroup(iteratorInput.GetVariableSet());
            lifetimeTypeVariableGroup.CreateReferenceAndPossibleBorrowTypesForFacade(_nodeFacade[iteratorInput], true, iteratorType);
            _nodeFacade[itemOutput].FacadeVariable.AdoptTypeVariableReference(itemType);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitLockTunnel(LockTunnel lockTunnel)
        {
            Terminal lockInput = lockTunnel.InputTerminals.ElementAt(0),
                referenceOutput = lockTunnel.OutputTerminals.ElementAt(0);
            _nodeFacade.CreateInputLifetimeGroup(InputReferenceMutability.AllowImmutable).AddTerminalFacade(lockInput);
            _nodeFacade[referenceOutput] = new SimpleTerminalFacade(referenceOutput);

            TypeVariableReference dataVariableType = _typeVariableSet.CreateReferenceToNewTypeVariable();
            TypeVariableReference lockType = _typeVariableSet.CreateReferenceToConstructorType("LockingCell", dataVariableType);
            LifetimeTypeVariableGroup lifetimeTypeVariableGroup = new LifetimeTypeVariableGroup(lockInput.GetVariableSet());
            lifetimeTypeVariableGroup.CreateReferenceAndPossibleBorrowTypesForFacade(
                _nodeFacade[lockInput],
                false,
                lockType);
            Lifetime innerLifetime = referenceOutput.GetVariableSet().DefineLifetimeThatOutlastsDiagram();
            TypeVariableReference referenceType = _typeVariableSet.CreateReferenceToReferenceType(
                false,
                lockType,
                _typeVariableSet.CreateReferenceToLifetimeType(innerLifetime));
            _nodeFacade[referenceOutput].FacadeVariable.AdoptTypeVariableReference(referenceType);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitLoopConditionTunnel(LoopConditionTunnel loopConditionTunnel)
        {
            // TODO: how to determine the mutability of the outer loop condition variable?
            Terminal loopConditionInput = loopConditionTunnel.InputTerminals.ElementAt(0),
                loopConditionOutput = loopConditionTunnel.OutputTerminals.ElementAt(0);
            _nodeFacade[loopConditionInput] = new SimpleTerminalFacade(loopConditionInput);
            _nodeFacade[loopConditionOutput] = new SimpleTerminalFacade(loopConditionOutput);

            TypeVariableReference boolType = _typeVariableSet.CreateReferenceToLiteralType(PFTypes.Boolean);
            _nodeFacade[loopConditionInput].FacadeVariable.AdoptTypeVariableReference(boolType);
            Lifetime innerLifetime = loopConditionOutput.GetVariableSet().DefineLifetimeThatOutlastsDiagram();
            TypeVariableReference boolReferenceType = _typeVariableSet.CreateReferenceToReferenceType(
                true,
                boolType,
                _typeVariableSet.CreateReferenceToLifetimeType(innerLifetime));
            _nodeFacade[loopConditionOutput].FacadeVariable.AdoptTypeVariableReference(boolReferenceType);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitTunnel(Tunnel tunnel)
        {
            Terminal valueInput = tunnel.InputTerminals.ElementAt(0),
                valueOutput = tunnel.OutputTerminals.ElementAt(0);
            _nodeFacade[valueInput] = new SimpleTerminalFacade(valueInput);
            _nodeFacade[valueOutput] = new SimpleTerminalFacade(valueOutput);

            // TODO: something will need to unify these two variables, with caution for the different lifetimes
            _nodeFacade[valueInput].FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToNewTypeVariable());
            _nodeFacade[valueOutput].FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToNewTypeVariable());

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitTerminateLifetimeTunnel(TerminateLifetimeTunnel terminateLifetimeTunnel)
        {
            Terminal valueOutput = terminateLifetimeTunnel.OutputTerminals.ElementAt(0);
            var valueFacade = new SimpleTerminalFacade(valueOutput);
            _nodeFacade[valueOutput] = valueFacade;

            NationalInstruments.Dfir.BorderNode beginLifetimeBorderNode = (NationalInstruments.Dfir.BorderNode)terminateLifetimeTunnel.BeginLifetimeTunnel;
            Terminal beginLifetimeTerminal = beginLifetimeBorderNode.GetOuterTerminal(0);
            valueFacade.FacadeVariable.MergeInto(beginLifetimeTerminal.GetFacadeVariable());
            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitUnwrapOptionTunnel(UnwrapOptionTunnel unwrapOptionTunnel)
        {
            Terminal optionInput = unwrapOptionTunnel.InputTerminals.ElementAt(0),
                unwrappedOutput = unwrapOptionTunnel.OutputTerminals.ElementAt(0);
            _nodeFacade[optionInput] = new SimpleTerminalFacade(optionInput);
            _nodeFacade[unwrappedOutput] = new SimpleTerminalFacade(unwrappedOutput);

            // TODO: something will need to unify the two type variables, with caustion for the different lifetimes
            _nodeFacade[optionInput].FacadeVariable.AdoptTypeVariableReference(_typeVariableSet
                .CreateReferenceToConstructorType("Option", _typeVariableSet.CreateReferenceToNewTypeVariable()));
            _nodeFacade[unwrappedOutput].FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToNewTypeVariable());

            return true;
        }
    }
}
