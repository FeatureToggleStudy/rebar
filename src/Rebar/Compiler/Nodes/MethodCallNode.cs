﻿using System;
using System.Collections.Generic;
using NationalInstruments.Dfir;
using NationalInstruments.DataTypes;

namespace Rebar.Compiler.Nodes
{
    internal class MethodCallNode : DfirNode
    {
        private readonly List<PassthroughTerminalPair> _passthroughTerminalPairs;
        private readonly Dictionary<Terminal, NIType> _terminalParameters = new Dictionary<Terminal, NIType>();

        public MethodCallNode(Node parentNode, NIType signature) : base(parentNode)
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

        private MethodCallNode(Node parentNode, MethodCallNode nodeToCopy, NodeCopyInfo nodeCopyInfo)
            : base(parentNode, nodeToCopy, nodeCopyInfo)
        {
        }

        protected override Node CopyNodeInto(Node newParentNode, NodeCopyInfo copyInfo)
        {
            return new MethodCallNode(newParentNode, this, copyInfo);
        }

        public override T AcceptVisitor<T>(IDfirNodeVisitor<T> visitor)
        {
            throw new NotImplementedException();
        }
    }
}
