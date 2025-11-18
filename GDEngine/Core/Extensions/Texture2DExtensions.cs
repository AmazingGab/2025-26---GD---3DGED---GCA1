using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GDEngine.Core.Utilities
{
    /// <summary>
    /// Extension helpers for <see cref="Texture2D"/>.
    /// </summary>
    /// <see cref="Texture2D"/>
    public static class Texture2DExtensions  //step 1 - static and <class>Extensions
    {
     
        /// <summary>
        /// Returns the centre point of the texture in pixel coordinates.
        /// Useful as an origin when drawing with <see cref="SpriteBatch"/>.
        /// </summary>
        /// <param name="texture">The texture instance.</param>
        /// <returns>Centre of the texture in pixels as a <see cref="Vector2"/>.</returns>
        public static Vector2 GetCenter(this Texture2D texture) //2 - static method, 3 - use this in 1st param
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));

            float x = texture.Width * 0.5f;
            float y = texture.Height * 0.5f;

            return new Vector2(x, y);
        }
    
    }
}
