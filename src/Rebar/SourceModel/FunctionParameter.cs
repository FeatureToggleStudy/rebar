using System.Xml.Linq;
using NationalInstruments.CommonModel;
using NationalInstruments.DataTypes;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;

namespace Rebar.SourceModel
{
    public class FunctionParameter : Content, IDiagramParameter
    {
        private readonly ParameterCallDirection _direction;
        private readonly NIType _dataType;
        private readonly string _name;

        private const string ElementName = "FunctionParameter";

        [XmlParserFactoryMethod(ElementName, Function.ParsableNamespaceName)]
        public static FunctionParameter CreateFunctionParameter(IElementCreateInfo elementCreateInfo)
        {
            var functionParameter = new FunctionParameter();
            functionParameter.Init(elementCreateInfo);
            return functionParameter;
        }

        /// <inheritdoc />
        public override XName XmlElementName => XName.Get(ElementName, Function.ParsableNamespaceName);

        private FunctionParameter()
            : this(ParameterCallDirection.Input, PFTypes.Void, string.Empty)
        {
        }

        public FunctionParameter(ParameterCallDirection direction, NIType type, string name)
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

        public Element Element => this;

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

        public ParameterCallDirection PreferredDirection => _direction;
    }
}
