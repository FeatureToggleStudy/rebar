using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments.DataTypes;
using NationalInstruments.Dfir;
using Rebar.Common;
using Rebar.Compiler;
using Rebar.Compiler.Nodes;

namespace Tests.Rebar.Unit.Execution
{
    [TestClass]
    public class OptionPatternStructureExecutionTests : ExecutionTestBase
    {
        [TestMethod]
        public void OptionPatternStructureWithSomeValueWiredToSelector_Execute_SomeDiagramIsExecuted()
        {
            var test = new OptionPatternStructureWithInspectOnEachDiagramTest(this, true);

            test.CompileAndExecuteFunction();

            AssertByteArrayIsInt32(test.SomeInspectNodeValue, 1);
            AssertByteArrayIsInt32(test.NoneInspectNodeValue, 0);
        }

        [TestMethod]
        public void OptionPatternStructureWithNoneValueWiredToSelector_Execute_NoneDiagramIsExecuted()
        {
            var test = new OptionPatternStructureWithInspectOnEachDiagramTest(this, false);

            test.CompileAndExecuteFunction();

            AssertByteArrayIsInt32(test.SomeInspectNodeValue, 0);
            AssertByteArrayIsInt32(test.NoneInspectNodeValue, 1);
        }

        private class OptionPatternStructureWithInspectOnEachDiagramTest
        {
            private readonly ExecutionTestBase _test;
            private readonly DfirRoot _function;
            private readonly FunctionalNode _someInspectNode, _noneInspectNode;

            public OptionPatternStructureWithInspectOnEachDiagramTest(ExecutionTestBase test, bool selectorValueIsSome)
            {
                _test = test;
                _function = DfirRoot.Create();
                OptionPatternStructure patternStructure = _test.CreateOptionPatternStructure(_function.BlockDiagram);
                if (selectorValueIsSome)
                {
                    FunctionalNode someConstructor = new FunctionalNode(_function.BlockDiagram, Signatures.SomeConstructorType);
                    Wire.Create(_function.BlockDiagram, someConstructor.OutputTerminals[0], patternStructure.Selector.InputTerminals[0]);
                    _test.ConnectConstantToInputTerminal(someConstructor.InputTerminals[0], PFTypes.Int32, 0, false);
                }
                else
                {
                    FunctionalNode someConstructor = new FunctionalNode(_function.BlockDiagram, Signatures.SomeConstructorType);
                    _test.ConnectConstantToInputTerminal(someConstructor.InputTerminals[0], PFTypes.Int32, 0, false);
                    FunctionalNode assign = new FunctionalNode(_function.BlockDiagram, Signatures.AssignType);
                    Wire optionValueWire = Wire.Create(_function.BlockDiagram, someConstructor.OutputTerminals[0], assign.InputTerminals[0]);
                    optionValueWire.SetWireBeginsMutableVariable(true);
                    FunctionalNode noneConstructor = new FunctionalNode(_function.BlockDiagram, Signatures.NoneConstructorType);
                    Wire.Create(_function.BlockDiagram, noneConstructor.OutputTerminals[0], assign.InputTerminals[1]);
                    Wire.Create(_function.BlockDiagram, assign.OutputTerminals[0], patternStructure.Selector.InputTerminals[0]);
                }

                _someInspectNode = new FunctionalNode(patternStructure.Diagrams[0], Signatures.InspectType);
                _test.ConnectConstantToInputTerminal(_someInspectNode.InputTerminals[0], PFTypes.Int32, 1, false);

                _noneInspectNode = new FunctionalNode(patternStructure.Diagrams[1], Signatures.InspectType);
                _test.ConnectConstantToInputTerminal(_noneInspectNode.InputTerminals[0], PFTypes.Int32, 1, false);
            }

            public void CompileAndExecuteFunction()
            {
                var executionInstance = _test.CompileAndExecuteFunction(_function);
                SomeInspectNodeValue = executionInstance.GetLastValueFromInspectNode(_someInspectNode);
                NoneInspectNodeValue = executionInstance.GetLastValueFromInspectNode(_noneInspectNode);
            }

            public byte[] SomeInspectNodeValue { get; set; }

            public byte[] NoneInspectNodeValue { get; set; }
        }
    }
}
