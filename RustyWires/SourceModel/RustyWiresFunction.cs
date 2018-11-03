using System.Linq;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;
using System.Xml.Linq;
using NationalInstruments.MocCommon.SourceModel;
using System.Collections.Generic;
using NationalInstruments.VI.SourceModel;
using RustyWires.Compiler;
using NationalInstruments.Design;
using NationalInstruments.Core;
using NationalInstruments.CommonModel;
using NationalInstruments.DataTypes;
using System;

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
        public override IEnumerable<IDiagramParameter> Parameters => Components.OfType<RustyWiresFunctionParameter>();

        internal void AddInputParameter()
        {
            var parameter = new RustyWiresFunctionParameter(ParameterCallDirection.Input, PFTypes.String.CreateMutableValue(), "input");
            AddComponent(parameter);

            InputParameterAccessor inputAccessor = RootDiagram.Components.OfType<InputParameterAccessor>().FirstOrDefault();
            if (inputAccessor == null)
            {
                inputAccessor = new InputParameterAccessor(); // TODO: should be Create(ForNew)
                RootDiagram.AddChild(inputAccessor);
            }
            inputAccessor.UpdateTerminals();
        }
    }

    public class RustyWiresFunctionParameter : Content, IDiagramParameter
    {
        private readonly ParameterCallDirection _direction;
        private readonly NIType _dataType;
        private readonly string _name;

        public RustyWiresFunctionParameter(ParameterCallDirection direction, NIType type, string name)
        {
            _direction = direction;
            _dataType = type;
            _name = name;
        }

        public ParameterCallDirection CallDirection
        {
            get
            {
                return _direction;
            }
            set
            {
            }
        }

        public ParameterCallUsage CallUsage
        {
            get
            {
                return ParameterCallUsage.Required;
            }
            set
            {
            }
        }

        public NIType DataType
        {
            get
            {
                return _dataType;
            }
            set
            {
            }
        }

        public Element Element
        {
            get
            {
                return this;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
            }
        }

        public ParameterCallDirection PreferredDirection
        {
            get
            {
                return _direction;
            }
        }
    }
}
