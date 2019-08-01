﻿using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NationalInstruments;
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

        /// <inheritdoc />
        protected override void Init(IElementCreateInfo info)
        {
            base.Init(info);
            if (info.ForParse)
            {
                // info.FixupRegistrar.AddPostParseFixupOrder(nameof(FixupCaseStructure), CaseDiagramPatternSerializer.CaseDiagramPatternSerializerPostParseFixupName);
                // info.FixupRegistrar.AddPostParseFixupOrder(DataTypeSerializer.DataTypeSerializerPostParseFixupName, CaseStructurePostParseFixupName);
                info.FixupRegistrar.RegisterPostParseFixup(this, FixupOptionPatternStructure, nameof(FixupOptionPatternStructure));
            }
        }

        /// <summary>
        /// This method is a post-parser fixup method for configuring <see cref="OptionPatternStructure"/>s on this structure
        /// after the rest of the structure and its subcomponents is parsed.
        /// </summary>
        /// <remarks>This was copied from CaseStructure.cs in VI. This exists because the serializer for 
        /// StackedStructureTunnel terminals does not record things like the Role of each terminal, since they
        /// can be recovered from which diagram the terminal is associated with. This is the method that
        /// does that reassociation; it has to run after the tunnel and all associated Diagrams have been parsed.</remarks>
        private static void FixupOptionPatternStructure(Element element, IElementServices services)
        {
            var optionPatternStructure = (OptionPatternStructure)element;
            // EnsureView will make sure all nested diagrams are sized consistently.  This shouldn't technically be necessary if everything was
            // persisted correctly.  However, there were some previous bugs where case diagram bounds weren't persisted correctly.  Also, we may
            // eventually decide not to persist bounds for nested diagrams and just calculate them from the owning structure.  For that to work,
            // we'll always need to call EnsureView.
            optionPatternStructure.EnsureView(EnsureViewHints.Bounds);

#if FALSE
            CaseSelector selector = optionPatternStructure.Components.OfType<CaseSelector>().FirstOrDefault();
            if (selector != null)
            {
                var selectorOuterTerm = selector.BorderNodeTerminals.First();
                selectorOuterTerm.Direction = Direction.Input;

                if (selectorOuterTerm.DataType.IsUnset() || selectorOuterTerm.DataType.IsVoid())
                {
                    if (optionPatternStructure.LoadSelectorType.IsUnset() || optionPatternStructure.LoadSelectorType.IsVoid())
                    {
                        selectorOuterTerm.DataType = PFTypes.Boolean;
                    }
                    else
                    {
                        selectorOuterTerm.DataType = optionPatternStructure.LoadSelectorType;
                    }
                }

                selectorOuterTerm.Hotspot = TerminalHotspots.Input1;
                optionPatternStructure.Selector = selector;
            }
#endif

            List<OptionPatternStructureTunnel> tunnels = optionPatternStructure.BorderNodes.OfType<OptionPatternStructureTunnel>().ToList();
            foreach (var tunnel in tunnels)
            {
                if (tunnel.Terminals.Count() == optionPatternStructure.NestedDiagrams.Count() + 1)
                {
                    var outerTerm = tunnel.BorderNodeTerminals.First();
                    outerTerm.Primary = true;
                    outerTerm.Role = BorderNodeTerminalRole.Outer;
                    if (tunnel.TerminalIdentifiersMatchAliasParent)
                    {
                        optionPatternStructure.AddTerminalAlias(outerTerm);
                    }

                    foreach (var terminalDiagramPair in tunnel.BorderNodeTerminals.Skip(1).Zip(optionPatternStructure.NestedDiagrams))
                    {
                        BorderNodeTerminal innerTerminal = terminalDiagramPair.Key;
                        innerTerminal.Primary = true;
                        innerTerminal.Role = BorderNodeTerminalRole.Inner;
                        NestedDiagram diagram = terminalDiagramPair.Value;
                        diagram.AddTerminalAlias(innerTerminal);
                    }
                }
                else
                {
                    throw new InvalidParseException("Attempting to setup tunnels on OptionPatternStructure ID=".NotLocalized() + optionPatternStructure.Identifier.ToParsableString() +
                                                    " at post-parse fixup, OptionPatternStructure has malformed tunnel without enough terminals.".NotLocalized());
                }

                // This correctly places the hotspots of the tunnels so that wires that are about to be connected to them
                // during parsing will have the accurate positions and not get diagonal wires
                tunnel.EnsureView(EnsureViewHints.Bounds);
            }
        }

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
