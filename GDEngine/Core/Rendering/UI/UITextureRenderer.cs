using GDEngine.Core.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GDEngine.Core.Rendering.UI
{
    /// <summary>
    /// Draws a Texture2D in screen space via centralized batching in <see cref="UIRenderer"/>.
    /// Refactored to use base class fields for texture, scale, origin, sourceRect, and color.
    /// </summary>
    public class UITextureRenderer : UIRenderer
    {
        #region Fields
        private Texture2D? _texture;
        private Vector2 _position;
        #endregion

        #region Properties
        public Texture2D? Texture
        {
            get => _texture;
            set => _texture = value;
        }

        public Vector2 Position { get => _position; set => _position = value; }

        /// <summary>
        /// Tint color for the texture. Accesses base class Color property.
        /// </summary>
        public Color Tint
        {
            get => Color;
            set => Color = value;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sets the origin to the center of the texture or source rectangle.
        /// Now uses base class helper method.
        /// </summary>
        public void CenterOrigin()
        {
            CenterOriginFromTexture(_texture, _sourceRect);
        }
        #endregion

        #region Lifecycle Methods
        public override void Draw(GraphicsDevice device, Camera? camera)
        {
            if (_spriteBatch == null || _texture == null) return;

            _spriteBatch.Draw(
                _texture,
                _position,
                _sourceRect,
                Color,
                RotationRadians,
                _origin,
                _scale,
                Effects,
                LayerDepth);
        }
        #endregion
    }
}