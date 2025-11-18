#nullable enable
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
using GDEngine.Core.Rendering;
using GDEngine.Core.Timing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace GDEngine.Core.Debug
{
    /// <summary>
    /// Enhanced FPS and performance stats overlay that draws in PostRender.
    /// Leverages the enhanced Time class for comprehensive performance monitoring.
    /// Includes memory tracking, FPS graphing, display profiles, and optimized rendering.
    /// Uses centralized batching via <see cref="UIRenderer"/>.
    /// Renders in a chosen screen corner with a configurable margin.
    /// </summary>
    public sealed class UIStatsRenderer : UIRenderer
    {
        #region Fields
        private SpriteFont _font = null!;
        private Texture2D? _backgroundTexture;

        // Layout
        private Vector2 _anchor = new Vector2(5f, 5f);        // top-left of header text
        private Vector2 _texturePadding = new Vector2(5f, 5f);
        private Rectangle _backRect;
        private float _headerTemplateWidth;
        private float _gapAfterHeader;

        // Style
        private Color _shadow = Color.Black;
        private Color _text = Color.Yellow;
        private Color _bgColor = new Color(40, 40, 40, 125);  // grey with alpha
        private Color _pauseColor = Color.Orange;
        private Color _goodColor = Color.LightGreen;
        private Color _acceptableColor = Color.Yellow;
        private Color _warningColor = Color.Orange;
        private Color _criticalColor = Color.Red;

        // Content - OPTIMIZED: Reuse StringBuilder and List
        private readonly StringBuilder _headerBuilder = new StringBuilder(100);
        private string _header = string.Empty;
        private System.Func<IEnumerable<string>>? _linesProvider;
        private readonly List<string> _extra = new List<string>(16);
        private readonly Dictionary<string, float> _lineWidthCache = new Dictionary<string, float>(32);

        // Corner anchoring
        private ScreenCorner _screenCorner = ScreenCorner.TopLeft;
        private Vector2 _margin = new Vector2(10f, 10f);

        // Display options
        private DisplayProfile _profile = DisplayProfile.Standard;
        private bool _showMemoryStats = false;
        private bool _showFPSGraph = false;
        private const int FPSGraphWidth = 60;
        private const int FPSGraphHeight = 20;
        private readonly CircularBuffer<float> _fpsHistory = new CircularBuffer<float>(FPSGraphWidth);
        private Texture2D? _graphTexture;

        // Update rate control
        private float _updateInterval = 0.1f; // 10 Hz by default
        private float _updateAccumulator = 0f;
        private bool _needsUpdate = true;

        // Performance thresholds
        private float _goodFPSThreshold = 55f;
        private float _acceptableFPSThreshold = 45f;
        private float _warningFPSThreshold = 30f;
        // Below warning threshold = critical (red)

        // Memory tracking
        private long _lastTotalMemory = 0;
        private int _lastGen0Count = 0;
        private int _lastGen1Count = 0;
        private int _lastGen2Count = 0;

        //REFACTOR: Cache last FPS/MS values to avoid rebuilding string when unchanged
        private float _lastFps = 0f;
        private float _lastMs = 0f;
        private float _lastUptime = 0f;
        private bool _lastPauseState = false;
        #endregion

        #region Properties
        public SpriteFont Font
        {
            get { return _font; }
            set { _font = value; }
        }

        public Color TextColor
        {
            get { return _text; }
            set { _text = value; }
        }

        public Color Shadow
        {
            get { return _shadow; }
            set { _shadow = value; }
        }

        public Color BackgroundColor
        {
            get { return _bgColor; }
            set { _bgColor = value; }
        }

        public Color PauseColor
        {
            get { return _pauseColor; }
            set { _pauseColor = value; }
        }

        public Color GoodColor
        {
            get { return _goodColor; }
            set { _goodColor = value; }
        }

        public Color AcceptableColor
        {
            get { return _acceptableColor; }
            set { _acceptableColor = value; }
        }

        public Color WarningColor
        {
            get { return _warningColor; }
            set { _warningColor = value; }
        }

        public Color CriticalColor
        {
            get { return _criticalColor; }
            set { _criticalColor = value; }
        }

        public Vector2 TexturePadding
        {
            get { return _texturePadding; }
            set { _texturePadding = value; }
        }

        /// <summary>
        /// Function that provides additional debug lines to render under the FPS header.
        /// Called once per update interval.
        /// </summary>
        public System.Func<IEnumerable<string>>? LinesProvider
        {
            get { return _linesProvider; }
            set { _linesProvider = value; }
        }

        /// <summary>
        /// Corner of the game window that this overlay should attach to.
        /// </summary>
        public ScreenCorner ScreenCorner
        {
            get { return _screenCorner; }
            set { _screenCorner = value; }
        }

        /// <summary>
        /// Margin in pixels from the chosen screen corner to the outer edge of the panel.
        /// </summary>
        public Vector2 Margin
        {
            get { return _margin; }
            set { _margin = value; }
        }

        /// <summary>
        /// Display profile preset controlling which stats are shown.
        /// </summary>
        public DisplayProfile Profile
        {
            get { return _profile; }
            set
            {
                _profile = value;
                ApplyProfile();
            }
        }

        /// <summary>
        /// Show memory usage and garbage collection statistics.
        /// </summary>
        public bool ShowMemoryStats
        {
            get { return _showMemoryStats; }
            set { _showMemoryStats = value; }
        }

        /// <summary>
        /// Show visual FPS history graph (sparkline).
        /// </summary>
        public bool ShowFPSGraph
        {
            get { return _showFPSGraph; }
            set { _showFPSGraph = value; }
        }

        /// <summary>
        /// Update interval in seconds. Lower values update more frequently but cost more CPU.
        /// Default is 0.1 (10 updates per second).
        /// </summary>
        public float UpdateInterval
        {
            get { return _updateInterval; }
            set { _updateInterval = MathF.Max(0.016f, value); } // Min 60 Hz
        }

        /// <summary>
        /// FPS threshold for "good" performance (green color).
        /// </summary>
        public float GoodFPSThreshold
        {
            get { return _goodFPSThreshold; }
            set { _goodFPSThreshold = value; }
        }

        /// <summary>
        /// FPS threshold for "acceptable" performance (yellow color).
        /// </summary>
        public float AcceptableFPSThreshold
        {
            get { return _acceptableFPSThreshold; }
            set { _acceptableFPSThreshold = value; }
        }

        /// <summary>
        /// FPS threshold for "warning" performance (orange color).
        /// Below this threshold = critical (red color).
        /// </summary>
        public float WarningFPSThreshold
        {
            get { return _warningFPSThreshold; }
            set { _warningFPSThreshold = value; }
        }
        #endregion

        #region Lifecycle Methods
        protected override void Awake()
        {
            base.Awake();

            // Use base class _graphicsDevice (no need for duplicate field)
            if (_graphicsDevice != null && _backgroundTexture == null)
            {
                _backgroundTexture = new Texture2D(_graphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _backgroundTexture.SetData(new[] { Color.White });
            }

            if (_font != null)
            {
                // Measure template width for layout calculations
                const string template = "FPS: 000.0 (Avg: 000.0)  |  Frame: 00.00ms  |  Uptime: 000000s";
                _headerTemplateWidth = _font.MeasureString(template).X;
            }

            // Apply initial profile settings
            ApplyProfile();
        }

        protected override void LateUpdate(float deltaTime)
        {
            // Update FPS history every frame for smooth graph
            if (_showFPSGraph)
            {
                _fpsHistory.Push(Time.CurrentFPS);
            }

            // Accumulate time for update interval
            _updateAccumulator += Time.UnscaledDeltaTimeSecs;

            // Only rebuild text when update interval elapsed
            if (_updateAccumulator >= _updateInterval)
            {
                _updateAccumulator -= _updateInterval;
                _needsUpdate = true;
            }

            if (!_needsUpdate)
                return;

            _needsUpdate = false;

            // Clear width cache periodically to prevent unbounded growth
            if (_lineWidthCache.Count > 100)
                _lineWidthCache.Clear();

            // Build header using StringBuilder (OPTIMIZATION: avoids string allocations)
            BuildHeaderString();
            float headerWidth = MeasureAndCacheWidth(_header);

            int linesCount = 1;
            float maxWidth = MathF.Max(_headerTemplateWidth, headerWidth);

            //REFACTOR: Clear and reuse list instead of allocating new one
            _extra.Clear();

            if (ShouldShowDetailedStats())
            {
                AddDetailedStats(_extra);
            }

            if (ShouldShowFrameTimeStats())
            {
                AddFrameTimeStats(_extra);
            }

            if (ShouldShowTimeScaleInfo() && (Time.IsPaused || Time.TimeScale != 1.0f))
            {
                AddTimeScaleInfo(_extra);
            }

            if (_showMemoryStats)
            {
                AddMemoryStats(_extra);
            }

            // Add custom lines from provider
            if (_linesProvider != null)
            {
                foreach (var line in _linesProvider())
                {
                    _extra.Add(line);
                    float width = MeasureAndCacheWidth(line);
                    if (width > maxWidth)
                        maxWidth = width;
                }
            }

            linesCount += _extra.Count;

            // Account for FPS graph if shown
            if (_showFPSGraph && _graphicsDevice != null)
            {
                linesCount++; // Extra line for graph
                float graphWidth = FPSGraphWidth + _texturePadding.X;
                if (graphWidth > maxWidth)
                    maxWidth = graphWidth;
            }

            _gapAfterHeader = _extra.Count > 0 || _showFPSGraph ? 5f : 0f;

            float graphHeightAddition = _showFPSGraph ? FPSGraphHeight + 5f : 0f;
            float totalHeight = _texturePadding.Y * 2f + _font.LineSpacing * linesCount + _gapAfterHeader + graphHeightAddition;
            float totalWidth = _texturePadding.X * 2f + maxWidth;

            // Work out where the panel should sit in the window
            Vector2 panelTopLeft = ComputePanelTopLeft(totalWidth, totalHeight);

            // Anchor is the top-left of the header text inside the panel
            _anchor = panelTopLeft + _texturePadding;

            _backRect = new Rectangle(
                (int)MathF.Floor(panelTopLeft.X),
                (int)MathF.Floor(panelTopLeft.Y),
                (int)MathF.Ceiling(totalWidth),
                (int)MathF.Ceiling(totalHeight));
        }

        public override void Draw(GraphicsDevice device, Camera? camera)
        {
            if (_spriteBatch == null || _font == null)
                return;

            float backgroundDepth = Behind(LayerDepth);

            // Background panel
            if (_backgroundTexture != null)
            {
                _spriteBatch.Draw(
                    _backgroundTexture,
                    _backRect,
                    null,
                    _bgColor,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    backgroundDepth);
            }

            // Determine header color based on performance and pause state
            Color headerColor = GetHeaderColor();

            // Header line with shadow - use base class helper
            DrawStringWithShadow(
                _font,
                _header,
                _anchor,
                headerColor,
                RotationRadians,
                Vector2.Zero,
                1f,
                Effects,
                LayerDepth,
                true);

            // Extra lines
            float y = _anchor.Y + _font.LineSpacing + _gapAfterHeader;
            for (int i = 0; i < _extra.Count; i++)
            {
                var pos = new Vector2(_anchor.X, y);

                DrawStringWithShadow(
                    _font,
                    _extra[i],
                    pos,
                    _text,
                    RotationRadians,
                    Vector2.Zero,
                    1f,
                    Effects,
                    LayerDepth,
                    true);

                y += _font.LineSpacing;
            }

            // Draw FPS graph if enabled
            if (_showFPSGraph && _graphicsDevice != null)
            {
                DrawFPSGraph(new Vector2(_anchor.X, y), backgroundDepth);
            }
        }

        protected override void OnDisabled()
        {
            // Clean up graph texture when disabled
            if (_graphTexture != null)
            {
                _graphTexture.Dispose();
                _graphTexture = null;
            }
        }

        protected override void OnDestroy()
        {
            // Clean up graph texture on destruction
            if (_graphTexture != null)
            {
                _graphTexture.Dispose();
                _graphTexture = null;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Measures string width and caches the result to avoid duplicate measurements.
        ///REFACTOR: Implements the previously-declared but unused cache.
        /// </summary>
        private float MeasureAndCacheWidth(string text)
        {
            if (!_lineWidthCache.TryGetValue(text, out float width))
            {
                width = _font.MeasureString(text).X;
                _lineWidthCache[text] = width;
            }
            return width;
        }

        /// <summary>
        /// Applies settings based on the selected display profile
        /// </summary>
        private void ApplyProfile()
        {
            switch (_profile)
            {
                case DisplayProfile.Minimal:
                    _showMemoryStats = false;
                    _showFPSGraph = false;
                    _updateInterval = 0.25f; // 4 Hz
                    break;

                case DisplayProfile.Standard:
                    _showMemoryStats = false;
                    _showFPSGraph = false;
                    _updateInterval = 0.1f; // 10 Hz
                    break;

                case DisplayProfile.Detailed:
                    _showMemoryStats = true;
                    _showFPSGraph = false;
                    _updateInterval = 0.1f; // 10 Hz
                    break;

                case DisplayProfile.Profiling:
                    _showMemoryStats = true;
                    _showFPSGraph = true;
                    _updateInterval = 0.05f; // 20 Hz
                    break;
            }
        }

        /// <summary>
        /// Determines if detailed stats should be shown based on profile
        /// </summary>
        private bool ShouldShowDetailedStats()
        {
            return _profile == DisplayProfile.Detailed || _profile == DisplayProfile.Profiling;
        }

        /// <summary>
        /// Determines if frame time stats should be shown based on profile
        /// </summary>
        private bool ShouldShowFrameTimeStats()
        {
            return _profile == DisplayProfile.Profiling;
        }

        /// <summary>
        /// Determines if TimeScale info should be shown based on profile
        /// </summary>
        private bool ShouldShowTimeScaleInfo()
        {
            return _profile != DisplayProfile.Minimal;
        }

        /// <summary>
        /// Builds the header string using StringBuilder to avoid allocations.
        /// MAJOR OPTIMIZATION: Only rebuilds when values change significantly.
        /// </summary>
        private void BuildHeaderString()
        {
            // Use Time class properties directly
            float fps = Time.CurrentFPS;
            float avgFps = Time.AverageFPS;
            float ms = Time.UnscaledDeltaTimeSecs * 1000f;
            float uptime = (float)Time.RealtimeSinceStartupSecs;
            bool paused = Time.IsPaused;

            //REFACTOR: Only rebuild if values changed significantly or pause state changed
            bool needsRebuild =
                MathF.Abs(fps - _lastFps) > 0.5f ||
                MathF.Abs(avgFps - _lastMs) > 0.5f ||
                MathF.Abs(uptime - _lastUptime) > 0.1f ||
                paused != _lastPauseState;

            if (!needsRebuild && _header.Length > 0)
                return;

            _lastFps = fps;
            _lastMs = avgFps;
            _lastUptime = uptime;
            _lastPauseState = paused;

            // Clear and rebuild
            _headerBuilder.Clear();

            // Format varies by profile
            if (_profile == DisplayProfile.Minimal)
            {
                _headerBuilder.Append("FPS: ");
                _headerBuilder.AppendFormat("{0:0.0}", avgFps);
                if (paused)
                    _headerBuilder.Append(" [PAUSED]");
            }
            else
            {
                _headerBuilder.Append("FPS: ");
                _headerBuilder.AppendFormat("{0:0.0}", fps);
                _headerBuilder.Append(" (Avg: ");
                _headerBuilder.AppendFormat("{0:0.0}", avgFps);
                _headerBuilder.Append(")  |  Frame: ");
                _headerBuilder.AppendFormat("{0:0.00}", ms);
                _headerBuilder.Append("ms  |  Uptime: ");
                _headerBuilder.AppendFormat("{0,6:F2}", uptime);
                _headerBuilder.Append("s");
                if (paused)
                    _headerBuilder.Append(" [PAUSED]");
            }

            _header = _headerBuilder.ToString();
        }

        /// <summary>
        /// Adds detailed performance statistics to the extra lines list
        /// </summary>
        private void AddDetailedStats(List<string> lines)
        {
            // Min/Max frame times converted to FPS
            float minFPS = Time.MaxFrameTime > 0 ? 1.0f / Time.MaxFrameTime : 0f;
            float maxFPS = Time.MinFrameTime > 0 ? 1.0f / Time.MinFrameTime : 0f;

            lines.Add($"FPS Range: {minFPS:0.0} - {maxFPS:0.0}");
            lines.Add($"Frame Count: {Time.FrameCount}");
        }

        /// <summary>
        /// Adds frame time statistics to the extra lines list
        /// </summary>
        private void AddFrameTimeStats(List<string> lines)
        {
            float minMs = Time.MinFrameTime * 1000f;
            float maxMs = Time.MaxFrameTime * 1000f;
            float smoothMs = Time.SmoothDeltaTimeSecs * 1000f;

            lines.Add($"Frame Time: Min={minMs:0.00}ms, Max={maxMs:0.00}ms");
            lines.Add($"Smooth DT: {smoothMs:0.00}ms");
        }

        /// <summary>
        /// Adds TimeScale and pause information to the extra lines list
        /// </summary>
        private void AddTimeScaleInfo(List<string> lines)
        {
            if (Time.IsPaused)
            {
                lines.Add($"[PAUSED] TimeScale: {Time.TimeScale:0.00}");
            }
            else if (Time.TimeScale != 1.0f)
            {
                string scaleDescription = Time.TimeScale < 1.0f ? "Slow Motion" : "Fast Forward";
                lines.Add($"TimeScale: {Time.TimeScale:0.00} ({scaleDescription})");
                lines.Add($"Game Time: {Time.TimeSinceStartupSecs:F2}s");
            }
        }

        /// <summary>
        /// Adds memory usage and garbage collection statistics to the extra lines list
        /// </summary>
        private void AddMemoryStats(List<string> lines)
        {
            // Get current memory stats
            _lastTotalMemory = GC.GetTotalMemory(false);
            _lastGen0Count = GC.CollectionCount(0);
            _lastGen1Count = GC.CollectionCount(1);
            _lastGen2Count = GC.CollectionCount(2);

            // Convert to MB
            float memoryMB = _lastTotalMemory / (1024f * 1024f);

            lines.Add($"Memory: {memoryMB:0.0}MB");
            lines.Add($"GC: Gen0={_lastGen0Count}, Gen1={_lastGen1Count}, Gen2={_lastGen2Count}");
        }

        /// <summary>
        /// Draws an FPS history graph (sparkline style)
        /// </summary>
        private void DrawFPSGraph(Vector2 position, float depth)
        {
            if (_backgroundTexture == null || _fpsHistory.Count == 0)
                return;

            // Find min/max FPS in history for scaling
            var history = _fpsHistory.ToArray();
            float minFPS = float.MaxValue;
            float maxFPS = float.MinValue;

            foreach (float fps in history)
            {
                if (fps < minFPS) minFPS = fps;
                if (fps > maxFPS) maxFPS = fps;
            }

            // Avoid division by zero
            float range = maxFPS - minFPS;
            if (range < 1f) range = 1f;

            // Draw graph background
            Rectangle graphBounds = new Rectangle(
                (int)position.X,
                (int)position.Y,
                FPSGraphWidth,
                FPSGraphHeight);

            _spriteBatch?.Draw(
                _backgroundTexture,
                graphBounds,
                null,
                new Color(0, 0, 0, 100),
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                depth);

            // Draw each FPS sample as a vertical line
            for (int i = 0; i < history.Length; i++)
            {
                float fps = history[i];
                float normalizedHeight = (fps - minFPS) / range;
                int barHeight = (int)(normalizedHeight * FPSGraphHeight);

                if (barHeight > 0)
                {
                    Rectangle bar = new Rectangle(
                        (int)position.X + i,
                        (int)position.Y + (FPSGraphHeight - barHeight),
                        1,
                        barHeight);

                    // Color based on FPS thresholds
                    Color barColor = GetColorForFPS(fps);

                    _spriteBatch?.Draw(
                        _backgroundTexture,
                        bar,
                        null,
                        barColor,
                        0f,
                        Vector2.Zero,
                        SpriteEffects.None,
                        depth + 0.001f);
                }
            }

            // Draw target FPS line (60 FPS) if it's in range
            if (60f >= minFPS && 60f <= maxFPS)
            {
                float targetY = position.Y + FPSGraphHeight * (1f - ((60f - minFPS) / range));
                Rectangle targetLine = new Rectangle(
                    (int)position.X,
                    (int)targetY,
                    FPSGraphWidth,
                    1);

                _spriteBatch?.Draw(
                    _backgroundTexture,
                    targetLine,
                    null,
                    new Color(255, 255, 255, 128),
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    depth + 0.002f);
            }
        }

        /// <summary>
        /// Gets color for FPS value based on thresholds
        /// </summary>
        private Color GetColorForFPS(float fps)
        {
            if (fps >= _goodFPSThreshold)
                return _goodColor;
            else if (fps >= _acceptableFPSThreshold)
                return _acceptableColor;
            else if (fps >= _warningFPSThreshold)
                return _warningColor;
            else
                return _criticalColor;
        }

        /// <summary>
        /// Determines the color for the header text based on performance and state
        /// </summary>
        private Color GetHeaderColor()
        {
            // If paused, use pause color
            if (Time.IsPaused)
                return _pauseColor;

            // Use multi-tier thresholds for performance
            return GetColorForFPS(Time.AverageFPS);
        }

        private Vector2 ComputePanelTopLeft(float panelWidth, float panelHeight)
        {
            if (_graphicsDevice == null)
                return _margin; // Fallback: top-left with margin

            var vp = _graphicsDevice.Viewport;

            switch (_screenCorner)
            {
                case ScreenCorner.TopLeft:
                    return new Vector2(
                        _margin.X,
                        _margin.Y);

                case ScreenCorner.TopRight:
                    return new Vector2(
                        vp.Width - panelWidth - _margin.X,
                        _margin.Y);

                case ScreenCorner.BottomLeft:
                    return new Vector2(
                        _margin.X,
                        vp.Height - panelHeight - _margin.Y);

                default: // BottomRight
                    return new Vector2(
                        vp.Width - panelWidth - _margin.X,
                        vp.Height - panelHeight - _margin.Y);
            }
        }
        #endregion
    }

    /// <summary>
    /// Display profile presets for UIStatsRenderer
    /// </summary>
    public enum DisplayProfile
    {
        /// <summary>
        /// Minimal display - just average FPS. Updates at 4 Hz. Best for release builds.
        /// </summary>
        Minimal,

        /// <summary>
        /// Standard display - FPS, frame time, uptime. Updates at 10 Hz. Good default for development.
        /// </summary>
        Standard,

        /// <summary>
        /// Detailed display - adds FPS range, frame count, and memory stats. Updates at 10 Hz.
        /// </summary>
        Detailed,

        /// <summary>
        /// Profiling display - all stats including FPS graph. Updates at 20 Hz. For performance analysis.
        /// </summary>
        Profiling
    }

    public enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
/*#nullable enable
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
using GDEngine.Core.Entities;
using GDEngine.Core.Rendering;
using GDEngine.Core.Timing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX.Direct2D1;
using System.Collections.Generic;

namespace GDEngine.Core.Debug
{
    /// <summary>
    /// FPS + custom text lines overlay that draws in PostRender. Attach to a GameObject.
    /// Uses centralized batching via <see cref="UIRenderer"/>.
    /// Renders in a chosen screen corner with a configurable margin.
    /// </summary>
    public sealed class UIStatsRenderer : UIRenderer
    {
        #region Fields
        private readonly CircularBuffer<float> _recentDt = new CircularBuffer<float>(60);

        private SpriteFont _font = null!;
        private GraphicsDevice? _graphicsDevice;
        private Texture2D? _backgroundTexture;

        // Layout
        private Vector2 _anchor = new Vector2(5f, 5f);        // top-left of header text
        private Vector2 _texturePadding = new Vector2(5f, 5f);
        private Rectangle _backRect;
        private float _headerTemplateWidth;
        private float _gapAfterHeader;

        // Style
        private Color _shadow = Color.Black;
        private Color _text = Color.Yellow;
        private Color _bgColor = new Color(40, 40, 40, 125);  // grey with alpha

        // Content
        private string _header = string.Empty;
        private System.Func<IEnumerable<string>>? _linesProvider;
        private List<string>? _extra;

        // Corner anchoring
        private ScreenCorner _screenCorner = ScreenCorner.TopLeft;
        private Vector2 _margin = new Vector2(10f, 10f);
        #endregion

        #region Properties
        public SpriteFont Font
        {
            get { return _font; }
            set { _font = value; }
        }

        public Color TextColor
        {
            get { return _text; }
            set { _text = value; }
        }

        public Color Shadow
        {
            get { return _shadow; }
            set { _shadow = value; }
        }

        public Color BackgroundColor
        {
            get { return _bgColor; }
            set { _bgColor = value; }
        }

        public Vector2 TexturePadding
        {
            get { return _texturePadding; }
            set { _texturePadding = value; }
        }

        /// <summary>
        /// Function that provides additional debug lines to render under the FPS header.
        /// Called once per LateUpdate.
        /// </summary>
        public System.Func<IEnumerable<string>>? LinesProvider
        {
            get { return _linesProvider; }
            set { _linesProvider = value; }
        }

        /// <summary>
        /// Corner of the game window that this overlay should attach to.
        /// </summary>
        public ScreenCorner ScreenCorner
        {
            get { return _screenCorner; }
            set { _screenCorner = value; }
        }

        /// <summary>
        /// Margin in pixels from the chosen screen corner to the outer edge of the panel.
        /// </summary>
        public Vector2 Margin
        {
            get { return _margin; }
            set { _margin = value; }
        }
        #endregion

        #region Lifecycle Methods
        protected override void Awake()
        {
            base.Awake();

            _graphicsDevice = GameObject?.Scene?.Context.GraphicsDevice;
            if (_graphicsDevice != null && _backgroundTexture == null)
            {
                _backgroundTexture = new Texture2D(_graphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _backgroundTexture.SetData(new[] { Color.White });
            }

            if (_font != null)
            {
                const string template = "FPS: 0000.0  |  Render: 00.00 ms  |  Uptime: 000000s";
                _headerTemplateWidth = _font.MeasureString(template).X;
            }
        }

        protected override void LateUpdate(float deltaTime)
        {
            float dt = MathF.Max(Time.UnscaledDeltaTimeSecs, 1e-6f);
            _recentDt.Push(dt);

            var arr = _recentDt.ToArray();
            float sum = 0f;
            for (int i = 0; i < arr.Length; i++)
                sum += arr[i];

            float avgDt = arr.Length > 0 ? sum / arr.Length : dt;

            float fps = avgDt > 0f ? 1f / avgDt : 0f;
            float ms = avgDt * 1000f;

            _header = $"FPS: {fps:0.0}  |  Render: {ms:0.00} ms  |  Uptime: {Time.RealtimeSinceStartupSecs,6:F2}s";

            int linesCount = 1;
            float maxWidth = _headerTemplateWidth;

            _extra = null;
            int extraLineCount = 0;

            if (_linesProvider != null)
            {
                _extra = new List<string>();
                foreach (var line in _linesProvider())
                {
                    _extra.Add(line);
                    float width = _font.MeasureString(line).X;
                    if (width > maxWidth)
                        maxWidth = width;
                }
                extraLineCount = _extra.Count;
            }

            // Calculate gap separately from line count
            _gapAfterHeader = extraLineCount > 0 ? 10f : 0f;

            // Total height = padding + header + gap + extras + padding
            float totalHeight = _texturePadding.Y * 2f +              // Top padding
                               _font.LineSpacing +                     // Header line
                               _gapAfterHeader +                       // Gap
                               (_font.LineSpacing * extraLineCount);   // Extra lines
                                                                       // (bottom padding already in first term)

            float totalWidth = _texturePadding.X * 2f + maxWidth;

            // Work out where the panel should sit in the window
            Vector2 panelTopLeft = ComputePanelTopLeft(totalWidth, totalHeight);

            // Anchor is the top-left of the header text inside the panel
            _anchor = panelTopLeft + _texturePadding;

            _backRect = new Rectangle(
                (int)MathF.Floor(panelTopLeft.X),
                (int)MathF.Floor(panelTopLeft.Y),
                (int)MathF.Ceiling(totalWidth),
                (int)MathF.Ceiling(totalHeight));
        }

        public override void Draw(GraphicsDevice device, Camera? camera)
        {
            if (_spriteBatch == null || _font == null)
                return;

            float backgroundDepth = Behind(LayerDepth);

            // Background panel
            if (_backgroundTexture != null)
            {
                _spriteBatch.Draw(
                    _backgroundTexture,
                    _backRect,
                    null,
                    _bgColor,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    backgroundDepth);
            }

            // Header line
            _spriteBatch.DrawString(
                _font,
                _header,
                _anchor + _shadowNudge,
                _shadow,
                RotationRadians,
                Vector2.Zero,
                1f,
                Effects,
                backgroundDepth);

            _spriteBatch.DrawString(
                _font,
                _header,
                _anchor,
                _text,
                RotationRadians,
                Vector2.Zero,
                1f,
                Effects,
                LayerDepth);

            // Extra lines
            float y = _anchor.Y + _font.LineSpacing + _gapAfterHeader;
            if (_extra != null)
            {
                for (int i = 0; i < _extra.Count; i++)
                {
                    var pos = new Vector2(_anchor.X, y);

                    _spriteBatch.DrawString(
                        _font,
                        _extra[i],
                        pos + _shadowNudge,
                        _shadow,
                        RotationRadians,
                        Vector2.Zero,
                        1f,
                        Effects,
                        backgroundDepth);

                    _spriteBatch.DrawString(
                        _font,
                        _extra[i],
                        pos,
                        _text,
                        RotationRadians,
                        Vector2.Zero,
                        1f,
                        Effects,
                        LayerDepth);

                    y += _font.LineSpacing;
                }
            }
        }
        #endregion

        #region Methods
        private Vector2 ComputePanelTopLeft(float panelWidth, float panelHeight)
        {
            if (_graphicsDevice == null)
                return _margin; // Fallback: top-left with margin

            var vp = _graphicsDevice.Viewport;

            switch (_screenCorner)
            {
                case ScreenCorner.TopLeft:
                    return new Vector2(
                        _margin.X,
                        _margin.Y);

                case ScreenCorner.TopMiddle:
                    return new Vector2(
                        (vp.Width - panelWidth) * 0.5f,
                        _margin.Y);

                case ScreenCorner.TopRight:
                    return new Vector2(
                        vp.Width - panelWidth - _margin.X,
                        _margin.Y);

                case ScreenCorner.LeftMiddle:
                    return new Vector2(
                        _margin.X,
                        (vp.Height - panelHeight) * 0.5f);

                case ScreenCorner.Center:
                    return new Vector2(
                        (vp.Width - panelWidth) * 0.5f,
                        (vp.Height - panelHeight) * 0.5f);

                case ScreenCorner.RightMiddle:
                    return new Vector2(
                        vp.Width - panelWidth - _margin.X,
                        (vp.Height - panelHeight) * 0.5f);

                case ScreenCorner.BottomLeft:
                    return new Vector2(
                        _margin.X,
                        vp.Height - panelHeight - _margin.Y);

                case ScreenCorner.BottomMiddle:
                    return new Vector2(
                        (vp.Width - panelWidth) * 0.5f,
                        vp.Height - panelHeight - _margin.Y);

                case ScreenCorner.BottomRight:
                default:
                    return new Vector2(
                        vp.Width - panelWidth - _margin.X,
                        vp.Height - panelHeight - _margin.Y);
            }
        }
        #endregion
    }

    public enum ScreenCorner
    {
        TopLeft,
        TopMiddle,
        TopRight,
        LeftMiddle,
        Center,
        RightMiddle,
        BottomLeft,
        BottomMiddle,
        BottomRight
    }
}
*/