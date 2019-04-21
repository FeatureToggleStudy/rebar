﻿using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using NationalInstruments.DataTypes;

namespace Rebar.Common
{
    internal sealed class TypeVariableSet
    {
        #region Type variable kinds

        [DebuggerDisplay("{DebuggerDisplay}")]
        private abstract class TypeBase
        {
            public abstract string DebuggerDisplay { get; }

            public abstract NIType RenderNIType();

            public abstract Lifetime Lifetime { get; }
        }

        private sealed class TypeVariable : TypeBase
        {
            private CopyConstraint[] _constraints;

            public TypeVariable(int id, IEnumerable<CopyConstraint> constraints)
            {
                Id = id;
                _constraints = constraints.ToArray();
            }

            public int Id { get; }

            public IEnumerable<CopyConstraint> Constraints => _constraints;

            public void AdoptConstraintsFromVariable(TypeVariable other)
            {
                _constraints = _constraints.Concat(other._constraints).ToArray();
            }

            public override string DebuggerDisplay => $"T${Id}";

            public override NIType RenderNIType()
            {
                return PFTypes.Void;
            }

            public override Lifetime Lifetime => Lifetime.Empty;
        }

        private sealed class LiteralType : TypeBase
        {
            public LiteralType(NIType type)
            {
                Type = type;
            }

            public NIType Type { get; }

            public override string DebuggerDisplay => Type.AsFormattedStringSingleLine;

            public override NIType RenderNIType()
            {
                return Type;
            }

            public override Lifetime Lifetime => Lifetime.Unbounded;
        }

        private sealed class ConstructorType : TypeBase
        {
            public ConstructorType(string constructorName, TypeVariableReference argument)
            {
                ConstructorName = constructorName;
                Argument = argument;
            }

            public string ConstructorName { get; }

            public TypeVariableReference Argument { get; }

            public override string DebuggerDisplay => $"{ConstructorName} ({Argument.DebuggerDisplay})";

            public override NIType RenderNIType()
            {
                NIType argumentNIType = Argument.RenderNIType();
                switch (ConstructorName)
                {
                    case "Vector":
                        return argumentNIType.CreateVector();
                    case "Iterator":
                        return argumentNIType.CreateIterator();
                    case "LockingCell":
                        return argumentNIType.CreateLockingCell();
                    case "NonLockingCell":
                        return argumentNIType.CreateNonLockingCell();
                    case "Option":
                        return argumentNIType.CreateOption();
                    default:
                        throw new NotSupportedException();
                }
            }

            public override Lifetime Lifetime => Argument.Lifetime;
        }

        private sealed class ReferenceType : TypeBase
        {
            private abstract class Mutability
            {
                public abstract bool Mutable { get; }
                public abstract void UnifyMutability(Mutability unifyWith);
            }

            private sealed class ConstantMutability : Mutability
            {
                public ConstantMutability(bool mutable)
                {
                    Mutable = mutable;
                }

                public override bool Mutable { get; }

