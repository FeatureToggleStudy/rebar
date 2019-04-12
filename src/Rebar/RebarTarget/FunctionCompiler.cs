﻿using System;
using System.Collections.Generic;
using System.Linq;
using NationalInstruments;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Compiler.Nodes;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.RebarTarget.Execution;

namespace Rebar.RebarTarget
{
    internal class FunctionCompiler : VisitorTransformBase, IDfirNodeVisitor<bool>, IDfirStructureVisitor<bool>
    {
        #region Functional Nodes

        private static readonly Dictionary<string, Action<FunctionCompiler, FunctionalNode>> _functionalNodeCompilers;

        static FunctionCompiler()
        {
            _functionalNodeCompilers = new Dictionary<string, Action<FunctionCompiler, FunctionalNode>>();
            _functionalNodeCompilers["ImmutPass"] = CompileNothing;
            _functionalNodeCompilers["MutPass"] = CompileNothing;
            _functionalNodeCompilers["CreateCopy"] = CompileCreateCopy;
            _functionalNodeCompilers["SelectReference"] = CompileSelectReference;
            _functionalNodeCompilers["Output"] = CompileOutput;
            _functionalNodeCompilers["VectorCreate"] = CompileNothing;
            _functionalNodeCompilers["VectorInsert"] = CompileNothing;

            _functionalNodeCompilers["Increment"] = (_, __) => CompilePureUnaryPrimitive(_, __, UnaryPrimitiveOps.Increment);
            _functionalNodeCompilers["Not"] = (_, __) => CompilePureUnaryPrimitive(_, __, UnaryPrimitiveOps.Not);
            _functionalNodeCompilers["Add"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.Add);
            _functionalNodeCompilers["Subtract"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.Subtract);
            _functionalNodeCompilers["Multiply"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.Multiply);
            _functionalNodeCompilers["Divide"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.Divide);
            _functionalNodeCompilers["And"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.And);
            _functionalNodeCompilers["Or"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.Or);
            _functionalNodeCompilers["Xor"] = (_, __) => CompilePureBinaryPrimitive(_, __, BinaryPrimitiveOps.Xor);

            _functionalNodeCompilers["AccumulateAdd"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.Add);
            _functionalNodeCompilers["AccumulateSubtract"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.Subtract);
            _functionalNodeCompilers["AccumulateMultiply"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.Multiply);
            _functionalNodeCompilers["AccumulateDivide"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.Divide);
            _functionalNodeCompilers["AccumulateAnd"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.And);
            _functionalNodeCompilers["AccumulateOr"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.Or);
            _functionalNodeCompilers["AccumulateXor"] = (_, __) => CompileMutatingBinaryPrimitive(_, __, BinaryPrimitiveOps.Xor);
        }

        private static void CompileNothing(FunctionCompiler compiler, FunctionalNode noopNode)
        {
        }

        private static void CompileCreateCopy(FunctionCompiler compiler, FunctionalNode createCopyNode)
        {
            VariableReference copyFrom = createCopyNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                copyTo = createCopyNode.OutputTerminals.ElementAt(1).GetTrueVariable();
            compiler.LoadLocalAllocationReference(copyTo);
            // TODO: this should be a generic load/store
            compiler.LoadValueAsReference(copyFrom);
            compiler._builder.EmitDerefInteger();
            compiler._builder.EmitStoreInteger();
        }

        private static void CompileSelectReference(FunctionCompiler compiler, FunctionalNode selectReferenceNode)
        {
            LabelBuilder falseLabel = compiler._builder.CreateLabel(),
                endLabel = compiler._builder.CreateLabel();
            VariableReference input1 = selectReferenceNode.InputTerminals.ElementAt(1).GetTrueVariable(),
                input2 = selectReferenceNode.InputTerminals.ElementAt(2).GetTrueVariable(),
                selector = selectReferenceNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                selectedReference = selectReferenceNode.OutputTerminals.ElementAt(1).GetTrueVariable();
            compiler.LoadLocalAllocationReference(selectedReference);
            compiler.LoadValueAsReference(selector);
            compiler._builder.EmitDerefInteger();
            compiler._builder.EmitBranchIfFalse(falseLabel);

            // true
            compiler.LoadValueAsReference(input1);
            compiler._builder.EmitBranch(endLabel);

            // false
            compiler._builder.SetLabel(falseLabel);
            compiler.LoadValueAsReference(input2);

            // end
            compiler._builder.SetLabel(endLabel);
            compiler._builder.EmitStorePointer();
        }

