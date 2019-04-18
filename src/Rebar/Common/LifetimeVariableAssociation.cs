﻿using System.Collections.Generic;

namespace Rebar.Common
{
    internal sealed class LifetimeVariableAssociation
    {
        private class LifetimeVariableInfo
        {
            public List<VariableReference> InterruptedVariables { get; } = new List<VariableReference>();
        }

        private readonly Dictionary<Lifetime, LifetimeVariableInfo> _lifetimeVariableInfos = new Dictionary<Lifetime, LifetimeVariableInfo>();

        private LifetimeVariableInfo GetLifetimeVariableInfo(Lifetime lifetime)
        {
            LifetimeVariableInfo info;
            if (!_lifetimeVariableInfos.TryGetValue(lifetime, out info))
            {
                info = new LifetimeVariableInfo();
                _lifetimeVariableInfos[lifetime] = info;
            }
            return info;
        }

        public void AddVariableInterruptedByLifetime(VariableReference variableReference, Lifetime lifetime)
        {
            GetLifetimeVariableInfo(lifetime).InterruptedVariables.Add(variableReference);
        }

        public IEnumerable<VariableReference> GetVariablesInterruptedByLifetime(Lifetime lifetime)
        {
            return GetLifetimeVariableInfo(lifetime).InterruptedVariables;
        }
    }
}
