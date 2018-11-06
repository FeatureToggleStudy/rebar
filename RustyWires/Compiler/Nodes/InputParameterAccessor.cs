using System.Collections.Generic;
using System.Linq;
using NationalInstruments.Dfir;
using NationalInstruments.DataTypes;

namespace RustyWires.Compiler.Nodes
{
    internal class InputParameterAccessor : RustyWiresDfirNode
    {
        public InputParameterAccessor(Node parent, int terminals) 
            : base(parent)
        {
            NIType type = PFTypes.Void.CreateMutableValue();
            for (int i = 0; i < terminals; ++i)
            {
                CreateTerminal(Direction.Output, type, "input");
            }
        }

        private InputParameterAccessor(Node parent, InputParameterAccessor toCopy, NodeCopyInfo copyInfo)
            : base(parent, toCopy, copyInfo)
        {
        }

        protected override Node CopyNodeInto(Node newParentNode, NodeCopyInfo copyInfo)
        {
            return new InputParameterAccessor(newParentNode, this, copyInfo);
        }

        public override IEnumerable<PassthroughTerminalPair> PassthroughTerminalPairs => Enumerable.Empty<PassthroughTerminalPair>();

        public override void SetOutputVariableTypesAndLifetimes()
        {
            foreach (var terminal in Terminals)
            {
                VariableSet variableSet = terminal.GetVariableSet();
                Variable variable = terminal.GetVariable();
                Lifetime parameterLifetime = variableSet.DefineLifetimeThatOutlastsDiagram();
                variable.SetTypeAndLifetime(terminal.DataType, parameterLifetime);
            }
        }
    }
}
