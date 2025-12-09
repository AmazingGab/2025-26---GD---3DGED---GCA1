using GDEngine.Core;
using GDEngine.Core.Audio;
using GDEngine.Core.Collections;
using GDEngine.Core.Components;
using GDEngine.Core.Debug;
using GDEngine.Core.Entities;
using GDEngine.Core.Events;
using GDEngine.Core.Factories;
using GDEngine.Core.Gameplay;
using GDEngine.Core.Impulses;
using GDEngine.Core.Input.Data;
using GDEngine.Core.Input.Devices;
using GDEngine.Core.Managers;
using GDEngine.Core.Orchestration;
using GDEngine.Core.Rendering;
using GDEngine.Core.Rendering.Base;
using GDEngine.Core.Rendering.UI;
using GDEngine.Core.Screen;
using GDEngine.Core.Serialization;
using GDEngine.Core.Services;
using GDEngine.Core.Systems;
using GDEngine.Core.Timing;
using GDEngine.Core.Utilities;
using GDGame.Demos.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;
using Color = Microsoft.Xna.Framework.Color;

namespace GDGame
{
    public class Main : Game
    {
        #region Core Fields (Common to all games)     
        private GraphicsDeviceManager _graphics;
        private ContentDictionary<Texture2D> _textureDictionary;
        private ContentDictionary<Model> _modelDictionary;
        private ContentDictionary<SpriteFont> _fontDictionary;
        private ContentDictionary<SoundEffect> _soundDictionary;
        private ContentDictionary<Effect> _effectsDictionary;
        private bool _disposed = false;
        private Material _matBasicUnlit, _matBasicLit, _matAlphaCutout, _matBasicUnlitGround;

        public event Action? PlayAgainRequested;
        public event Action? BackToMenuRequested;
        #endregion

        #region Demo Fields (remove in the game)
        private AnimationCurve3D _animationPositionCurve, _animationRotationCurve;
        private AnimationCurve _animationCurve;
        private KeyboardState _newKBState, _oldKBState;
        private int _damageAmount;
        private MouseState _oldMouseState;
        private MouseState _newMouseState;
        public int score;
        private bool isRoach;
        private bool _menuVisible = true;
        private bool _taskUiCreated = false;
        private bool _lastMenuVisible = false;
        private GameObject _menuLogoGO;
        private GameObject _taskBarGO;
        private GameObject uiReticleGO;

        // Simple debug subscription for collision events
        private IDisposable _collisionSubscription;

        // LayerMask used to filter which collisions we care about in debug
        private LayerMask _collisionDebugMask = LayerMask.All;
        private UIMenuPanel _mainMenuPanel, _audioMenuPanel;
        private SceneManager _sceneManager;
        private float _currentHealth = 100;
        private MenuManager _menuManager;
        private bool hasSpatula;
        private KeyboardState _newKBState2;
        private KeyboardState _oldKBState2;
        private float timeLeft;
        private bool roachKilled = false;

        private GameObject _dialogueGO;
        private UIText _dialogueText;
        private bool _isDialogueOpen = false;
        #endregion

        #region Core Methods (Common to all games)     
        public Main()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            #region Core
            Window.Title = "My Amazing Game";
            InitializeGraphics(ScreenResolution.R_WXGA_16_10_1280x800);
            InitializeMouse();
            InitializeContext();

            var relativeFilePathAndName = "assets/data/asset_manifest.json";
            LoadAssetsFromJSON(relativeFilePathAndName);
            InitializeEffects();

            // Game component that exists outside scene to manage and swap scenes
            InitializeSceneManager();

            // Create the scene and register it
            InitializeScene();

            // Safe to use _sceneManager.ActiveScene from here on
            InitializeSystems();
            InitializeCameras();
            InitializeCameraManagers();

            int scale = 500;
            InitializeSkyParent();
            InitializeSkyBox(scale);
            InitializeCollidableGround(scale);
            //InitializePlayer();
            #endregion

            #region Demos

            #region Animation curves
            // Camera-demos
            InitializeAnimationCurves();
            #endregion

            #region Collidables
            // Demo event listeners on collision
            InitializeCollisionEventListener();

            // Collidable game object demos
            //DemoCollidablePrimitive(new Vector3(0, 20, 5.1f), Vector3.One * 6, new Vector3(15, 45, 45));
            //DemoCollidablePrimitive(new Vector3(0, 10, 5.2f), Vector3.One * 1, new Vector3(45, 0, 0));
            //DemoCollidablePrimitive(new Vector3(0, 5, 5.3f), Vector3.One * 1, new Vector3(0, 0, 45));
            //DemoCollidableModel(new Vector3(0, 50, 10), Vector3.Zero, new Vector3(2, 1.25f, 2));
            //DemoCollidableModel(new Vector3(0, 40, 11), Vector3.Zero, new Vector3(2, 1.25f, 2));
            //DemoCollidableModel(new Vector3(0, 25, 12), Vector3.Zero, new Vector3(2, 1.25f, 2));
            #endregion

            DemoCollidableModel(new Vector3(0, 1, 10), new Vector3(-90, 0, 0), new Vector3(1.5f, 0.5f, 0.2f), false, "mainRoach");
            for (int i = 0; i < 40; i++)
            {
                Random rng = new Random();
                DemoCollidableModel(new Vector3(rng.Next(-20, 170), 1, rng.Next(-60, 50)), new Vector3(-90, 0, 0), new Vector3(1.5f, 0.5f, 0.2f), true, "roach");
            }

            DemoCollidableSpatula(new Vector3(8, 0.5f, 12), new Vector3(0, 0, 180), new Vector3(0.3f, 0.3f, .7f));
            DemoCameraParent(new Vector3(0, 0, 0), new Vector3(90, 0, 0), new Vector3(0.3f, 1f, 1f));


            DemoCollidableMap(new Vector3(80, 0, 0), new Vector3(-90, 0, 0), new Vector3(100, 55, 5));
            DemoLoadFromJSON();

           
            #endregion

            #region Loading GameObjects from JSON
            DemoLoadFromJSON();
            #endregion

        
            #endregion

            // Mouse reticle
            InitializeUI();

            // Main menu
            InitializeMenuManager();

            // Set the active scene
            _sceneManager.SetActiveScene(AppData.LEVEL_1_NAME);

            // Set win/lose conditions
            SetWinConditions();

            // Set pause and show menu
            SetPauseShowMenu();

            base.Initialize();
        }

        private void SetPauseShowMenu()
        {
            // Give scenemanager the events reference so that it can publish the pause event
            _sceneManager.EventBus = EngineContext.Instance.Events;
            // Set paused and publish pause event
            _sceneManager.Paused = true;

            // Put all components that should be paused to sleep
            EngineContext.Instance.Events.Subscribe<GamePauseChangedEvent>(e =>
            {
                bool paused = e.IsPaused;

                _sceneManager.ActiveScene.GetSystem<PhysicsSystem>()?.SetPaused(paused);
                _sceneManager.ActiveScene.GetSystem<PhysicsDebugSystem>()?.SetPaused(paused);
                _sceneManager.ActiveScene.GetSystem<GameStateSystem>()?.SetPaused(paused);
            });
        }

