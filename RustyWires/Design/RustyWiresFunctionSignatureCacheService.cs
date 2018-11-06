using System;
using NationalInstruments.MocCommon.SourceModel;
using NationalInstruments.SourceModel;
using NationalInstruments.SourceModel.Envoys;
using RustyWires.SourceModel;

namespace RustyWires.Design
{
    /// <summary>
    /// Factory class for <see cref="RustyWiresFunctionSignatureCacheService"/>
    /// </summary>
    [ExportEnvoyServiceFactory(typeof(IMethodCallTarget))]
    [ProvidedInterface(typeof(IDependencyTargetExport))]
    [BindsToModelDefinitionType(RustyWiresFunction.RustyWiresFunctionDefinitionType)]
    [BindOnLoaded]
    public class RustyWiresFunctionSignatureCacheServiceFactory : EnvoyServiceFactory
    {
        /// <inheritdoc/>
        protected override EnvoyService CreateService()
        {
            return Host.CreateInstance<RustyWiresFunctionSignatureCacheService>();
        }
    }

    /// <summary>
    /// Envoy service that is attached to a RustyWires function source file reference.
    /// This service provides information about the function either through cache or definition.
    /// </summary>
    public sealed class RustyWiresFunctionSignatureCacheService : MocCommonFunctionSignatureCacheService
    {
        protected override BasicModelCache CreateBasicModelCache()
        {
            return FunctionDefinitionSignatureCache.FunctionDefinitionSignatureCacheFactory(new ElementCreateInfo(Host));
        }

        public override bool TryGetDefaultValue(string parameterName, out object defaultValue)
        {
            defaultValue = null;
            return false;
        }

        public override bool TryGetDefaultValueText(string parameterName, out string defaultValueText)
        {
            defaultValueText = "none";
            return true;
        }

        public override bool TryGetCurrentValue(string parameterName, out object currentValue)
        {
            currentValue = null;
            return false;
        }
    }
}
