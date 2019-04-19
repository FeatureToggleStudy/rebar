using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.Compiler.Nodes;

namespace Tests.Rebar.Unit.Compiler
{
    [TestClass]
    public class BorrowTunnelTests : CompilerTestBase
    {
        [TestMethod]
        public void BorrowTunnel_SetVariableTypes_OutputLifetimeIsBoundedAndDoesNotOutlastDiagram()
        {
            DfirRoot function = DfirRoot.Create();
            Frame frame = Frame.Create(function.BlockDiagram);
            var borrowTunnel = CreateBorrowTunnel(frame, BorrowMode.Immutable);
            ConnectConstantToInputTerminal(borrowTunnel.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(function);

            VariableReference borrowOutputVariable = borrowTunnel.OutputTerminals[0].GetTrueVariable();
            Lifetime lifetime = borrowOutputVariable.Lifetime;
            Assert.IsTrue(lifetime.IsBounded);
            Assert.IsFalse(lifetime.DoesOutlastDiagram(frame.Diagram));
        }

        [TestMethod]
        public void BorrowTunnel_SetVariableTypes_OutputLifetimeHasCorrectInterruptedVariables()
        {
            DfirRoot function = DfirRoot.Create();
            Frame frame = Frame.Create(function.BlockDiagram);
            var borrowTunnel = CreateBorrowTunnel(frame, BorrowMode.Immutable);
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

        private static BorrowTunnel CreateBorrowTunnel(Structure structure, BorrowMode borrowMode)
        {
            var borrowTunnel = new BorrowTunnel(structure, borrowMode);
            var terminateLifetimeDfir = new TerminateLifetimeTunnel(structure);
            borrowTunnel.TerminateLifetimeTunnel = terminateLifetimeDfir;
            terminateLifetimeDfir.BeginLifetimeTunnel = borrowTunnel;
            return borrowTunnel;
        }
    }
}
