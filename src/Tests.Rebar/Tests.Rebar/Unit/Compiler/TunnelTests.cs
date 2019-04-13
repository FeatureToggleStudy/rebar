﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.Compiler.Nodes;

namespace Tests.Rebar.Unit.Compiler
{
    [TestClass]
    public class TunnelTests : CompilerTestBase
    {
        [TestMethod]
        public void FrameInputTunnelWithOwnerTypeWiredIn_SetVariableTypes_TypeSetOnOutputVariable()
        {
            DfirRoot dfirRoot = DfirRoot.Create();
            Frame frame = Frame.Create(dfirRoot.BlockDiagram);
            Tunnel tunnel = CreateInputTunnel(frame);
            ConnectConstantToInputTerminal(tunnel.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(dfirRoot);

            VariableReference outputVariable = tunnel.OutputTerminals[0].GetTrueVariable();
            Assert.IsTrue(outputVariable.Type.IsInt32());
        }

        [TestMethod]
        public void FrameOutputTunnelWithOwnerTypeWiredIn_SetVariableTypes_TypeSetOnOutputVariable()
        {
            DfirRoot dfirRoot = DfirRoot.Create();
            Frame frame = Frame.Create(dfirRoot.BlockDiagram);
            Tunnel tunnel = CreateOutputTunnel(frame);
            ConnectConstantToInputTerminal(tunnel.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(dfirRoot);

            VariableReference outputVariable = tunnel.OutputTerminals[0].GetTrueVariable();
            Assert.IsTrue(outputVariable.Type.IsInt32());
        }

        [TestMethod]
        public void FrameInputTunnelWithReferenceTypeWiredIn_SetVariableTypes_TypeAndLifetimeSetOnOutputVariable()
        {
            DfirRoot dfirRoot = DfirRoot.Create();
            Frame frame = Frame.Create(dfirRoot.BlockDiagram);
            Tunnel tunnel = CreateInputTunnel(frame);
            ExplicitBorrowNode borrow = ConnectExplicitBorrowToInputTerminal(tunnel.InputTerminals[0]);
            ConnectConstantToInputTerminal(borrow.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(dfirRoot);

            VariableReference outputVariable = tunnel.OutputTerminals[0].GetTrueVariable();
            Assert.IsTrue(outputVariable.Type.IsImmutableReferenceType());
            Assert.IsTrue(outputVariable.Lifetime.DoesOutlastDiagram);
        }

        [TestMethod]
        public void FrameOutputTunnelWithReferenceTypeWiredIn_SetVariableTypes_TypeAndEmptyLifetimeSetOnOutputVariable()
        {
            DfirRoot dfirRoot = DfirRoot.Create();
            Frame frame = Frame.Create(dfirRoot.BlockDiagram);
            Tunnel tunnel = CreateOutputTunnel(frame);
            ExplicitBorrowNode borrow = ConnectExplicitBorrowToInputTerminal(tunnel.InputTerminals[0]);
            ConnectConstantToInputTerminal(borrow.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(dfirRoot);

            VariableReference outputVariable = tunnel.OutputTerminals[0].GetTrueVariable();
            Assert.IsTrue(outputVariable.Type.IsImmutableReferenceType());
            Assert.IsTrue(outputVariable.Lifetime.IsEmpty);
        }

        private Tunnel CreateInputTunnel(Frame frame)
        {
            return frame.CreateTunnel(Direction.Input, TunnelMode.LastValue, PFTypes.Void,PFTypes.Void);
        }

        private Tunnel CreateOutputTunnel(Frame frame)
        {
            return frame.CreateTunnel(Direction.Output, TunnelMode.LastValue, PFTypes.Void, PFTypes.Void);
        }
    }
}
