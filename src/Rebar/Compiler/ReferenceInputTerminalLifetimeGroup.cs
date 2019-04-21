using System;
using System.Collections.Generic;
using System.Linq;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler.Nodes;

namespace Rebar.Compiler
{
    internal sealed class ReferenceInputTerminalLifetimeGroup
    {
        private readonly AutoBorrowNodeFacade _nodeFacade;
        private readonly InputReferenceMutability _mutability;
        private readonly List<ReferenceInputTerminalFacade> _facades = new List<ReferenceInputTerminalFacade>();
        private bool _borrowRequired, _mutableBorrow;
        private readonly Lazy<Lifetime> _lazyBorrowLifetime;

        public ReferenceInputTerminalLifetimeGroup(
            AutoBorrowNodeFacade nodeFacade,
            InputReferenceMutability mutability,
            Lazy<Lifetime> lazyNewLifetime)
        {
            _nodeFacade = nodeFacade;
            _mutability = mutability;
            _lazyBorrowLifetime = lazyNewLifetime;
        }

        private Lifetime BorrowLifetime => _lazyBorrowLifetime.Value;

        private void SetBorrowRequired(bool mutableBorrow)
        {
            _borrowRequired = true;
            _mutableBorrow = mutableBorrow;
        }

        public void AddTerminalFacade(Terminal inputTerminal, Terminal terminateLifetimeOutputTerminal = null)
        {
            var terminalFacade = new ReferenceInputTerminalFacade(inputTerminal, _mutability, this);
            _nodeFacade[inputTerminal] = terminalFacade;
            _facades.Add(terminalFacade);
            if (terminateLifetimeOutputTerminal != null)
            {
                var outputFacade = new TerminateLifetimeOutputTerminalFacade(terminateLifetimeOutputTerminal, terminalFacade);
                _nodeFacade[terminateLifetimeOutputTerminal] = outputFacade;
            }
        }

        public void UpdateFacadesFromInput()
        {
            foreach (var facade in _facades)
            {
                facade.UpdateFromFacadeInput();
            }
        }

        public void SetInterruptedVariables(LifetimeVariableAssociation lifetimeVariableAssociation)
        {
            if (_borrowRequired)
            {
                foreach (var facade in _facades)
                {
                    lifetimeVariableAssociation.AddVariableInterruptedByLifetime(facade.FacadeVariable, BorrowLifetime);
                }
            }
        }

        private bool VariablesAllMutableReferencesInSameLifetime(IEnumerable<VariableReference> variables)
        {
            Lifetime firstLifetime = variables.FirstOrDefault().Lifetime ?? Lifetime.Empty;
            return variables.All(v => v.Type.IsMutableReferenceType() && v.Lifetime == firstLifetime);
        }

        private bool VariablesAllImmutableReferencesInSameLifetime(IEnumerable<VariableReference> variables)
        {
            Lifetime firstLifetime = variables.FirstOrDefault().Lifetime ?? Lifetime.Empty;
            return variables.All(v => v.Type.IsImmutableReferenceType() && v.Lifetime == firstLifetime);
        }

        private void ComputeBorrowsFromInput(out bool outputIsMutableReference, out bool beginNewLifetime)
        {
            VariableReference[] variables = _facades.Select(f => f.FacadeVariable).ToArray();
            // 1. If all inputs are mutable references with the same bounded lifetime, then
            // output a mutable reference in that lifetime
            Lifetime firstLifetime = variables[0].Lifetime ?? Lifetime.Empty;
            if ((_mutability == InputReferenceMutability.RequireMutable || _mutability == InputReferenceMutability.Polymorphic)
                && VariablesAllMutableReferencesInSameLifetime(variables))
            {
                outputIsMutableReference = true;
                beginNewLifetime = false;
                return;
            }

            // 2. If all inputs can be borrowed into mutable references, then output a mutable
            // reference in a new diagram-bounded lifetime
            if (_mutability == InputReferenceMutability.RequireMutable
                || (_mutability == InputReferenceMutability.Polymorphic
                    && variables.All(v => v.Type.IsMutableReferenceType() || v.Mutable)))
            {
                outputIsMutableReference = true;
                beginNewLifetime = true;
                return;
            }

            // 3. If all inputs are immutable references with the same bounded lifetime, then
            // output an immutable reference in that lifetime
            if (VariablesAllImmutableReferencesInSameLifetime(variables))
            {
                outputIsMutableReference = false;
                beginNewLifetime = false;
                return;
            }

            // 4. Otherwise, output an immutable reference in a new diagram-bounded lifetime.
            outputIsMutableReference = false;
            beginNewLifetime = true;
            return;
        }

