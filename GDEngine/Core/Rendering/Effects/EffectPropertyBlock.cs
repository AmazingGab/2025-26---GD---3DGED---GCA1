using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GDEngine.Core.Rendering
{
    /// <summary>
    /// Per-object surface overrides that are independent of the concrete <see cref="Effect"/> type.
    /// Keys like "MainTexture", "Tint", "Alpha", "Bones" are intentionally shared across all effect types.
    /// </summary>
    /// <see cref="IEffectBinder"/>
    public sealed class EffectPropertyBlock
    {
        #region Static Fields
        #endregion

        #region Fields
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
        #endregion

        #region Properties
        public IEnumerable<KeyValuePair<string, object>> Entries => _values;

        // Student-friendly aliases
        public Texture2D MainTexture { set => SetTexture(PropertyKeys.MainTexture, value); }
        public Texture2D SecondaryTexture { set => SetTexture(PropertyKeys.Texture2, value); }
        public Color Tint { set => SetColor(PropertyKeys.Tint, value); }
        public float Alpha { set => SetFloat(PropertyKeys.Alpha, value); }
        public bool UseLighting { set => SetBool(PropertyKeys.UseLighting, value); }
        public float SpecularPower { set => SetFloat(PropertyKeys.SpecularPower, value); }
        public Matrix[] Bones { set => SetMatrices(PropertyKeys.Bones, value); }
        #endregion

        #region Constructors
        #endregion

        #region Methods
        // Typed setters students will actually use
        public void SetTexture(string key, Texture2D tex) => _values[key] = tex;
        public void SetFloat(string key, float v) => _values[key] = v;
        public void SetInt(string key, int v) => _values[key] = v;
        public void SetBool(string key, bool v) => _values[key] = v;
        public void SetColor(string key, Color c) => _values[key] = c;
        public void SetVector2(string key, Vector2 v) => _values[key] = v;
        public void SetVector3(string key, Vector3 v) => _values[key] = v;
        public void SetVector4(string key, Vector4 v) => _values[key] = v;
        public void SetMatrix(string key, Matrix m) => _values[key] = m;
        public void SetMatrices(string key, Matrix[] ms) => _values[key] = ms;
        public void SetSampler(string key, SamplerState s) => _values[key] = s;

        public bool TryGet<T>(string key, out T? value)
        {
            if (_values.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        public void Clear() => _values.Clear();
        #endregion

        #region Lifecycle Methods
        // None
        #endregion

        #region Housekeeping Methods
        #endregion
    }

    /// <summary>
    /// Canonical property keys used by <see cref="EffectPropertyBlock"/> and binders.
    /// </summary>
    public static class PropertyKeys
    {
        public const string MainTexture = "MainTexture";
        public const string Texture2 = "Texture2";
        public const string Tint = "Tint";
        public const string Alpha = "Alpha";
        public const string UseLighting = "UseLighting";
        public const string SpecularPower = "SpecularPower";
        public const string Bones = "Bones";
        public const string EnvironmentMap = "EnvironmentMap";
        public const string EnvAmount = "EnvAmount";
        public const string Fresnel = "Fresnel";
        public const string ReferenceAlpha = "ReferenceAlpha";
        public const string VertexColorEnabled = "VertexColorEnabled";
    }
}
