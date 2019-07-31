using System.Xml.Linq;
using NationalInstruments.Core;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Persistence;
using NationalInstruments.VI.SourceModel;

namespace Rebar.SourceModel
{
    public class OptionPatternStructure : StackedStructure
    {
        private const string ElementName = "OptionPatternStructure";

        [XmlParserFactoryMethod(ElementName, Function.ParsableNamespaceName)]
        public static OptionPatternStructure CreateOptionPatternStructure(IElementCreateInfo elementCreateInfo)
        {
            var optionPatternStructure = new OptionPatternStructure();
            optionPatternStructure.Init(elementCreateInfo);
            return optionPatternStructure;
        }

        public override XName XmlElementName => XName.Get(ElementName, Function.ParsableNamespaceName);

        public override IBorderNodeGuide GetGuide(BorderNode borderNode)
        {
            var max = GetMaxXYForBorderNode(this, borderNode);
            RectangleSides sides = borderNode is CaseSelector ? RectangleSides.Left : RectangleSides.All;
            var height = max.Y + borderNode.Height + OuterBorderThickness.Bottom;
            var width = max.X + borderNode.Width + OuterBorderThickness.Right;
            RectangleBorderNodeGuide guide = new RectangleBorderNodeGuide(new SMRect(0, 0, width, height), sides, BorderNodeDocking.None, OuterBorderThickness, GetAvoidRects(borderNode));
            guide.EdgeOverflow = StockDiagramGeometries.StandardTunnelOffsetForStructures;
            return guide;
        }

        public override BorderNode MakeDefaultBorderNode(Diagram startDiagram, Diagram endDiagram, Wire wire, StructureIntersection intersection)
        {
            return MakeDefaultTunnelCore<OptionPatternStructureTunnel>(startDiagram, endDiagram, wire);
        }
    }
}
