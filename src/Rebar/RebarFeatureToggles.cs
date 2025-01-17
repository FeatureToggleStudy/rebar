﻿using NationalInstruments.Core;
using NationalInstruments.FeatureToggles;

namespace Rebar
{
    [ExportFeatureToggles]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), CellDataType, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), RebarTarget, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), LLVMCompiler, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), OutputNode, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), VectorAndSliceTypes, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), VisualizeVariableIdentity, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), StringDataType, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), AllIntegerTypes, CodeReadiness.Release)]
    [ExposeFeatureToggle(typeof(RebarFeatureToggles), FileHandleDataType, CodeReadiness.Release)]
    public sealed class RebarFeatureToggles : FeatureTogglesProvider<RebarFeatureToggles>
    {
        private const string RebarFeatureCategory = "Rebar";
        private const string FeaturePrefix = "Rebar.FeatureToggles.";

        public const string CellDataType = FeaturePrefix + nameof(CellDataType);
        public const string RebarTarget = FeaturePrefix + nameof(RebarTarget);
        public const string LLVMCompiler = FeaturePrefix + nameof(LLVMCompiler);
        public const string OutputNode = FeaturePrefix + nameof(OutputNode);
        public const string VectorAndSliceTypes = FeaturePrefix + nameof(VectorAndSliceTypes);
        public const string VisualizeVariableIdentity = FeaturePrefix + nameof(VisualizeVariableIdentity);
        public const string StringDataType = FeaturePrefix + nameof(StringDataType);
        public const string AllIntegerTypes = FeaturePrefix + nameof(AllIntegerTypes);
        public const string FileHandleDataType = FeaturePrefix + nameof(FileHandleDataType);

        public static bool IsCellDataTypeEnabled => _cellDataType.IsEnabled;
        public static bool IsRebarTargetEnabled => _rebarTarget.IsEnabled;
        public static bool IsLLVMCompilerEnabled => _llvmCompiler.IsEnabled;
        public static bool IsOutputNodeEnabled => _outputNode.IsEnabled;
        public static bool IsVectorAndSliceTypesEnabled => _vectorAndSliceTypes.IsEnabled;
        public static bool IsVisualizeVariableIdentityEnabled => _visualizeVariableIdentity.IsEnabled;
        public static bool IsStringDataTypeEnabled => _stringDataType.IsEnabled;
        public static bool IsAllIntegerTypesEnabled => _allIntegerTypes.IsEnabled;
        public static bool IsFileHandleDataTypeEnabled => _fileHandleDataType.IsEnabled;

        private static readonly FeatureToggleValueCache _cellDataType = CreateFeatureToggleValueCache(CellDataType);
        private static readonly FeatureToggleValueCache _rebarTarget = CreateFeatureToggleValueCache(RebarTarget);
        private static readonly FeatureToggleValueCache _llvmCompiler = CreateFeatureToggleValueCache(LLVMCompiler);
        private static readonly FeatureToggleValueCache _outputNode = CreateFeatureToggleValueCache(OutputNode);
        private static readonly FeatureToggleValueCache _vectorAndSliceTypes = CreateFeatureToggleValueCache(VectorAndSliceTypes);
        private static readonly FeatureToggleValueCache _visualizeVariableIdentity = CreateFeatureToggleValueCache(VisualizeVariableIdentity);
        private static readonly FeatureToggleValueCache _stringDataType = CreateFeatureToggleValueCache(StringDataType);
        private static readonly FeatureToggleValueCache _allIntegerTypes = CreateFeatureToggleValueCache(AllIntegerTypes);
        private static readonly FeatureToggleValueCache _fileHandleDataType = CreateFeatureToggleValueCache(FileHandleDataType);
    }
}
