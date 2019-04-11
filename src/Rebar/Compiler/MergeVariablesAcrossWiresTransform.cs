using System.Linq;
using NationalInstruments;
using NationalInstruments.Dfir;
using Rebar.Common;

namespace Rebar.Compiler
{
    internal class MergeVariablesAcrossWiresTransform : VisitorTransformBase
    {
        private readonly TerminalTypeUnificationResults _typeUnificationResults;

        public MergeVariablesAcrossWiresTransform(TerminalTypeUnificationResults typeUnificationResults)
        {
            _typeUnificationResults = typeUnificationResults;
        }

        protected override void VisitBorderNode(BorderNode borderNode)
        {
        }

        protected override void VisitNode(Node node)
        {
        }

        protected override void VisitWire(Wire wire)
        {
            // Merge together all connected wire and node terminals
            foreach (var wireTerminal in wire.Terminals)
            {
                var connectedNodeTerminal = wireTerminal.ConnectedTerminal;
                if (connectedNodeTerminal != null)
                {
                    VariableReference wireVariable = wireTerminal.GetFacadeVariable(),
                        nodeVariable = connectedNodeTerminal.GetFacadeVariable();
                    if (wireTerminal.Direction == Direction.Input)
                    {
                        // TODO: this should be a unification in order to check that the wire type is Copyable
                        wireVariable.MergeInto(nodeVariable);
                    }
                    else
                    {
                        AutoBorrowNodeFacade connectedNodeFacade = AutoBorrowNodeFacade.GetNodeFacade(connectedNodeTerminal.ParentNode);
                        TerminalFacade terminalFacade = connectedNodeFacade[connectedNodeTerminal];
                        ITypeUnificationResult unificationResult = _typeUnificationResults.GetTypeUnificationResult(connectedNodeTerminal);
                        terminalFacade.UnifyWithConnectedWireTypeAsNodeInput(wireVariable, unificationResult);
                    }
                }
            }

            // If source is available and there are copied sinks, set source variable type and lifetime on copied sinks
            if (!wire.SinkTerminals.HasMoreThan(1))
            {
                return;
            }
            Terminal sourceTerminal;
            wire.TryGetSourceTerminal(out sourceTerminal);
            VariableReference? sourceVariable = sourceTerminal?.GetFacadeVariable();
            if (sourceVariable == null)
            {
                return;
            }
            TypeVariableSet typeVariableSet = wire.GetTypeVariableSet();
            foreach (var sinkTerminal in wire.SinkTerminals.Skip(1))
            {
                VariableReference sinkVariable = sinkTerminal.GetFacadeVariable();
                ITypeUnificationResult unificationResult = _typeUnificationResults.GetTypeUnificationResult(sinkTerminal);
                sinkVariable.UnifyTypeVariableInto(sourceVariable.Value, unificationResult);
            }
        }
    }
}
