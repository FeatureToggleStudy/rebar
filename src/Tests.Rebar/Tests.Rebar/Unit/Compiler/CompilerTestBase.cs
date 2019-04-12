using NationalInstruments.Compiler;
using NationalInstruments.Dfir;
using Rebar.Compiler;

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
    }
}