        private void InitializeSceneManager()
        {
            _sceneManager = new SceneManager(this);
            Components.Add(_sceneManager);
        }

        private void InitializeCameraManagers()
        {
            //inside scene
            var go = new GameObject("Camera Manager");
            go.AddComponent<CameraEventListener>();
            _sceneManager.ActiveScene.Add(go);
        }

        private void InitializeMenuManager()
        {
            _menuManager = new MenuManager(this, _sceneManager);
            Components.Add(_menuManager);
            Texture2D logoTex = _textureDictionary.Get("Logo");
            Texture2D buttonTex = _textureDictionary.Get("play_button");
            Texture2D sliderTrackTex = _textureDictionary.Get("slider_track");
            Texture2D sliderHandleTex = _textureDictionary.Get("slider_handle");
            Texture2D controlsLayoutTex = _textureDictionary.Get("main_menu_background");
            SpriteFont uiFont = _fontDictionary.Get("menufont");
            Texture2D mainBg = _textureDictionary.Get("main_menu_background");
            Texture2D audioBg = _textureDictionary.Get("main_menu_background");
            Texture2D controlsBg = _textureDictionary.Get("main_menu_background");

            Texture2D winLogo = _textureDictionary.Get("win_screen_logo");
            Texture2D loseLogo = _textureDictionary.Get("lose_screen_logo");
            _menuManager.SetLoseLogo(loseLogo);
            _menuManager.SetWinLogo(winLogo);
            _menuManager.Initialize(
                _sceneManager.ActiveScene,
                buttonTex,
                sliderTrackTex,
                sliderHandleTex,
                controlsLayoutTex,
                uiFont,
                mainBg,
                audioBg,
                controlsBg
            );
            InitializeMenuLogo();

            _menuManager.ApplyMainButtonImages(
                _textureDictionary.Get("play_button"),
                _textureDictionary.Get("options_button"),
                _textureDictionary.Get("credits_button"),
                _textureDictionary.Get("exit_button"));
            _menuManager.ApplyBackButtonImages(
                _textureDictionary.Get("back_button"),
                _textureDictionary.Get("back_button"));
            _menuManager.ApplyGameOverButtonImages(
                _textureDictionary.Get("play_again_button"),
                _textureDictionary.Get("back_to_menu_button"));


            _menuManager.PlayRequested += () =>
            {
                InitializeTaskUI();
                InitializeUIReticleRenderer();
                _sceneManager.Paused = true;
                _menuManager.HideMenus();

                SetMenuLogoVisible(false);
                SetTaskBarVisible(true);
                SetReticleoVisible(false);

                IsMouseVisible = true;
                ShowDialogue("EW... THERE IS A ROACH! I NEED\nA SPATULA BY PRESSING 'E' AND\nTHEN SQUASH IT!");
            };

            _menuManager.ExitRequested += () =>
            {
                Exit();
            };

            _menuManager.MusicVolumeChanged += v =>
            {
                System.Diagnostics.Debug.WriteLine("MusicVolumeChanged: " + v);
            };

            _menuManager.SfxVolumeChanged += v =>
            {
                System.Diagnostics.Debug.WriteLine("SfxVolumeChanged: " + v);
            };

            //_menuManager.ShowGameOver();
            //_sceneManager.Paused = true;
            //IsMouseVisible = true;

            //_menuManager.PlayAgainRequested += () =>
            //{
            //    RestartLevel();
            //    _menuManager.HideGameOver();
            //    _sceneManager.Paused = false;
            //};

            //_menuManager.BackToMenuRequested += () =>
            //{
            //    _menuManager.HideGameOver();
            //    _menuManager.ShowMainMenu();
            //};

            _sceneManager.Paused = true;
            _menuManager.ShowMainMenu();

            IsMouseVisible = true;
            SetTaskBarVisible(false);
            SetMenuLogoVisible(true);
            SetReticleoVisible(false);


        }

        private GameObject CreateImageButton(string textureKey, Vector2 centerPosition, Action onClick)
        {

            var go = new GameObject($"Button_{textureKey}");
            _sceneManager.ActiveScene.Add(go);

            Texture2D tex = _textureDictionary.Get(textureKey);

            var graphic = go.AddComponent<UITexture>();
            graphic.Texture = tex;

            graphic.Size = new Vector2(tex.Width, tex.Height);

            Vector2 topLeft = centerPosition - (graphic.Size * 0.5f);
            graphic.Position = topLeft;

            graphic.Tint = Color.White;
            graphic.LayerDepth = UILayer.Menu;

            var button = go.AddComponent<UIButton>();

            button.TargetGraphic = graphic;
            button.AutoSizeFromTargetGraphic = false;

            button.Position = topLeft;
            button.Size = graphic.Size;


            button.NormalColor = Color.White;
            button.HighlightedColor = Color.LightGray;
            button.PressedColor = Color.Gray;
            button.DisabledColor = Color.DarkGray;


            button.Clicked += onClick;


            button.PointerEntered += () => graphic.Tint = Color.White;
            button.PointerExited += () => graphic.Tint = Color.White;
            button.PointerDown += () => graphic.Tint = Color.LightGray;
            button.PointerUp += () => graphic.Tint = Color.White;

            return go;
        }

        private void InitializeMenuLogo()
        {
            var logoTex = _textureDictionary.Get("Logo");

            _menuLogoGO = new GameObject("MenuLogo");
            var logoUI = _menuLogoGO.AddComponent<UITexture>();
            logoUI.Texture = logoTex;

            var nativeSize = new Vector2(logoTex.Width, logoTex.Height);
            var size = nativeSize * 1f;
            logoUI.Size = size;

            int screenW = _graphics.PreferredBackBufferWidth;
            logoUI.Position = new Vector2(screenW - size.X - 45f, 10f);
            logoUI.LayerDepth = UILayer.Menu;

            _sceneManager.ActiveScene.Add(_menuLogoGO);
        }

        private void InitializeTaskUI()
        {
            if (_taskUiCreated)
                return;

            _taskUiCreated = true;

            _taskBarGO = new GameObject("TaskBar");
            var taskBarTexture = _textureDictionary.Get("task_bar");
            var bg = _taskBarGO.AddComponent<UITexture>();
            bg.Texture = taskBarTexture;
            bg.Anchor = TextAnchor.TopLeft;
            bg.Position = new Vector2(20f, 20f);
            bg.Scale = Vector2.One;
            bg.LayerDepth = UILayer.HUD;
            var kidsBusFont = _fontDictionary.Get("KidsBus");
            var bodyText = _taskBarGO.AddComponent<UIText>();
            bodyText.Font = kidsBusFont;
            bodyText.Anchor = TextAnchor.TopLeft;
            bodyText.PositionProvider = () => new Vector2(43f, 82f);
            bodyText.FallbackColor = new Color(72, 59, 32);
            bodyText.LayerDepth = UILayer.Menu;
            bodyText.DropShadow = false;
            bodyText.TextProvider = () => "SQUASH THEM ALL!";
            _sceneManager.ActiveScene.Add(_taskBarGO);
        }
        private void OnPlayAgainClicked() => PlayAgainRequested?.Invoke();
        private void OnBackToMenuFromGameOver() => BackToMenuRequested?.Invoke();

