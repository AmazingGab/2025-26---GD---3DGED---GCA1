using GDEngine.Core.Entities;
using GDEngine.Core.Rendering;
using GDEngine.Core.Rendering.UI;
using GDEngine.Core.Timing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GDEngine.Core.Managers
{
    /// <summary>
    /// High-level menu controller that creates and manages three UI panels:
    /// 1) Main menu (Play, Audio, Controls, Exit).
    /// 2) Audio menu (Music + SFX sliders, Back).
    /// 3) Controls menu (controls layout texture + Back).
    /// This class is a MonoGame <see cref="GameComponent"/> so it
    /// can live alongside a <c>SceneManager : DrawableGameComponent</c>
    /// and configure a dedicated "menu scene" that is separate from
    /// gameplay scenes.
    /// </summary>
    /// <see cref="Scene"/>
    /// <see cref="UIMenuPanel"/>
    /// <see cref="UIButton"/>
    /// <see cref="UISlider"/>
    /// <see cref="UITexture"/>
    public sealed class MenuManager : GameComponent
    {
        #region Static Fields
        #endregion

        #region Fields
        private Scene? _menuScene;

        private bool _isWin = true;

        public event Action? BackToMenuRequested;

        private UIMenuPanel? _gameOverPanel;
        private UIButton? _playAgainButton;
        private UIButton? _backToMenuButton;


        // Panels
        private UIMenuPanel? _mainMenuPanel;
        private UIMenuPanel? _audioMenuPanel;
        private UIMenuPanel? _controlsMenuPanel;

        // Main menu buttons
        private UIButton? _playButton;
        private UIButton? _audioButton;
        private UIButton? _controlsButton;
        private UIButton? _exitButton;

        // Audio menu controls
        private UIButton? _audioBackButton;
        private UISlider? _musicSlider;
        private UISlider? _sfxSlider;

        // Controls menu controls
        private UIButton? _controlsBackButton;
        private UITexture? _controlsLayoutTexture;

        // Assets
        private Texture2D? _buttonTexture;
        private Texture2D? _sliderTrackTexture;
        private Texture2D? _sliderHandleTexture;
        private Texture2D? _controlsLayout;
        private SpriteFont? _font;
        private Texture2D? _winLogoTexture;

        private bool _configured;
        private bool _built;
        private bool _menuVisible;
        private KeyboardState _newKBState;
        private KeyboardState _oldKBState;
        private SceneManager _sceneManager;
        private Texture2D? _mainPanelBackground;
        private Texture2D? _audioPanelBackground;
        private Texture2D? _controlsPanelBackground;
        private Texture2D? _loseLogoTexture;
        private UITexture? _gameOverLogo;
        #endregion

        #region Properties
        /// <summary>
        /// Returns true if any menu panel is currently visible.
        /// </summary>
        public bool IsMenuVisible
        {
            get { return _menuVisible; }
        }
        /// <summary>
        /// Raised when the user presses the Play button on the main menu.
        /// The game should subscribe and start gameplay / unpause.
        /// </summary>
        public event Action? PlayRequested;

        /// <summary>
        /// Raised when the user presses the Exit button on the main menu.
        /// The game (or a higher-level system) should subscribe and call Game.Exit().
        /// </summary>
        public event Action? ExitRequested;

        /// <summary>
        /// Raised when the Music slider value changes (0-1 by default).
        /// </summary>
        public event Action<float>? MusicVolumeChanged;

        /// <summary>
        /// Raised when the SFX slider value changes (0-1 by default).
        /// </summary>
        public event Action<float>? SfxVolumeChanged;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a <see cref="MenuManager"/> as a MonoGame <see cref="GameComponent"/>.
        /// Add this to <c>Game.Components</c> in your Game subclass.
        /// </summary>
        public MenuManager(Game game, SceneManager sceneManager)
            : base(game)
        {
            _sceneManager = sceneManager;
        }

        public int CurrentScore { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Configure the manager with:
        /// - The dedicated menu <see cref="Scene"/> (separate from gameplay scenes).
        /// - Textures and font for the UI.
        /// 
        /// Once called, the manager builds the three menu panels into the menu scene.
        /// </summary>
        public void Initialize(
         Scene menuScene,
         Texture2D buttonTexture,
         Texture2D sliderTrackTexture,
         Texture2D sliderHandleTexture,
         Texture2D controlsLayoutTexture,
         SpriteFont font,
         Texture2D mainPanelBackground,
         Texture2D audioPanelBackground,
         Texture2D controlsPanelBackground)
        {
            if (menuScene == null)
                throw new ArgumentNullException(nameof(menuScene));
            if (buttonTexture == null)
                throw new ArgumentNullException(nameof(buttonTexture));
            if (sliderTrackTexture == null)
                throw new ArgumentNullException(nameof(sliderTrackTexture));
            if (sliderHandleTexture == null)
                throw new ArgumentNullException(nameof(sliderHandleTexture));
            if (controlsLayoutTexture == null)
                throw new ArgumentNullException(nameof(controlsLayoutTexture));
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (mainPanelBackground == null)
                throw new ArgumentNullException(nameof(mainPanelBackground));
            if (audioPanelBackground == null)
                throw new ArgumentNullException(nameof(audioPanelBackground));
            if (controlsPanelBackground == null)
                throw new ArgumentNullException(nameof(controlsPanelBackground));

            _menuScene = menuScene;
            _buttonTexture = buttonTexture;
            _sliderTrackTexture = sliderTrackTexture;
            _sliderHandleTexture = sliderHandleTexture;
            _controlsLayout = controlsLayoutTexture;
            _font = font;

            _mainPanelBackground = mainPanelBackground;
            _audioPanelBackground = audioPanelBackground;
            _controlsPanelBackground = controlsPanelBackground;

            _configured = true;

            TryBuildMenus();
        }

        /// <summary>
        /// Show the main menu and hide the other panels.
        /// This assumes the menu scene is currently active in the SceneManager.
        /// </summary>
        public void ShowMainMenu()
        {
            if (_mainMenuPanel == null ||
              _audioMenuPanel == null ||
              _controlsMenuPanel == null)
                return;

            if (_gameOverPanel != null)
                _gameOverPanel.IsVisible = false;

            SetActivePanel(_mainMenuPanel, _audioMenuPanel, _controlsMenuPanel);
        }

        /// <summary>
        /// Show the audio menu and hide the other panels.
        /// </summary>
        public void ShowAudioMenu()
        {
            if (_mainMenuPanel == null ||
                _audioMenuPanel == null ||
                _controlsMenuPanel == null)
                return;

            SetActivePanel(_audioMenuPanel, _mainMenuPanel, _controlsMenuPanel);
        }

        /// <summary>
        /// Show the controls menu and hide the other panels.
        /// </summary>
        public void ShowControlsMenu()
        {
            if (_mainMenuPanel == null ||
                _audioMenuPanel == null ||
                _controlsMenuPanel == null)
                return;

            SetActivePanel(_controlsMenuPanel, _mainMenuPanel, _audioMenuPanel);
        }
        public void ShowGameOver(bool won)
        {
            _isWin = won;
            UpdateGameOverLogo();
            ShowGameOverScreen();
        }
        public void ShowGameOverScreen()
        {
            if (_gameOverPanel == null)
                return;

            if (_mainMenuPanel != null)
                _mainMenuPanel.IsVisible = false;

            if (_audioMenuPanel != null)
                _audioMenuPanel.IsVisible = false;

            if (_controlsMenuPanel != null)
                _controlsMenuPanel.IsVisible = false;

            _gameOverPanel.IsVisible = true;

            _menuVisible = true;
        }
        public void HideGameOver()
        {
            if (_gameOverPanel == null) return;
            _gameOverPanel.IsVisible = false;
        }
        private void HideMenuBackgrounds()
        {
            if (_menuScene == null)
                return;

            foreach (var go in _menuScene.GameObjects)
            {
                var ui = go.GetComponent<UITexture>();
                if (ui != null)
                    ui.Enabled = false;
            }
        }
        private void TryBuildMenus()
        {
            if (_built)
                return;

            if (!_configured)
                return;

            if (_menuScene == null)
                return;

            if (_buttonTexture == null ||
                _sliderTrackTexture == null ||
                _sliderHandleTexture == null ||
                _controlsLayout == null ||
                _font == null)
                return;

            BuildPanels(_menuScene);
            _built = true;

            ShowMainMenu();
        }
        //public void SetFinalScore(int value)
        //{
        //    _finalScore = value;
        //}
        public void SetWinLogo(Texture2D tex)
        {
            _winLogoTexture = tex;
        }
        public void ApplyMainButtonImages(
         Texture2D playImage,
         Texture2D audioImage,
         Texture2D controlsImage,
         Texture2D exitImage)
        {

            void SwapButtonImage(UIButton? button, Texture2D image)
            {

                var go = button?.GameObject;

                var graphic = go?.GetComponent<UITexture>();

                graphic.Texture = image;
                graphic.Size = new Vector2(image.Width, image.Height);
                button.Size = graphic.Size;


                var label = go.GetComponent<UIText>();
                label.Enabled = false;
            }

            SwapButtonImage(_playButton, playImage);
            SwapButtonImage(_audioButton, audioImage);
            SwapButtonImage(_controlsButton, controlsImage);
            SwapButtonImage(_exitButton, exitImage);
        }
        private void BuildPanels(Scene scene)
        {
            int backBufferWidth = Game.GraphicsDevice.PresentationParameters.BackBufferWidth;
            int backBufferHeight = Game.GraphicsDevice.PresentationParameters.BackBufferHeight;
            Vector2 viewportSize = new Vector2(backBufferWidth, backBufferHeight);

            // Basic layout: top-left-ish anchor + consistent item size
            Vector2 panelPosition = new Vector2(100f, 180f); ;
            Vector2 itemSize = new Vector2(390, 100f);
            float spacing = 20f;

            // Main menu panel
            GameObject mainRoot = new GameObject("UI_MainMenuPanel");
            scene.Add(mainRoot);

            _mainMenuPanel = mainRoot.AddComponent<UIMenuPanel>();
            _mainMenuPanel.PanelPosition = panelPosition;
            _mainMenuPanel.ItemSize = itemSize;
            _mainMenuPanel.VerticalSpacing = spacing;
            _mainMenuPanel.IsVisible = true;

            if (_mainPanelBackground != null)
            {
                GameObject mainBgRoot = new GameObject("UI_MainMenuBackground");
                scene.Add(mainBgRoot);
                mainBgRoot.Transform.SetParent(_mainMenuPanel.Transform);

                var mainBg = mainBgRoot.AddComponent<UITexture>();
                mainBg.Texture = _mainPanelBackground;
                mainBg.Size = viewportSize;        // cover screen
                mainBg.Position = Vector2.Zero;
                mainBg.Tint = Color.White;
                mainBg.LayerDepth = UILayer.MenuBack;  // above global dim, below buttons
            }

            _playButton = _mainMenuPanel.AddButton(
                "",
                _buttonTexture!,
                _font!,
                OnPlayClicked);

            _audioButton = _mainMenuPanel.AddButton(
                "",
                _buttonTexture!,
                _font!,
                OnAudioClicked);

            _controlsButton = _mainMenuPanel.AddButton(
                "",
                _buttonTexture!,
                _font!,
                OnControlsClicked);

            _exitButton = _mainMenuPanel.AddButton(
        "",
        _buttonTexture!,
        _font!,
        OnExitClicked);

            // Tell the main panel to scan its hierarchy and register
            // all UITexture/UISelectable children (including the background).
            _mainMenuPanel.RefreshChildren();

            // -----------------------------------------------------------------
            // Audio menu panel
            // -----------------------------------------------------------------
            GameObject audioRoot = new GameObject("UI_AudioMenuPanel");
            scene.Add(audioRoot);

            _audioMenuPanel = audioRoot.AddComponent<UIMenuPanel>();
            _audioMenuPanel.PanelPosition = panelPosition;
            _audioMenuPanel.ItemSize = itemSize;
            _audioMenuPanel.VerticalSpacing = spacing;
            _audioMenuPanel.IsVisible = false;

            if (_audioPanelBackground != null)
            {
                GameObject audioBgRoot = new GameObject("UI_AudioMenuBackground");
                scene.Add(audioBgRoot);
                audioBgRoot.Transform.SetParent(_audioMenuPanel.Transform);

                var audioBg = audioBgRoot.AddComponent<UITexture>();
                audioBg.Texture = _audioPanelBackground;
                audioBg.Size = viewportSize;
                audioBg.Position = Vector2.Zero;
                audioBg.Tint = Color.White;
                audioBg.LayerDepth = UILayer.MenuBack;
            }

            _musicSlider = _audioMenuPanel.AddSlider(
                "Music",
                _sliderTrackTexture!,
                _sliderHandleTexture!,
                _font!,
                0f,
                1f,
                0.8f,
                OnMusicSliderChanged);

            _sfxSlider = _audioMenuPanel.AddSlider(
                "SFX",
                _sliderTrackTexture!,
                _sliderHandleTexture!,
                _font!,
                0f,
                1f,
                0.8f,
                OnSfxSliderChanged);

            _audioBackButton = _audioMenuPanel.AddButton(
                "",
                _buttonTexture!,
                _font!,
                OnBackToMainFromAudio);

            // Register audio panel children (including its background)
            _audioMenuPanel.RefreshChildren();

            // -----------------------------------------------------------------
            // Controls menu panel
            // -----------------------------------------------------------------
            GameObject controlsRoot = new GameObject("UI_ControlsMenuPanel");
            scene.Add(controlsRoot);

            _controlsMenuPanel = controlsRoot.AddComponent<UIMenuPanel>();
            _controlsMenuPanel.PanelPosition = panelPosition;
            _controlsMenuPanel.ItemSize = itemSize;
            _controlsMenuPanel.VerticalSpacing = spacing;
            _controlsMenuPanel.IsVisible = false;

            if (_controlsPanelBackground != null)
            {
                GameObject controlsBgRoot = new GameObject("UI_ControlsMenuBackground");
                scene.Add(controlsBgRoot);
                controlsBgRoot.Transform.SetParent(_controlsMenuPanel.Transform);

                var controlsBg = controlsBgRoot.AddComponent<UITexture>();
                controlsBg.Texture = _controlsPanelBackground;
                controlsBg.Size = viewportSize;
                controlsBg.Position = Vector2.Zero;
                controlsBg.Tint = Color.White;
                controlsBg.LayerDepth = UILayer.MenuBack;
            }

            _controlsBackButton = _controlsMenuPanel.AddButton(
                "",
                _buttonTexture!,
                _font!,
                OnBackToMainFromControls);
            //End screen

            GameObject gameOverRoot = new GameObject("UI_GameOverPanel");
            scene.Add(gameOverRoot);

            _gameOverPanel = gameOverRoot.AddComponent<UIMenuPanel>();
            _gameOverPanel = gameOverRoot.AddComponent<UIMenuPanel>();

            _gameOverPanel.PanelPosition = new Vector2(
                backBufferWidth * 0.5f - itemSize.X * 0.5f,
                backBufferHeight * 0.60f
            );


            _gameOverPanel.VerticalSpacing = 12f;
            _gameOverPanel.IsVisible = false;


            if (_mainPanelBackground != null)
            {
                GameObject bgRoot = new GameObject("UI_GameOverBackground");
                scene.Add(bgRoot);
                bgRoot.Transform.SetParent(_gameOverPanel.Transform);

                var bg = bgRoot.AddComponent<UITexture>();
                bg.Texture = _mainPanelBackground;
                bg.Size = viewportSize;
                bg.LayerDepth = UILayer.MenuBack;
            }


            Texture2D logoToUse = null;

            if (_isWin)
            {
                if (_winLogoTexture != null)
                {
                    logoToUse = _winLogoTexture;
                }
            }
            else
            {
                if (_loseLogoTexture != null)
                {
                    logoToUse = _loseLogoTexture;
                }
            }

            if (logoToUse != null)
            {
                var logoGO = new GameObject("GameOverLogo");
                scene.Add(logoGO);
                logoGO.Transform.SetParent(_gameOverPanel.Transform);

                _gameOverLogo = logoGO.AddComponent<UITexture>();
                _gameOverLogo.LayerDepth = UILayer.Menu;

                float scale = 0.55f;

                _gameOverLogo.Size = new Vector2(
                    logoToUse.Width * scale,
                    logoToUse.Height * scale);

                _gameOverLogo.LayerDepth = UILayer.Menu;
            }
            // Already present, keep it
            _gameOverPanel.RefreshChildren();

            _playAgainButton = _gameOverPanel.AddButton(
                "",
               _buttonTexture!,
               _font!,
               OnPlayAgainClicked);
            _backToMenuButton = _gameOverPanel.AddButton(
               "",
              _buttonTexture!,
              _font!,
              OnBackToMenuFromGameOver);


        }
        public void ApplyBackButtonImages(Texture2D audioBackImage,
                                  Texture2D controlsBackImage)
        {
            void SwapButtonImage(UIButton? button, Texture2D image)
            {
                var go = button?.GameObject;

                var graphic = go.GetComponent<UITexture>();

                graphic.Texture = image;
                graphic.Size = new Vector2(image.Width, image.Height);

                button.Size = graphic.Size;

                var label = go.GetComponent<UIText>();
                if (label != null)
                    label.Enabled = false;
            }

            SwapButtonImage(_audioBackButton, audioBackImage);
            SwapButtonImage(_controlsBackButton, controlsBackImage);
        }
        public void ApplyGameOverButtonImages(Texture2D playAgainImage, Texture2D backImage)
        {
            void Swap(UIButton? button, Texture2D image)
            {

                var go = button.GameObject;
                var graphic = go.GetComponent<UITexture>();
                graphic.Texture = image;
                graphic.Size = new Vector2(image.Width, image.Height);

                float buttonScale = 1.08f;
                graphic.Size = new Vector2(
                    image.Width * buttonScale,
                    image.Height * buttonScale);

                button.Size = graphic.Size;

                var label = go.GetComponent<UIText>();
                if (label != null)
                    label.Enabled = false;
            }

            Swap(_playAgainButton, playAgainImage);
            Swap(_backToMenuButton, backImage);
        }
        /// <summary>
        /// Show the full menu (background + main menu).
        /// Use this when opening the menu from the game (e.g. Esc or on startup).
        /// </summary>
        /// 
        private void UpdateGameOverLogo()
        {
            int backBufferWidth = Game.GraphicsDevice.PresentationParameters.BackBufferWidth;
            int backBufferHeight = Game.GraphicsDevice.PresentationParameters.BackBufferHeight;

            if (_gameOverLogo == null)
                return;

            Texture2D tex = null;

            if (_isWin)
            {
                if (_winLogoTexture != null)
                {
                    tex = _winLogoTexture;
                }
            }
            else
            {
                if (_loseLogoTexture != null)
                {
                    tex = _loseLogoTexture;
                }
            }

            if (tex == null)
                return;

            float scale = 0.55f;

            _gameOverLogo.Texture = tex;
            _gameOverLogo.Size = new Vector2(
                tex.Width * scale,
                tex.Height * scale);

            float centerX = backBufferWidth * 0.30f;
            float logoY = backBufferHeight * 0.15f;

            _gameOverLogo.Position = new Vector2(centerX, logoY);  
        }

        public void ShowMenuRoot()
        {
            _menuVisible = true;

            ShowMainMenu();
        }
        private void OnBackToMenuFromGameOver()
        {
            ShowMainMenu();
        }
        private void OnPlayAgainClicked()
        {
            PlayRequested?.Invoke();
        }

        /// <summary>
        /// Hides all menu panels and the background.
        /// Use this when resuming gameplay (Play button, Esc to close).
        /// </summary>
        public void HideMenus()
        {

            _menuVisible = false;

            if (_mainMenuPanel != null)
                _mainMenuPanel.IsVisible = false;

            if (_audioMenuPanel != null)
                _audioMenuPanel.IsVisible = false;

            if (_controlsMenuPanel != null)
                _controlsMenuPanel.IsVisible = false;

            if (_gameOverPanel != null)
                _gameOverPanel.IsVisible = false;

            HideMenuBackgrounds();

        }

        private void SetActivePanel(UIMenuPanel toShow, UIMenuPanel toHideA, UIMenuPanel toHideB)
        {
            toShow.IsVisible = true;
            toHideA.IsVisible = false;
            toHideB.IsVisible = false;

            _menuVisible = true;
        }
        public void SetLoseLogo(Texture2D tex)
        {
            _loseLogoTexture = tex;
        }


        private void OnPlayClicked()
        {
            PlayRequested?.Invoke();
        }

        private void OnAudioClicked()
        {
            ShowAudioMenu();
        }

        private void OnControlsClicked()
        {
            ShowControlsMenu();
        }

        private void OnExitClicked()
        {
            ExitRequested?.Invoke();
        }

        private void OnBackToMainFromAudio()
        {
            ShowMainMenu();
        }

        private void OnBackToMainFromControls()
        {
            ShowMainMenu();
        }

        private void OnMusicSliderChanged(float value)
        {
            MusicVolumeChanged?.Invoke(value);
        }

        private void OnSfxSliderChanged(float value)
        {
            SfxVolumeChanged?.Invoke(value);
        }
        #endregion

        #region Lifecycle Methods
        public override void Update(GameTime gameTime)
        {
            // Get new state
            _newKBState = Keyboard.GetState();

            // As a manager, this does not drive any Scene updates itself.
            // The DrawableGameComponent SceneManager should be responsible
            // for calling menuScene.Update/Draw and choosing which scene
            // is currently active.
            if (!_built && _configured)
                TryBuildMenus();

            ShowHideMenu();

            // Store old state (allows us to do was pressed type checks)
            _oldKBState = _newKBState;

            base.Update(gameTime);
        }

        private void ShowHideMenu()
        {
            if (_newKBState.IsKeyDown(Keys.Escape) && !_oldKBState.IsKeyDown(Keys.Escape))
            {
                if (IsMenuVisible)
                {
                    Time.Resume();
                    _sceneManager.Paused = false;
                    HideMenus();
                }
                else
                {
                    Time.TogglePause();
                    _sceneManager.Paused = true;
                    ShowMenuRoot();
                }
            }
        }
        #endregion

        #region Housekeeping Methods
        public override string ToString()
        {
            return "UIManager(MenuScene=" + (_menuScene?.Name ?? "null") + ", Built=" + (_built ? "true" : "false") + ")";
        }
        #endregion
    }
}