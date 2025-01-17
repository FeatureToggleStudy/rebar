﻿using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler.Nodes;

namespace Tests.Rebar.Unit.Compiler
{
    [TestClass]
    public class AutomaticNodeInsertionTests : CompilerTestBase
    {
        private static readonly NIType _outputOwnerSignature;
        private static readonly NIType _outputOwnerStringSignature;
        private static readonly NIType _stringSlicePassthroughSignature;

        static AutomaticNodeInsertionTests()
        {
            NIFunctionBuilder signatureBuilder = PFTypes.Factory.DefineFunction("outputOwner");
            Signatures.AddOutputParameter(signatureBuilder, PFTypes.Int32, "owner");
            _outputOwnerSignature = signatureBuilder.CreateType();
            signatureBuilder = PFTypes.Factory.DefineFunction("outputString");
            Signatures.AddOutputParameter(signatureBuilder, PFTypes.String, "owner");
            _outputOwnerStringSignature = signatureBuilder.CreateType();
            signatureBuilder = PFTypes.Factory.DefineFunction("stringSlicePassthrough");
            Signatures.AddInputOutputParameter(
                signatureBuilder, 
                DataTypes.StringSliceType.CreateImmutableReference(Signatures.AddGenericLifetimeTypeParameter(signatureBuilder, "TLife")), 
                "stringSlice");
            _stringSlicePassthroughSignature = signatureBuilder.CreateType();
        }