        private void InitializeDialogueUI()
        {

            _dialogueGO = new GameObject("DialogueWindow");

            var bgTex = _textureDictionary.Get("dialugue_ui");

            var textureComp = _dialogueGO.AddComponent<UITexture>();
            textureComp.Texture = bgTex;
            textureComp.Size = new Vector2(bgTex.Width, bgTex.Height);

            int screenW = _graphics.PreferredBackBufferWidth;
            int screenH = _graphics.PreferredBackBufferHeight;

            float xPos = (screenW - bgTex.Width) / 2;

            float yPos = screenH - bgTex.Height - 50;

            textureComp.Position = new Vector2(xPos, yPos);

            textureComp.LayerDepth = 0.1f;

            _dialogueText = _dialogueGO.AddComponent<UIText>();
            _dialogueText.Font = _fontDictionary.Get("KidsBus");
            _dialogueText.FallbackColor = new Color(72, 59, 32);

            _dialogueText.PositionProvider = () => textureComp.Position + new Vector2(40, 25);
            _dialogueText.LayerDepth = 0.05f;

            var btn = _dialogueGO.AddComponent<UIButton>();
            btn.TargetGraphic = textureComp;
            btn.Size = textureComp.Size;
            btn.Position = textureComp.Position; 

            btn.Clicked += () =>
            {
                CloseDialogue();
            };

            _sceneManager.ActiveScene.Add(_dialogueGO);
            SetDialogueVisible(false);
        }

        private void SetDialogueVisible(bool visible)
        {
            if (_dialogueGO == null) return;

            foreach (var renderable in _dialogueGO.GetComponents<UIRenderer>())
            {
                renderable.Enabled = visible;
            }

            var btn = _dialogueGO.GetComponent<UIButton>();
            if (btn != null) btn.Enabled = visible;
        }
        private void ShowDialogue(string message)
        {
            if (_dialogueText != null)
            {
                _dialogueText.TextProvider = () => message;

                SetDialogueVisible(true);
                _isDialogueOpen = true;
                _sceneManager.Paused = true;
               
                SetReticleoVisible(false);
            }
        }

        private void CloseDialogue()
        {
            SetDialogueVisible(false);
            _isDialogueOpen = false;
            _sceneManager.Paused = false;
            IsMouseVisible = false;
            SetReticleoVisible(true);
        }
        private void InitializeUISystems()
        {
            var uiEventSystem = new UIEventSystem();
            _sceneManager.ActiveScene.Add(uiEventSystem);
        }
        private void SetTaskBarVisible(bool visible)
        {
            if (_taskBarGO == null) return;

            foreach (var ui in _taskBarGO.GetComponents<UIRenderer>())
                ui.Enabled = visible;
        }
        private void SetMenuLogoVisible(bool visible)
        {
            if (_menuLogoGO == null) return;

            foreach (var ui in _menuLogoGO.GetComponents<UIRenderer>())
                ui.Enabled = visible;
        }
        private void SetReticleoVisible(bool visible)
        {
            if (uiReticleGO == null) return;

            foreach (var ui in uiReticleGO.GetComponents<UIRenderer>())
                ui.Enabled = visible;
        }

        private void InitializeCollidableGround(int scale = 500)
        {
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            gameObject = new GameObject("ground");
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            meshFilter = MeshFilterFactory.CreateQuadGridTexturedUnlit(_graphics.GraphicsDevice,
                 1,
                 1,
                 1,
                 1,
                 20,
                 20);

            gameObject.Transform.ScaleBy(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0), true);
            gameObject.Transform.TranslateTo(new Vector3(0, -0.5f, 0));

            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlitGround;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("floor");

            // Add a box collider matching the ground size
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.Size = new Vector3(scale, scale, 0.025f);
            collider.Center = new Vector3(0, 0, -0.0125f);

            // Add rigidbody as Static (immovable)
            var rigidBody = gameObject.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            gameObject.IsStatic = true;

