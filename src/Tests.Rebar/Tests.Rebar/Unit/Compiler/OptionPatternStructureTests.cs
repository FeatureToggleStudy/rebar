using System;
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
    public class OptionPatternStructureTests : CompilerTestBase
    {
        [TestMethod]
        public void OptionPatternStructureWithOptionValueConnectedToSelector_SetVariableTypes_SelectorInnerTerminalOnSomeDiagramHasOptionInnerType()
        {
            DfirRoot function = DfirRoot.Create();
            OptionPatternStructure patternStructure = CreateOptionPatternStructure(function.BlockDiagram);
            FunctionalNode someConstructor = new FunctionalNode(function.BlockDiagram, Signatures.SomeConstructorType);
            Wire.Create(function.BlockDiagram, someConstructor.OutputTerminals[0], patternStructure.Selector.InputTerminals[0]);
            ConnectConstantToInputTerminal(someConstructor.InputTerminals[0], PFTypes.Int32, false);

            RunSemanticAnalysisUpToSetVariableTypes(function);

            VariableReference innerSelectorVariable = patternStructure.Selector.OutputTerminals[0].GetTrueVariable();
            Assert.IsTrue(innerSelectorVariable.Type.IsInt32());
        }

        private OptionPatternStructure CreateOptionPatternStructure(Diagram parentDiagram)
        {
            OptionPatternStructure patternStructure = new OptionPatternStructure(parentDiagram);
            patternStructure.CreateDiagram();
            return patternStructure;
        }
    }
}
