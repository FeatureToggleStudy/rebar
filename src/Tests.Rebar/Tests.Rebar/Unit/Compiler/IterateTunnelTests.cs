using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.Compiler.Nodes;
using Loop = Rebar.Compiler.Nodes.Loop;

namespace Tests.Rebar.Unit.Compiler
{
    [TestClass]
    public class IterateTunnelTests : CompilerTestBase
    {
#if FALSE
        [TestMethod]
        public void IterateTunnelWithIterableTypeWired_SetVariableTypes_OutputLifetimeIsBoundedAndDoesNotOutlastDiagram()
        {
            DfirRoot function = DfirRoot.Create();
            Loop loop = new Loop(function.BlockDiagram);
            var iterateTunnel = CreateIterateTunnel(loop);
            // ConnectConstantToInputTerminal(iterateTunnel.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(function);

            VariableReference borrowOutputVariable = iterateTunnel.OutputTerminals[0].GetTrueVariable();
            Lifetime lifetime = borrowOutputVariable.Lifetime;
            Assert.IsTrue(lifetime.IsBounded);
            Assert.IsFalse(lifetime.DoesOutlastDiagram(loop.Diagrams[0]));
        }

        [TestMethod]
        public void IterateTunnelWithIterableTypeWired_SetVariableTypes_OutputLifetimeHasCorrectInterruptedVariables()
        {
            DfirRoot function = DfirRoot.Create();
            Loop loop = new Loop(function.BlockDiagram);
            var borrowTunnel = CreateIterateTunnel(loop);
            ConnectConstantToInputTerminal(borrowTunnel.InputTerminals[0], PFTypes.Int32, false);
            var lifetimeAssociation = new LifetimeVariableAssociation();

            RunSemanticAnalysisUpToSetVariableTypes(function, null, null, lifetimeAssociation);

            VariableReference borrowOutputVariable = borrowTunnel.OutputTerminals[0].GetTrueVariable(),
                borrowInputVariable = borrowTunnel.InputTerminals[0].GetTrueVariable();
            Lifetime lifetime = borrowOutputVariable.Lifetime;
            IEnumerable<VariableReference> interruptedVariables = lifetimeAssociation.GetVariablesInterruptedByLifetime(lifetime);
            Assert.AreEqual(1, interruptedVariables.Count());
            Assert.AreEqual(borrowInputVariable, interruptedVariables.First());
        }
#endif

        private static IterateTunnel CreateIterateTunnel(Loop loop)
        {
            var iterateTunnel = new IterateTunnel(loop);
            var terminateLifetimeDfir = new TerminateLifetimeTunnel(loop);
            iterateTunnel.TerminateLifetimeTunnel = terminateLifetimeDfir;
            terminateLifetimeDfir.BeginLifetimeTunnel = iterateTunnel;
            return iterateTunnel;
        }
    }
}
