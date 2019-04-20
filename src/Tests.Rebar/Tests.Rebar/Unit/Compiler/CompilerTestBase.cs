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

        internal void RunSemanticAnalysisUpToSetVariableTypes(
            DfirRoot dfirRoot, 
            CompileCancellationToken cancellationToken = null,
            TerminalTypeUnificationResults unificationResults = null,
            LifetimeVariableAssociation lifetimeVariableAssociation = null)
        {
            cancellationToken = cancellationToken ?? new CompileCancellationToken();
            unificationResults = unificationResults ?? new TerminalTypeUnificationResults();
            lifetimeVariableAssociation = lifetimeVariableAssociation ?? new LifetimeVariableAssociation();
            RunSemanticAnalysisUpToCreateNodeFacades(dfirRoot, cancellationToken);
            new MergeVariablesAcrossWiresTransform(unificationResults).Execute(dfirRoot, cancellationToken);
            new SetVariableTypesAndLifetimesTransform(lifetimeVariableAssociation).Execute(dfirRoot, cancellationToken);
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

        internal static BorrowTunnel CreateBorrowTunnel(Structure structure, BorrowMode borrowMode)
        {
            var borrowTunnel = new BorrowTunnel(structure, borrowMode);
            var terminateLifetimeDfir = new TerminateLifetimeTunnel(structure);
            borrowTunnel.TerminateLifetimeTunnel = terminateLifetimeDfir;
            terminateLifetimeDfir.BeginLifetimeTunnel = borrowTunnel;
            return borrowTunnel;
        }
    }
}
