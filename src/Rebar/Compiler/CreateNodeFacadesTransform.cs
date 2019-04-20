using System;
using System.Collections.Generic;
using System.Linq;
using NationalInstruments;
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
            LifetimeGraphIdentifier diagramGraphIdentifier = new LifetimeGraphIdentifier(diagram.UniqueId);
            diagram.SetLifetimeGraphIdentifier(diagramGraphIdentifier);
            Diagram parentDiagram = diagram.ParentNode?.ParentDiagram;
            LifetimeGraphIdentifier parentGraphIdentifier = parentDiagram != null 
                ? new LifetimeGraphIdentifier(parentDiagram.UniqueId) 
                : default(LifetimeGraphIdentifier);
            diagram.DfirRoot.GetLifetimeGraphTree().EstablishLifetimeGraph(diagramGraphIdentifier, parentGraphIdentifier);
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

            private LifetimeTypeVariableGroup(Diagram diagram, VariableSet variableSet)
            {                
                _variableSet = variableSet;
                _typeVariableSet = variableSet.TypeVariableSet;
                LifetimeGraphTree lifetimeGraphTree = diagram.DfirRoot.GetLifetimeGraphTree();
                LifetimeGraphIdentifier diagramGraphIdentifier = diagram.GetLifetimeGraphIdentifier();
                LazyNewLifetime = new Lazy<Lifetime>(() => lifetimeGraphTree.CreateLifetimeThatIsBoundedByLifetimeGraph(diagramGraphIdentifier));
                LifetimeType = _typeVariableSet.CreateReferenceToLifetimeType(LazyNewLifetime);
            }

            public static LifetimeTypeVariableGroup CreateFromTerminal(Terminal terminal)
            {
                return new LifetimeTypeVariableGroup(terminal.ParentDiagram, terminal.ParentDiagram.GetVariableSet());
            }

            public static LifetimeTypeVariableGroup CreateFromNode(Node node)
            {
                return new LifetimeTypeVariableGroup(node.ParentDiagram, node.ParentDiagram.GetVariableSet());
            }

            public Lazy<Lifetime> LazyNewLifetime { get; }

            public TypeVariableReference LifetimeType { get; }

            public void CreateReferenceTypeForFacade(TerminalFacade terminalFacade, InputReferenceMutability mutability, TypeVariableReference underlyingTypeReference)
            {
                TypeVariableReference referenceType;
                if (mutability == InputReferenceMutability.Polymorphic)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    referenceType = _typeVariableSet.CreateReferenceToReferenceType(
                        (mutability != InputReferenceMutability.AllowImmutable),
                        underlyingTypeReference,
                        LifetimeType);
                }
                if (LazyNewLifetime.IsValueCreated)
                {
                    throw new InvalidOperationException("Cannot add borrowed variables after creating new lifetime.");
                }
                terminalFacade.TrueVariable.AdoptTypeVariableReference(referenceType);
            }
        }

        bool IDfirNodeVisitor<bool>.VisitConstant(Constant constant)
        {
            Terminal valueOutput = constant.OutputTerminals.ElementAt(0);
            _nodeFacade[valueOutput] = new SimpleTerminalFacade(valueOutput);

            _nodeFacade[valueOutput].FacadeVariable.AdoptTypeVariableReference(_typeVariableSet.CreateReferenceToLiteralType(constant.DataType));
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

        bool IDfirNodeVisitor<bool>.VisitExplicitBorrowNode(ExplicitBorrowNode explicitBorrowNode)
        {
            foreach (var terminal in explicitBorrowNode.Terminals)
            {
                _nodeFacade[terminal] = new SimpleTerminalFacade(terminal);
            }

            if (explicitBorrowNode.AlwaysCreateReference && explicitBorrowNode.AlwaysBeginLifetime)
            {
                bool mutable = explicitBorrowNode.BorrowMode == BorrowMode.Mutable;
                VariableSet variableSet = explicitBorrowNode.ParentDiagram.GetVariableSet();
                Lifetime borrowLifetime = explicitBorrowNode.OutputTerminals.First().DefineLifetimeThatIsBoundedByDiagram();
                TypeVariableReference borrowLifetimeType = _typeVariableSet.CreateReferenceToLifetimeType(borrowLifetime);

                foreach (var terminalPair in explicitBorrowNode.InputTerminals.Zip(explicitBorrowNode.OutputTerminals))
                {
                    Terminal inputTerminal = terminalPair.Key, outputTerminal = terminalPair.Value;
                    TypeVariableReference inputTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
                    inputTerminal.GetFacadeVariable().AdoptTypeVariableReference(inputTypeVariable);
                    TypeVariableReference outputReferenceType = _typeVariableSet.CreateReferenceToReferenceType(mutable, inputTypeVariable, borrowLifetimeType);
                    outputTerminal.GetFacadeVariable().AdoptTypeVariableReference(outputReferenceType);
                }
            }
            else
            {
                // TODO
                throw new NotImplementedException();
            }

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitFunctionalNode(FunctionalNode functionalNode)
        {
            int inputIndex = 0, outputIndex = 0;
            var genericTypeParameters = new Dictionary<NIType, TypeVariableReference>();
            var lifetimeFacadeGroups = new Dictionary<NIType, ReferenceInputTerminalLifetimeGroup>();
            var lifetimeVariableGroups = new Dictionary<NIType, LifetimeTypeVariableGroup>();

            if (functionalNode.Signature.IsOpenGeneric())
            {
                foreach (NIType genericParameterNIType in functionalNode.Signature.GetGenericParameters())
                {
                    if (genericParameterNIType.IsGenericParameter())
                    {
                        if (genericParameterNIType.IsLifetimeType())
                        {
                            var group = LifetimeTypeVariableGroup.CreateFromNode(functionalNode);
                            lifetimeVariableGroups[genericParameterNIType] = group;
                            genericTypeParameters[genericParameterNIType] = group.LifetimeType;
                        }
                        else if (genericParameterNIType.IsMutabilityType())
                        {
                            genericTypeParameters[genericParameterNIType] = _typeVariableSet.CreateReferenceToMutabilityType();
                        }
                        else
                        {
                            genericTypeParameters[genericParameterNIType] = _typeVariableSet.CreateReferenceToNewTypeVariable();
                        }
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
                    if (parameterDataType.IsRebarReferenceType())
                    {
                        CreateFacadesForInoutReferenceParameter(
                            parameterDataType,
                            inputTerminal,
                            outputTerminal,
                            genericTypeParameters,
                            lifetimeFacadeGroups,
                            lifetimeVariableGroups);
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
                    if (parameterDataType.IsRebarReferenceType())
                    {
                        CreateFacadesForInoutReferenceParameter(
                            parameterDataType,
                            inputTerminal,
                            null,
                            genericTypeParameters,
                            lifetimeFacadeGroups,
                            lifetimeVariableGroups);
                    }
                    else
                    {
                        _nodeFacade[inputTerminal] = new SimpleTerminalFacade(inputTerminal);

                        TypeVariableReference typeVariableReference = CreateTypeVariableReferenceFromNIType(parameterDataType, genericTypeParameters);
                        _nodeFacade[inputTerminal].TrueVariable.AdoptTypeVariableReference(typeVariableReference);
                    }
                }
                else
                {
                    throw new NotSupportedException("Parameter is neither input nor output");
                }
            }
            return true;
        }

        private void CreateFacadesForInoutReferenceParameter(
            NIType parameterDataType,
            Terminal inputTerminal,
            Terminal outputTerminal,
            Dictionary<NIType, TypeVariableReference> genericTypeParameters,
            Dictionary<NIType, ReferenceInputTerminalLifetimeGroup> lifetimeFacadeGroups,
            Dictionary<NIType, LifetimeTypeVariableGroup> lifetimeVariableGroups)
        {
            NIType lifetimeType = parameterDataType.GetReferenceLifetimeType();
            bool isMutable = parameterDataType.IsMutableReferenceType();
            InputReferenceMutability mutability = parameterDataType.GetInputReferenceMutabilityFromType();
            var lifetimeGroup = lifetimeVariableGroups[lifetimeType];
            ReferenceInputTerminalLifetimeGroup facadeGroup;
            if (!lifetimeFacadeGroups.TryGetValue(lifetimeType, out facadeGroup))
            {
                facadeGroup = _nodeFacade.CreateInputLifetimeGroup(mutability, lifetimeGroup.LazyNewLifetime);
            }
            // TODO: should not add outputTerminal here if borrow cannot be auto-terminated
            // i.e., if there are in-only or out-only parameters that share lifetimeType
            facadeGroup.AddTerminalFacade(inputTerminal, outputTerminal);

            TypeVariableReference referentTypeVariableReference = CreateTypeVariableReferenceFromNIType(parameterDataType.GetReferentType(), genericTypeParameters);
            TypeVariableReference referenceType;
            if (mutability == InputReferenceMutability.Polymorphic)
            {
                TypeVariableReference mutabilityTypeReference = genericTypeParameters[parameterDataType.GetReferenceMutabilityType()];
                referenceType = _typeVariableSet.CreateReferenceToPolymorphicReferenceType(
                    mutabilityTypeReference,
                    referentTypeVariableReference,
                    lifetimeGroup.LifetimeType);
            }
            else
            {
                referenceType = _typeVariableSet.CreateReferenceToReferenceType(
                    (mutability != InputReferenceMutability.AllowImmutable),
                    referentTypeVariableReference,
                    lifetimeGroup.LifetimeType);
            }
            _nodeFacade[inputTerminal].TrueVariable.AdoptTypeVariableReference(referenceType);
        }

        private TypeVariableReference CreateTypeVariableReferenceFromNIType(NIType type, Dictionary<NIType, TypeVariableReference> genericTypeParameters)
        {
            if (type.IsGenericParameter())
            {
                return genericTypeParameters[type];
            }
            else if (!type.IsGenericType())
            {
                return _typeVariableSet.CreateReferenceToLiteralType(type);
            }
            else
            {
                if (type.IsRebarReferenceType())
                {
                    TypeVariableReference referentType = CreateTypeVariableReferenceFromNIType(type.GetReferentType(), genericTypeParameters);
                    TypeVariableReference lifetimeType = CreateTypeVariableReferenceFromNIType(type.GetReferenceLifetimeType(), genericTypeParameters);
                    if (type.IsPolymorphicReferenceType())
                    {
                        TypeVariableReference mutabilityType = CreateTypeVariableReferenceFromNIType(type.GetReferenceMutabilityType(), genericTypeParameters);
                        return _typeVariableSet.CreateReferenceToPolymorphicReferenceType(mutabilityType, referentType, lifetimeType);
                    }
                    return _typeVariableSet.CreateReferenceToReferenceType(type.IsMutableReferenceType(), referentType, lifetimeType);
                }
                string constructorTypeName = type.GetName();
                var constructorParameters = type.GetGenericParameters();
                if (constructorParameters.Count == 1)
                {
                    TypeVariableReference parameterType = CreateTypeVariableReferenceFromNIType(constructorParameters.ElementAt(0), genericTypeParameters);
                    return _typeVariableSet.CreateReferenceToConstructorType(constructorTypeName, parameterType);
                }
                throw new NotImplementedException();
            }
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
            Lifetime innerLifetime = borrowOutput.DefineLifetimeThatIsBoundedByDiagram();
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
            LifetimeTypeVariableGroup lifetimeTypeVariableGroup = LifetimeTypeVariableGroup.CreateFromTerminal(iteratorInput);
            _nodeFacade
                .CreateInputLifetimeGroup(InputReferenceMutability.RequireMutable, lifetimeTypeVariableGroup.LazyNewLifetime)
                .AddTerminalFacade(iteratorInput);
            _nodeFacade[itemOutput] = new SimpleTerminalFacade(itemOutput);

            // TODO: iteratorType should be an Iterator trait constraint, related to itemType
            TypeVariableReference itemType = _typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32);
            TypeVariableReference iteratorType = _typeVariableSet.CreateReferenceToConstructorType("Iterator", itemType);
            lifetimeTypeVariableGroup.CreateReferenceTypeForFacade(_nodeFacade[iteratorInput], InputReferenceMutability.RequireMutable, iteratorType);
            _nodeFacade[itemOutput].FacadeVariable.AdoptTypeVariableReference(itemType);

            return true;
        }

        bool IDfirNodeVisitor<bool>.VisitLockTunnel(LockTunnel lockTunnel)
        {
            Terminal lockInput = lockTunnel.InputTerminals.ElementAt(0),
                referenceOutput = lockTunnel.OutputTerminals.ElementAt(0);
            LifetimeTypeVariableGroup lifetimeTypeVariableGroup = LifetimeTypeVariableGroup.CreateFromTerminal(lockInput);
            _nodeFacade
                .CreateInputLifetimeGroup(InputReferenceMutability.AllowImmutable, lifetimeTypeVariableGroup.LazyNewLifetime)
                .AddTerminalFacade(lockInput);
            _nodeFacade[referenceOutput] = new SimpleTerminalFacade(referenceOutput);

            TypeVariableReference dataVariableType = _typeVariableSet.CreateReferenceToNewTypeVariable();
            TypeVariableReference lockType = _typeVariableSet.CreateReferenceToConstructorType("LockingCell", dataVariableType);
            lifetimeTypeVariableGroup.CreateReferenceTypeForFacade(
                _nodeFacade[lockInput],
                InputReferenceMutability.AllowImmutable,
                lockType);
            Lifetime innerLifetime = referenceOutput.DefineLifetimeThatIsBoundedByDiagram();
            TypeVariableReference referenceType = _typeVariableSet.CreateReferenceToReferenceType(
                true,
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
            Lifetime innerLifetime = loopConditionOutput.DefineLifetimeThatIsBoundedByDiagram();
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

            _nodeFacade[valueOutput] = new SimpleTerminalFacade(valueOutput);
            TypeVariableReference typeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            _nodeFacade[valueOutput].FacadeVariable.AdoptTypeVariableReference(typeVariable);

            var parentFrame = tunnel.ParentStructure as Frame;
            bool executesConditionally = parentFrame != null && DoesFrameExecuteConditionally(parentFrame);
            if (executesConditionally)
            {
                _nodeFacade[valueInput] = new TunnelTerminalFacade(valueInput, _nodeFacade[valueOutput]);
            }
            else
            {
                _nodeFacade[valueInput] = new SimpleTerminalFacade(valueInput);
                _nodeFacade[valueInput].FacadeVariable.AdoptTypeVariableReference(typeVariable);
            }
            return true;
        }

        private bool DoesFrameExecuteConditionally(Frame frame)
        {
            // TODO: handle multi-frame flat sequence structures
            return frame.BorderNodes.OfType<UnwrapOptionTunnel>().Any();
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
            Terminal optionInput = unwrapOptionTunnel.InputTerminals[0],
                unwrappedOutput = unwrapOptionTunnel.OutputTerminals[0];
            _nodeFacade[optionInput] = new SimpleTerminalFacade(optionInput);
            _nodeFacade[unwrappedOutput] = new SimpleTerminalFacade(unwrappedOutput);

            TypeVariableReference innerTypeVariable = _typeVariableSet.CreateReferenceToNewTypeVariable();
            _nodeFacade[optionInput].FacadeVariable.AdoptTypeVariableReference(
                _typeVariableSet.CreateReferenceToConstructorType("Option", innerTypeVariable));
            _nodeFacade[unwrappedOutput].FacadeVariable.AdoptTypeVariableReference(innerTypeVariable);
            return true;
        }
    }
}
