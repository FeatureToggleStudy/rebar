﻿using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler.Nodes;

namespace Rebar.Compiler
{
    /// <summary>
    /// <see cref="TerminalFacade"/> implementation for a non-reference terminal, i.e., one that does not expect to be
    /// auto-borrowed. Its true and facade variables are always identical.
    /// </summary>
    internal class SimpleTerminalFacade : TerminalFacade
    {
        public SimpleTerminalFacade(Terminal terminal)
            : base(terminal)
        {
            bool terminalIsWireFirst = terminal.IsOutput && !(terminal.ParentNode is TerminateLifetimeNode);
            bool mutableVariable = false;
            if (!(terminal.ParentNode is Wire) && terminal.IsConnected && terminalIsWireFirst)
            {
                var connectedWire = (Wire)terminal.ConnectedTerminal.ParentNode;
                connectedWire.SetIsFirstVariableWire(true);
                mutableVariable = connectedWire.GetWireBeginsMutableVariable();
            }
            TrueVariable = terminal.GetVariableSet().CreateNewVariable(mutableVariable);
        }

        public override VariableReference FacadeVariable => TrueVariable;

        public override VariableReference TrueVariable { get; }

        public override void UpdateFromFacadeInput()
        {
            // Nothing to do here; TrueVariable is already the same as FacadeVariable
        }

        public override void UnifyWithConnectedWireTypeAsNodeInput(VariableReference wireFacadeVariable, ITypeUnificationResult unificationResult)
        {
            FacadeVariable.UnifyTypeVariableInto(wireFacadeVariable, unificationResult);
            FacadeVariable.MergeInto(wireFacadeVariable);
        }
    }
}
