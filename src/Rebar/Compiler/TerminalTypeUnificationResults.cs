﻿using System.Collections.Generic;
using NationalInstruments.Compiler.SemanticAnalysis;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;

namespace Rebar.Compiler
{
    internal sealed class TerminalTypeUnificationResults
    {
        private class TerminalUnificationResult
        {
            public TerminalUnificationResult(TypeVariableReference terminalTypeVariable, TypeVariableReference unifyWith)
            {
                TerminalTypeVariable = terminalTypeVariable;
                UnifyWith = unifyWith;
            }

            public TypeVariableReference TerminalTypeVariable { get; }

            public TypeVariableReference UnifyWith { get; }

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

        public ITypeUnificationResult GetTypeUnificationResult(Terminal terminal, TypeVariableReference terminalTypeVariable, TypeVariableReference unifyWith)
        {
            TerminalUnificationResult unificationResult;
            if (!_unificationResults.TryGetValue(terminal, out unificationResult))
            {
                _unificationResults[terminal] = unificationResult = new TerminalUnificationResult(terminalTypeVariable, unifyWith);
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

            if (unificationResult.TypeMismatch)
            {
                NIType expectedType = unificationResult.UnifyWith.RenderNIType();
                NIType actualType = unificationResult.TerminalTypeVariable.RenderNIType();
                terminal.SetDfirMessage(TerminalUserMessages.CreateTypeConflictMessage(actualType, expectedType));
            }
            if (unificationResult.ExpectedMutable)
            {
                terminal.SetDfirMessage(Messages.TerminalDoesNotAcceptImmutableType);
            }
        }
    }
}
