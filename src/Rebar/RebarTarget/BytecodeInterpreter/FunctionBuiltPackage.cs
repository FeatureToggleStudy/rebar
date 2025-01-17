﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NationalInstruments.Compiler;
using NationalInstruments.Core;
using NationalInstruments.ExecutionFramework;

namespace Rebar.RebarTarget.BytecodeInterpreter
{
    [Serializable]
    public class FunctionBuiltPackage : IBuiltPackage, ISerializable
    {
        public FunctionBuiltPackage(
            SpecAndQName identity,
            QualifiedName targetIdentity,
            Function function)
        {
            RuntimeEntityIdentity = identity;
            TargetIdentity = targetIdentity;
            Function = function;
        }

        protected FunctionBuiltPackage(SerializationInfo info, StreamingContext context)
        {
            RuntimeEntityIdentity = (SpecAndQName)info.GetValue(nameof(RuntimeEntityIdentity), typeof(SpecAndQName));
            TargetIdentity = (QualifiedName)info.GetValue(nameof(TargetIdentity), typeof(QualifiedName));
            Token = (BuiltPackageToken)info.GetValue(nameof(Token), typeof(BuiltPackageToken));
            Function = (Function)info.GetValue(nameof(Function), typeof(Function));
        }

        public Function Function { get; }

        public bool IsPackageValid
        {
            get
            {
                return !RebarFeatureToggles.IsLLVMCompilerEnabled;
            }
        }

        public IRuntimeEntityIdentity RuntimeEntityIdentity { get; }

        public QualifiedName TargetIdentity { get; }

        public BuiltPackageToken Token { get; set; }

        public byte[] GetBinary()
        {
            // TODO: figure out how to serialize an LLVM module and return it here
            return new byte[] { 0xFF };
        }

        public IEnumerable<IRuntimeEntityIdentity> GetDependencies()
        {
            return Enumerable.Empty<IRuntimeEntityIdentity>(); // TODO
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(RuntimeEntityIdentity), RuntimeEntityIdentity);
            info.AddValue(nameof(TargetIdentity), TargetIdentity);
            info.AddValue(nameof(Token), Token);
            info.AddValue(nameof(Function), Function);
        }
    }
}
