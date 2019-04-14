using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments.Dfir;
using Rebar.Common;

namespace Rebar.Compiler
{
    internal class TunnelTerminalFacade : TerminalFacade
    {
        public TunnelTerminalFacade(Terminal terminal) : base(terminal)
        {
            TrueVariable = terminal.GetVariableSet().CreateNewVariable();
        }

        public override VariableReference FacadeVariable => TrueVariable;

        public override VariableReference TrueVariable { get; }

        public override void UnifyWithConnectedWireTypeAsNodeInput(VariableReference wireFacadeVariable, TerminalTypeUnificationResults unificationResults)
        {
            
        }

        public override void UpdateFromFacadeInput()
        {
        }
    }
}