        [TestMethod]
        public void OwnerWireConnectedToReferenceInputTerminal_AutomaticNodeInsertion_BorrowNodeAndTerminateLifetimeNodeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            FunctionalNode immutablePassthrough = new FunctionalNode(function.BlockDiagram, Signatures.ImmutablePassthroughType);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], immutablePassthrough.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            ExplicitBorrowNode borrowNode = AssertDiagramContainsNodeWithSources<ExplicitBorrowNode>(function.BlockDiagram, outputOwner.OutputTerminals[0]);
            Assert.AreEqual(borrowNode.OutputTerminals[0], immutablePassthrough.InputTerminals[0].GetImmediateSourceTerminal());
            TerminateLifetimeNode terminateLifetime = AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, immutablePassthrough.OutputTerminals[0]);
            AssertDiagramContainsNodeWithSources<DropNode>(function.BlockDiagram, terminateLifetime.OutputTerminals[0]);
        }

        [TestMethod]
        public void BorrowNodeWithUnwiredOutput_AutomaticNodeInsertion_TerminateLifetimeNodeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], borrow.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, borrow.OutputTerminals[0]);
        }

        [TestMethod]
        public void BorrowNodeIntoImmutablePassthroughWithUnwiredOutput_AutomaticNodeInsertion_TerminateLifetimeNodeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], borrow.InputTerminals[0]);
            FunctionalNode immutablePassthrough = new FunctionalNode(function.BlockDiagram, Signatures.ImmutablePassthroughType);
            Wire.Create(function.BlockDiagram, borrow.OutputTerminals[0], immutablePassthrough.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, immutablePassthrough.OutputTerminals[0]);
        }

        [TestMethod]
        public void BorrowNodeBranchedIntoTwoImmutablePassthroughsWithUnwiredOutputs_AutomaticNodeInsertion_TerminateLifetimeNodeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], borrow.InputTerminals[0]);
            FunctionalNode immutablePassthrough1 = new FunctionalNode(function.BlockDiagram, Signatures.ImmutablePassthroughType);
            FunctionalNode immutablePassthrough2 = new FunctionalNode(function.BlockDiagram, Signatures.ImmutablePassthroughType);
            Wire.Create(function.BlockDiagram, borrow.OutputTerminals[0], immutablePassthrough1.InputTerminals[0], immutablePassthrough2.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, immutablePassthrough1.OutputTerminals[0], immutablePassthrough2.OutputTerminals[0]);
        }

        [TestMethod]
        public void BorrowNodeIntoBorrowNodeWithUnwiredOutput_AutomaticNodeInsertion_TwoTerminateLifetimeNodesInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            ExplicitBorrowNode outerBorrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], outerBorrow.InputTerminals[0]);
            ExplicitBorrowNode innerBorrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outerBorrow.OutputTerminals[0], innerBorrow.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            var innerTerminateLifetime = function.BlockDiagram.Nodes.OfType<TerminateLifetimeNode>().FirstOrDefault(
                t => t.InputTerminals[0].GetImmediateSourceTerminal() == innerBorrow.OutputTerminals[0]);
            Assert.IsNotNull(innerTerminateLifetime);
            var outerTerminateLifetime = function.BlockDiagram.Nodes.OfType<TerminateLifetimeNode>().FirstOrDefault(
                t => t.InputTerminals[0].GetImmediateSourceTerminal() == innerTerminateLifetime.OutputTerminals[0]);
            Assert.IsNotNull(outerTerminateLifetime);
        }

        [TestMethod]
        public void BorrowNodeIntoFrameTunnelWithUnwiredOutput_AutomaticNodeInsertion_TunnelAndTerminateLifetimeNodeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], borrow.InputTerminals[0]);
            Frame frame = Frame.Create(function.BlockDiagram);
            Tunnel tunnel = CreateInputTunnel(frame);
            Wire.Create(function.BlockDiagram, borrow.OutputTerminals[0], tunnel.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            Tunnel outputTunnel = frame.BorderNodes.FirstOrDefault(t => t.Direction == Direction.Output) as Tunnel;
            Assert.IsNotNull(outputTunnel);
            Assert.AreEqual(tunnel.OutputTerminals[0], outputTunnel.InputTerminals[0].GetImmediateSourceTerminal());
            AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, outputTunnel.OutputTerminals[0]);
        }

        [TestMethod]
        public void UnconsumedOwnerVariable_AutomaticNodeInsertion_DropNodeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);

            RunCompilationUpToAutomaticNodeInsertion(function);

            AssertDiagramContainsNodeWithSources<DropNode>(function.BlockDiagram, outputOwner.OutputTerminals[0]);
        }

        [TestMethod]
        public void BorrowNodeWithUnwiredOutput_AutomaticNodeInsertion_DropNodeInsertedDownstreamOfTerminateLifetime()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerSignature);
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputOwner.OutputTerminals[0], borrow.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            var terminateLifetime = AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, borrow.OutputTerminals[0]);
            AssertDiagramContainsNodeWithSources<DropNode>(function.BlockDiagram, terminateLifetime.OutputTerminals[0]);
        }

        [TestMethod]
        public void BorrowTunnelWithUnwiredOutput_AutomaticNodeInsertion_NoTerminateLifetimeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            Frame frame = Frame.Create(function.BlockDiagram);
            BorrowTunnel borrowTunnel = CreateBorrowTunnel(frame, BorrowMode.Immutable);
            FunctionalNode outputStringOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerStringSignature);
            Wire.Create(function.BlockDiagram, outputStringOwner.OutputTerminals[0], borrowTunnel.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            Assert.IsFalse(frame.Diagram.Nodes.OfType<TerminateLifetimeNode>().Any());
        }

        [TestMethod]
        public void BorrowTunnelIntoReferenceTransformer_AutomaticNodeInsertion_NoTerminateLifetimeInserted()
        {
            DfirRoot function = DfirRoot.Create();
            Frame frame = Frame.Create(function.BlockDiagram);
            BorrowTunnel borrowTunnel = CreateBorrowTunnel(frame, BorrowMode.Immutable);
            FunctionalNode outputStringOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerStringSignature);
            Wire.Create(function.BlockDiagram, outputStringOwner.OutputTerminals[0], borrowTunnel.InputTerminals[0]);
            FunctionalNode stringToSlice = new FunctionalNode(frame.Diagram, Signatures.StringToSliceType);
            Wire.Create(frame.Diagram, borrowTunnel.OutputTerminals[0], stringToSlice.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            Assert.IsFalse(frame.Diagram.Nodes.OfType<TerminateLifetimeNode>().Any());
        }

        #region StringToSlice insertion

        [TestMethod]
        public void OwnerStringConnectedToStringSliceReferenceInputTerminal_AutomaticNodeInsertion_BorrowNodeAndStringToSliceInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputStringOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerStringSignature);
            FunctionalNode concatString = new FunctionalNode(function.BlockDiagram, _stringSlicePassthroughSignature);
            Wire.Create(function.BlockDiagram, outputStringOwner.OutputTerminals[0], concatString.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            ExplicitBorrowNode borrowNode = AssertDiagramContainsNodeWithSources<ExplicitBorrowNode>(function.BlockDiagram, outputStringOwner.OutputTerminals[0]);
            FunctionalNode stringToSlice = AssertDiagramContainsNodeWithSources<FunctionalNode>(function.BlockDiagram, f => f.Signature == Signatures.StringToSliceType, borrowNode.OutputTerminals[0]);
            Assert.AreEqual(stringToSlice.OutputTerminals[0], concatString.InputTerminals[0].GetImmediateSourceTerminal());
            TerminateLifetimeNode terminateLifetime = AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, concatString.OutputTerminals[0]);
            AssertDiagramContainsNodeWithSources<DropNode>(function.BlockDiagram, terminateLifetime.OutputTerminals[0]);
        }

        [TestMethod]
        public void StringReferenceConnectedToStringSliceReferenceInputTerminal_AutomaticNodeInsertion_BorrowNodeAndStringToSliceInserted()
        {
            DfirRoot function = DfirRoot.Create();
            FunctionalNode outputStringOwner = new FunctionalNode(function.BlockDiagram, _outputOwnerStringSignature);
            ExplicitBorrowNode borrow = new ExplicitBorrowNode(function.BlockDiagram, BorrowMode.Immutable, 1, true, true);
            Wire.Create(function.BlockDiagram, outputStringOwner.OutputTerminals[0], borrow.InputTerminals[0]);
            FunctionalNode concatString = new FunctionalNode(function.BlockDiagram, _stringSlicePassthroughSignature);
            Wire.Create(function.BlockDiagram, borrow.OutputTerminals[0], concatString.InputTerminals[0]);

            RunCompilationUpToAutomaticNodeInsertion(function);

            ExplicitBorrowNode reborrowNode = AssertDiagramContainsNodeWithSources<ExplicitBorrowNode>(function.BlockDiagram, b => b != borrow, borrow.OutputTerminals[0]);
            FunctionalNode stringToSlice = AssertDiagramContainsNodeWithSources<FunctionalNode>(function.BlockDiagram, f => f.Signature == Signatures.StringToSliceType, reborrowNode.OutputTerminals[0]);
            Assert.AreEqual(stringToSlice.OutputTerminals[0], concatString.InputTerminals[0].GetImmediateSourceTerminal());
            TerminateLifetimeNode innerTerminateLifetime = AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, concatString.OutputTerminals[0]);
            TerminateLifetimeNode outerTerminateLifetime = AssertDiagramContainsNodeWithSources<TerminateLifetimeNode>(function.BlockDiagram, t => t != innerTerminateLifetime, innerTerminateLifetime.OutputTerminals[0]);
            AssertDiagramContainsNodeWithSources<DropNode>(function.BlockDiagram, outerTerminateLifetime.OutputTerminals[0]);
        }

        #endregion

        private TNode AssertDiagramContainsNodeWithSources<TNode>(Diagram diagram, params Terminal[] sources) where TNode : Node
        {
            return AssertDiagramContainsNodeWithSources<TNode>(diagram, null, sources);
        }

        private TNode AssertDiagramContainsNodeWithSources<TNode>(Diagram diagram, Func<TNode, bool> nodePredicate, params Terminal[] sources) where TNode : Node
        {
            TNode node = diagram.Nodes.OfType<TNode>().FirstOrDefault(nodePredicate ?? (tNode => true));
            Assert.IsNotNull(node, $"Expected to find a {typeof(TNode).Name}");
            Assert.AreEqual(sources.Length, node.InputTerminals.Count);
            for (int i = 0; i < sources.Length; ++i)
            {
                Assert.AreEqual(sources[i], node.InputTerminals[i].GetImmediateSourceTerminal());
            }
            return node;
        }
    }
}
