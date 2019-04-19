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
            if (borderNode is Nodes.TerminateLifetimeTunnel)
            {
                return;
            }
            UnifyNodeInputTerminalTypes(borderNode);
        }

        protected override void VisitNode(Node node)
        {
            UnifyNodeInputTerminalTypes(node);
        }

        private void UnifyNodeInputTerminalTypes(Node node)
        {
            AutoBorrowNodeFacade nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(node);
            foreach (var nodeTerminal in node.InputTerminals)
            {
                var connectedWireTerminal = nodeTerminal.ConnectedTerminal;
                VariableReference unifyWithVariable = connectedWireTerminal != null
                    // Unify node input terminal with its connected source
                    ? connectedWireTerminal.GetFacadeVariable()
                    // Unify node input with immutable Void type
                    : nodeTerminal.GetVariableSet().CreateNewVariableForUnwiredTerminal();
                nodeFacade[nodeTerminal].UnifyWithConnectedWireTypeAsNodeInput(unifyWithVariable, _typeUnificationResults);
            }
        }

        protected override void VisitWire(Wire wire)
        {
            // Merge the wire's input terminal with its connected source
            foreach (var wireTerminal in wire.InputTerminals)
            {
                var connectedNodeTerminal = wireTerminal.ConnectedTerminal;
                if (connectedNodeTerminal != null)
                {
                    VariableReference wireVariable = wireTerminal.GetFacadeVariable(),
                        nodeVariable = connectedNodeTerminal.GetFacadeVariable();
                    // TODO: this should be a unification in order to check that the wire type is Copyable
                    wireVariable.MergeInto(nodeVariable);
                }
            }

            // Unify types within a branched wire
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
                sinkTerminal.GetFacadeVariable().AdoptTypeVariableReference(sourceVariable.Value.TypeVariableReference);
            }
        }
    }
}