            _sceneManager.ActiveScene.Add(gameObject);
        }

        private void DemoCollidableMap(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale)
        {
            var go = new GameObject("map");
            go.Transform.TranslateTo(position);
            go.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            go.Transform.ScaleTo(scale);

            var model = _modelDictionary.Get("walls");
            var texture = _textureDictionary.Get("wallpaper");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _sceneManager.ActiveScene.Add(go);


            // Add box collider (1x1x1 cube)
            var collider = go.AddComponent<BoxCollider>();
            collider.Size = scale; // Collider is FULL size
            collider.Center = new Vector3(0, 0, 0);

            // Add rigidbody (Dynamic so it falls)

            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            rigidBody.Mass = 1.0f;

        }

        private void KillRoach(GameObject roach)
        {
            var events = EngineContext.Instance.Events;
            // List<GameObject> roaches = _scene.FindAll((GameObject go) => go.Name.Equals("test crate textured cube"));
            //var cameraObject = _scene.Find(go => go.Name.Equals(AppData.CAMERA_NAME_FIRST_PERSON));
            var spatula = _sceneManager.ActiveScene.Find(go => go.Name.Equals("spatula"));
            bool togglePressed = _newMouseState.LeftButton == ButtonState.Pressed && _oldMouseState.LeftButton == ButtonState.Released;
            if (togglePressed && hasSpatula)
            {
                if (roach.Name == "mainRoach")
                {
                    List<GameObject> roaches = _sceneManager.ActiveScene.FindAll((GameObject go) => go.Name.Equals("roach"));
                    foreach (var r in roaches)
                    {
                        r.Enabled = true;
                    }
                    ShowDialogue("THERE ARE SO MANY OF THEM! \nI NEED TO SQUASH THEM ALL!");
                }    
                //foreach (var roach in roaches)
                //{

                //var distToWaypoint = Vector3.Distance(cameraObject.Transform.Position, roach.Transform.Position);
                //if (roach != null && distToWaypoint < 10 && isRoach)
                //{
                _sceneManager.ActiveScene.Remove(roach);
                events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1",
            1, false, null));
                score += 100;
                if (!roachKilled)
                    spatula.Transform.RotateBy(Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(-10)), worldSpace: false);
                roachKilled = true;

            }
        }
        private void AddSpatula(GameObject spatula)
        {
            var events = EngineContext.Instance.Events;
            // List<GameObject> roaches = _scene.FindAll((GameObject go) => go.Name.Equals("test crate textured cube"));
            var cameraGO = _sceneManager.ActiveScene.Find(go => go.Name.Equals(AppData.CAMERA_NAME_FIRST_PERSON));
            bool togglePressed = _newKBState2.IsKeyDown(Keys.E) && !_oldKBState2.IsKeyDown(Keys.E);
            System.Diagnostics.Debug.WriteLine(togglePressed);
            if (togglePressed)
            {

                //foreach (var roach in roaches)
                //{

                //var distToWaypoint = Vector3.Distance(cameraObject.Transform.Position, roach.Transform.Position);
                //if (roach != null && distToWaypoint < 10 && isRoach)
                //{
                _sceneManager.ActiveScene.Remove(spatula);
                events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1",
            1, false, null));
                GameObject playerSpatula = InitializeModel(new Vector3(2, -6, 0),
               new Vector3(45, 0, 0),
               new Vector3(0.3f, 0.3f, 1), "spatula", "spatula", "spatula");

                playerSpatula.Transform.SetParent(cameraGO);
                hasSpatula = true;
                //score += 1000;
                //break;
                //}
                //}

            }
        }

        private void InitializePlayer()
        {
            GameObject player = InitializeModel(new Vector3(0, 5, 10),
                new Vector3(0, 0, 0),
                2 * Vector3.One, "crate1", "monkey1", AppData.PLAYER_NAME);

            var simpleDriveController = new SimpleDriveController();
            player.AddComponent(simpleDriveController);

            // Listen for damage events on the player
            player.AddComponent<DamageEventListener>();

            // Adds an inventory to the player
            player.AddComponent<InventoryComponent>();
        }

        private void InitializePIPCamera(Vector3 position,
      Viewport viewport, int depth, int index = 0)
        {
            var pipCameraGO = new GameObject("PIP camera");
            pipCameraGO.Transform.TranslateTo(position);
            pipCameraGO.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(-90), 0));

            //if (index == 0)
            //{
            //    pipCameraGO.AddComponent<KeyboardWASDController>();
            //    pipCameraGO.AddComponent<MouseYawPitchController>();
            //}

            var camera = pipCameraGO.AddComponent<Camera>();
            camera.StackRole = Camera.StackType.Overlay;
            camera.ClearFlags = Camera.ClearFlagsType.DepthOnly;
            camera.Depth = depth; //-100

            camera.Viewport = viewport; // new Viewport(0, 0, 400, 300);

            _sceneManager.ActiveScene.Add(pipCameraGO);
        }

        private void InitializeAnimationCurves()
        {
            //1D animation curve demo (e.g. scale, audio volume, lerp factor for color, etc)
            _animationCurve = new AnimationCurve(CurveLoopType.Cycle);
            _animationCurve.AddKey(0f, 10);
            _animationCurve.AddKey(2f, 11); //up
            _animationCurve.AddKey(0f, 12); //down
            _animationCurve.AddKey(8f, 13); //up further
            _animationCurve.AddKey(0f, 13.5f); //down

            //3D animation curve demo
            _animationPositionCurve = new AnimationCurve3D(CurveLoopType.Cycle);
            _animationPositionCurve.AddKey(new Vector3(0, 0, 0), 0);
            _animationPositionCurve.AddKey(new Vector3(0, 0, -20), 1);
            _animationPositionCurve.AddKey(new Vector3(20, 0, -20), 2);
            _animationPositionCurve.AddKey(new Vector3(40, 0, -30), 3);
            // _animationPositionCurve.AddKey(new Vector3(0, 4, 0), 4);

            // Absolute yaw/pitch/roll angles (radians) over time
            _animationRotationCurve = new AnimationCurve3D(CurveLoopType.Oscillate);
            _animationRotationCurve.AddKey(new Vector3(0, 0, 0), 0);              // yaw, pitch, roll
            _animationRotationCurve.AddKey(new Vector3(0, MathHelper.PiOver2, 0), 1);
            _animationRotationCurve.AddKey(new Vector3(0, MathHelper.Pi, 0), 2);
            _animationRotationCurve.AddKey(new Vector3(0, 0, 0), 3);
        }

        private void InitializeGraphics(Integer2 resolution)
        {
            // Enable per-monitor DPI awareness so the window/UI scales crisply on multi-monitor setups with different DPIs (avoids blurriness when moving between screens).
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

            // Set preferred resolution
            ScreenResolution.SetResolution(_graphics, resolution);

            // Center on primary display (set to index of the preferred monitor)
            WindowUtility.CenterOnMonitor(this, 1);
        }

        private void InitializeMouse()
        {
            Mouse.SetPosition(_graphics.PreferredBackBufferWidth / 2, _graphics.PreferredBackBufferHeight / 2);

            // Set old state at start so its not null for comparison with new state in Update
            _oldKBState = Keyboard.GetState();
        }

        private void InitializeContext()
        {
            EngineContext.Initialize(GraphicsDevice, Content);
        }

        /// <summary>
        /// New asset loading from JSON using AssetEntry and ContentDictionary::LoadFromManifest
        /// </summary>
        /// <param name="relativeFilePathAndName"></param>
        /// <see cref="AssetEntry"/>
        /// <see cref="ContentDictionary{T}"/>
        private void LoadAssetsFromJSON(string relativeFilePathAndName)
        {
            // Make dictionaries to store assets
            _textureDictionary = new ContentDictionary<Texture2D>();
            _modelDictionary = new ContentDictionary<Model>();
            _fontDictionary = new ContentDictionary<SpriteFont>();
            _soundDictionary = new ContentDictionary<SoundEffect>();
            _effectsDictionary = new ContentDictionary<Effect>();
            //TODO - Add dictionary loading for other assets - song, other?

            var manifests = JSONSerializationUtility.LoadData<AssetManifest>(Content, relativeFilePathAndName); // single or array
            if (manifests.Count > 0)
            {
                foreach (var m in manifests)
                {
                    _modelDictionary.LoadFromManifest(m.Models, e => e.Name, e => e.ContentPath, overwrite: true);
                    _textureDictionary.LoadFromManifest(m.Textures, e => e.Name, e => e.ContentPath, overwrite: true);
                    _fontDictionary.LoadFromManifest(m.Fonts, e => e.Name, e => e.ContentPath, overwrite: true);
                    _soundDictionary.LoadFromManifest(m.Sounds, e => e.Name, e => e.ContentPath, overwrite: true);
                    _effectsDictionary.LoadFromManifest(m.Effects, e => e.Name, e => e.ContentPath, overwrite: true);
                    //TODO - Add dictionary loading for other assets - song, other?
                }
            }
        }

        private void InitializeEffects()
        {
            #region Unlit Textured BasicEffect 
            var unlitBasicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = false,
                VertexColorEnabled = false
            };

            _matBasicUnlit = new Material(unlitBasicEffect);
            _matBasicUnlit.StateBlock = RenderStates.Opaque3D();      // depth on, cull CCW
            _matBasicUnlit.SamplerState = SamplerState.LinearClamp;   // helps avoid texture seams on sky

            //ground texture where UVs above [0,0]-[1,1]
            _matBasicUnlitGround = new Material(unlitBasicEffect.Clone());
            _matBasicUnlitGround.StateBlock = RenderStates.Opaque3D();      // depth on, cull CCW
            _matBasicUnlitGround.SamplerState = SamplerState.AnisotropicWrap;   // wrap texture based on UV values

            #endregion

            #region Lit Textured BasicEffect 
            var litBasicEffect = new BasicEffect(_graphics.GraphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                PreferPerPixelLighting = true,
                VertexColorEnabled = false
            };
            litBasicEffect.EnableDefaultLighting();
            //litBasicEffect.AmbientLightColor = Color.Red.ToVector3();
            //litBasicEffect.EmissiveColor = Color.Green.ToVector3();
            //litBasicEffect.FogEnabled = true;
            //litBasicEffect.FogColor = Color.LightGray.ToVector3();
            //litBasicEffect.FogStart = 1;
            //litBasicEffect.FogEnd = 100;
            //litBasicEffect.SpecularPower = 8;  //int, power of 2, 1, 2, 4, 8
            //litBasicEffect.SpecularColor = Color.Yellow.ToVector3();
            _matBasicLit = new Material(litBasicEffect);
            _matBasicLit.StateBlock = RenderStates.Opaque3D();

            #endregion

            #region Alpha-test for foliage/billboards
            var alphaFx = new AlphaTestEffect(GraphicsDevice)
            {
                VertexColorEnabled = false
            };
            _matAlphaCutout = new Material(alphaFx);

            // Depth test/write on; no blending (cutout happens in the effect). 
            // Make it two-sided so the quad is visible from both sides.
            _matAlphaCutout.StateBlock = RenderStates.Cutout3D()
                .WithRaster(new RasterizerState { CullMode = CullMode.None });

            // Clamp avoids edge bleeding from transparent borders.
            // (Use LinearWrap if the foliage textures tile.)
            _matAlphaCutout.SamplerState = SamplerState.LinearClamp;

            #endregion
        }

        private void InitializeScene()
        {
            // Make a scene that will store all drawn objects and systems for that level
            var scene = new Scene(EngineContext.Instance, "outdoors - level 1");

            // Add each new scene into the manager
            _sceneManager.AddScene(AppData.LEVEL_1_NAME, scene);

            // Set the active scene before anything that uses ActiveScene
            _sceneManager.SetActiveScene(AppData.LEVEL_1_NAME);
        }

        private void InitializeSystems()
        {
            InitializePhysicsSystem();
            InitializePhysicsDebugSystem(true);
            InitializeEventSystem();  //propagate events  
            InitializeInputSystem();  //input
            InitializeCameraAndRenderSystems(); //update cameras, draw renderable game objects, draw ui and menu
            InitializeAudioSystem();
            InitializeOrchestrationSystem(false); //show debugger
            InitializeImpulseSystem();    //camera shake, audio duck volumes etc
            InitializeUIEventSystem();
            InitializeGameStateSystem();   //manage and track game state
                                           //  InitializeNavMeshSystem();
        }

        private void InitializeNavMeshSystem()
        {
            var scene = _sceneManager.ActiveScene;

            // Core navmesh system (implements INavigationService)
            var navMeshSystem = scene.AddSystem(new NavMeshSystem());

            // Debug overlay (F2 toggle)
            scene.Add(new NavMeshDebugSystem());
        }

        private void InitializeGameStateSystem()
        {
            // Add game state system
            _sceneManager.ActiveScene.AddSystem(new GameStateSystem());
        }

        private void InitializeUIEventSystem()
        {
            _sceneManager.ActiveScene.AddSystem(new UIEventSystem());
        }

        private void InitializeImpulseSystem()
        {
            _sceneManager.ActiveScene.Add(new ImpulseSystem(EngineContext.Instance.Impulses));
        }

        private void InitializeOrchestrationSystem(bool debugEnabled)
        {
            var orchestrationSystem = new OrchestrationSystem();
            orchestrationSystem.Configure(options =>
            {
                options.Time = Orchestrator.OrchestrationTime.Unscaled;
                options.LocalScale = 1;
                options.Paused = false;
            });
            _sceneManager.ActiveScene.Add(orchestrationSystem);

            // Debugger
            if (debugEnabled)
            {
                GameObject debugGO = new GameObject("Perf Stats");
                var debugRenderer = debugGO.AddComponent<UIDebugInfo>();

                debugRenderer.Font = _fontDictionary.Get("perf_stats_font");
                debugRenderer.ScreenCorner = ScreenCorner.TopLeft;
                debugRenderer.Margin = new Vector2(10f, 10f);

                // Register orchestration as a debug provider
                if (orchestrationSystem != null)
                    debugRenderer.Providers.Add(orchestrationSystem);

                var perfProvider = new PerformanceDebugInfoProvider
                {
                    Profile = DisplayProfile.Profiling,
                    ShowMemoryStats = true
                };

                debugRenderer.Providers.Add(perfProvider);

                _sceneManager.ActiveScene.Add(debugGO);
            }

        }

        private void InitializeAudioSystem()
        {
            _sceneManager.ActiveScene.Add(new AudioSystem(_soundDictionary));
        }

        private void InitializePhysicsDebugSystem(bool isEnabled)
        {
            if (isEnabled)
            {
                var physicsDebugRenderer = _sceneManager.ActiveScene.AddSystem(new PhysicsDebugSystem());

                // Toggle debug rendering on/off
                physicsDebugRenderer.Enabled = isEnabled; // or false to hide

                // Optional: Customize colors
                physicsDebugRenderer.StaticColor = Color.Green;      // Immovable objects
                physicsDebugRenderer.KinematicColor = Color.Blue;    // Animated objects
                physicsDebugRenderer.DynamicColor = Color.Yellow;    // Physics-driven objects
                physicsDebugRenderer.TriggerColor = Color.Red;       // Trigger volumes
            }

        }

        private void InitializePhysicsSystem()
        {
            // 1. add physics
            var physicsSystem = _sceneManager.ActiveScene.AddSystem(new PhysicsSystem());
            physicsSystem.Gravity = AppData.GRAVITY;
        }

        private void InitializeEventSystem()
        {
            _sceneManager.ActiveScene.Add(new EventSystem(EngineContext.Instance.Events));
        }

        private void InitializeCameraAndRenderSystems()
        {
            //manages camera
            var cameraSystem = new CameraSystem(_graphics.GraphicsDevice, -100);
            _sceneManager.ActiveScene.Add(cameraSystem);

            //3d
            var renderSystem = new RenderSystem(-100);
            _sceneManager.ActiveScene.Add(renderSystem);

            //2d
            var uiRenderSystem = new UIRenderSystem(-100);
            _sceneManager.ActiveScene.Add(uiRenderSystem); // draws in PostRender after RenderingSystem (order = -100)
        }

        private void InitializeInputSystem()
        {
            //set mouse, keyboard binding keys (e.g. WASD)
            var bindings = InputBindings.Default;
            // optional tuning
            bindings.MouseSensitivity = 0.12f;  // mouse look scale
            bindings.DebounceMs = 60;           // key/mouse debounce in ms
            bindings.EnableKeyRepeat = true;    // hold-to-repeat
            bindings.KeyRepeatMs = 300;         // repeat rate in ms

            // Create the input system 
            var inputSystem = new InputSystem();

            // Register all the devices, you don't have to, but its for the demo
            inputSystem.Add(new GDKeyboardInput(bindings));
            inputSystem.Add(new GDMouseInput(bindings));
            inputSystem.Add(new GDGamepadInput(PlayerIndex.One, "Gamepad P1"));

            _sceneManager.ActiveScene.Add(inputSystem);
        }

        private void InitializeCameras()
        {
            Scene scene = _sceneManager.ActiveScene;

            GameObject cameraGO = null;
            Camera camera = null;
            #region Static birds-eye camera
            cameraGO = new GameObject(AppData.CAMERA_NAME_STATIC_BIRDS_EYE);
            camera = cameraGO.AddComponent<Camera>();
            camera.FieldOfView = MathHelper.ToRadians(80);
            //ISRoT
            cameraGO.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0));
            cameraGO.Transform.TranslateTo(Vector3.UnitY * 50);

            // _cameraGO.AddComponent<MouseYawPitchController>();

            scene.Add(cameraGO);

            // _camera.FieldOfView
            //TODO - add camera
            #endregion

            #region Third-person camera
            cameraGO = new GameObject(AppData.CAMERA_NAME_THIRD_PERSON);
            camera = cameraGO.AddComponent<Camera>();

            var thirdPersonController = new ThirdPersonController();
            thirdPersonController.TargetName = AppData.PLAYER_NAME;
            thirdPersonController.ShoulderOffset = 0;
            thirdPersonController.FollowDistance = 50;
            thirdPersonController.RotationDamping = 20;
            cameraGO.AddComponent(thirdPersonController);
            scene.Add(cameraGO);
            #endregion

            #region First-person camera
            var position = new Vector3(0, 5, 25);

            //camera GO
            cameraGO = new GameObject(AppData.CAMERA_NAME_FIRST_PERSON);

            //set position 
            cameraGO.Transform.TranslateTo(position);

            //add camera component to the GO
            camera = cameraGO.AddComponent<Camera>();
            camera.FarPlane = 1000;

            //feed off whatever screen dimensions you set InitializeGraphics
            camera.AspectRatio = (float)_graphics.PreferredBackBufferWidth / _graphics.PreferredBackBufferHeight;
            cameraGO.AddComponent<SimpleDriveController>();
            cameraGO.AddComponent<MouseYawPitchController>();
            cameraGO.AddComponent<CameraImpulseListener>();

            //var collider = cameraGO.AddComponent<CapsuleCollider>();
            //collider.Height = 5f;
            //collider.Radius = 0.25f;

            //var rb = cameraGO.AddComponent<RigidBody>();
            //rb.BodyType = BodyType.Dynamic;
            //rb.Mass = 80f;       // “human-ish”
            //rb.UseGravity = true;
            //rb.LinearDamping = 0.0f;      // or a little drag if you prefer
            //rb.AngularDamping = 0.0f;

            //var physicsWASDController = cameraGO.AddComponent<PhysicsWASDController>();
            //physicsWASDController.MoveSpeed = 25f;                     // walk speed

            //var interComp = cameraGO.AddComponent<InteractionComponent>();
            //interComp.HitMask = LayerMask.Interactables;

            // Add it to the scene
            scene.Add(cameraGO);


            #endregion
            cameraGO = new GameObject(AppData.CAMERA_NAME_RAIL);
            cameraGO.Transform.TranslateTo(position);

            //add camera component to the GO
            camera = cameraGO.AddComponent<Camera>();
            camera.FarPlane = 1000;

            //feed off whatever screen dimensions you set InitializeGraphics
            camera.AspectRatio = (float)_graphics.PreferredBackBufferWidth / _graphics.PreferredBackBufferHeight;


            scene.Add(cameraGO);
            //replace with new SetActiveCamera that searches by string
            scene.SetActiveCamera(AppData.CAMERA_NAME_RAIL);
        }

        /// <summary>
        /// Add parent root at origin to rotate the sky
        /// </summary>
        private void InitializeSkyParent()
        {
            var _skyParent = new GameObject("SkyParent");
            var rot = _skyParent.AddComponent<RotationController>();

            // Turntable spin around local +Y
            rot._rotationAxisNormalized = Vector3.Up;

            // Dramatised fast drift at 2 deg/sec. 
            rot._rotationSpeedInRadiansPerSecond = MathHelper.ToRadians(2f);
            _sceneManager.ActiveScene.Add(_skyParent);
        }

        private void InitializeSkyBox(int scale = 500)
        {
            Scene scene = _sceneManager.ActiveScene;
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            // Find the sky parent object to attach sky to so sky rotates
            GameObject skyParent = scene.Find((GameObject go) => go.Name.Equals("SkyParent"));

            // back
            gameObject = new GameObject("back");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.TranslateTo(new Vector3(0, 0, -scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_back");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // left
            gameObject = new GameObject("left");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(90), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(-scale / 2, 0, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_left");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);


            // right
            gameObject = new GameObject("right");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(-90), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(scale / 2, 0, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_right");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // front
            gameObject = new GameObject("front");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(0, MathHelper.ToRadians(180), 0), true);
            gameObject.Transform.TranslateTo(new Vector3(0, 0, scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_front");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

            // sky (top)
            gameObject = new GameObject("sky");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(90), 0, MathHelper.ToRadians(90)), true);
            gameObject.Transform.TranslateTo(new Vector3(0, scale / 2, 0));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_sky");
            scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

        }

        private void InitializeUI()
        {
            InitializeScoreBoard();
            InitializeDialogueUI();
        }

        private void InitializeScoreBoard()
        {
            var scoreBoard = new GameObject("ScoreBoard");

            var uiFont = _fontDictionary.Get("mouse_reticle_font");

            var textRenderer = scoreBoard.AddComponent<UIText>();
            textRenderer.Font = uiFont;
            //textRenderer.Offset = new Vector2(0, 0);  // Position text below reticle
            textRenderer.Color = Color.White;
            textRenderer.PositionProvider = () => new Vector2(_graphics.GraphicsDevice.Viewport.Width - 100, 0);
            //textRenderer.Anchor = TextAnchor.Center;
            textRenderer.TextProvider = () => "Score: " + score;



            _sceneManager.ActiveScene.Add(scoreBoard);

            // Hide mouse since reticle will take its place
            IsMouseVisible = false;
        }

        private void InitializeUIReticleRenderer()
        {
            if (uiReticleGO != null)
                return;
            uiReticleGO = new GameObject("HUD");

            var reticleAtlas = _textureDictionary.Get("Crosshair_21");
            var uiFont = _fontDictionary.Get("mouse_reticle_font");

            // Reticle (cursor): always on top
            var reticle = new UIReticle(reticleAtlas);
            reticle.Origin = reticleAtlas.GetCenter();
            reticle.SourceRectangle = null;
            reticle.Scale = new Vector2(0.1f, 0.1f);
            reticle.RotationSpeedDegPerSec = 55;
            reticle.LayerDepth = UILayer.Cursor;
            uiReticleGO.AddComponent(reticle);

            var textRenderer = uiReticleGO.AddComponent<UIText>();
            textRenderer.Font = uiFont;
            textRenderer.Offset = new Vector2(0, 30);  // Position text below reticle
            textRenderer.Color = Color.White;
            textRenderer.PositionProvider = () => _graphics.GraphicsDevice.Viewport.GetCenter();
            textRenderer.Anchor = TextAnchor.Center;

            var picker = uiReticleGO.AddComponent<UIPickerInfo>();
            picker.HitMask = LayerMask.All;
            picker.MaxDistance = 500f;
            picker.HitTriggers = false;

            // Optional custom formatting
            picker.Formatter = hit =>
            {

                if (roachKilled)
                {
                    var spatula = _sceneManager.ActiveScene.Find(go => go.Name.Equals("spatula"));

                    timeLeft += Time.DeltaTimeSecs;
                    //System.Diagnostics.Debug.WriteLine(
                    //    timeLeft);
                    if (timeLeft > 1)
                    {
                        spatula.Transform.RotateBy(Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(10)), worldSpace: false);
                        timeLeft = 0;
                        roachKilled = false;
                    }
                }

                var go = hit.Body?.GameObject;
                if (go == null)
                    return string.Empty;
                if (go.Name.Equals("roach") || go.Name.Equals("mainRoach"))
                {
                    isRoach = true;
                    //_scene.Remove(go);
                    _newMouseState = Mouse.GetState();
                    KillRoach(go);

                    _oldMouseState = _newMouseState;
                    if (hasSpatula)
                        return $"LEFT CLICK TO SQUASH";
                    return $"YOU NEED A SPATULA";
                    //return $"{hit.Point}";
                }
                if (go.Name.Equals("spatula"))
                {
                    //System.Diagnostics.Debug.WriteLine("helloooo");
                    _newKBState2 = Keyboard.GetState();
                    AddSpatula(go);
                    _oldKBState2 = _newKBState2;
                    return $"E TO PICKUP";
                }
                else
                    isRoach = false;

                //return $"{go.Name}  d={hit.Distance:F1}";
                return "";
            };

            _sceneManager.ActiveScene.Add(uiReticleGO);

            // Hide mouse since reticle will take its place
            IsMouseVisible = false;
        }

        /// <summary>
        /// Adds a single-part FBX model into the scene.
        /// </summary>
        private GameObject InitializeModel(Vector3 position,
            Vector3 eulerRotationDegrees, Vector3 scale,
            string textureName, string modelName, string objectName)
        {
            GameObject gameObject = null;

            gameObject = new GameObject(objectName);
            gameObject.Transform.TranslateTo(position);
            gameObject.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            gameObject.Transform.ScaleTo(scale);

            // gameObject.Layer = LayerMask.Interactables | LayerMask.NPC;  //100000 | 010000 = 110000

            var model = _modelDictionary.Get(modelName);
            var texture = _textureDictionary.Get(textureName);
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            gameObject.AddComponent(meshFilter);

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;

            _sceneManager.ActiveScene.Add(gameObject);

            return gameObject;
        }
        protected override void Update(GameTime gameTime)
        {
            //call time update
            #region Core
            Time.Update(gameTime);

            //update Scene
            _sceneManager.ActiveScene.Update(Time.DeltaTimeSecs);
            if (_menuManager != null)
            {
                bool menuVisible = _menuManager.IsMenuVisible;

                if (menuVisible != _lastMenuVisible)
                {
                    _lastMenuVisible = menuVisible;

                    IsMouseVisible = menuVisible;

                    SetTaskBarVisible(!menuVisible);
                    SetReticleoVisible(!menuVisible);
                }
            }

            #endregion
            _newKBState = Keyboard.GetState();
            DemoStuff();

            #region Demo

            #endregion
            _oldKBState = _newKBState;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Override Dispose to clean up engine resources.
        /// MonoGame's Game class already implements IDisposable, so we override its Dispose method.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (disposing)
            {
                System.Diagnostics.Debug.WriteLine("Disposing Main...");

                // 1. Dispose Materials (which may own Effects)
                System.Diagnostics.Debug.WriteLine("Disposing Materials");
                _matBasicUnlit?.Dispose();
                _matBasicUnlit = null;

                _matBasicLit?.Dispose();
                _matBasicLit = null;

                _matAlphaCutout?.Dispose();
                _matAlphaCutout = null;

                // 2. Clear cached MeshFilters in factory registry
                System.Diagnostics.Debug.WriteLine("Clearing MeshFilter Registry");
                MeshFilterFactory.ClearRegistry();

                // 3. Dispose content dictionaries (now they implement IDisposable!)
                System.Diagnostics.Debug.WriteLine("Disposing Content Dictionaries");
                _textureDictionary?.Dispose();
                _textureDictionary = null;

                _modelDictionary?.Dispose();
                _modelDictionary = null;

                _fontDictionary?.Dispose();
                _fontDictionary = null;

                // 4. Dispose EngineContext (which owns SpriteBatch and Content)
                System.Diagnostics.Debug.WriteLine("Disposing EngineContext");
                EngineContext.Instance?.Dispose();

                // 5. Clear references to help GC
                System.Diagnostics.Debug.WriteLine("Clearing References");
                _animationCurve = null;
                _animationPositionCurve = null;
                _animationRotationCurve = null;

                // 6. Dispose of collision handlers
                if (_collisionSubscription != null)
                {
                    _collisionSubscription.Dispose();
                    _collisionSubscription = null;
                }

                System.Diagnostics.Debug.WriteLine("Main disposal complete");
            }

            _disposed = true;

            // Always call base.Dispose
            base.Dispose(disposing);
        }

      

        #region Demo Methods (remove in the game)
        #region Demo - Game State
        private void SetWinConditions()
        {
            var gameStateSystem = _sceneManager.ActiveScene.GetSystem<GameStateSystem>();

            // Value providers (Strategy pattern via delegates)
            Func<float> healthProvider = () =>
            {
                //get the player and access the player's health/speed/other variable
                return _currentHealth;
            };

            // Delegate for time
            Func<float> timeProvider = () =>
            {
                return (float)Time.RealtimeSinceStartupSecs;
            };

            // Lose condition: health < 10 AND time > 60
            IGameCondition loseCondition =
                GameConditions.FromPredicate("all enemies visited", checkEnemiesVisited);

            IGameCondition winCondition =
            GameConditions.FromPredicate("reached gate", checkReachedGate);

            // Configure GameStateSystem (no win condition yet)
            gameStateSystem.ConfigureConditions(winCondition, loseCondition);
            gameStateSystem.StateChanged += HandleGameStateChange;
        }

        private bool checkReachedGate()
        {
            // we could pause the game on a win
            //Time.TimeScale = 0;

            return score >= 2000; ;
        }

        private bool checkEnemiesVisited()
        {
            return Time.RealtimeSinceStartupSecs > 120;
        }

        private void HandleGameStateChange(GameOutcomeState oldState, GameOutcomeState newState)
        {
            System.Diagnostics.Debug.WriteLine($"Old state was {oldState} and new state is {newState}");


            if (newState == GameOutcomeState.Lost)
            {
                System.Diagnostics.Debug.WriteLine("You lost!");
                _menuManager.ShowGameOver(false);
            }
            else if (newState == GameOutcomeState.Won)
            {
                System.Diagnostics.Debug.WriteLine("You win!");

                // Pause gameplay
                _sceneManager.Paused = true;

                // Pass final score into menu and show end screen
                if (_menuManager != null)
                {
                    _menuManager.CurrentScore = score;
                    _menuManager.ShowGameOver(true);
                }

                // Show mouse for UI interaction
                IsMouseVisible = true;
            }
        }
        #endregion

        private void DemoCollidableModel(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale, bool isMoving, string name)
        {
            var go = new GameObject(name);
            go.Transform.TranslateTo(position);
            go.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            go.Transform.ScaleTo(scale);

            go.Layer = LayerMask.Interactables;

            var model = _modelDictionary.Get("roach");
            var texture = _textureDictionary.Get("roach");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _sceneManager.ActiveScene.Add(go);

            // Add box collider (1x1x1 cube)
            var collider = go.AddComponent<BoxCollider>();
            collider.Size = new Vector3(3f, 2f, 2f);  // Collider is FULL size
            collider.Center = new Vector3(0, 0, -0.3f);

            // Add rigidbody (Dynamic so it falls)
            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Dynamic;
            rigidBody.Mass = 1.0f;
            if(isMoving) {
                go.AddComponent<RoachController>();
                go.Enabled = false;
            }
                
            //go.Enabled = false;
        }
        private void DemoCollidableSpatula(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale)
        {

            var go = new GameObject("spatula");
            go.Transform.TranslateTo(position);
            go.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            go.Transform.ScaleTo(scale);

            go.Layer = LayerMask.Interactables;

            var model = _modelDictionary.Get("spatula");
            var texture = _textureDictionary.Get("spatula");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _sceneManager.ActiveScene.Add(go);

            // Add box collider (1x1x1 cube)
            var collider = go.AddComponent<BoxCollider>();
            collider.Size = new Vector3(2f, 2f, 8f);  // Collider is FULL size
            collider.Center = new Vector3(0, 0, -3);

            // Add rigidbody (Dynamic so it falls)
            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            rigidBody.Mass = 1.0f;

            
        }
        private void DemoCameraParent(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale)
        {

            var go = new GameObject("CameraParent");
            _sceneManager.ActiveScene.Add(go);
            CurveController curveController = new CurveController();
            curveController.PositionCurve = _animationPositionCurve;
            curveController.Duration = 10;
            curveController.TargetCurve = _animationPositionCurve;
            curveController.Loop = false;
            
            go.AddComponent(curveController);
            GameObject cameraObject = _sceneManager.ActiveScene.Find(go => go.Name.Equals(AppData.CAMERA_NAME_RAIL));
            cameraObject.Transform.SetParent(go);
            
        }

        private void DemoStuff()
        {
            // Get new state
            //_newKBState = Keyboard.GetState();
          
            DemoToggleFullscreen();
            DemoAudioSystem();
          
            DemoImpulsePublish();
            //a demo relating to GameStateSystem
            //_currentHealth--;

            // Store old state (allows us to do was pressed type checks)
           // _oldKBState = _newKBState;
        }

        private void DemoImpulsePublish()
        {
            var impulses = EngineContext.Instance.Impulses;
            bool isSpacePressed = _newKBState.IsKeyDown(Keys.Space) && !_oldKBState.IsKeyDown(Keys.Space);
            if (isSpacePressed) 
            {
                _sceneManager.ActiveScene.SetActiveCamera(AppData.CAMERA_NAME_FIRST_PERSON);
            }
           

        }

       

        private void DemoAudioSystem()
        {
            var events = EngineContext.Instance.Events;
            
            //TODO - Exercise
            bool isD3Pressed = _newKBState.IsKeyDown(Keys.D3) && !_oldKBState.IsKeyDown(Keys.D3);
            if (isD3Pressed)
            {
                events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1",
                    1, false, null));
            }

            bool isD4Pressed = _newKBState.IsKeyDown(Keys.D4) && !_oldKBState.IsKeyDown(Keys.D4);
            if (isD4Pressed)
            {
                events.Publish(new PlayMusicEvent("secret_door", 1, 8));
            }

            bool isD5Pressed = _newKBState.IsKeyDown(Keys.D5) && !_oldKBState.IsKeyDown(Keys.D5);
            if (isD5Pressed)
            {
                events.Publish(new StopMusicEvent(4));
            }

            bool isD6Pressed = _newKBState.IsKeyDown(Keys.D6) && !_oldKBState.IsKeyDown(Keys.D6);
            if (isD6Pressed)
            {
                events.Publish(new FadeChannelEvent(AudioMixer.AudioChannel.Master,
                    0.1f, 4));
            }

            bool isD7Pressed = _newKBState.IsKeyDown(Keys.D7) && !_oldKBState.IsKeyDown(Keys.D7);
            if (isD7Pressed)
            {
                //expensive and crude => move to Component::Start()
                var go = _sceneManager.ActiveScene.Find(go => go.Name.Equals(AppData.PLAYER_NAME));
                Transform emitterTransform = go.Transform;

                events.Publish(new PlaySfxEvent("hand_gun1",
                    1, true, emitterTransform));
            }
        }

        private void DemoToggleFullscreen()
        {
            bool togglePressed = _newKBState.IsKeyDown(Keys.F5) && !_oldKBState.IsKeyDown(Keys.F5);
            if (togglePressed)
                _graphics.ToggleFullScreen();
        }

      

      

        private void DemoLoadFromJSON()
        {
            //var relativeFilePathAndName = "assets/data/single_model_spawn.json";
            //List<ModelSpawnData> mList = JSONSerializationUtility.LoadData<ModelSpawnData>(Content, relativeFilePathAndName);

            ////load a single model
            //foreach (var d in mList)
            //    InitializeModel(d.Position, d.RotationDegrees, d.Scale, d.TextureName, d.ModelName, d.ObjectName);

            var relativeFilePathAndName = "assets/data/multi_model_spawn.json";
            //load multiple models
            foreach (var d in JSONSerializationUtility.LoadData<ModelSpawnData>(Content, relativeFilePathAndName))
                InitializeModel(d.Position, d.RotationDegrees, d.Scale, d.TextureName, d.ModelName, d.ObjectName);
        }

       

       
        /// <summary>
        /// Subscribes a simple debug listener for physics collision events.
        /// </summary>
        private void InitializeCollisionEventListener()
        {
            var events = EngineContext.Instance.Events;

            // Lowest friction: just subscribe with default priority & no filter
            _collisionSubscription = events.Subscribe<CollisionEvent>(OnCollisionEvent);
        }

        /// <summary>
        /// Very simple collision debug handler.
        /// Adjust field names to match your CollisionEvent struct.
        /// </summary>
        private void OnCollisionEvent(CollisionEvent evt)
        {
            // Early-out if this collision does not involve any layer we care about.
            if (!evt.Matches(_collisionDebugMask))
                return;

            var bodyA = evt.BodyA;
            var bodyB = evt.BodyB;

            var nameA = bodyA?.GameObject?.Name ?? "<null>";
            var nameB = bodyB?.GameObject?.Name ?? "<null>";

            var layerA = evt.LayerA;
            var layerB = evt.LayerB;

            //System.Diagnostics.Debug.WriteLine(
            //    $"[Collision] {nameA} (Layer {layerA}) <-> {nameB} (Layer {layerB})");
        }


        #endregion
    }
}