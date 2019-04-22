﻿using System.Linq;
using NationalInstruments.Dfir;
using NationalInstruments.DataTypes;
using Rebar.Common;

namespace Rebar.Compiler.Nodes
{
    internal class TerminateLifetimeNode : DfirNode
    {
        public TerminateLifetimeNode(Node parentNode, int inputs, int outputs) : base(parentNode)
        {
            var immutableReferenceType = PFTypes.Void.CreateImmutableReference();
            for (int i = 0; i < inputs; ++i)
            {
                CreateTerminal(Direction.Input, immutableReferenceType, "inner lifetime");
            }
            for (int i = 0; i < outputs; ++i)
            {
                CreateTerminal(Direction.Output, immutableReferenceType, "outer lifetime");
            }
        }

        private TerminateLifetimeNode(Node parentNode, TerminateLifetimeNode nodeToCopy, NodeCopyInfo nodeCopyInfo)
            : base(parentNode, nodeToCopy, nodeCopyInfo)
        {
        }

        protected override Node CopyNodeInto(Node newParentNode, NodeCopyInfo copyInfo)
        {
            return new TerminateLifetimeNode(newParentNode, this, copyInfo);
        }

        public TerminateLifetimeErrorState ErrorState { get; set; }

        public int? RequiredInputCount { get; set; }

        public int? RequiredOutputCount { get; set; }

        /// <inheritdoc />
        public override T AcceptVisitor<T>(IDfirNodeVisitor<T> visitor)
        {
            return visitor.VisitTerminateLifetimeNode(this);
        }

        public void UpdateTerminals(int inputTerminalCount, int outputTerminalCount)
        {
            AutoBorrowNodeFacade nodeFacade = AutoBorrowNodeFacade.GetNodeFacade(this);
            var immutableReferenceType = PFTypes.Void.CreateImmutableReference();
            int currentInputTerminalCount = InputTerminals.Count();
            if (currentInputTerminalCount < inputTerminalCount)
            {
                for (; currentInputTerminalCount < inputTerminalCount; ++currentInputTerminalCount)
                {
                    var terminal = CreateTerminal(Direction.Input, immutableReferenceType, "inner lifetime");
                    nodeFacade[terminal] = new SimpleTerminalFacade(terminal, terminal.GetTypeVariableSet().CreateReferenceToNewTypeVariable());
                    MoveTerminalToIndex(terminal, currentInputTerminalCount);
                }
            }
            else if (currentInputTerminalCount > inputTerminalCount)
            {
                int i = currentInputTerminalCount - 1;
                while (i >= 0 && currentInputTerminalCount > inputTerminalCount)
                {
                    Terminal inputTerminal = InputTerminals.ElementAt(i);
                    if (!inputTerminal.IsConnected)
                    {
                        RemoveTerminalAtIndex(inputTerminal.Index);
                        --currentInputTerminalCount;
                    }
                    --i;
                }
            }

            int currentOutputTerminalCount = OutputTerminals.Count();
            if (currentOutputTerminalCount < outputTerminalCount)
            {
                for (; currentOutputTerminalCount < outputTerminalCount; ++currentOutputTerminalCount)
                {
                    var terminal = CreateTerminal(Direction.Output, immutableReferenceType, "outer lifetime");
                    nodeFacade[terminal] = new SimpleTerminalFacade(terminal, terminal.GetTypeVariableSet().CreateReferenceToNewTypeVariable());
                }
            }
            else if (currentOutputTerminalCount > outputTerminalCount)
            {
                int i = currentOutputTerminalCount - 1;
                while (i >= 0 && currentOutputTerminalCount > outputTerminalCount)
                {
                    Terminal outputTerminal = OutputTerminals.ElementAt(i);
                    if (!outputTerminal.IsConnected)
                    {
                        RemoveTerminalAtIndex(outputTerminal.Index);
                        --currentOutputTerminalCount;
                    }
                    --i;
                }
            }
        }
    }

    internal enum TerminateLifetimeErrorState
    {
        InputLifetimesNotUnique,

        InputLifetimeCannotBeTerminated,

        NotAllVariablesInLifetimeConnected,

        NoError,
    }
}