        public void CreateBorrowAndTerminateLifetimeNodes()
        {
            if (_borrowRequired)
            {
                Node parentNode = _facades.First().Terminal.ParentNode;
                NationalInstruments.Dfir.BorderNode parentBorderNode = parentNode as NationalInstruments.Dfir.BorderNode;
                BorrowMode borrowMode = _mutableBorrow ? BorrowMode.Mutable : BorrowMode.Immutable;
                int borrowInputCount = _facades.Count;
                Diagram inputParentDiagram = _facades.First().Terminal.ParentDiagram;
                var explicitBorrow = new ExplicitBorrowNode(inputParentDiagram, borrowMode, borrowInputCount, true, false);
                AutoBorrowNodeFacade borrowNodeFacade = AutoBorrowNodeFacade.GetNodeFacade(explicitBorrow);
                foreach (var terminal in explicitBorrow.Terminals)
                {
                    borrowNodeFacade[terminal] = new SimpleTerminalFacade(terminal);
                }

                int index = 0;
                foreach (var facade in _facades)
                {
                    Terminal input = facade.Terminal;
                    Terminal borrowOutput = explicitBorrow.OutputTerminals.ElementAt(index);
                    InsertBorrowAheadOfTerminal(input, explicitBorrow, index);
                    ++index;
                }

                List<TerminateLifetimeOutputTerminalFacade> terminates = new List<TerminateLifetimeOutputTerminalFacade>();
                foreach (var terminal in parentNode.OutputTerminals)
                {
                    var terminateFacade = _nodeFacade[terminal] as TerminateLifetimeOutputTerminalFacade;
                    if (terminateFacade != null && _facades.Contains(terminateFacade.InputFacade))
                    {
                        terminates.Add(terminateFacade);
                    }
                }

                if (terminates.Count == borrowInputCount)
                {
                    Diagram outputParentDiagram = terminates.First().Terminal.ParentDiagram;
                    var terminateLifetime = new TerminateLifetimeNode(outputParentDiagram, borrowInputCount, borrowInputCount);
                    AutoBorrowNodeFacade terminateLifetimeFacade = AutoBorrowNodeFacade.GetNodeFacade(terminateLifetime);
                    foreach (var terminal in terminateLifetime.Terminals)
                    {
                        terminateLifetimeFacade[terminal] = new SimpleTerminalFacade(terminal);
                    }

                    index = 0;
                    foreach (var terminate in terminates)
                    {
                        InsertTerminateLifetimeBehindTerminal(terminate.Terminal, terminateLifetime, index);
                    }
                }
                else if (terminates.Count > 0)
                {
                    throw new InvalidOperationException("Mismatched terminates and borrows; not sure what to do");
                }
            }
        }

        private static void InsertBorrowAheadOfTerminal(
            Terminal borrowReceiver,
            ExplicitBorrowNode explicitBorrow,
            int index)
        {
            Terminal borrowInput = explicitBorrow.InputTerminals.ElementAt(index),
                borrowOutput = explicitBorrow.OutputTerminals.ElementAt(index);

            // wiring
            borrowReceiver.ConnectedTerminal.ConnectTo(borrowInput);
            borrowOutput.WireTogether(borrowReceiver, SourceModelIdSource.NoSourceModelId);

            // variables
            borrowInput.GetFacadeVariable().MergeInto(borrowReceiver.GetFacadeVariable());
            borrowOutput.GetFacadeVariable().MergeInto(borrowReceiver.GetTrueVariable());
        }

        private static void InsertTerminateLifetimeBehindTerminal(
            Terminal lifetimeSource,
            TerminateLifetimeNode terminateLifetime,
            int index)
        {
            Terminal terminateLifetimeInput = terminateLifetime.InputTerminals.ElementAt(index),
                terminateLifetimeOutput = terminateLifetime.OutputTerminals.ElementAt(index);

            // wiring: output
            if (lifetimeSource.IsConnected)
            {
                lifetimeSource.ConnectedTerminal.ConnectTo(terminateLifetimeOutput);
            }
            lifetimeSource.WireTogether(terminateLifetimeInput, SourceModelIdSource.NoSourceModelId);

            // variables: output
            terminateLifetimeInput.GetFacadeVariable().MergeInto(lifetimeSource.GetTrueVariable());
            terminateLifetimeOutput.GetFacadeVariable().MergeInto(lifetimeSource.GetFacadeVariable());
        }

        private class ReferenceInputTerminalFacade : TerminalFacade
        {
            private readonly VariableSet _variableSet;
            private readonly InputReferenceMutability _mutability;
            private readonly ReferenceInputTerminalLifetimeGroup _group;

