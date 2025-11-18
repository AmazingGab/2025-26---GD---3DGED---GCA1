
using GDEngine.Core.Entities;
using GDEngine.Core.Enums;
using GDEngine.Core.Orchestration;
using GDEngine.Core.Services;
using GDEngine.Core.Systems.Base;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GDEngine.Core.Systems
{
    /// <summary>
    /// Frame-driven system that hosts the <see cref="Orchestrator"/> and updates it in the Update lifecycle.
    /// </summary>
    /// <see cref="SystemBase"/>
    public sealed class OrchestrationSystem : SystemBase
    {
        #region Static Fields
        #endregion

        #region Fields
        private readonly Orchestrator _orchestrator;
        private readonly Orchestrator.OrchestratorOptions _options;
        private EngineContext? _context;
        private Scene? _scene;
        #endregion

        #region Properties
        public Orchestrator Orchestrator
        {
            get { return _orchestrator; }
        }
        #endregion

        #region Constructors
        public OrchestrationSystem(int order = 0)
            : base(FrameLifecycle.Update, order)
        {
            _orchestrator = new Orchestrator();
            _options = Orchestrator.OrchestratorOptions.Default;
        }
        #endregion

        #region Methods
        public void Configure(Action<Orchestrator.OrchestratorOptions> configure)
        {
            if (configure != null)
                configure(_options);
        }

        public void SetEventPublisher(Action<object> publish)
        {
            _orchestrator.SetEventPublisher(publish);
        }
        #endregion

        #region Lifecycle Methods
        protected override void OnAdded()
        {
            _scene = Scene;
            _context = _scene != null ? _scene.Context : null;
        }

        public override void Update(float deltaTime)
        {
            if (!Enabled)
                return;

            if (_scene == null || _context == null)
                return;

            float srcDelta;
            if (_options.Time == Orchestrator.OrchestrationTime.Scaled)
                srcDelta = GDEngine.Core.Timing.Time.DeltaTimeSecs;
            else
                srcDelta = GDEngine.Core.Timing.Time.UnscaledDeltaTimeSecs;

            float localScale = _options.LocalScale;
            if (localScale < 0f)
                localScale = 0f;

            float dt = _options.Paused ? 0f : srcDelta * localScale;

            Orchestrator.OrchestrationTick tick = new Orchestrator.OrchestrationTick(dt, _context, _scene);
            _orchestrator.TickWithOptions(tick, _options);
        }
        #endregion

        #region Housekeeping Methods
        public override string ToString()
        {
            return "OrchestrationSystem(" + _orchestrator.ToString() + ")";
        }
        #endregion
    }

    /// <summary>
    /// PostRender overlay that draws a simple text summary of orchestration state.
    /// </summary>
    /// <see cref="Orchestrator"/>
    /// <see cref="FrameLifecycle.PostRender"/>
    public sealed class OrchestrationInspectorSystem : SystemBase, IDisposable
    {
        #region Static Fields
        private static readonly SpriteSortMode _sort = SpriteSortMode.BackToFront;
        private static readonly RasterizerState _raster = RasterizerState.CullNone;
        private static readonly DepthStencilState _depth = DepthStencilState.None;
        private static readonly BlendState _blend = BlendState.AlphaBlend;
        private static readonly SamplerState _sampler = SamplerState.PointClamp;
        #endregion

        #region Fields
        private readonly Func<SpriteFont?> _getFont;

        private EngineContext? _context;
        private Scene? _scene;
        private OrchestrationSystem? _orchSystem;

        private SpriteFont? _font;
        private Texture2D? _pixel;

        private Vector2 _origin = new Vector2(8f, 8f);
        private float _scale = 1f;
        private bool _visible = true;
        private bool _showPerSequence = true;

        private Color _textColor = Color.Yellow;
        private Color _bgColor = new Color(40, 40, 40, 125); // grey with alpha
        #endregion

        #region Properties
        #endregion

        #region Constructors
        public OrchestrationInspectorSystem(Func<SpriteFont?> getFont)
            : base(FrameLifecycle.PostRender, 100)
        {
            _getFont = getFont;
        }
        #endregion

        #region Methods
        public void SetVisible(bool visible)
        {
            _visible = visible;
        }

        public void SetOrigin(Vector2 origin)
        {
            _origin = origin;
        }

        public void SetScale(float scale)
        {
            if (scale < 0.5f)
                scale = 0.5f;
            if (scale > 2f)
                scale = 2f;

            _scale = scale;
        }

        public void ShowPerSequence(bool show)
        {
            _showPerSequence = show;
        }
        #endregion

        #region Lifecycle Methods
        protected override void OnAdded()
        {
            _scene = Scene;
            _context = _scene != null ? _scene.Context : null;
            if (_scene != null)
                _orchSystem = _scene.GetSystem<OrchestrationSystem>();
        }

        public override void Draw(float deltaTime)
        {
            if (!_visible)
                return;

            if (_orchSystem == null)
                return;

            if (_context == null || _context.SpriteBatch == null)
                return;

            if (_font == null)
            {
                _font = _getFont();
                if (_font == null)
                    return;
            }

            if (_pixel == null)
            {
                if (_context.GraphicsDevice == null)
                    return;

                _pixel = new Texture2D(_context.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _pixel.SetData(new[] { Color.White });
            }

            Orchestrator orch = _orchSystem.Orchestrator;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Orchestrator  Time=");
            sb.Append(orch.CurrentOptions.Time.ToString());
            sb.Append("  x");
            sb.Append(orch.CurrentOptions.LocalScale.ToString("0.##"));
            if (orch.CurrentOptions.Paused)
                sb.Append(" [PAUSED]");
            sb.AppendLine();
            sb.Append("Sequences=");
            sb.Append(orch.SequenceCount);
            sb.Append("  Active=");
            sb.Append(orch.ActiveCount);
            sb.AppendLine();

            if (_showPerSequence)
            {
                sb.AppendLine();
                sb.Append(orch.DebugSummary());
            }

            string text = sb.ToString();
            SpriteBatch spriteBatch = _context.SpriteBatch;
            Vector2 size = _font.MeasureString(text) * _scale;
            Rectangle rect = new Rectangle(
                (int)_origin.X - 6,
                (int)_origin.Y - 6,
                (int)size.X + 12,
                (int)size.Y + 12);

            spriteBatch.Begin(_sort, _blend, _sampler, _depth, _raster);
            spriteBatch.Draw(_pixel, rect, _bgColor);
            spriteBatch.DrawString(_font, text, _origin, _textColor, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
            spriteBatch.End();
        }
        #endregion

        #region Housekeeping Methods
        public void Dispose()
        {
            if (_pixel != null && !_pixel.IsDisposed)
                _pixel.Dispose();

            _pixel = null;
        }
        #endregion
    }
}
