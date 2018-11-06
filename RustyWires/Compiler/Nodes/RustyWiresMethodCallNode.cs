using System.Collections.Generic;
using System.Linq;
using NationalInstruments.Dfir;
using NationalInstruments.DataTypes;

namespace RustyWires.Compiler.Nodes
{
    internal class RustyWiresMethodCallNode : RustyWiresDfirNode
    {
        private readonly List<PassthroughTerminalPair> _passthroughTerminalPairs;
        private readonly Dictionary<Terminal, NIType> _terminalParameters = new Dictionary<Terminal, NIType>();

        public RustyWiresMethodCallNode(Node parentNode, NIType signature) : base(parentNode)
        {
            foreach (var parameter in signature.GetParameters())
            {
                NIType dataType = parameter.GetDataType();
                string name = parameter.GetUserVisibleParameterName();
                Terminal inputTerminal, outputTerminal;
                if (parameter.IsInputOnlyParameter())
                {
                    inputTerminal = CreateTerminal(Direction.Input, dataType, name);
                    _terminalParameters[inputTerminal] = parameter;
                }
                else if (parameter.IsOutputOnlyParameter())
                {
                    outputTerminal = CreateTerminal(Direction.Output, dataType, name);
                    _terminalParameters[outputTerminal] = parameter;
                }
                else
                {
                    inputTerminal = CreateTerminal(Direction.Input, dataType, name);
                    outputTerminal = CreateTerminal(Direction.Output, dataType, name);
                    _terminalParameters[inputTerminal] = parameter;
                    _terminalParameters[outputTerminal] = parameter;
                    _passthroughTerminalPairs = _passthroughTerminalPairs ?? new List<PassthroughTerminalPair>();
                    _passthroughTerminalPairs.Add(new PassthroughTerminalPair(inputTerminal, outputTerminal));
                }
            }
        }

        private RustyWiresMethodCallNode(Node parentNode, RustyWiresMethodCallNode nodeToCopy, NodeCopyInfo nodeCopyInfo)
            : base(parentNode, nodeToCopy, nodeCopyInfo)
        {
        }

        protected override Node CopyNodeInto(Node newParentNode, NodeCopyInfo copyInfo)
        {
            return new RustyWiresMethodCallNode(newParentNode, this, copyInfo);
        }

        public override IEnumerable<PassthroughTerminalPair> PassthroughTerminalPairs => _passthroughTerminalPairs ?? Enumerable.Empty<PassthroughTerminalPair>();

        public override void SetOutputVariableTypesAndLifetimes()
        {
            // TODO: when there can be output parameters, set types based on the target signature
            base.SetOutputVariableTypesAndLifetimes();
        }

        public override void CheckVariableUsages()
        {
            foreach (var inputTerminal in InputTerminals)
            {
                VariableUsageValidator validator = inputTerminal.GetValidator();
                NIType parameter = _terminalParameters[inputTerminal];
                NIType dataType = parameter.GetDataType();
                if (dataType.IsRWMutableType())
                {
                    validator.TestVariableIsMutableType();
                }
                if (!dataType.IsRWReferenceType())
                {
                    validator.TestVariableIsOwnedType();
                }
                NIType parameterUnderlyingType = dataType.GetUnderlyingTypeFromRustyWiresType();
                validator.TestExpectedUnderlyingType(parameterUnderlyingType);
            }
        }
    }
}
