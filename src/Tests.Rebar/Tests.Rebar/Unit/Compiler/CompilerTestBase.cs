using NationalInstruments.Compiler;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.Compiler.Nodes;

namespace Tests.Rebar.Unit.Compiler
{
    public class CompilerTestBase
    {
        protected void RunSemanticAnalysisUpToCreateNodeFacades(DfirRoot dfirRoot, CompileCancellationToken cancellationToken = null)
        {
            ExecutionOrderSortingVisitor.SortDiagrams(dfirRoot);
            cancellationToken = cancellationToken ?? new CompileCancellationToken();
            new CreateNodeFacadesTransform().Execute(dfirRoot, cancellationToken);
        }

        protected void RunSemanticAnalysisUpToSetVariableTypes(
            DfirRoot dfirRoot, 
            CompileCancellationToken cancellationToken = null,
            TerminalTypeUnificationResults unificationResults = null)
        {
            cancellationToken = cancellationToken ?? new CompileCancellationToken();
            unificationResults = unificationResults ?? new TerminalTypeUnificationResults();
            RunSemanticAnalysisUpToCreateNodeFacades(dfirRoot, cancellationToken);
            new MergeVariablesAcrossWiresTransform(unificationResults).Execute(dfirRoot, cancellationToken);
            new SetVariableTypesAndLifetimesTransform().Execute(dfirRoot, cancellationToken);
        }

        protected void RunSemanticAnalysisUpToValidation(DfirRoot dfirRoot)
        {
            var cancellationToken = new CompileCancellationToken();
            var unificationResults = new TerminalTypeUnificationResults();
            RunSemanticAnalysisUpToSetVariableTypes(dfirRoot, cancellationToken, unificationResults);
            new ValidateVariableUsagesTransform(unificationResults).Execute(dfirRoot, cancellationToken);
        }

        protected static void ConnectConstantToInputTerminal(Terminal inputTerminal, NIType variableType, bool mutable)
        {
            Constant constant = Constant.Create(inputTerminal.ParentDiagram, variableType.CreateDefaultValue(), variableType);
            Wire wire = Wire.Create(inputTerminal.ParentDiagram, constant.OutputTerminal, inputTerminal);
            wire.SetWireBeginsMutableVariable(mutable);
        }

        internal static ExplicitBorrowNode ConnectExplicitBorrowToInputTerminal(Terminal inputTerminal)
        {
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(inputTerminal.ParentDiagram, BorrowMode.Immutable, 1, true, true);
            Wire wire = Wire.Create(inputTerminal.ParentDiagram, borrow.OutputTerminals[0], inputTerminal);
            return borrow;
        }
    }
}
