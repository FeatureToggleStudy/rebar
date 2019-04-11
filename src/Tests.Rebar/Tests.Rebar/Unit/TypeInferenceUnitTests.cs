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

            typeVariableSet.Unify(typeVariable, literalReference, new TestTypeUnificationResult());

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

            typeVariableSet.Unify(typeVariable2, typeVariable1, new TestTypeUnificationResult());
            typeVariableSet.Unify(typeVariable1, literalReference, new TestTypeUnificationResult());

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

            typeVariableSet.Unify(constructorType1, constructorType2, new TestTypeUnificationResult());

            Assert.IsTrue(innerTypeVariable.RenderNIType().IsInt32());
        }

        #endregion
    }

    internal class TestTypeUnificationResult : ITypeUnificationResult
    {
        public void SetExpectedMutable()
        {
        }

        public void SetTypeMismatch()
        {
            TypeMismatch = true;
        }

        public bool TypeMismatch { get; private set; }
    }
}
