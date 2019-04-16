﻿using NationalInstruments.Dfir;
using Rebar.Common;

namespace Rebar.Compiler
{
    internal static class VariableExtensions
    {
        private static readonly AttributeDescriptor _typeVariableSetTokenName = new AttributeDescriptor("Rebar.Compiler.TypeVariableSet", true);
        private static readonly AttributeDescriptor _lifetimeGraphTreeTokenName = new AttributeDescriptor("Rebar.Compiler.LifetimeGraphTree", true);
        private static readonly AttributeDescriptor _variableSetTokenName = new AttributeDescriptor("Rebar.Compiler.VariableSet", true);
        private static readonly AttributeDescriptor _lifetimeGraphIdentifierTokenName = new AttributeDescriptor("Rebar.Compiler.LifetimeGraphIdentifier", true);

        public static TypeVariableSet GetTypeVariableSet(this DfirRoot dfirRoot)
        {
            var token = dfirRoot.GetOrCreateNamedSparseAttributeToken<TypeVariableSet>(_typeVariableSetTokenName);
            return token.GetAttribute(dfirRoot);
        }

        public static LifetimeGraphTree GetLifetimeGraphTree(this DfirRoot dfirRoot)
        {
            var token = dfirRoot.GetOrCreateNamedSparseAttributeToken<LifetimeGraphTree>(_lifetimeGraphTreeTokenName);
            return token.GetAttribute(dfirRoot);
        }

        public static LifetimeGraphIdentifier GetLifetimeGraphIdentifier(this Diagram diagram)
        {
            var token = diagram.DfirRoot.GetOrCreateNamedSparseAttributeToken<LifetimeGraphIdentifier>(_lifetimeGraphIdentifierTokenName);
            return token.GetAttribute(diagram);
        }

        public static void SetLifetimeGraphIdentifier(this Diagram diagram, LifetimeGraphIdentifier identifier)
        {
            var token = diagram.DfirRoot.GetOrCreateNamedSparseAttributeToken<LifetimeGraphIdentifier>(_lifetimeGraphIdentifierTokenName);
            token.SetAttribute(diagram, identifier);
        }

        public static TypeVariableSet GetTypeVariableSet(this Node node)
        {
            return node.DfirRoot.GetTypeVariableSet();
        }

        public static TypeVariableSet GetTypeVariableSet(this Terminal terminal)
        {
            return terminal.DfirRoot.GetTypeVariableSet();
        }

        public static void SetVariableSet(this Diagram diagram, VariableSet variableSet)
        {
            var token = diagram.DfirRoot.GetOrCreateNamedSparseAttributeToken<VariableSet>(_variableSetTokenName);
            diagram.SetAttribute(token, variableSet);
        }

        public static VariableSet GetVariableSet(this Diagram diagram)
        {
            var token = diagram.DfirRoot.GetOrCreateNamedSparseAttributeToken<VariableSet>(_variableSetTokenName);
            return token.GetAttribute(diagram);
        }

        public static VariableSet GetVariableSet(this Terminal terminal)
        {
            return terminal.ParentDiagram.GetVariableSet();
        }

        /// <summary>
        /// Gets the true <see cref="VariableReference"/> associated with the <see cref="Terminal"/>, i.e., the reference
        /// to the variable that will be supplied directly to the terminal as input.
        /// </summary>
        /// <param name="terminal">The terminal.</param>
        /// <returns>The true <see cref="VariableReference"/>.</returns>
        public static VariableReference GetTrueVariable(this Terminal terminal)
        {
            TerminalFacade terminalFacade = AutoBorrowNodeFacade.GetNodeFacade(terminal.ParentNode)[terminal];
            return terminalFacade?.TrueVariable ?? new VariableReference();
        }

        /// <summary>
        /// Gets the facade <see cref="VariableReference"/> associated with the <see cref="Terminal"/>, i.e., the reference
        /// to the variable seen from the outside to be associated with the terminal. This may be different from the 
        /// terminal's true variable as a result of auto-borrowing.
        /// </summary>
        /// <param name="terminal">The terminal.</param>
        /// <returns>The true <see cref="VariableReference"/>.</returns>
        public static VariableReference GetFacadeVariable(this Terminal terminal)
        {
            TerminalFacade terminalFacade = AutoBorrowNodeFacade.GetNodeFacade(terminal.ParentNode)[terminal];
            return terminalFacade?.FacadeVariable ?? new VariableReference();
        }

        public static VariableUsageValidator GetValidator(this Terminal terminal)
        {
            return new VariableUsageValidator(terminal);
        }

        public static Lifetime DefineLifetimeThatIsBoundedByDiagram(this Terminal terminal, params VariableReference[] decomposedVariables)
        {
            return terminal.GetVariableSet().LifetimeGraphTree.CreateLifetimeThatIsBoundedByLifetimeGraph(terminal.ParentDiagram.GetLifetimeGraphIdentifier());
        }

        public static bool DoesOutlastDiagram(this Lifetime lifetime, Diagram diagram)
        {
            return lifetime.DoesOutlastLifetimeGraph(diagram.GetLifetimeGraphIdentifier());
        }
    }
}
