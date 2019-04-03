using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NationalInstruments;
using NationalInstruments.DataTypes;
using Rebar.Common;

namespace Tests.Rebar.Unit
{
    [TestClass]
    public class TypeInferenceUnitTests
    {
        [TestMethod]
        public void LiteralTypeAndTypeVariable_Unify_BothBecomeLiteralType()
        {
            TypeVariableSet typeVariableSet = new TypeVariableSet();
            TypeVariableReference literalReference = typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32);
            TypeVariableReference typeVariable = typeVariableSet.CreateReferenceToNewTypeVariable();

            typeVariableSet.Unify(typeVariable, literalReference);

            Assert.IsTrue(literalReference.RenderNIType().IsInt32());
            Assert.IsTrue(typeVariable.RenderNIType().IsInt32());
        }

        [TestMethod]
        public void TwoTypeVariables_Unify_BothBecomeSingleTypeVariable()
        {
            TypeVariableSet typeVariableSet = new TypeVariableSet();
            TypeVariableReference literalReference = typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32);
            TypeVariableReference typeVariable1 = typeVariableSet.CreateReferenceToNewTypeVariable(),
                typeVariable2 = typeVariableSet.CreateReferenceToNewTypeVariable();

            typeVariableSet.Unify(typeVariable2, typeVariable1);
            typeVariableSet.Unify(typeVariable1, literalReference);

            Assert.IsTrue(typeVariable1.RenderNIType().IsInt32());
            Assert.IsTrue(typeVariable2.RenderNIType().IsInt32());
        }

        #region Constructor Types

        [TestMethod]
        public void TwoConstructorTypesWithSameConstructorName_Unify_InnerTypesAreUnified()
        {
            TypeVariableSet typeVariableSet = new TypeVariableSet();
            TypeVariableReference innerTypeVariable = typeVariableSet.CreateReferenceToNewTypeVariable();
            TypeVariableReference constructorType1 = typeVariableSet.CreateReferenceToConstructorType("Vector",
                innerTypeVariable);
            TypeVariableReference constructorType2 = typeVariableSet.CreateReferenceToConstructorType("Vector",
                typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32));

            typeVariableSet.Unify(constructorType1, constructorType2);

            Assert.IsTrue(innerTypeVariable.RenderNIType().IsInt32());
        }

        #endregion

        #region Possible Borrow Types

        [TestMethod]
        public void PossibleImmutableBorrowTypeAndLiteralType_Unify_UnifiesTrueVariableTypeWithImmutableReferenceType()
        {
            TypeVariableSet typeVariableSet = new TypeVariableSet();
            TypeVariableReference inputTypeReference = typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32);
            VariableSet variableSet = new VariableSet(typeVariableSet);
            PossibleBorrowSetup setup = SetupPossibleBorrow(typeVariableSet, variableSet, false);

            typeVariableSet.Unify(setup.PossibleBorrow, inputTypeReference);

            Assert.IsTrue(setup.UnderlyingTypeVariable.RenderNIType().IsInt32());
            // TODO: need to check that lifetime variable was unified with borrowLifetime            
        }

        [TestMethod]
        public void PossibleImmutableBorrowTypeAndImmutableReferenceType_Unify_UnifiesTrueVariableTypeWithImmutableReferenceType()
        {
            TypeVariableSet typeVariableSet = new TypeVariableSet();
            VariableSet variableSet = new VariableSet(typeVariableSet);
            Lifetime referenceLifetime = variableSet.DefineLifetimeThatIsBoundedByDiagram(Enumerable.Empty<VariableReference>());
            TypeVariableReference inputTypeReference = typeVariableSet.CreateReferenceToReferenceType(
                false,
                typeVariableSet.CreateReferenceToLiteralType(PFTypes.Int32),
                typeVariableSet.CreateReferenceToLifetimeType(referenceLifetime));
            PossibleBorrowSetup setup = SetupPossibleBorrow(typeVariableSet, variableSet, false);

            typeVariableSet.Unify(setup.PossibleBorrow, inputTypeReference);

            Assert.IsTrue(setup.UnderlyingTypeVariable.RenderNIType().IsInt32());
        }

        private class PossibleBorrowSetup
        {
            public TypeVariableReference UnderlyingTypeVariable { get; set; }

            public TypeVariableReference LifetimeTypeVariable { get; set; }

            public TypeVariableReference ReferenceType { get; set; }

            public TypeVariableReference PossibleBorrow { get; set; }

            public Lifetime BorrowLifetime { get; set; }
        }

        private PossibleBorrowSetup SetupPossibleBorrow(TypeVariableSet typeVariableSet, VariableSet variableSet, bool mutableBorrow)
        {
            PossibleBorrowSetup possibleBorrowSetup = new PossibleBorrowSetup();
            possibleBorrowSetup.UnderlyingTypeVariable = typeVariableSet.CreateReferenceToNewTypeVariable();
            possibleBorrowSetup.LifetimeTypeVariable = typeVariableSet.CreateReferenceToNewTypeVariable();
            possibleBorrowSetup.ReferenceType = typeVariableSet.CreateReferenceToReferenceType(
                false, possibleBorrowSetup.UnderlyingTypeVariable, possibleBorrowSetup.LifetimeTypeVariable);
            VariableReference facadeVariable = variableSet.CreateNewVariable(),
                trueVariable = variableSet.CreateNewVariable();
            trueVariable.AdoptTypeVariableReference(possibleBorrowSetup.ReferenceType);
            possibleBorrowSetup.BorrowLifetime = variableSet.DefineLifetimeThatIsBoundedByDiagram(facadeVariable.ToEnumerable());
            possibleBorrowSetup.PossibleBorrow = typeVariableSet.CreateReferenceToPossibleBorrowType(
                false, facadeVariable, trueVariable, new Lazy<Lifetime>(() => possibleBorrowSetup.BorrowLifetime));
            return possibleBorrowSetup;
        }

        #endregion
    }
}