        private static void CompileOutput(FunctionCompiler compiler, FunctionalNode outputNode)
        {
            VariableReference input = outputNode.InputTerminals.ElementAt(0).GetTrueVariable();
            compiler.LoadValueAsReference(input);
            compiler._builder.EmitDerefInteger();
            compiler._builder.EmitOutput_TEMP();
        }

        private static void CompilePureUnaryPrimitive(FunctionCompiler compiler, FunctionalNode primitiveNode, UnaryPrimitiveOps operation)
        {
            VariableReference input = primitiveNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                output = primitiveNode.OutputTerminals.ElementAt(1).GetTrueVariable();
            compiler.LoadLocalAllocationReference(output);
            compiler.EmitUnaryOperationOnVariable(input, operation);
            compiler._builder.EmitStoreInteger();
        }

        private static void CompilePureBinaryPrimitive(FunctionCompiler compiler, FunctionalNode primitiveNode, BinaryPrimitiveOps operation)
        {
            VariableReference input1 = primitiveNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                input2 = primitiveNode.InputTerminals.ElementAt(1).GetTrueVariable(),
                output = primitiveNode.OutputTerminals.ElementAt(2).GetTrueVariable();
            compiler.LoadLocalAllocationReference(output);
            compiler.LoadValueAsReference(input1);
            compiler._builder.EmitDerefInteger();
            compiler.LoadValueAsReference(input2);
            compiler._builder.EmitDerefInteger();
            compiler.EmitBinaryOperation(operation);
            compiler._builder.EmitStoreInteger();
        }

        private static void CompileMutatingUnaryPrimitive(FunctionCompiler compiler, FunctionalNode primitiveNode, UnaryPrimitiveOps operation)
        {
            VariableReference input = primitiveNode.InputTerminals.ElementAt(0).GetTrueVariable();
            compiler.LoadValueAsReference(input);
            compiler.EmitUnaryOperationOnVariable(input, operation);
            compiler._builder.EmitStoreInteger();
        }

        private static void CompileMutatingBinaryPrimitive(FunctionCompiler compiler, FunctionalNode primitiveNode, BinaryPrimitiveOps operation)
        {
            VariableReference input1 = primitiveNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                input2 = primitiveNode.InputTerminals.ElementAt(1).GetTrueVariable();
            compiler.LoadValueAsReference(input1);
            compiler._builder.EmitDuplicate();
            compiler._builder.EmitDerefInteger();
            compiler.LoadValueAsReference(input2);
            compiler._builder.EmitDerefInteger();
            compiler.EmitBinaryOperation(operation);
            compiler._builder.EmitStoreInteger();
        }

        private static void CompileRange(FunctionCompiler compiler, FunctionalNode rangeNode)
        {
            VariableReference lowInput = rangeNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                highInput = rangeNode.InputTerminals.ElementAt(1).GetTrueVariable(),
                output = rangeNode.OutputTerminals.ElementAt(0).GetTrueVariable();

            compiler.LoadLocalAllocationReference(output);
            compiler._builder.EmitDuplicate();
            compiler.LoadLocalAllocationReference(lowInput);
            compiler._builder.EmitDerefInteger();
            compiler._builder.EmitLoadIntegerImmediate(1);
            compiler._builder.EmitSubtract();
            compiler._builder.EmitStoreInteger();

            compiler._builder.EmitLoadIntegerImmediate(4);
            compiler._builder.EmitAdd();
            compiler.LoadLocalAllocationReference(highInput);
            compiler._builder.EmitDerefInteger();
            compiler._builder.EmitStoreInteger();
        }

        #endregion

        private readonly FunctionBuilder _builder;
        private readonly Dictionary<VariableReference, ValueSource> _variableAllocations;

        public FunctionCompiler(FunctionBuilder builder, Dictionary<VariableReference, ValueSource> variableAllocations)
        {
            _builder = builder;
            _variableAllocations = variableAllocations;
        }

        private void LoadValueAsReference(VariableReference local)
        {
            ValueSource allocation = _variableAllocations[local];
            var localAllocation = allocation as LocalAllocationValueSource;
            if (localAllocation != null)
            {
                _builder.EmitLoadLocalAddress((byte)localAllocation.Index);
                _builder.EmitDerefPointer();
            }
            else
            {
                var constantLocalReference = (ConstantLocalReferenceValueSource)allocation;
                _builder.EmitLoadLocalAddress((byte)constantLocalReference.ReferencedIndex);
            }
        }

