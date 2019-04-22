using System.Collections.Generic;
using NationalInstruments.Dfir;
using Rebar.Common;

namespace Rebar.Compiler
{
    internal class TunnelTerminalFacade : TerminalFacade
    {
        private readonly TerminalFacade _outputTerminalFacade;

        public TunnelTerminalFacade(Terminal terminal, TerminalFacade outputTerminalFacade) : base(terminal)
        {
            TrueVariable = terminal.GetVariableSet().CreateNewVariable();
            _outputTerminalFacade = outputTerminalFacade;
        }

        public override VariableReference FacadeVariable => TrueVariable;

        public override VariableReference TrueVariable { get; }

        public override void UnifyWithConnectedWireTypeAsNodeInput(VariableReference wireFacadeVariable, TerminalTypeUnificationResults unificationResults)
        {
            TypeVariableSet typeVariableSet = Terminal.GetTypeVariableSet();
            LifetimeGraphIdentifier innerDiagramLifetimeGraph = Terminal.ParentDiagram.GetLifetimeGraphIdentifier();
            var constraint = new OutlastsLifetimeGraphConstraint(innerDiagramLifetimeGraph);
            TypeVariableReference inputTypeReference = typeVariableSet.CreateReferenceToNewTypeVariable(new List<Constraint>() { constraint });
            ITypeUnificationResult inputUnificationResult = unificationResults.GetTypeUnificationResult(Terminal, inputTypeReference, wireFacadeVariable.TypeVariableReference);
            typeVariableSet.Unify(inputTypeReference, wireFacadeVariable.TypeVariableReference, inputUnificationResult);
            TrueVariable.MergeInto(wireFacadeVariable);

            string constructorName;
            TypeVariableReference innerType, optionType;
            if (typeVariableSet.TryDecomposeConstructorType(TrueVariable.TypeVariableReference, out constructorName, out innerType)
                && constructorName == "Option")
            {
                optionType = TrueVariable.TypeVariableReference;
            }
            else
            {
                optionType = typeVariableSet.CreateReferenceToConstructorType("Option", TrueVariable.TypeVariableReference);
            }
            TypeVariableReference outputTypeReference = _outputTerminalFacade.TrueVariable.TypeVariableReference;
            ITypeUnificationResult outputUnificationResult = unificationResults.GetTypeUnificationResult(_outputTerminalFacade.Terminal, outputTypeReference, optionType);
            typeVariableSet.Unify(outputTypeReference, optionType, outputUnificationResult);
        }
    }
}