            public ReferenceInputTerminalFacade(Terminal terminal, InputReferenceMutability mutability, ReferenceInputTerminalLifetimeGroup group)
                : base(terminal)
            {
                _mutability = mutability;
                _group = group;
                _variableSet = terminal.GetVariableSet();
                FacadeVariable = _variableSet.CreateNewVariable();
                TrueVariable = _variableSet.CreateNewVariable();
            }

            public override VariableReference FacadeVariable { get; }

            public override VariableReference TrueVariable { get; }

            public override void UpdateFromFacadeInput()
            {
                // TypeVariableReference typeReference = TrueVariable.TypeVariableReference;
                // TrueVariable.SetTypeAndLifetime(typeReference.RenderNIType(), typeReference.Lifetime);
            }

            public override void UnifyWithConnectedWireTypeAsNodeInput(VariableReference wireFacadeVariable, TerminalTypeUnificationResults unificationResults)
            {
                FacadeVariable.MergeInto(wireFacadeVariable);

                TypeVariableSet typeVariableSet = _variableSet.TypeVariableSet;
                TypeVariableReference other = wireFacadeVariable.TypeVariableReference;
                TypeVariableReference u, l;
                bool otherIsMutableReference;
                bool otherIsReference = typeVariableSet.TryDecomposeReferenceType(other, out u, out l, out otherIsMutableReference);
                TypeVariableReference underlyingType = otherIsReference ? u : other;
                switch (_mutability)
                {
                    case InputReferenceMutability.RequireMutable:
                        {
                            TypeVariableReference lifetimeType;
                            if (!otherIsReference)
                            {
                                _group.SetBorrowRequired(true);
                                lifetimeType = typeVariableSet.CreateReferenceToLifetimeType(_group.BorrowLifetime);                                
                            }
                            else
                            {
                                lifetimeType = l;
                            }
                            TypeVariableReference mutableReference = typeVariableSet.CreateReferenceToReferenceType(true, underlyingType, lifetimeType);
                            ITypeUnificationResult unificationResult = unificationResults.GetTypeUnificationResult(
                                Terminal,
                                TrueVariable.TypeVariableReference,
                                mutableReference);
                            bool mutable = otherIsReference ? otherIsMutableReference : wireFacadeVariable.Mutable;
                            if (!mutable)
                            {
                                unificationResult.SetExpectedMutable();
                            }
                            typeVariableSet.Unify(TrueVariable.TypeVariableReference, mutableReference, unificationResult);
                            // TODO: after unifying these two, might be good to remove mutRef--I guess by merging?
                            break;
                        }
                    case InputReferenceMutability.AllowImmutable:
                        {
                            bool needsBorrow = !(otherIsReference && !otherIsMutableReference);
                            TypeVariableReference lifetimeType;
                            if (needsBorrow)
                            {
                                lifetimeType = typeVariableSet.CreateReferenceToLifetimeType(_group.BorrowLifetime);
                                _group.SetBorrowRequired(false);
                            }
                            else
                            {
                                lifetimeType = l;
                            }
                            TypeVariableReference immutableReference = typeVariableSet.CreateReferenceToReferenceType(false, underlyingType, lifetimeType);
                            ITypeUnificationResult unificationResult = unificationResults.GetTypeUnificationResult(
                                Terminal,
                                TrueVariable.TypeVariableReference,
                                immutableReference);
                            typeVariableSet.Unify(TrueVariable.TypeVariableReference, immutableReference, unificationResult);
                            // TODO: after unifying these two, might be good to remove immRef--I guess by merging?
                            break;
                        }
                    case InputReferenceMutability.Polymorphic:
                        {
                            TypeVariableReference lifetimeType;
                            if (!otherIsReference)
                            {
                                _group.SetBorrowRequired(false /* TODO depends on current state and input mutability */);
                                lifetimeType = typeVariableSet.CreateReferenceToLifetimeType(_group.BorrowLifetime);
                            }
                            else
                            {
                                // TODO: if TrueVariable.TypeVariableReference is already known to be immutable and
                                // wire reference is mutable, then we need a borrow
                                lifetimeType = l;
                            }
                            bool mutable = otherIsReference ? otherIsMutableReference : wireFacadeVariable.Mutable;
                            TypeVariableReference reference = typeVariableSet.CreateReferenceToReferenceType(mutable, underlyingType, lifetimeType);
                            ITypeUnificationResult unificationResult = unificationResults.GetTypeUnificationResult(
                                Terminal,
                                TrueVariable.TypeVariableReference,
                                reference);
                            typeVariableSet.Unify(TrueVariable.TypeVariableReference, reference, unificationResult);
                            break;
                        }
                }
            }
        }
    }
}