        private void LoadLocalAllocationReference(VariableReference local)
        {
            var localAllocation = _variableAllocations[local] as LocalAllocationValueSource;
            if (localAllocation == null)
            {
                throw new ArgumentException("The given variable is not associated with a local allocation.", "local");
            }
            _builder.EmitLoadLocalAddress((byte)localAllocation.Index);
        }

        private void BorrowFromVariableIntoVariable(VariableReference from, VariableReference into)
        {
            if (_variableAllocations[into] is LocalAllocationValueSource)
            {
                LoadLocalAllocationReference(into);
                LoadLocalAllocationReference(from);
                _builder.EmitStorePointer();
            }
        }

        private void EmitUnaryOperationOnVariable(VariableReference variable, UnaryPrimitiveOps operation)
        {
            switch (operation)
            {
                case UnaryPrimitiveOps.Increment:
                    _builder.EmitLoadIntegerImmediate(1);
                    LoadValueAsReference(variable);
                    _builder.EmitDerefInteger();
                    _builder.EmitAdd();
                    break;
                case UnaryPrimitiveOps.Not:
                    _builder.EmitLoadIntegerImmediate(1);
                    LoadValueAsReference(variable);
                    _builder.EmitDerefInteger();
                    _builder.EmitSubtract();
                    break;
            }
        }

