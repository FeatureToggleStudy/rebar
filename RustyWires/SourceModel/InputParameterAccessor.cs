using System.Collections.Generic;
using System.Linq;
using NationalInstruments.MocCommon.SourceModel;
using NationalInstruments.SourceModel;
using RustyWires.Compiler;

namespace RustyWires.SourceModel
{
    public class InputParameterAccessor : RustyWiresSimpleNode
    {
        public override bool CanDelete => false;

        protected override ViewElementTemplate DefaultTemplate => ViewElementTemplate.List;

        protected override void SetIconViewGeometry()
        {
        }

        private readonly Dictionary<DataItem, Terminal> _parameterTerminals = new Dictionary<DataItem, Terminal>();

        internal void UpdateTerminals()
        {
            var function = (RustyWiresFunction)Definition;
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
        }

        public override void AcceptVisitor(IElementVisitor visitor)
        {
            var rustyWiresVisitor = visitor as IRustyWiresFunctionVisitor;
            if (rustyWiresVisitor != null)
            {
                rustyWiresVisitor.VisitInputParameterAccessor(this);
            }
            else
            {
                base.AcceptVisitor(visitor);
            }
        }
    }
}
