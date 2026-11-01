using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.WebGL;

namespace ScrapFishing.Build
{
    // 배포 대상별 빌드가 바꾸는 PlayerSettings(템플릿·스크립팅 심볼·압축 등)를 기억했다가 빌드가 끝나면 되돌린다.
    // using (new BuildSettingsScope(template, defines)) { ... } 안에서 빌드한다. 회사·제품 이름은 대상과 무관해 건드리지 않는다.
    public sealed class BuildSettingsScope : IDisposable
    {
        readonly string _template;
        readonly string _defines;
        readonly WebGLCompressionFormat _compression;
        readonly bool _decompressionFallback;
        readonly WebGLExceptionSupport _exceptions;
        readonly ManagedStrippingLevel _stripping;
        readonly Il2CppCodeGeneration _codeGeneration;
        readonly Il2CppCompilerConfiguration _compilerConfiguration;
        readonly WasmCodeOptimization _codeOptimization;
        readonly bool _splash;

        public BuildSettingsScope(string template, string defines)
            : this(template, defines, WebGLCompressionFormat.Gzip, true)
        {
        }

        // 압축만 대상별로 다르게 할 때(CrazyGames는 Brotli + 압축 풀기 폴백 끔). 끝나면 원래 압축 설정으로 되돌린다.
        public BuildSettingsScope(string template, string defines, WebGLCompressionFormat compression, bool decompressionFallback)
        {
            _template = PlayerSettings.WebGL.template;
            _defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            _compression = PlayerSettings.WebGL.compressionFormat;
            _decompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            _exceptions = PlayerSettings.WebGL.exceptionSupport;
            _stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.WebGL);
            _codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);
            _compilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL);
            _codeOptimization = UserBuildSettings.codeOptimization;
            _splash = PlayerSettings.SplashScreen.show;

            WebGLBuilder.ApplySettings();
            PlayerSettings.WebGL.template = template;
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, defines);
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
        }

        public void Dispose()
        {
            PlayerSettings.WebGL.template = _template;
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, _defines);
            PlayerSettings.WebGL.compressionFormat = _compression;
            PlayerSettings.WebGL.decompressionFallback = _decompressionFallback;
            PlayerSettings.WebGL.exceptionSupport = _exceptions;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, _stripping);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, _codeGeneration);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, _compilerConfiguration);
            UserBuildSettings.codeOptimization = _codeOptimization;
            PlayerSettings.SplashScreen.show = _splash;
            AssetDatabase.SaveAssets();
        }
    }
}
