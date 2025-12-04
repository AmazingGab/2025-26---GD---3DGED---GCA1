using System;
using System.Collections.Generic;
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
        private Scene _scene;
        private Material _matBasicUnlit, _matBasicLit, _matAlphaCutout, _matBasicUnlitGround;
        #endregion

        #region Demo Fields (remove in the game)
        private AnimationCurve3D _animationPositionCurve, _animationRotationCurve;
        private AnimationCurve _animationCurve;
        private KeyboardState _newKBState, _oldKBState;
        private int _damageAmount;
        private MouseState _oldMouseState;
        private MouseState _newMouseState;

        // Simple debug subscription for collision events
        private IDisposable _collisionSubscription;

        // LayerMask used to filter which collisions we care about in debug
        private LayerMask _collisionDebugMask = LayerMask.All;
        private UIMenuPanel _mainMenuPanel, _audioMenuPanel;
        private SceneManager _sceneManager;
        private float _currentHealth = 100;
        private MenuManager _menuManager;
        public int score;
        private GameObject _cameraGO;
        private Camera _camera;
        private UITextRenderer _uiStatsRenderer;
        private bool isRoach;
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
            InitializeGraphics(GDEngine.Core.ScreenResolution.R_WXGA_16_10_1280x800);
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
            InitializePlayer();
      
            #endregion

            #region Demos

            // Camera-demos
            InitializeAnimationCurves();

            // Demo event listeners on collision
            InitializeCollisionEventListener();
            
            // Collidable game object demos
            
            DemoCollidableModel(new Vector3(0, 1, 10), new Vector3(-90,0,0), new Vector3(1.5f, 0.5f, 0.2f));
            DemoCollidableModel(new Vector3(5, 1, 11), new Vector3(-90, 0, 0), new Vector3(1.5f, 0.5f, 0.2f));
            DemoCollidableModel(new Vector3(10, 1, 12), new Vector3(-90, 0, 0), new Vector3(1.5f, 0.5f, 0.2f));

            DemoCollidableModel(new Vector3(15, 1, 12), new Vector3(-90, 0, 0), new Vector3(1.5f, 0.5f, 0.2f));
            DemoCollidableModel(new Vector3(20, 1, 12), new Vector3(-90, 0, 0), new Vector3(1.5f, 0.5f, 0.2f));

            DemoCollidableMap(new Vector3(80, 0, 0), new Vector3(-90, 0, 0), new Vector3(100, 55,5));
            DemoLoadFromJSON();
           
            #endregion

            #region Core
            // Setup UI renderers after all game objects added since ui text may use a gameobject as target
            InitializeUI();
            #endregion
            

            GameObject _roachParent = new GameObject("RoachParent");
            GameObject gameObject = null;
            var roachParts = MeshFilterFactory.CreateAllFromModel(_modelDictionary.Get("housewalls"), GraphicsDevice);
            _scene.Add(_roachParent);

            for (int i = 0; i < roachParts.Count; i++)
            {
                gameObject = new GameObject("roach+part"+1);
                //gameObject.Transform.ScaleTo(new Vector3(scale / 10, scale / 10, scale / 10));
                var meshFilter = roachParts[i];
                gameObject.AddComponent(meshFilter);
                var meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.Material = _matBasicLit;
                meshRenderer.Overrides.MainTexture = _textureDictionary.Get("roach_texture");
                _scene.Add(gameObject);
                gameObject.Transform.SetParent(_roachParent.Transform);
            }

            _roachParent.Transform.TranslateTo(new Vector3(300f, 0, 0));
            _roachParent.Transform.ScaleTo(new Vector3(100f, 85f, 5f));
            _roachParent.Transform.RotateEulerBy(new Vector3(MathHelper.ToRadians(-90), 0, 0), true);


            var imageUI = new GameObject("imageUI");
            var uiTexture = _textureDictionary.Get("dialogue");
            var imageRenderer = imageUI.AddComponent<UITextureRenderer>();
            imageRenderer.Texture = uiTexture;
            imageRenderer.Position = new Vector2(_graphics.GraphicsDevice.Viewport.Width / 2 - uiTexture.Width / 2, _graphics.GraphicsDevice.Viewport.Height - uiTexture.Height / 2 - 200);
            imageRenderer.LayerDepth = UILayer.HUD;
            _scene.Add(imageUI);

            var textUI = new GameObject("textUI");
            var uiFont = _fontDictionary.Get("menufont");
            var textRenderer = textUI.AddComponent<UITextRenderer>();
            textRenderer.Font = uiFont;
            textRenderer.Color = Color.Black;
            textRenderer.DropShadow = false;
            textRenderer.TextProvider = () => "grrr.. Oh, I'm really hungry. I left some \ncookies on the kitchen counter. \nI need to get them. I will have \nto turn on the lights.";
            textRenderer.Anchor = TextAnchor.Center;
            textRenderer.PositionProvider = () => new Vector2(_graphics.GraphicsDevice.Viewport.Width / 2, _graphics.GraphicsDevice.Viewport.Height/ 2 + 150);
            textRenderer.LayerDepth = UIRenderer.Behind(UILayer.HUD);
            _scene.Add(textUI);

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

        private void InitializeMenuManager()
        {
            _menuManager = new MenuManager(this, _sceneManager);
            Components.Add(_menuManager);

            Texture2D btnTex = _textureDictionary.Get("button_rectangle_10");
            Texture2D trackTex = _textureDictionary.Get("Free Flat Hyphen Icon");
            Texture2D handleTex = _textureDictionary.Get("Free Flat Toggle Thumb Centre Icon");
            Texture2D controlsTx = _textureDictionary.Get("mona lisa");
            SpriteFont uiFont = _fontDictionary.Get("menufont");

            // Wire UIManager to the menu scene
            _menuManager.Initialize(_sceneManager.ActiveScene,
                btnTex, trackTex, handleTex, controlsTx, uiFont,
                _textureDictionary.Get("mainmenu_monkey"),
                 _textureDictionary.Get("controlsmenu_monkey"),
                  _textureDictionary.Get("controlsmenu_monkey"));

            // Subscribe to high-level events
            _menuManager.PlayRequested += () =>
            {
                _sceneManager.Paused = false;
                _menuManager.HideMenus();

                //fade out menu sound
            };

            _menuManager.ExitRequested += () =>
            {
                Exit();
            };

            _menuManager.MusicVolumeChanged += v =>
            {
                // Forward to audio manager
                System.Diagnostics.Debug.WriteLine("MusicVolumeChanged");

                //raise event to set sound
                // EngineContext.Instance.Events.Publish(new PlaySfxEvent)
            };

            _menuManager.SfxVolumeChanged += v =>
            {
                // Forward to audio manager
                System.Diagnostics.Debug.WriteLine("SfxVolumeChanged");

                //raise event to set sound
            };


        }

        private void InitializeManagers()
        {
            var go = new GameObject("Camera Manager");
            go.AddComponent<CameraEventListener>();
            _scene.Add(go);
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
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("ground_grass");

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
            _animationPositionCurve = new AnimationCurve3D(CurveLoopType.Oscillate);
            _animationPositionCurve.AddKey(new Vector3(0, 4, 0), 0);
            _animationPositionCurve.AddKey(new Vector3(5, 8, 2), 1);
            _animationPositionCurve.AddKey(new Vector3(10, 12, 4), 2);
            _animationPositionCurve.AddKey(new Vector3(0, 4, 0), 3);

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
            GDEngine.Core.ScreenResolution.SetResolution(_graphics, resolution);

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
            _scene = new Scene(EngineContext.Instance, "outdoors - level 1");
        }

        private void InitializeSystems()
        {
            InitializePhysicsSystem();
            InitializePhysicsDebugSystem(false);
            InitializeEventSystem();  //propagate events  
            InitializeInputSystem();  //input
            InitializeCameraAndRenderSystems(); //update cameras, draw renderable game objects, draw ui and menu
            InitializeAudioSystem();
            InitializeOrchestrationSystem(false); //show debugger
            InitializeImpulseSystem();    //camera shake, audio duck volumes etc
            InitializeUIEventSystem();
            InitializeGameStateSystem();
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
            _scene.Add(new ImpulseSystem(EngineContext.Instance.Impulses));
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
            _scene.Add(orchestrationSystem);

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
            _scene.Add(new AudioSystem(_soundDictionary));
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
            var physicsSystem = _scene.AddSystem(new PhysicsSystem());
            physicsSystem.Gravity = AppData.GRAVITY;
        }

        private void InitializeEventSystem()
        {
            _scene.Add(new EventSystem(EngineContext.Instance.Events));
        }

        private void InitializeCameraAndRenderSystems()
        {
            var cameraSystem = new CameraSystem(_graphics.GraphicsDevice, -100);
            _scene.Add(cameraSystem);

            var renderSystem = new RenderSystem(-100);
            _scene.Add(renderSystem);

            var uiRenderSystem = new UIRenderSystem(100);
            _scene.Add(uiRenderSystem); // draws in PostRender after RenderingSystem (order = -100)
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

            //register all the devices, you dont have to, but its for the demo
            inputSystem.Add(new GDKeyboardInput(bindings));
            inputSystem.Add(new GDMouseInput(bindings));
            inputSystem.Add(new GDGamepadInput(PlayerIndex.One, "Gamepad P1"));

            _scene.Add(inputSystem);
        }

        private void InitializeCameras()
        {
            #region First-person camera
            var position = new Vector3(0, 5, 25);

            //camera GO
            _cameraGO = new GameObject(AppData.CAMERA_NAME_FIRST_PERSON);
            //set position 
            _cameraGO.Transform.TranslateTo(position);
            //add camera component to the GO
            _camera = _cameraGO.AddComponent<Camera>();
            _camera.FarPlane = 1000;
            ////feed off whatever screen dimensions you set InitializeGraphics
            _camera.AspectRatio = (float)_graphics.PreferredBackBufferWidth / _graphics.PreferredBackBufferHeight;
            _cameraGO.AddComponent<SimpleDriveController>();
            _cameraGO.AddComponent<MouseYawPitchController>();

            // Add it to the scene
            _scene.Add(_cameraGO);
            #endregion


            _scene.SetActiveCamera(_cameraGO.GetComponent<Camera>());

            GameObject player = InitializeModel(new Vector3(2, -6, 0),
                new Vector3(45, 0, 0),
                new Vector3(0.3f, 1, 1), "crate1", "spatula", AppData.PLAYER_NAME);
            player.Transform.SetParent(_cameraGO);
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
            _scene.Add(_skyParent);
        }

        private void InitializeSkyBox(int scale = 500)
        {
            GameObject gameObject = null;
            MeshFilter meshFilter = null;
            MeshRenderer meshRenderer = null;

            // Find the sky parent object to attach sky to so sky rotates
            GameObject skyParent = _scene.Find((GameObject go) => go.Name.Equals("SkyParent"));

            // back
            gameObject = new GameObject("back");
            gameObject.Transform.ScaleTo(new Vector3(scale, scale, 1));
            gameObject.Transform.TranslateTo(new Vector3(0, 0, -scale / 2));
            meshFilter = MeshFilterFactory.CreateQuadTexturedLit(_graphics.GraphicsDevice);
            gameObject.AddComponent(meshFilter);
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicUnlit;
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("skybox_back");
            _scene.Add(gameObject);

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
            _scene.Add(gameObject);

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
            _scene.Add(gameObject);

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
            _scene.Add(gameObject);

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
            _scene.Add(gameObject);

            //set parent to allow rotation
            gameObject.Transform.SetParent(skyParent.Transform);

        }

        private void InitializeUI()
        {
            InitializeUIReticleRenderer();
            InitializeScoreBoard();
        }
        private void InitializeScoreBoard()
        {
            var scoreBoard = new GameObject("ScoreBoard");

            //var reticleAtlas = _textureDictionary.Get("Crosshair_21");
            var uiFont = _fontDictionary.Get("mouse_reticle_font");

            // Reticle (cursor): always on top
           // var reticle = new UIReticleRenderer(reticleAtlas);
            //reticle.Origin = reticleAtlas.GetCenter();
            //reticle.SourceRectangle = null;
            ////reticle.Scale = new Vector2(0.1f, 0.1f);
            //reticle.RotationSpeedDegPerSec = 55;
            //reticle.LayerDepth = UILayer.Cursor;
            //uiReticleGO.AddComponent(reticle);

            var textRenderer = scoreBoard.AddComponent<UITextRenderer>();
            textRenderer.Font = uiFont;
            //textRenderer.Offset = new Vector2(0, 0);  // Position text below reticle
            textRenderer.Color = Color.White;
            textRenderer.PositionProvider = () => new Vector2(_graphics.GraphicsDevice.Viewport.Width-100, 0);
            //textRenderer.Anchor = TextAnchor.Center;
            textRenderer.TextProvider = () => "Score: " + score;



            _scene.Add(scoreBoard);

            // Hide mouse since reticle will take its place
            IsMouseVisible = false;
        }
        private void InitializeUIReticleRenderer()
        {
            var uiReticleGO = new GameObject("HUD");

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

            // Optional custom formatting:
            picker.Formatter = hit =>
            {
                var go = hit.Body?.GameObject;
                if (go == null)
                    return string.Empty;
                if (go.Name.Equals("roach"))
                {
                    isRoach = true;
                    //_scene.Remove(go);
                    _newMouseState = Mouse.GetState();
                    KillRoach(go);
                    _oldMouseState = _newMouseState;
                }
                   
                else
                    isRoach = false;

                    return $"{go.Name}  d={hit.Distance:F1}";
            };

            _scene.Add(uiReticleGO);

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

            var model = _modelDictionary.Get(modelName);
            var texture = _textureDictionary.Get(textureName);
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            gameObject.AddComponent(meshFilter);

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;

            _scene.Add(gameObject);

            return gameObject;
        }
        protected override void Update(GameTime gameTime)
        {
            //call time update
            #region Core
            Time.Update(gameTime);

            //update Scene
            _scene.Update(Time.DeltaTimeSecs);
            #endregion

            #region Demo
            
            #endregion

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            //just as called update, we now have to call draw to call the draw in the renderingsystem
            _scene.Draw(Time.DeltaTimeSecs);

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

                // 1. Dispose Scene (which will cascade to GameObjects and Components)
                System.Diagnostics.Debug.WriteLine("Disposing Scene");
                _scene?.Dispose();
                _scene = null;

                // 2. Dispose Materials (which may own Effects)
                System.Diagnostics.Debug.WriteLine("Disposing Materials");
                _matBasicUnlit?.Dispose();
                _matBasicUnlit = null;

                _matBasicLit?.Dispose();
                _matBasicLit = null;

                _matAlphaCutout?.Dispose();
                _matAlphaCutout = null;

                // 3. Clear cached MeshFilters in factory registry
                System.Diagnostics.Debug.WriteLine("Clearing MeshFilter Registry");
                MeshFilterFactory.ClearRegistry();

                // 4. Dispose content dictionaries (now they implement IDisposable!)
                System.Diagnostics.Debug.WriteLine("Disposing Content Dictionaries");
                _textureDictionary?.Dispose();
                _textureDictionary = null;

                _modelDictionary?.Dispose();
                _modelDictionary = null;

                _fontDictionary?.Dispose();
                _fontDictionary = null;

                // 5. Dispose EngineContext (which owns SpriteBatch and Content)
                System.Diagnostics.Debug.WriteLine("Disposing EngineContext");
                EngineContext.Instance?.Dispose();

                // 6. Clear references to help GC
                System.Diagnostics.Debug.WriteLine("Clearing References");
                _animationCurve = null;
                _animationPositionCurve = null;
                _animationRotationCurve = null;

                // 7. Dispose of collision handlers
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

        #endregion    }

        #region Demo Methods (remove in the game)
        private void DemoCollidableGround(int scale = 500)
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
            meshRenderer.Overrides.MainTexture = _textureDictionary.Get("ground_grass");

            // Add a box collider matching the ground size
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.Size = new Vector3(scale, scale, 0.025f);
            collider.Center = new Vector3(0, 0, -0.0125f);

            // Add rigidbody as Static (immovable)
            var rigidBody = gameObject.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Static;
            gameObject.IsStatic = true;

            _scene.Add(gameObject);
        }

        private void DemoCollidableModel(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale)
        {
            var go = new GameObject("roach");
            go.Transform.TranslateTo(position);
            go.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            go.Transform.ScaleTo(scale);

            var model = _modelDictionary.Get("roach");
            var texture = _textureDictionary.Get("mona lisa");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _scene.Add(go);


            // Add box collider (1x1x1 cube)
            var collider = go.AddComponent<BoxCollider>();
            collider.Size = new Vector3(3f, 2f, 2f);  // Collider is FULL size
            collider.Center = new Vector3(0,0,-0.3f);

            // Add rigidbody (Dynamic so it falls)
            var rigidBody = go.AddComponent<RigidBody>();
            rigidBody.BodyType = BodyType.Dynamic;
            rigidBody.Mass = 1.0f;

            go.AddComponent<RoachController>();
        }
        private void DemoCollidableMap(Vector3 position, Vector3 eulerRotationDegrees, Vector3 scale)
        {
            var go = new GameObject("map");
            go.Transform.TranslateTo(position);
            go.Transform.RotateEulerBy(eulerRotationDegrees * MathHelper.Pi / 180f);
            go.Transform.ScaleTo(scale);

            var model = _modelDictionary.Get("map2");
            var texture = _textureDictionary.Get("mona lisa");
            var meshFilter = MeshFilterFactory.CreateFromModel(model, _graphics.GraphicsDevice, 0, 0);
            go.AddComponent(meshFilter);

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Material = _matBasicLit;
            meshRenderer.Overrides.MainTexture = texture;
            _scene.Add(go);


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
            bool togglePressed = _newMouseState.LeftButton == ButtonState.Pressed && _oldMouseState.LeftButton == ButtonState.Released;
            if (togglePressed)
            {
                
                //foreach (var roach in roaches)
                //{

                    //var distToWaypoint = Vector3.Distance(cameraObject.Transform.Position, roach.Transform.Position);
                    //if (roach != null && distToWaypoint < 10 && isRoach)
                    //{
                        _scene.Remove(roach);
                        events.Publish(new PlaySfxEvent("SFX_UI_Click_Designed_Pop_Generic_1",
                    1, false, null));
                        score += 100;
                        //break;
                    //}
                //}

            }
        }

        
        

        private void DemoLoadFromJSON()
        {
            var relativeFilePathAndName = "assets/data/single_model_spawn.json";
            List<ModelSpawnData> mList = JSONSerializationUtility.LoadData<ModelSpawnData>(Content, relativeFilePathAndName);

            //load a single model
            foreach (var d in mList)
                InitializeModel(d.Position, d.RotationDegrees, d.Scale, d.TextureName, d.ModelName, d.ObjectName);

            relativeFilePathAndName = "assets/data/multi_model_spawn.json";
            //load multiple models
            foreach (var d in JSONSerializationUtility.LoadData<ModelSpawnData>(Content, relativeFilePathAndName))
                InitializeModel(d.Position, d.RotationDegrees, d.Scale, d.TextureName, d.ModelName, d.ObjectName);
        }

        private bool checkEnemiesVisited()
        {
            //get inventory and eval using boolean if all enemies visited;
            return false;
        }

        private bool checkReachedGate()
        {
            // we could pause the game on a win
            //Time.TimeScale = 0;
            return false;
        }

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

        private void HandleGameStateChange(GameOutcomeState oldState, GameOutcomeState newState)
        {
            System.Diagnostics.Debug.WriteLine($"Old state was {oldState} and new state is {newState}");

            if (newState == GameOutcomeState.Lost)
            {
                System.Diagnostics.Debug.WriteLine("You lost!");
                //play sound
                //reset player
                //load next level
                //we decide what losing looks like here!
                //Exit();
            }
            else if (newState == GameOutcomeState.Won)
            {
                System.Diagnostics.Debug.WriteLine("You win!");
            }

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