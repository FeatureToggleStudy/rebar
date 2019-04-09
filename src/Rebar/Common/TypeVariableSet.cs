﻿using System.Collections.Generic;
using System;
using System.Diagnostics;
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
            public TypeVariable(int id)
            {
                Id = id;
            }

            public int Id { get; }

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
                public abstract void UnifyMutability(Mutability unifyWith);
            }

            private sealed class ConstantMutability : Mutability
            {
                private readonly bool _mutable;

                public ConstantMutability(bool mutable)
                {
                    _mutable = mutable;
                }

                public override void UnifyMutability(Mutability unifyWith)
                {
                    var unifyWithConstant = unifyWith as ConstantMutability;
                    if (unifyWithConstant != null)
                    {
                        if (_mutable != unifyWithConstant._mutable)
                        {
                            // type error
                        }
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
                    throw new NotImplementedException();
                }
            }

            private readonly Mutability _mutability;

            public ReferenceType(bool mutable, TypeVariableReference underlyingType, TypeVariableReference lifetimeType)
            {
                Mutable = mutable;
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

            public bool Mutable { get; }

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
                    string mut = Mutable ? "mut " : string.Empty;
                    return $"& ({LifetimeType.DebuggerDisplay}) {mut}{UnderlyingType.DebuggerDisplay}";
                }
            }

            public override NIType RenderNIType()
            {
                NIType underlyingNIType = UnderlyingType.RenderNIType();
                return Mutable ? underlyingNIType.CreateMutableReference() : underlyingNIType.CreateImmutableReference();
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

        private sealed class PossibleBorrowType : TypeBase
        {
            private readonly Lazy<Lifetime> _lazyNewLifetime;

            public PossibleBorrowType(InputReferenceMutability mutability, VariableReference borrowFrom, VariableReference borrowInto, Lazy<Lifetime> lazyNewLifetime)
            {
                Mutability = mutability;
                BorrowFrom = borrowFrom;
                BorrowInto = borrowInto;
                _lazyNewLifetime = lazyNewLifetime;
            }

            public InputReferenceMutability Mutability { get; }

            public VariableReference BorrowFrom { get; }

            public VariableReference BorrowInto { get; }

            public Lifetime NewLifetime => _lazyNewLifetime.Value;

            public override string DebuggerDisplay
            {
                get
                {
                    string mutable;
                    switch (Mutability)
                    {
                        case InputReferenceMutability.AllowImmutable:
                            mutable = "imm";
                            break;
                        case InputReferenceMutability.RequireMutable:
                            mutable = "mut";
                            break;
                        default:
                            mutable = "poly";
                            break;
                    }
                    return $"PossibleBorrow {mutable} {BorrowFrom.Id} -> {BorrowInto.Id}";
                }
            }

            public override NIType RenderNIType()
            {
                throw new NotImplementedException();
            }

            public override Lifetime Lifetime
            {
                get
                {
                    throw new NotImplementedException();
                }
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
            int id = _currentTypeVariable++;
            return CreateReferenceToNewType(new TypeVariable(id));
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

        public TypeVariableReference CreateReferenceToPossibleBorrowType(InputReferenceMutability mutability, VariableReference borrowFrom, VariableReference borrowInto, Lazy<Lifetime> lazyNewLifetime)
        {
            return CreateReferenceToNewType(new PossibleBorrowType(mutability, borrowFrom, borrowInto, lazyNewLifetime));
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

        public void Unify(TypeVariableReference toUnify, TypeVariableReference toUnifyWith)
        {
            TypeBase toUnifyTypeBase = GetTypeForTypeVariableReference(toUnify),
                toUnifyWithTypeBase = GetTypeForTypeVariableReference(toUnifyWith);
            if (toUnifyTypeBase is PossibleBorrowType && !(toUnifyWithTypeBase is TypeVariable))
            {
                UnifyPossibleBorrowType(toUnify, toUnifyWith);
                return;
            }
            if (toUnifyWithTypeBase is PossibleBorrowType && !(toUnifyTypeBase is TypeVariable))
            {
                UnifyPossibleBorrowType(toUnifyWith, toUnify);
                return;
            }

            LiteralType toUnifyLiteral = toUnifyTypeBase as LiteralType,
                toUnifyWithLiteral = toUnifyWithTypeBase as LiteralType;
            if (toUnifyLiteral != null && toUnifyWithLiteral != null)
            {
                if (toUnifyLiteral.Type == toUnifyWithLiteral.Type)
                {
                    MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                    return;
                }
                // type error
                return;
            }

            ConstructorType toUnifyConstructor = toUnifyTypeBase as ConstructorType,
                toUnifyWithConstructor = toUnifyWithTypeBase as ConstructorType;
            if (toUnifyConstructor != null && toUnifyWithConstructor != null)
            {
                if (toUnifyConstructor.ConstructorName == toUnifyWithConstructor.ConstructorName)
                {
                    Unify(toUnifyConstructor.Argument, toUnifyWithConstructor.Argument);
                    MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                    return;
                }
                // type error
                return;
            }

            ReferenceType toUnifyReference = toUnifyTypeBase as ReferenceType,
                toUnifyWithReference = toUnifyWithTypeBase as ReferenceType;
            if (toUnifyReference != null && toUnifyWithReference != null)
            {
                toUnifyReference.UnifyMutability(toUnifyWithReference);
                Unify(toUnifyReference.UnderlyingType, toUnifyWithReference.UnderlyingType);
                Unify(toUnifyReference.LifetimeType, toUnifyWithReference.LifetimeType);
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
                MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                return;
            }
            if (toUnifyTypeVariable != null)
            {
                MergeTypeVariableIntoTypeVariable(toUnify, toUnifyWith);
                return;
            }
            if (toUnifyWithTypeVariable != null)
            {
                MergeTypeVariableIntoTypeVariable(toUnifyWith, toUnify);
                return;
            }

            // type error
            return;
        }

        private void UnifyPossibleBorrowType(TypeVariableReference possibleBorrow, TypeVariableReference other)
        {
            PossibleBorrowType possibleBorrowType = (PossibleBorrowType)GetTypeForTypeVariableReference(possibleBorrow);
            TypeBase otherTypeBase = GetTypeForTypeVariableReference(other);
            ReferenceType otherReferenceType = otherTypeBase as ReferenceType;
            switch (possibleBorrowType.Mutability)
            {
                case InputReferenceMutability.RequireMutable:
                {
                    MergeTypeVariableIntoTypeVariable(possibleBorrow, other);
                    TypeVariableReference underlyingType = otherReferenceType != null ? otherReferenceType.UnderlyingType : other;
                    TypeVariableReference lifetimeType = CreateReferenceToLifetimeType(otherReferenceType != null 
                        ? otherReferenceType.Lifetime
                        : possibleBorrowType.NewLifetime);
                    TypeVariableReference mutRef = CreateReferenceToReferenceType(true, underlyingType, lifetimeType);
                    Unify(possibleBorrowType.BorrowInto.TypeVariableReference, mutRef);
                    // TODO: after unifying these two, might be good to remove mutRef--I guess by merging?
                    // somehow tell facade associated with possibleBorrowType that a borrow is required
                    break;
                }
                case InputReferenceMutability.AllowImmutable:
                {
                    MergeTypeVariableIntoTypeVariable(possibleBorrow, other);
                    TypeVariableReference underlyingType = otherReferenceType != null ? otherReferenceType.UnderlyingType : other;
                    TypeVariableReference lifetimeType = CreateReferenceToLifetimeType(otherReferenceType != null && !otherReferenceType.Mutable
                        ? other.Lifetime
                        : possibleBorrowType.NewLifetime);
                    TypeVariableReference immRef = CreateReferenceToReferenceType(false, underlyingType, lifetimeType);
                    Unify(possibleBorrowType.BorrowInto.TypeVariableReference, immRef);
                    // TODO: after unifying these two, might be good to remove immRef--I guess by merging?
                    // somehow tell facade associated with possibleBorrowType that a borrow is required
                    break;
                }
                case InputReferenceMutability.Polymorphic:
                {
                    break;
                }
            }
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
    }
}
