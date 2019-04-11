using System.Collections.Generic;
using NationalInstruments.Dfir;
using Rebar.Common;

namespace Rebar.Compiler
{
    internal sealed class TerminalTypeUnificationResults
    {
        private class TerminalUnificationResult
        {
            public bool TypeMismatch { get; set; }

            public bool ExpectedMutable { get; set; }
        }

        private class TerminalTypeUnificationResult : ITypeUnificationResult
        {
            private readonly TerminalUnificationResult _unificationResult;

            public TerminalTypeUnificationResult(TerminalUnificationResult unificationResult)
            {
                _unificationResult = unificationResult;
            }

            public void SetExpectedMutable()
            {
                _unificationResult.ExpectedMutable = true;
            }

            public void SetTypeMismatch()
            {
                _unificationResult.TypeMismatch = true;
            }
        }

        private Dictionary<Terminal, TerminalUnificationResult> _unificationResults = new Dictionary<Terminal, TerminalUnificationResult>();

        public ITypeUnificationResult GetTypeUnificationResult(Terminal terminal)
        {
            TerminalUnificationResult unificationResult;
            if (!_unificationResults.TryGetValue(terminal, out unificationResult))
            {
                _unificationResults[terminal] = unificationResult = new TerminalUnificationResult();
            }
            return new TerminalTypeUnificationResult(unificationResult);
        }

        public void SetMessagesOnTerminal(Terminal terminal)
        {
            TerminalUnificationResult unificationResult;
            if (!_unificationResults.TryGetValue(terminal, out unificationResult))
            {
                return;
            }
        }
    }
}
