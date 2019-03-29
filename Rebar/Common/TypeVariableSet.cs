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
        }

        private sealed class TypeVariable : TypeBase
        {
            public TypeVariable(int id)
            {
                Id = id;
            }

            public int Id { get; }

            public override string DebuggerDisplay => $"T${Id}";
        }

        private sealed class LiteralType : TypeBase
        {
            public LiteralType(NIType type)
            {
                Type = type;
            }

            public NIType Type { get; }

            public override string DebuggerDisplay => Type.AsFormattedStringSingleLine;
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
        }

        private sealed class PossibleBorrowType : TypeBase
        {
            public PossibleBorrowType(bool mutable, VariableReference borrowFrom, VariableReference borrowInto)
            {
                Mutable = mutable;
                BorrowFrom = borrowFrom;
                BorrowInto = borrowInto;
            }

            public bool Mutable { get; }

            public VariableReference BorrowFrom { get; }

            public VariableReference BorrowInto { get; }

            public override string DebuggerDisplay
            {
                get
                {
                    string mutable = Mutable ? "mut" : "imm";
                    return $"PossibleBorrow {mutable} {BorrowFrom.Id} -> {BorrowInto.Id}";
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

        public TypeVariableReference CreateReferenceToPossibleBorrowType(bool mutable, VariableReference borrowFrom, VariableReference borrowInto)
        {
            return CreateReferenceToNewType(new PossibleBorrowType(mutable, borrowFrom, borrowInto));
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
            ConstructorType otherConstructorType = otherTypeBase as ConstructorType;
            if (possibleBorrowType.Mutable)
            {
                if (otherConstructorType != null)
                {
                    if (otherConstructorType.ConstructorName == "MutRef")
                    {
                        MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                        possibleBorrowType.BorrowInto.MergeInto(possibleBorrowType.BorrowFrom);
                        return;
                    }
                    if (otherConstructorType.ConstructorName == "ImmRef")
                    {
                        // type error
                        return;
                    }
                }
                MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                TypeVariableReference mutRef = CreateReferenceToConstructorType("MutRef", other);
                Unify(possibleBorrowType.BorrowInto.TypeVariableReference, mutRef);
                // TODO: after unifying these two, might be good to remove mutRef--I guess by merging?
                // somehow tell facade associated with possibleBorrowType that a borrow is required
            }
            else
            {
                TypeVariableReference immRef;
                if (otherConstructorType != null)
                {
                    if (otherConstructorType.ConstructorName == "ImmRef")
                    {
                        MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                        possibleBorrowType.BorrowInto.MergeInto(possibleBorrowType.BorrowFrom);
                        return;
                    }
                    if (otherConstructorType.ConstructorName == "MutRef")
                    {
                        MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                        immRef = CreateReferenceToConstructorType("ImmRef", otherConstructorType.Argument);
                        Unify(possibleBorrowType.BorrowInto.TypeVariableReference, immRef);
                        // TODO: after unifying these two, might be good to remove immRef--I guess by merging?
                        // Or should unifying two Constructor types merge them after unifying their Arguments?
                        // somehow tell facade associated with possibleBorrowType that a borrow is required
                        return;
                    }
                }
                MergeTypeVariableIntoTypeVariable(possibleBorrow, other);

                immRef = CreateReferenceToConstructorType("ImmRef", other);
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
    }
}
