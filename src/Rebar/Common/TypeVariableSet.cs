using NationalInstruments.DataTypes;
using System.Collections.Generic;
using System;
using System.Diagnostics;

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
                    case "Vec":
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
            public ReferenceType(bool mutable, TypeVariableReference underlyingType, TypeVariableReference lifetimeType)
            {
                Mutable = mutable;
                UnderlyingType = underlyingType;
                LifetimeType = lifetimeType;
            }

            public bool Mutable { get; }

            public TypeVariableReference UnderlyingType { get; }

            public TypeVariableReference LifetimeType { get; }

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

        private sealed class PossibleBorrowType : TypeBase
        {
            private readonly Lazy<Lifetime> _lazyNewLifetime;

            public PossibleBorrowType(bool mutable, VariableReference borrowFrom, VariableReference borrowInto, Lazy<Lifetime> lazyNewLifetime)
            {
                Mutable = mutable;
                BorrowFrom = borrowFrom;
                BorrowInto = borrowInto;
                _lazyNewLifetime = lazyNewLifetime;
            }

            public bool Mutable { get; }

            public VariableReference BorrowFrom { get; }

            public VariableReference BorrowInto { get; }

            public Lifetime NewLifetime => _lazyNewLifetime.Value;

            public override string DebuggerDisplay
            {
                get
                {
                    string mutable = Mutable ? "mut" : "imm";
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

        public TypeVariableReference CreateReferenceToPossibleBorrowType(bool mutable, VariableReference borrowFrom, VariableReference borrowInto, Lazy<Lifetime> lazyNewLifetime)
        {
            return CreateReferenceToNewType(new PossibleBorrowType(mutable, borrowFrom, borrowInto, lazyNewLifetime));
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
                if (toUnifyReference.Mutable != toUnifyWithReference.Mutable)
                {
                    // type error
                    return;
                }
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
            if (possibleBorrowType.Mutable)
            {
                if (otherReferenceType != null)
                {
                    if (otherReferenceType.Mutable)
                    {
                        MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                        possibleBorrowType.BorrowInto.MergeInto(possibleBorrowType.BorrowFrom);
                        return;
                    }

                    // type error
                    return;
                }
                MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                TypeVariableReference mutRef = CreateReferenceToReferenceType(true, other, CreateReferenceToLifetimeType(possibleBorrowType.NewLifetime));
                Unify(possibleBorrowType.BorrowInto.TypeVariableReference, mutRef);
                // TODO: after unifying these two, might be good to remove mutRef--I guess by merging?
                // somehow tell facade associated with possibleBorrowType that a borrow is required
            }
            else
            {
                TypeVariableReference immRef;
                if (otherReferenceType != null)
                {
                    if (!otherReferenceType.Mutable)
                    {
                        MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                        Unify(possibleBorrowType.BorrowInto.TypeVariableReference, other);
                        possibleBorrowType.BorrowInto.MergeInto(possibleBorrowType.BorrowFrom);
                        return;
                    }
                    MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                    immRef = CreateReferenceToReferenceType(false, otherReferenceType.UnderlyingType, CreateReferenceToLifetimeType(possibleBorrowType.NewLifetime));
                    Unify(possibleBorrowType.BorrowInto.TypeVariableReference, immRef);
                    // TODO: after unifying these two, might be good to remove immRef--I guess by merging?
                    // Or should unifying two Constructor types merge them after unifying their Arguments?
                    // somehow tell facade associated with possibleBorrowType that a borrow is required
                    return;
                }
                MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                // each of these TODOs should be basically a constant lifetime type of the associated lazy new lifetime
                immRef = CreateReferenceToReferenceType(false, other, CreateReferenceToLifetimeType(possibleBorrowType.NewLifetime));
                Unify(possibleBorrowType.BorrowInto.TypeVariableReference, immRef);
                // TODO: after unifying these two, might be good to remove immRef--I guess by merging?
                // somehow tell facade associated with possibleBorrowType that a borrow is required
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
