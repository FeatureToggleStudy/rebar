using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NationalInstruments.CommonModel;
using NationalInstruments.Core;
using NationalInstruments.DataTypes;
using NationalInstruments.Design;
using NationalInstruments.MocCommon.SourceModel;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;
using NationalInstruments.VI.SourceModel;
using RustyWires.Compiler;

namespace RustyWires.SourceModel
{
    public class RustyWiresFunction : DataflowFunctionDefinition
    {
        #region Dynamic Properties

        /// <summary>
        /// Namespace name
        /// </summary>
        public const string ParsableNamespaceName = "http://www.ni.com/RustyWires";

        private const string ElementName = "RustyWiresFunction";

        public const string RustyWiresMocIdentifier = "RustyWiresFunction.Moc";

        #endregion

        /// <summary>
        /// DefinitionType
        /// </summary>
        public const string RustyWiresFunctionDefinitionType = "RustyWires.SourceModel.RustyWiresFunction";

        public static readonly string DiagramClipboardDataFormat = ClipboardFormatHelper.RegisterClipboardFormat(
            DragDrop.NIDataFormatPrefix + DiagramPaletteConstants.DiagramPaletteIdentifier,
            "RustyWiresDiagram");

        /// <summary>
        ///  Get the root diagram of the sketch.
        /// </summary>
        public RootDiagram Diagram => Components.OfType<RootDiagram>().Single();

        private RustyWiresFunction()
            : base(new BlockDiagram(), false)
        {
        }

        [ExportDefinitionFactory(RustyWiresFunctionDefinitionType)]
        [StaticBindingKeywords(RustyWiresMocIdentifier)]
        // [StaticBindingKeywords("ProjectItemCopyPasteDefaultService")]
        [XmlParserFactoryMethod(ElementName, ParsableNamespaceName)]
        public static RustyWiresFunction Create(IElementCreateInfo elementCreateInfo)
        {
            var rustyWiresFunction = new RustyWiresFunction();
            rustyWiresFunction.Host = elementCreateInfo.Host;
            rustyWiresFunction.Init(elementCreateInfo);
            return rustyWiresFunction;
        }

        protected override RootDiagram CreateNewRootDiagram()
        {
            return BlockDiagram.Create(ElementCreateInfo.ForNew);
        }

        public override XName XmlElementName => XName.Get(ElementName, RustyWiresFunction.ParsableNamespaceName);

        /// <inheritdoc />
        public override IWiringBehavior WiringBehavior => new VirtualInstrumentWiringBehavior();

        /// <inheritdoc />
        protected override void CreateBatchRules(ICollection<ModelBatchRule> rules)
        {
            rules.Add(new CoreBatchRule());
            // rules.Add(new UIModelContextBatchRule());
            rules.Add(new VerticalGrowNodeBoundsRule());
            // rules.Add(new GroupRule());
            rules.Add(new DockedConstantBatchRule());
            rules.Add(new WiringBatchRule());
            rules.Add(new WireCommentBatchRule());
            rules.Add(new SequenceStructureBatchRule());
        }

        /// <inheritdoc />
        public override void AcceptVisitor(IElementVisitor visitor)
        {
            var rustyWiresVisitor = visitor as IRustyWiresFunctionVisitor;
            if (rustyWiresVisitor != null)
            {
                rustyWiresVisitor.VisitRustyWiresFunction(this);
            }
            else
            {
                base.AcceptVisitor(visitor);
            }
        }

        // TODO
        public override IEnumerable<IDiagramParameter> Parameters => Components.OfType<IDiagramParameter>();

        internal void AddInputParameter()
        {
            var dataItem = DataItem.Create(ElementCreateInfo.ForNew);
            dataItem.CallDirection = ParameterCallDirection.Input;
            dataItem.DataType = PFTypes.String.CreateMutableValue();
            dataItem.Name = "input";
            AddComponent(dataItem);

            InputParameterAccessor inputAccessor = RootDiagram.Components.OfType<InputParameterAccessor>().FirstOrDefault();
            if (inputAccessor == null)
            {
                inputAccessor = new InputParameterAccessor(); // TODO: should be Create(ForNew)
                RootDiagram.AddChild(inputAccessor);
            }
            inputAccessor.UpdateTerminals();

            ConnectorPane.AddParameter(dataItem);
        }
    }
}