                public override void UnifyMutability(Mutability unifyWith)
                {
                    var unifyWithConstant = unifyWith as ConstantMutability;
                    if (unifyWithConstant != null)
                    {
                        if (Mutable != unifyWithConstant.Mutable)
                        {
                            // type error
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            private sealed class VariableMutability : Mutability
            {
                private readonly TypeVariableReference _mutabilityVariable;

                public VariableMutability(TypeVariableReference mutabilityVariable)
                {
                    _mutabilityVariable = mutabilityVariable;
                }

                public override void UnifyMutability(Mutability unifyWith)
                {
                    ConstantMutability constant = unifyWith as ConstantMutability;
                    if (constant != null)
                    {
                        _mutabilityVariable.AndWith(constant.Mutable);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                public override bool Mutable
                {
                    get
                    {
                        return _mutabilityVariable.Mutable;
                    }
                }
            }

            private readonly Mutability _mutability;

            public ReferenceType(bool mutable, TypeVariableReference underlyingType, TypeVariableReference lifetimeType)
            {
                _mutability = new ConstantMutability(mutable);
                UnderlyingType = underlyingType;
                LifetimeType = lifetimeType;
            }

            public ReferenceType(TypeVariableReference mutability, TypeVariableReference underlyingType, TypeVariableReference lifetimeType)
            {
                _mutability = new VariableMutability(mutability);
                UnderlyingType = underlyingType;
                LifetimeType = lifetimeType;
            }

            public bool Mutable => _mutability.Mutable;

            public TypeVariableReference UnderlyingType { get; }

            public TypeVariableReference LifetimeType { get; }

            public void UnifyMutability(ReferenceType unifyWith)
            {
                _mutability.UnifyMutability(unifyWith._mutability);
            }

            public override string DebuggerDisplay
            {
                get
                {
                    string mut = _mutability.Mutable ? "mut " : string.Empty;
                    return $"& ({LifetimeType.DebuggerDisplay}) {mut}{UnderlyingType.DebuggerDisplay}";
                }
            }

            public override NIType RenderNIType()
            {
                NIType underlyingNIType = UnderlyingType.RenderNIType();
                return _mutability.Mutable ? underlyingNIType.CreateMutableReference() : underlyingNIType.CreateImmutableReference();
            }

            public override Lifetime Lifetime => LifetimeType.Lifetime;
        }

        private sealed class LifetimeTypeContainer : TypeBase
        {
            private readonly Lazy<Lifetime> _lazyNewLifetime;

            public LifetimeTypeContainer(Lazy<Lifetime> lazyNewLifetime)
            {
                _lazyNewLifetime = lazyNewLifetime;
            }

            public Lifetime LifetimeValue { get; private set; }

            public override Lifetime Lifetime => LifetimeValue;

            public void AdoptLifetimeIfPossible(Lifetime lifetime)
            {
                if (LifetimeValue == null)
                {
                    LifetimeValue = lifetime;
                }
                else if (LifetimeValue != lifetime)
                {
                    AdoptNewLifetime();
                }
                // TODO: instead of using a canned supertype lifetime, it would be good to construct new supertype
                // lifetimes from whatever we get unified with on the fly
            }

            public void AdoptNewLifetime()
            {
                LifetimeValue = _lazyNewLifetime.Value;
            }

            public override string DebuggerDisplay
            {
                get
                {
                    // TODO
                    return "Lifetime";
                }
            }

            public override NIType RenderNIType()
            {
                throw new NotImplementedException();
            }
        }

        private sealed class MutabilityTypeVariable : TypeBase
        {
            private bool? _value;

            public void AndWith(bool value)
            {
                if (!_value.HasValue)
                {
                    _value = value;
                }
                else
                {
                    _value = (_value.Value && value);
                }
            }

            public bool Mutable
            {
                get
                {
                    if (_value.HasValue)
                    {
                        return _value.Value;
                    }
                    else
                    {
                        throw new InvalidOperationException("Mutability has not been determined");
                    }
                }
            }

            public override string DebuggerDisplay
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override Lifetime Lifetime
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override NIType RenderNIType()
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        private List<TypeBase> _types = new List<TypeBase>();
        private List<TypeBase> _typeReferences = new List<TypeBase>();
        private int _currentReferenceIndex = 0;
        private int _currentTypeVariable = 0;

        public TypeVariableReference CreateReferenceToLiteralType(NIType type)
        {
            return CreateReferenceToNewType(new LiteralType(type));
        }

        public TypeVariableReference CreateReferenceToNewTypeVariable()
        {
            return CreateReferenceToNewTypeVariable(Enumerable.Empty<CopyConstraint>());
        }

        public TypeVariableReference CreateReferenceToNewTypeVariable(IEnumerable<CopyConstraint> constraints)
        {
            int id = _currentTypeVariable++;
            return CreateReferenceToNewType(new TypeVariable(id, constraints));
        }

        public TypeVariableReference CreateReferenceToConstructorType(string constructorName, TypeVariableReference argument)
        {
            return CreateReferenceToNewType(new ConstructorType(constructorName, argument));
        }

        public TypeVariableReference CreateReferenceToReferenceType(bool mutable, TypeVariableReference underlyingType, TypeVariableReference lifetimeType)
        {
            return CreateReferenceToNewType(new ReferenceType(mutable, underlyingType, lifetimeType));
        }

        public TypeVariableReference CreateReferenceToPolymorphicReferenceType(TypeVariableReference mutabilityType, TypeVariableReference underlyingType, TypeVariableReference lifetimeType)
        {
            return CreateReferenceToNewType(new ReferenceType(mutabilityType, underlyingType, lifetimeType));
        }

        public TypeVariableReference CreateReferenceToLifetimeType(Lazy<Lifetime> lazyNewLifetime)
        {
            return CreateReferenceToNewType(new LifetimeTypeContainer(lazyNewLifetime));
        }

        public TypeVariableReference CreateReferenceToLifetimeType(Lifetime lifetime)
        {
            var lifetimeTypeContainer = new LifetimeTypeContainer(null);
            lifetimeTypeContainer.AdoptLifetimeIfPossible(lifetime);
            return CreateReferenceToNewType(lifetimeTypeContainer);
        }

        public TypeVariableReference CreateReferenceToMutabilityType()
        {
            return CreateReferenceToNewType(new MutabilityTypeVariable());
        }

        private TypeVariableReference CreateReferenceToNewType(TypeBase type)
        {
            int referenceIndex = _currentReferenceIndex++;
            _types.Add(type);
            SetTypeAtReferenceIndex(type, referenceIndex);
            return new TypeVariableReference(this, referenceIndex);
        }

        private void SetTypeAtReferenceIndex(TypeBase type, int referenceIndex)
        {
            while (_typeReferences.Count <= referenceIndex)
            {
                _typeReferences.Add(null);
            }
            _typeReferences[referenceIndex] = type;
        }

        private TypeBase GetTypeForTypeVariableReference(TypeVariableReference typeVariableReference)
        {
            return _typeReferences[typeVariableReference.ReferenceIndex];
        }

        private void MergeTypeVariableIntoTypeVariable(TypeVariableReference toMerge, TypeVariableReference mergeInto)
        {
            TypeBase typeToMerge = GetTypeForTypeVariableReference(toMerge),
                typeToMergeInto = GetTypeForTypeVariableReference(mergeInto);
            if (typeToMerge != null && typeToMergeInto != null)
            {
                for (int i = 0; i < _typeReferences.Count; ++i)
                {
                    if (_typeReferences[i] == typeToMerge)
                    {
                        _typeReferences[i] = typeToMergeInto;
                    }
                }
            }
            _types.Remove(typeToMerge);
        }

        public void Unify(TypeVariableReference toUnify, TypeVariableReference toUnifyWith, ITypeUnificationResult unificationResult)
        {
            TypeBase toUnifyTypeBase = GetTypeForTypeVariableReference(toUnify),
                toUnifyWithTypeBase = GetTypeForTypeVariableReference(toUnifyWith);

            LiteralType toUnifyLiteral = toUnifyTypeBase as LiteralType,
                toUnifyWithLiteral = toUnifyWithTypeBase as LiteralType;
            if (toUnifyLiteral != null && toUnifyWithLiteral != null)
            {
                if (toUnifyLiteral.Type == toUnifyWithLiteral.Type)
                {
                    MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                    return;
                }
                unificationResult.SetTypeMismatch();
                return;
            }

            ConstructorType toUnifyConstructor = toUnifyTypeBase as ConstructorType,
                toUnifyWithConstructor = toUnifyWithTypeBase as ConstructorType;
            if (toUnifyConstructor != null && toUnifyWithConstructor != null)
            {
                if (toUnifyConstructor.ConstructorName == toUnifyWithConstructor.ConstructorName)
                {
                    Unify(toUnifyConstructor.Argument, toUnifyWithConstructor.Argument, unificationResult);
                    MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                    return;
                }
                unificationResult.SetTypeMismatch();
                return;
            }

            ReferenceType toUnifyReference = toUnifyTypeBase as ReferenceType,
                toUnifyWithReference = toUnifyWithTypeBase as ReferenceType;
            if (toUnifyReference != null && toUnifyWithReference != null)
            {
                toUnifyReference.UnifyMutability(toUnifyWithReference);
                Unify(toUnifyReference.UnderlyingType, toUnifyWithReference.UnderlyingType, unificationResult);
                Unify(toUnifyReference.LifetimeType, toUnifyWithReference.LifetimeType, unificationResult);
                return;
            }

            LifetimeTypeContainer toUnifyLifetime = toUnifyTypeBase as LifetimeTypeContainer,
                toUnifyWithLifetime = toUnifyWithTypeBase as LifetimeTypeContainer;
            if (toUnifyLifetime != null && toUnifyWithLifetime != null)
            {
                // toUnify is the possible supertype container here
                toUnifyLifetime.AdoptLifetimeIfPossible(toUnifyWithLifetime.LifetimeValue);
                return;
            }

            TypeVariable toUnifyTypeVariable = toUnifyTypeBase as TypeVariable,
                toUnifyWithTypeVariable = toUnifyWithTypeBase as TypeVariable;
            if (toUnifyTypeVariable != null && toUnifyWithTypeVariable != null)
            {
                toUnifyWithTypeVariable.AdoptConstraintsFromVariable(toUnifyTypeVariable);
                MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                return;
            }
            if (toUnifyTypeVariable != null)
            {
                UnifyTypeVariableWithNonTypeVariable(toUnify, toUnifyWith, unificationResult);
                return;
            }
            if (toUnifyWithTypeVariable != null)
            {
                UnifyTypeVariableWithNonTypeVariable(toUnifyWith, toUnify, unificationResult);
                return;
            }

            unificationResult.SetTypeMismatch();
            return;
        }

        private void UnifyTypeVariableWithNonTypeVariable(TypeVariableReference typeVariable, TypeVariableReference nonTypeVariable, ITypeUnificationResult unificationResult)
        {
            var t = (TypeVariable)GetTypeForTypeVariableReference(typeVariable);
            foreach (CopyConstraint constraint in t.Constraints)
            {
                constraint.ValidateConstraintForType(nonTypeVariable, unificationResult);
            }
            MergeTypeVariableIntoTypeVariable(typeVariable, nonTypeVariable);
        }

        public string GetDebuggerDisplay(TypeVariableReference typeVariableReference)
        {
            TypeBase typeBase = GetTypeForTypeVariableReference(typeVariableReference);
            return typeBase?.DebuggerDisplay ?? "invalid";
        }

        public NIType RenderNIType(TypeVariableReference typeVariableReference)
        {
            TypeBase typeBase = GetTypeForTypeVariableReference(typeVariableReference);
            return typeBase?.RenderNIType() ?? PFTypes.Void;
        }

        public Lifetime GetLifetime(TypeVariableReference typeVariableReference)
        {
            TypeBase typeBase = GetTypeForTypeVariableReference(typeVariableReference);
            return typeBase?.Lifetime ?? Lifetime.Empty;
        }

        public bool TryDecomposeReferenceType(TypeVariableReference type, out TypeVariableReference underlyingType, out TypeVariableReference lifetimeType, out bool mutable)
        {
            TypeBase typeBase = GetTypeForTypeVariableReference(type);
            var referenceType = typeBase as ReferenceType;
            if (referenceType == null)
            {
                underlyingType = lifetimeType = default(TypeVariableReference);
                mutable = false;
                return false;
            }
            underlyingType = referenceType.UnderlyingType;
            lifetimeType = referenceType.LifetimeType;
            mutable = referenceType.Mutable;
            return true;
        }

        public bool TryDecomposeConstructorType(TypeVariableReference type, out string constructorName, out TypeVariableReference argument)
        {
            TypeBase typeBase = GetTypeForTypeVariableReference(type);
            var constructorType = typeBase as ConstructorType;
            if (constructorType == null)
            {
                constructorName = null;
                argument = default(TypeVariableReference);
                return false;
            }
            constructorName = constructorType.ConstructorName;
            argument = constructorType.Argument;
            return true;
        }

        public void AndWith(TypeVariableReference type, bool value)
        {
            var mutabilityType = GetTypeForTypeVariableReference(type) as MutabilityTypeVariable;
            if (mutabilityType == null)
            {
                throw new ArgumentException("Type should be a mutability type");
            }
            mutabilityType.AndWith(value);
        }

        public bool GetMutable(TypeVariableReference type)
        {
            var mutabilityType = GetTypeForTypeVariableReference(type) as MutabilityTypeVariable;
            if (mutabilityType == null)
            {
                throw new ArgumentException("Type should be a mutability type");
            }
            return mutabilityType.Mutable;
        }
    }

    [DebuggerDisplay("{DebuggerDisplay}")]
    internal struct TypeVariableReference
    {
        public TypeVariableReference(TypeVariableSet typeVariableSet, int referenceIndex)
        {
            TypeVariableSet = typeVariableSet;
            ReferenceIndex = referenceIndex;
        }

        public TypeVariableSet TypeVariableSet { get; }

        public int ReferenceIndex { get; }

        public string DebuggerDisplay => TypeVariableSet.GetDebuggerDisplay(this);

        public NIType RenderNIType() => TypeVariableSet.RenderNIType(this);

        public Lifetime Lifetime => TypeVariableSet.GetLifetime(this);

        // TODO: these two should hopefully be temporary
        public void AndWith(bool value) => TypeVariableSet.AndWith(this, value);

        public bool Mutable => TypeVariableSet.GetMutable(this);
    }

    internal class CopyConstraint
    {
        public void ValidateConstraintForType(TypeVariableReference type, ITypeUnificationResult unificationResult)
        {
            // TODO: probably not great to render an NIType at this stage
            if (!type.RenderNIType().WireTypeMayFork())
            {
                unificationResult.AddFailedTypeConstraint(this);
            }
        }
    }
}
