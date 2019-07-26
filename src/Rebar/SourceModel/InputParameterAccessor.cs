using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NationalInstruments.Core;
using NationalInstruments.MocCommon.SourceModel;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;
using Rebar.Compiler;

namespace Rebar.SourceModel
{
    public class InputParameterAccessor : SimpleNode
    {
        private const string ElementName = "InputParameterAccessor";

        [XmlParserFactoryMethod(ElementName, Function.ParsableNamespaceName)]
        public static InputParameterAccessor CreateInputParameterAccessor(IElementCreateInfo elementCreateInfo)
        {
            var inputParameterAccessor = new InputParameterAccessor();
            inputParameterAccessor.Init(elementCreateInfo);
            return inputParameterAccessor;
        }

        /// <inheritdoc />
        public override XName XmlElementName => XName.Get(ElementName, Function.ParsableNamespaceName);

        public override bool CanDelete => false;

        protected override void SetIconViewGeometry()
        {
            int outputs = 0;
            foreach (NodeTerminal terminal in FixedTerminals)
            {
                ++outputs;
                terminal.Hotspot = new SMPoint(Width, StockDiagramGeometries.GridSize * (2 * outputs - 1));
            }
            Height = StockDiagramGeometries.GridSize * 2 * Math.Max(2, outputs);
        }

        private readonly Dictionary<DataItem, Terminal> _parameterTerminals = new Dictionary<DataItem, Terminal>();

        internal void UpdateTerminals()
        {
            var function = (Function)Definition;
            var inputParameters = function
                .Parameters
                .OfType<DataItem>()
                .Where(parameter => parameter.CallDirection == NationalInstruments.CommonModel.ParameterCallDirection.Input).ToList();
            int index = 0;
            foreach (var parameter in inputParameters)
            {
                if (!_parameterTerminals.ContainsKey(parameter))
                {
                    var terminal = new NodeTerminal(Direction.Output, parameter.DataType, parameter.Name);
                    _parameterTerminals[parameter] = terminal;
                    FixedTerminals.Insert(index, terminal);
                }
                ++index;
            }
            SetIconViewGeometry();
        }

        public override void AcceptVisitor(IElementVisitor visitor)
        {
            var functionVisitor = visitor as IFunctionVisitor;
            if (functionVisitor != null)
            {
                functionVisitor.VisitInputParameterAccessor(this);
            }
            else
            {
                base.AcceptVisitor(visitor);
            }
        }
    }
}
