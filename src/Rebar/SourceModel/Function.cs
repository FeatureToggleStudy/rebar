﻿using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NationalInstruments.MocCommon.SourceModel;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;
using NationalInstruments.VI.SourceModel;
using Rebar.Compiler;

namespace Rebar.SourceModel
{
    public class Function : DataflowFunctionDefinition
    {
        #region Dynamic Properties

        /// <summary>
        /// Namespace name
        /// </summary>
        public const string ParsableNamespaceName = "http://www.ni.com/Rebar";

        private const string ElementName = "Function";

        public const string FunctionMocIdentifier = "RebarFunction.Moc";

        #endregion

        /// <summary>
        /// DefinitionType
        /// </summary>
        public const string FunctionDefinitionType = "Rebar.SourceModel.Function";

        /// <summary>
        ///  Get the root diagram of the function.
        /// </summary>
        public NationalInstruments.SourceModel.RootDiagram Diagram => Components.OfType<NationalInstruments.SourceModel.RootDiagram>().Single();

        private Function()
            : base(new BlockDiagram(), false)
        {
        }

        [ExportDefinitionFactory(FunctionDefinitionType)]
        [StaticBindingKeywords(FunctionMocIdentifier)]
        // [StaticBindingKeywords("ProjectItemCopyPasteDefaultService")]
        [XmlParserFactoryMethod(ElementName, ParsableNamespaceName)]
        public static Function Create(IElementCreateInfo elementCreateInfo)
        {
            var function = new Function();
            function.Host = elementCreateInfo.Host;
            function.Init(elementCreateInfo);
            return function;
        }

        protected override NationalInstruments.SourceModel.RootDiagram CreateNewRootDiagram()
        {
            return BlockDiagram.Create(ElementCreateInfo.ForNew);
        }

        public override XName XmlElementName => XName.Get(ElementName, Function.ParsableNamespaceName);

        /// <inheritdoc />
        public override IWiringBehavior WiringBehavior => new VirtualInstrumentWiringBehavior();

        /// <inheritdoc />
        protected override void CreateBatchRules(ICollection<ModelBatchRule> rules)
        {
            rules.Add(new CoreBatchRule());
            rules.Add(new ContentBatchRule());
            // rules.Add(new UIModelContextBatchRule());
            rules.Add(new VerticalGrowNodeBoundsRule());
            // rules.Add(new GroupRule());
            rules.Add(new DockedConstantBatchRule());
            rules.Add(new WiringBatchRule());
            rules.Add(new WireCommentBatchRule());
            rules.Add(new SequenceStructureBatchRule());
            rules.Add(new LoopBatchRule());
            rules.Add(new PairedTunnelBatchRule());
        }

        /// <inheritdoc />
        public override void AcceptVisitor(IElementVisitor visitor)
        {
            var functionVisitor = visitor as IFunctionVisitor;
            if (functionVisitor != null)
            {
                functionVisitor.VisitFunction(this);
            }
            else
            {
                base.AcceptVisitor(visitor);
            }
        }

        // TODO
        public override IEnumerable<IDiagramParameter> Parameters => Enumerable.Empty<IDiagramParameter>();
    }
}
