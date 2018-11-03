﻿using System.Diagnostics;
using NationalInstruments.Dfir;

namespace RustyWires.Compiler
{
    [DebuggerDisplay("{Category} {Id} : {BaseLifetime}")]
    internal abstract class Lifetime
    {
        [DebuggerDisplay("{_name}")]
        private class SingletonLifetime : Lifetime
        {
            private readonly bool _isEmpty;
            private readonly string _name;

            public SingletonLifetime(string name, bool isEmpty)
            {
                _name = name;
                _isEmpty = isEmpty;
            }

            public override bool IsEmpty => _isEmpty;

            public override bool DoesOutlastDiagram => !_isEmpty;

            public override bool IsBounded => false;
        }

        public static readonly Lifetime Unbounded = new SingletonLifetime("Unbounded", false);

        public static readonly Lifetime Static = new SingletonLifetime("Static", false);

        public static readonly Lifetime Empty = new SingletonLifetime("Empty", true);

        protected Lifetime()
        {
        }

        public abstract bool IsEmpty { get; }

        public abstract bool IsBounded { get; }

        public abstract bool DoesOutlastDiagram { get; }
    }
}
