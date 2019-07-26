using System;
using NationalInstruments.Dfir;
using NationalInstruments.DataTypes;

namespace Rebar.Compiler.Nodes
{
    internal class InputParameterAccessor : DfirNode
    {
        public InputParameterAccessor(Node parent, int terminals)
            : base(parent)
        {
            NIType type = PFTypes.Void;
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

        public override T AcceptVisitor<T>(IDfirNodeVisitor<T> visitor)
        {
            throw new NotImplementedException();
        }
    }
}
