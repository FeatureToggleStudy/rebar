using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NationalInstruments.Dfir;

namespace Rebar.Common
{
    internal sealed class LifetimeGraphTree
    {
        [DebuggerDisplay("{DebuggerDisplay}")]
        private class BoundedLifetime : Lifetime
        {
            private readonly BoundedLifetimeGraph _graph;

            public BoundedLifetime(BoundedLifetimeGraph graph)
            {
                _graph = graph;
            }

            public override bool IsEmpty => false;

            public override bool DoesOutlastDiagram => _graph.DoesOutlast(this, _graph.DiagramLifetime);

            public override bool IsBounded => true;

            private string DebuggerDisplay => DoesOutlastDiagram
                ? "Lifetime : Diagram"
                : "Diagram : Lifetime";
        }

        private class BoundedLifetimeGraph
        {
            private readonly Dictionary<BoundedLifetime, HashSet<BoundedLifetime>> _lifetimeSupertypes = new Dictionary<BoundedLifetime, HashSet<BoundedLifetime>>();

            public BoundedLifetimeGraph()
            {
                DiagramLifetime = new BoundedLifetime(this);
            }

            public BoundedLifetime DiagramLifetime { get; }

            public void SetOutlastsRelationship(Lifetime outlaster, Lifetime outlasted)
            {
                BoundedLifetime boundedOutlaster = outlaster as BoundedLifetime, boundedOutlasted = outlasted as BoundedLifetime;
                if (boundedOutlaster == null || boundedOutlasted == null)
                {
                    return;
                }
                if (IsSubtypeLifetimeOf(boundedOutlasted, boundedOutlaster))
                {
                    throw new ArgumentException("outlasted already outlasts outlaster");
                }
                if (IsSubtypeLifetimeOf(boundedOutlaster, boundedOutlaster))
                {
                    return;
                }
                HashSet<BoundedLifetime> supertypes;
                if (!_lifetimeSupertypes.TryGetValue(boundedOutlaster, out supertypes))
                {
                    supertypes = new HashSet<BoundedLifetime>();
                    _lifetimeSupertypes[boundedOutlaster] = supertypes;
                }
                supertypes.Add(boundedOutlasted);
            }

            public Lifetime CreateLifetimeThatIsBoundedByDiagram()
            {
                BoundedLifetime lifetime = new BoundedLifetime(this);
                SetOutlastsRelationship(DiagramLifetime, lifetime);
                return lifetime;
            }

            public bool DoesOutlast(Lifetime toCheck, Lifetime comparison)
            {
                var boundedToCheck = toCheck as BoundedLifetime;
                var boundedComparison = comparison as BoundedLifetime;
                if (boundedToCheck == null && boundedComparison != null)
                {
                    return DoesUnboundedOutlastBounded(toCheck);
                }
                if (boundedToCheck != null && boundedComparison == null)
                {
                    return DoesUnboundedOutlastBounded(comparison);
                }
                if (boundedToCheck != null && boundedToCheck != null)
                {
                    return IsSubtypeLifetimeOf(boundedToCheck, boundedComparison);
                }
                return false;
            }

            private bool DoesUnboundedOutlastBounded(Lifetime unbounded)
            {
                return unbounded == Lifetime.Static || unbounded == Lifetime.Unbounded;
            }

            private bool IsSubtypeLifetimeOf(BoundedLifetime toCheck, BoundedLifetime comparison)
            {
                HashSet<BoundedLifetime> supertypes;
                if (!_lifetimeSupertypes.TryGetValue(toCheck, out supertypes))
                {
                    return false;
                }
                if (supertypes.Contains(comparison))
                {
                    return true;
                }
                return supertypes.Any(supertype => IsSubtypeLifetimeOf(supertype, comparison));
            }
        }

        private Dictionary<Diagram, BoundedLifetimeGraph> _diagramGraphs = new Dictionary<Diagram, BoundedLifetimeGraph>();

        public void EstablishLifetimeGraph(Diagram diagram)
        {
            _diagramGraphs[diagram] = new BoundedLifetimeGraph();
        }

        // TODO: don't use DFIR
        public Lifetime CreateLifetimeThatIsBoundedByDiagram(Diagram diagram)
        {
            return _diagramGraphs[diagram].CreateLifetimeThatIsBoundedByDiagram();
        }

        // TODO: to be used by function types
        public Lifetime CreateLifetimeThatOutlastsRootDiagram()
        {
            throw new NotImplementedException();
        }
    }
}
