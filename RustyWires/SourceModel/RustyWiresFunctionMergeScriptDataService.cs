using System.Collections.Generic;
using System.Threading.Tasks;
using NationalInstruments.SourceModel.Envoys;
using NationalInstruments.SourceModel;
using NationalInstruments.MocCommon.SourceModel;
using NationalInstruments.DataTypes;
using NationalInstruments.SourceModel.Persistence;

namespace RustyWires.SourceModel
{
    /// <summary>
    /// Envoy service factory
    /// </summary>
    [ExportEnvoyServiceFactory(typeof(IProvideMergeScriptData))]
    [BindsToModelDefinitionType(RustyWiresFunction.RustyWiresFunctionDefinitionType)]
    internal class RustyWiresFunctionMergeScriptDataServiceFactory : EnvoyServiceFactory
    {
        /// <inheritdoc />
        protected override EnvoyService CreateService()
        {
            return new RustyWiresFunctionMergeScriptDataService();
        }

        /// <summary>
        /// Envoy service that provides RustyWires function merge script data.
        /// </summary>    
        private class RustyWiresFunctionMergeScriptDataService : EnvoyService, IProvideMergeScriptData
        {
            #region IProvideMergeScriptData

            /// <inheritdoc/>
            public virtual IEnumerable<MergeScriptData> MergeScriptData
            {
                get
                {
                    if (AssociatedEnvoy != null)
                    {
                        MergeScriptBuilder builder = new MergeScriptBuilder(Host);
                        var preferredEnvoy = AssociatedEnvoy.TryGetPreferredEnvoy() ?? AssociatedEnvoy;
                        var signatureCache = preferredEnvoy.GetFunctionDefinitionSignatureCache();

                        var methodCall = CreateMethodCall(signatureCache);
                        builder.AddElement(new MergeElementInfo(methodCall));

                        AddIconToBuilder(builder, methodCall, signatureCache);

                        var mergeText = builder.ToString();
                        yield return new MergeScriptData(
                            mergeText,
                            RustyWiresFunction.DiagramClipboardDataFormat,
                            RustyWiresDiagramPaletteLoader.RustyWiresDiagramPaletteIdentifier);
                    }
                }
            }

            /// <inheritdoc/>
            public virtual Task<IEnumerable<MergeScriptData>> GetFilteredMergeScriptsAsync(IMergeScriptFilter filter)
            {
                return ProvideMergeScriptDataHelpers.GetFilteredMergeScriptsAsync(MergeScriptData, filter);
            }

            private MethodCall CreateMethodCall(FunctionSignatureCache signatureCache)
            {
                var createInfo = new ElementCreateInfo(Host, null, null, null, null);
                var methodCall = MocCommonMethodCall.Create(createInfo);
                methodCall.Target = AssociatedEnvoy.MakeRelativeDependencyName();
                if (signatureCache != null)
                {
                    methodCall.Bounds = new NationalInstruments.Core.SMRect(methodCall.Left, methodCall.Top, signatureCache.Width, signatureCache.Height);
                }
                CreateTerminals(methodCall, signatureCache);
                return methodCall;
            }

            private void CreateTerminals(MethodCall methodCall, FunctionSignatureCache signatureCache)
            {
                var cachedParameters = signatureCache?.CachedParameters;
                if (cachedParameters != null)
                {
                    foreach (var cachedParameter in cachedParameters)
                    {
                        var direction = PFTypes.ParameterCallDirectionToDirection(cachedParameter.CallDirection);
                        var terminal = new NodeTerminal(
                            direction,
                            cachedParameter.DataType,
                            cachedParameter.SideIndex.ToString(),
                            cachedParameter.Hotspot);
                        methodCall.AddTerminal(terminal);
                    }
                }
            }

            private void AddIconToBuilder(MergeScriptBuilder builder, MethodCall methodCall, FunctionSignatureCache signatureCache)
            {
                if (signatureCache?.IconModel != null)
                {
                    var generationOptions = new ElementGenerationOptions(GenerationReason.Merge);
                    var table = IconTable.GetIconTableToGenerate(methodCall, generationOptions);
                    if (table != null)
                    {
                        table.AddIcon(methodCall.Target, signatureCache.IconModel);
                        builder.AddElement(new MergeElementInfo(table));
                    }
                }
            }
            #endregion
        }
    }
}
