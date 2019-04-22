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
            var loopCondition = borderNode as Nodes.LoopConditionTunnel;
            if (loopCondition != null)
            {
                Terminal nodeTerminal = loopCondition.InputTerminals[0];
                var connectedWireTerminal = nodeTerminal.ConnectedTerminal;
                if (connectedWireTerminal != null)
                {
                    // Unify node input terminal with its connected source
                    AutoBorrowNodeFacade nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(loopCondition);
                    nodeFacade[nodeTerminal].UnifyWithConnectedWireTypeAsNodeInput(connectedWireTerminal.GetFacadeVariable(), _typeUnificationResults);
                }                
            }
            else
            {
                UnifyNodeInputTerminalTypes(borderNode);
            }
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
                    ITypeUnificationResult unificationResult = _typeUnificationResults.GetTypeUnificationResult(
                        wireTerminal,
                        wireVariable.TypeVariableReference,
                        nodeVariable.TypeVariableReference);
                    wireVariable.UnifyTypeVariableInto(nodeVariable, unificationResult);
                    wireVariable.MergeInto(nodeVariable);
                }
            }
        }
    }
}