        private void EmitBinaryOperation(BinaryPrimitiveOps operation)
        {
            switch (operation)
            {
                case BinaryPrimitiveOps.Add:
                    _builder.EmitAdd();
                    break;
                case BinaryPrimitiveOps.Subtract:
                    _builder.EmitSubtract();
                    break;
                case BinaryPrimitiveOps.Multiply:
                    _builder.EmitMultiply();
                    break;
                case BinaryPrimitiveOps.Divide:
                    _builder.EmitDivide();
                    break;
                case BinaryPrimitiveOps.And:
                    _builder.EmitAnd();
                    break;
                case BinaryPrimitiveOps.Or:
                    _builder.EmitOr();
                    break;
                case BinaryPrimitiveOps.Xor:
                    _builder.EmitXor();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public bool VisitAssignNode(AssignNode assignNode)
        {
            VariableReference assignee = assignNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                value = assignNode.InputTerminals.ElementAt(1).GetTrueVariable();
            LoadValueAsReference(assignee);
            LoadLocalAllocationReference(value);
            _builder.EmitDerefInteger();
            _builder.EmitStoreInteger();
            return true;
        }

        public bool VisitBorrowTunnel(BorrowTunnel borrowTunnel)
        {
            VariableReference input = borrowTunnel.InputTerminals.ElementAt(0).GetTrueVariable(),
                output = borrowTunnel.OutputTerminals.ElementAt(0).GetTrueVariable();
            if (_variableAllocations[output] is LocalAllocationValueSource)
            {
                BorrowFromVariableIntoVariable(input, output);
            }
            return true;
        }

        public bool VisitConstant(Constant constant)
        {
            var output = constant.OutputTerminal.GetTrueVariable();
            if (constant.Value is int)
            {
                LoadLocalAllocationReference(output);
                _builder.EmitLoadIntegerImmediate((int)constant.Value);
                _builder.EmitStoreInteger();
            }
            else if (constant.Value is bool)
            {
                LoadLocalAllocationReference(output);
                _builder.EmitLoadIntegerImmediate((bool)constant.Value ? 1 : 0);
                _builder.EmitStoreInteger();
            }
            return true;
        }

        public bool VisitCreateCellNode(CreateCellNode createCellNode)
        {
            throw new NotImplementedException();
        }

        public bool VisitDropNode(DropNode dropNode)
        {
            throw new NotImplementedException();
        }

        public bool VisitExchangeValuesNode(ExchangeValuesNode exchangeValuesNode)
        {
            VariableReference var1 = exchangeValuesNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                var2 = exchangeValuesNode.InputTerminals.ElementAt(1).GetTrueVariable();
            LoadValueAsReference(var2);
            LoadValueAsReference(var1);
            _builder.EmitDuplicate();
            _builder.EmitDerefInteger();
            _builder.EmitSwap();
            LoadValueAsReference(var2);
            _builder.EmitDerefInteger();
            _builder.EmitStoreInteger();
            _builder.EmitStoreInteger();
            return true;
        }

        public bool VisitExplicitBorrowNode(ExplicitBorrowNode explicitBorrowNode)
        {
            VariableReference input = explicitBorrowNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                output = explicitBorrowNode.OutputTerminals.ElementAt(0).GetTrueVariable();
            BorrowFromVariableIntoVariable(input, output);
            return true;
        }

        public bool VisitFunctionalNode(FunctionalNode functionalNode)
        {
            Action<FunctionCompiler, FunctionalNode> compiler;
            string functionName = functionalNode.Signature.GetName();
            if (_functionalNodeCompilers.TryGetValue(functionName, out compiler))
            {
                compiler(this, functionalNode);
                return true;
            }
            throw new NotImplementedException();
        }

        public bool VisitLockTunnel(LockTunnel lockTunnel)
        {
            throw new NotImplementedException();
        }

        public bool VisitSomeConstructorNode(SomeConstructorNode someConstructorNode)
        {
            VariableReference input = someConstructorNode.InputTerminals.ElementAt(0).GetTrueVariable(),
                output = someConstructorNode.OutputTerminals.ElementAt(0).GetTrueVariable();
            LoadLocalAllocationReference(output);
            _builder.EmitDuplicate();
            _builder.EmitLoadIntegerImmediate(1);
            _builder.EmitStoreInteger();
            _builder.EmitLoadIntegerImmediate(4);
            _builder.EmitAdd();
            if (input.Type.IsRebarReferenceType())
            {
                LoadValueAsReference(input);
                _builder.EmitStorePointer();
            }
            else
            {
                LoadLocalAllocationReference(input);
                _builder.EmitDerefInteger();
                _builder.EmitStoreInteger();
            }
            return true;
        }

        public bool VisitTerminateLifetimeNode(TerminateLifetimeNode terminateLifetimeNode)
        {
            return true;
        }

        public bool VisitTerminateLifetimeTunnel(TerminateLifetimeTunnel terminateLifetimeTunnel)
        {
            return true;
        }

        private void CopyValue(NIType type)
        {
            // TODO: this should just be a generic Deref and Store
            if (type.IsRebarReferenceType())
            {
                _builder.EmitDerefPointer();
                _builder.EmitStorePointer();
            }
            else
            {
                _builder.EmitDerefInteger();
                _builder.EmitStoreInteger();
            }
        }

        public bool VisitTunnel(Tunnel tunnel)
        {
            VariableReference input = tunnel.InputTerminals.ElementAt(0).GetTrueVariable(),
                output = tunnel.OutputTerminals.ElementAt(0).GetTrueVariable();
            if (output.Type == input.Type.CreateOption())
            {
                LoadLocalAllocationReference(output);
                _builder.EmitLoadIntegerImmediate(1);
                _builder.EmitStoreInteger();
                LoadLocalAllocationReference(output);
                _builder.EmitLoadIntegerImmediate(4);
                _builder.EmitAdd();
                LoadLocalAllocationReference(input);
                CopyValue(input.Type);
                return true;
            }

            ValueSource inputValueSource = _variableAllocations[input],
                outputValueSource = _variableAllocations[output];
            if (inputValueSource != outputValueSource)
            {
                // For now assume that the allocator will always make the input and output the same ValueSource.
                throw new NotImplementedException();
            }
            return true;
        }

        protected override void VisitBorderNode(NationalInstruments.Dfir.BorderNode borderNode)
        {
            this.VisitRebarNode(borderNode);
        }

        protected override void VisitNode(Node node)
        {
            this.VisitRebarNode(node);
        }

        protected override void VisitWire(Wire wire)
        {
            if (wire.SinkTerminals.HasMoreThan(1))
            {
                VariableReference sourceVariable = wire.SourceTerminal.GetTrueVariable();
                if (!(_variableAllocations[sourceVariable] is LocalAllocationValueSource))
                {
                    // if the source is not a local allocation, then presumably the sinks aren't either and
                    // there's nothing to copy
                    // TODO: this may change later if it becomes possible to have different sink branches 
                    // have different mutability settings
                    return;
                }
                VariableReference[] sinkVariables = wire.SinkTerminals.Skip(1).Select(VariableExtensions.GetTrueVariable).ToArray();
                foreach (var sinkVariable in sinkVariables)
                {
                    LoadLocalAllocationReference(sinkVariable);
                }
                LoadLocalAllocationReference(sourceVariable);
                if (sourceVariable.Type.IsRebarReferenceType())
                {
                    _builder.EmitDerefPointer();
                }
                else
                {
                    _builder.EmitDerefInteger();
                }

                for (int i = 0; i < sinkVariables.Length; ++i)
                {
                    VariableReference sinkVariable = sinkVariables[i];
                    if (i < sinkVariables.Length - 1)
                    {
                        _builder.EmitDuplicate();
                    }
                    // TODO: this should just be a generic Deref and Store
                    if (sinkVariable.Type.IsRebarReferenceType())
                    {
                        _builder.EmitStorePointer();
                    }
                    else
                    {
                        _builder.EmitStoreInteger();
                    }
                }
            }
        }

        protected override void VisitStructure(Structure structure, StructureTraversalPoint traversalPoint)
        {
            base.VisitStructure(structure, traversalPoint);
            this.VisitRebarStructure(structure, traversalPoint);
        }

#region Frame

        private struct FrameData
        {
            public FrameData(LabelBuilder unwrapFailed, LabelBuilder end)
            {
                UnwrapFailed = unwrapFailed;
                End = end;
            }

            public LabelBuilder UnwrapFailed { get; }

            public LabelBuilder End { get; }
        }

        private readonly Dictionary<Frame, FrameData> _frameData = new Dictionary<Frame, FrameData>();

        public bool VisitFrame(Frame frame, StructureTraversalPoint traversalPoint)
        {
            switch (traversalPoint)
            {
                case StructureTraversalPoint.BeforeLeftBorderNodes:
                    VisitFrameBeforeLeftBorderNodes(frame);
                    break;
                case StructureTraversalPoint.AfterRightBorderNodes:
                    VisitFrameAfterRightBorderNodes(frame);
                    break;
            }
            return true;
        }

        private void VisitFrameBeforeLeftBorderNodes(Frame frame)
        {
            LabelBuilder unwrapFailed = _builder.CreateLabel(),
                end = _builder.CreateLabel();
            _frameData[frame] = new FrameData(unwrapFailed, end);
        }

        private void VisitFrameAfterRightBorderNodes(Frame frame)
        {
            FrameData frameData = _frameData[frame];
            _builder.EmitBranch(frameData.End);
            _builder.SetLabel(frameData.UnwrapFailed);
            foreach (Tunnel tunnel in frame.BorderNodes.OfType<Tunnel>().Where(t => t.Direction == Direction.Output))
            {
                // Store a None value for the tunnel
                VariableReference tunnelOutput = tunnel.OutputTerminals.ElementAt(0).GetTrueVariable();
                LoadLocalAllocationReference(tunnelOutput);
                _builder.EmitLoadIntegerImmediate(0);
                _builder.EmitStoreInteger();
            }
            _builder.SetLabel(frameData.End);
        }

        public bool VisitUnwrapOptionTunnel(UnwrapOptionTunnel unwrapOptionTunnel)
        {
            FrameData frameData = _frameData[(Frame)unwrapOptionTunnel.ParentStructure];
            VariableReference tunnelInput = unwrapOptionTunnel.InputTerminals.ElementAt(0).GetTrueVariable(),
                tunnelOutput = unwrapOptionTunnel.OutputTerminals.ElementAt(0).GetTrueVariable();
            LoadLocalAllocationReference(tunnelInput);
            _builder.EmitDerefInteger();
            _builder.EmitBranchIfFalse(frameData.UnwrapFailed);

            // TODO: we could cheat here and do nothing if we say that the address of the 
            // output is the address of the value within the input
            // (assuming Option<T> always ::= { bool, T })
            LoadLocalAllocationReference(tunnelOutput);
            LoadLocalAllocationReference(tunnelInput);
            _builder.EmitLoadIntegerImmediate(4);
            _builder.EmitAdd();
            if (tunnelOutput.Type.IsRebarReferenceType())
            {
                _builder.EmitDerefPointer();
                _builder.EmitStorePointer();
            }
            else
            {
                // TODO
                _builder.EmitDerefInteger();
                _builder.EmitStoreInteger();
            }
            return true;
        }

#endregion

#region Loop

        private struct LoopData
        {
            public LoopData(LabelBuilder start, LabelBuilder end, VariableReference loopCondition)
            {
                Start = start;
                End = end;
                LoopCondition = loopCondition;
            }

            public LabelBuilder Start { get; }

            public LabelBuilder End { get; }

            public VariableReference LoopCondition { get; }
        }

        private Dictionary<Compiler.Nodes.Loop, LoopData> _loopData = new Dictionary<Compiler.Nodes.Loop, LoopData>();

        public bool VisitLoop(Compiler.Nodes.Loop loop, StructureTraversalPoint traversalPoint)
        {
            // generate code for each left-side border node;
            // each border node that can affect condition should &&= the LoopCondition
            // variable with whether it allows loop to proceed
            switch (traversalPoint)
            {
                case StructureTraversalPoint.BeforeLeftBorderNodes:
                    VisitLoopBeforeLeftBorderNodes(loop);
                    break;
                case StructureTraversalPoint.AfterLeftBorderNodesAndBeforeDiagram:
                    VisitLoopAfterLeftBorderNodes(loop);
                    break;
                case StructureTraversalPoint.AfterRightBorderNodes:
                    VisitLoopAfterRightBorderNodes(loop);
                    break;
            }
            return true;
        }

        private void VisitLoopBeforeLeftBorderNodes(Compiler.Nodes.Loop loop)
        {
            LabelBuilder start = _builder.CreateLabel(),
                end = _builder.CreateLabel();
            LoopConditionTunnel loopCondition = loop.BorderNodes.OfType<LoopConditionTunnel>().First();
            Terminal loopConditionInput = loopCondition.InputTerminals.ElementAt(0);
            VariableReference loopConditionVariable = loopConditionInput.GetTrueVariable();
            _loopData[loop] = new LoopData(start, end, loopConditionVariable);

            if (!loopConditionInput.IsConnected)
            {
                // if loop condition was unwired, initialize it to true
                LoadLocalAllocationReference(loopConditionVariable);
                _builder.EmitLoadIntegerImmediate(1);
                _builder.EmitStoreInteger();
            }

            _builder.SetLabel(start);
        }

        private void VisitLoopAfterLeftBorderNodes(Compiler.Nodes.Loop loop)
        {
            LoopData loopData = _loopData[loop];
            LoadLocalAllocationReference(loopData.LoopCondition);
            _builder.EmitDerefInteger();
            _builder.EmitBranchIfFalse(loopData.End);
        }

        private void VisitLoopAfterRightBorderNodes(Compiler.Nodes.Loop loop)
        {
            LoopData loopData = _loopData[loop];
            _builder.EmitBranch(loopData.Start);
            _builder.SetLabel(loopData.End);
        }

        public bool VisitLoopConditionTunnel(LoopConditionTunnel loopConditionTunnel)
        {
            return true;
        }

        public bool VisitIterateTunnel(IterateTunnel iterateTunnel)
        {
            // TODO: this should eventually call a RangeIterator::next function
            LoopData loopData = _loopData[(Compiler.Nodes.Loop)iterateTunnel.ParentStructure];
            LoadLocalAllocationReference(loopData.LoopCondition);    // &cond
            _builder.EmitDuplicate();   // &cond, &cond
            _builder.EmitDerefInteger();    // &cond, cond

            VariableReference rangeInput = iterateTunnel.InputTerminals.ElementAt(0).GetTrueVariable();
            VariableReference output = iterateTunnel.OutputTerminals.ElementAt(0).GetTrueVariable();
            LoadValueAsReference(rangeInput);    // &range
            _builder.EmitDuplicate();
            _builder.EmitLoadIntegerImmediate(4);
            _builder.EmitAdd();     // &range, &range.max
            _builder.EmitDerefInteger();    // &range, range.max
            _builder.EmitSwap();    // range.max, &range
            _builder.EmitDuplicate();   // range.max, &range, &range
            _builder.EmitDuplicate();   // range.max, &range, &range, &range
            _builder.EmitDerefInteger();    // range.max, &range, &range, range.current
            _builder.EmitLoadIntegerImmediate(1);
            _builder.EmitAdd();     // range.max, &range, &range, range.current+1
            _builder.EmitDuplicate();
            LoadLocalAllocationReference(output);
            _builder.EmitSwap();            // range.max, &range, &range, range.current+1, &output, range.current+1
            _builder.EmitStoreInteger();    // range.max, &range, &range, range.current+1
            _builder.EmitStoreInteger();    // range.max, &range

            _builder.EmitDerefInteger();    // range.max, range.current
            _builder.EmitGreaterThan();     // &cond, cond, (range.max > range.current)
            _builder.EmitAnd(); // &cond, (cond && range.max > range.current)
            _builder.EmitStoreInteger();
            return true;
        }

#endregion
    }
}
