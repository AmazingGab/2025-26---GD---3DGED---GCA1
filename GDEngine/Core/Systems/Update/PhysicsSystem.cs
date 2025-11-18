using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using GDEngine.Core.Components;
using GDEngine.Core.Entities;
using GDEngine.Core.Enums;
using GDEngine.Core.Events;
using GDEngine.Core.Systems.Base;
using GDEngine.Core.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;
using MathHelper = Microsoft.Xna.Framework.MathHelper;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace GDEngine.Core.Systems
{
    /// <summary>
    /// The main physics management system using BepuPhysics v2.
    /// Handles simulation stepping, body registration, syncing Transforms,
    /// collision callbacks, gravity, and runtime body-type switching.
    /// </summary>
    public sealed class PhysicsSystem : SystemBase, IDisposable
    {
        #region Fields

        private Simulation _simulation = null!;
        private BufferPool _bufferPool = null!;
        private ThreadDispatcher _threadDispatcher = null!;

        private readonly List<RigidBody> _dynamicBodies = new List<RigidBody>(256);
        private readonly List<RigidBody> _kinematicBodies = new List<RigidBody>(64);
        private readonly List<RigidBody> _staticBodies = new List<RigidBody>(512);

        private readonly Dictionary<BodyHandle, RigidBody> _handleToComponent =
            new Dictionary<BodyHandle, RigidBody>(512);

        private Vector3 _gravity = new Vector3(0, -9.81f, 0);

        private int _velocityIterations = 12;
        private int _substepCount = 1;

        private float _fixedTimestep = -1f; // -1 = variable timestep
        private float _accumulator = 0f;

        private bool _disposed = false;

        #endregion


        #region Properties

        /// <summary>
        /// Global gravity. Runtime changes correctly update the simulation.
        /// </summary>
        public Vector3 Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }


        public int VelocityIterations
        {
            get => _velocityIterations;
            set => _velocityIterations = Math.Max(1, value);
        }

        public int SubstepCount
        {
            get => _substepCount;
            set => _substepCount = Math.Max(1, value);
        }

        public float FixedTimestep
        {
            get => _fixedTimestep;
            set => _fixedTimestep = value;
        }

        public Simulation Simulation => _simulation;

        #endregion


        #region Constructor

        public PhysicsSystem(int order = -50)
            : base(FrameLifecycle.LateUpdate, order)
        {
        }

        #endregion


        #region Registration Methods

        internal void RegisterBody(RigidBody rb)
        {
            switch (rb.BodyType)
            {
                case BodyType.Dynamic:
                    _dynamicBodies.Add(rb);
                    break;

                case BodyType.Kinematic:
                    _kinematicBodies.Add(rb);
                    break;

                case BodyType.Static:
                    _staticBodies.Add(rb);
                    break;
            }
        }

        internal void UnregisterBody(RigidBody rb)
        {
            _dynamicBodies.Remove(rb);
            _kinematicBodies.Remove(rb);
            _staticBodies.Remove(rb);

            if (rb.BodyHandle.HasValue)
                _handleToComponent.Remove(rb.BodyHandle.Value);
        }

        /// <summary>
        /// Called when a RigidBody switches Static/Dynamic/Kinematic at runtime.
        /// Ensures that bookkeeping in PhysicsSystem stays consistent.
        ///</summary>
        internal void NotifyBodyTypeChanged(RigidBody rb, BodyType oldType, BodyType newType)
        {
            _dynamicBodies.Remove(rb);
            _kinematicBodies.Remove(rb);
            _staticBodies.Remove(rb);

            switch (newType)
            {
                case BodyType.Dynamic:
                    _dynamicBodies.Add(rb);
                    break;

                case BodyType.Kinematic:
                    _kinematicBodies.Add(rb);
                    break;

                case BodyType.Static:
                    _staticBodies.Add(rb);
                    break;
            }
        }

        #endregion


        #region Simulation Add/Remove

        internal BodyHandle AddBodyToSimulation(RigidBody rb, BodyDescription desc)
        {
            var handle = _simulation.Bodies.Add(desc);
            _handleToComponent[handle] = rb;
            return handle;
        }

        internal StaticHandle AddStaticToSimulation(RigidBody rb, StaticDescription desc)
        {
            return _simulation.Statics.Add(desc);
        }

        internal void RemoveBodyFromSimulation(BodyHandle handle)
        {
            if (_simulation.Bodies.BodyExists(handle))
            {
                _simulation.Bodies.Remove(handle);
                _handleToComponent.Remove(handle);
            }
        }

        internal void RemoveStaticFromSimulation(StaticHandle handle)
        {
            if (_simulation.Statics.StaticExists(handle))
                _simulation.Statics.Remove(handle);
        }

        #endregion


        #region Lifecycle: OnAdded / Update / OnRemoved

        protected override void OnAdded()
        {
            _bufferPool = new BufferPool();
            _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount);

            var narrow = new NarrowPhaseCallbacks(this);
            var integrator = new PoseIntegratorCallbacks(this);   // <— pass reference to system
            var solve = new SolveDescription(_velocityIterations, _substepCount);

            _simulation = Simulation.Create(
                _bufferPool,
                narrow,
                integrator,
                solve
            );
        }


        public override void Update(float dt)
        {
            if (!Enabled)
                return;

            if (dt <= 0f)
                return;

            if (_fixedTimestep > 0f)
            {
                _accumulator += dt;

                while (_accumulator >= _fixedTimestep)
                {
                    Step(_fixedTimestep);
                    _accumulator -= _fixedTimestep;
                }
            }
            else
            {
                Step(dt);
            }
        }


        protected override void OnRemoved()
        {
            Dispose();
        }

        #endregion


        #region Simulation Step

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Step(float dt)
        {
            // 1. Sync TRANSFORM → PHYSICS for kinematics
            SyncKinematics();

            // 2. Run substeps
            float sdt = dt / _substepCount;
            for (int i = 0; i < _substepCount; i++)
                _simulation.Timestep(sdt, _threadDispatcher);

            // 3. Sync PHYSICS → TRANSFORM for dynamics
            SyncDynamics();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SyncKinematics()
        {
            foreach (var rb in _kinematicBodies)
            {
                if (rb.Enabled)
                    rb.SyncToPhysics();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SyncDynamics()
        {
            foreach (var rb in _dynamicBodies)
            {
                if (rb.Enabled)
                    rb.SyncFromPhysics();
            }
        }

        #endregion


        #region Raycast Stub

        // Leaving as a stub for now (Bepu v2 raycast example available if needed)
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            return false;
        }

        #endregion


        #region Disposal

        public void Dispose()
        {
            if (_disposed)
                return;

            _dynamicBodies.Clear();
            _kinematicBodies.Clear();
            _staticBodies.Clear();
            _handleToComponent.Clear();

            _simulation?.Dispose();
            _threadDispatcher?.Dispose();
            _bufferPool?.Clear();

            _disposed = true;
            System.Diagnostics.Debug.WriteLine("[PhysicsSystem] Disposed.");
        }

        #endregion


        #region NarrowPhase & PoseIntegrator Structs

        private struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
        {
            private PhysicsSystem _system;

            public NarrowPhaseCallbacks(PhysicsSystem sys)
            {
                _system = sys;
            }

            public void Initialize(Simulation simulation) { }

            public bool AllowContactGeneration(
                int workerIndex,
                CollidableReference a,
                CollidableReference b,
                ref float speculativeMargin)
            {
                speculativeMargin = 0.05f;
                return true;
            }

            public bool AllowContactGeneration(
                int workerIndex,
                CollidablePair pair,
                int childA,
                int childB) => true;

            public bool ConfigureContactManifold<TManifold>(
                int workerIndex,
                CollidablePair pair,
                ref TManifold manifold,
                out PairMaterialProperties props)
                where TManifold : unmanaged, IContactManifold<TManifold>
            {
                props = new PairMaterialProperties
                {
                    FrictionCoefficient = 0.5f,
                    MaximumRecoveryVelocity = 10f,
                    SpringSettings = new SpringSettings(120, 1)
                };
                return true;
            }

            public bool ConfigureContactManifold(
                int workerIndex,
                CollidablePair pair,
                int childA,
                int childB,
                ref ConvexContactManifold manifold) => true;

            public void Dispose() { }
        }


        private struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
        {
            private readonly PhysicsSystem _system;

            public PoseIntegratorCallbacks(PhysicsSystem system)
            {
                _system = system;
            }

            public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
            public readonly bool AllowSubstepsForUnconstrainedBodies => false;
            public readonly bool IntegrateVelocityForKinematics => false;

            public void Initialize(Simulation simulation) { }

            public void PrepareForIntegration(float dt) { }

            // Gravity is read directly from PhysicsSystem every frame
            public void IntegrateVelocity(
                System.Numerics.Vector<int> bodyIndices,
                Vector3Wide position,
                QuaternionWide orientation,
                BodyInertiaWide localInertia,
                System.Numerics.Vector<int> integrationMask,
                int workerIndex,
                System.Numerics.Vector<float> dt,
                ref BodyVelocityWide velocity)
            {
                var g = _system.Gravity.ToBepu();

                Vector3Wide gravityWide;
                Vector3Wide.Broadcast(g, out gravityWide);

                velocity.Linear.X += gravityWide.X * dt;
                velocity.Linear.Y += gravityWide.Y * dt;
                velocity.Linear.Z += gravityWide.Z * dt;
            }
        }


        #endregion
    }


    /// <summary>
    /// Result of a raycast query.
    /// </summary>
    public struct RaycastHit
    {
        public RigidBody? Body;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
    }
    /// <summary>
    /// Renders wireframe debug visualization for physics colliders in PostRender.
    /// Shows boxes, spheres, and capsules as colored wireframes to help debug physics issues.
    /// </summary>
    /// <remarks>
    /// Color coding:
    /// - Green: Static bodies (immovable)
    /// - Blue: Kinematic bodies (animated)
    /// - Yellow: Dynamic bodies (physics-driven)
    /// - Red: Triggers (no collision response)
    /// </remarks>
    public sealed class PhysicsDebugRenderer : SystemBase
    {
        #region Fields
        private Scene _scene = null!;
        private GraphicsDevice _device = null!;
        private BasicEffect _effect = null!;

        // Wireframe primitives cache
        private VertexPositionColor[] _boxVertices = null!;
        private short[] _boxIndices = null!;
        private VertexPositionColor[] _sphereVertices = null!;
        private short[] _sphereIndices = null!;

        // Settings
        private bool _enabled = true;
        private Color _staticColor = Color.Green;
        private Color _kinematicColor = Color.Blue;
        private Color _dynamicColor = Color.Yellow;
        private Color _triggerColor = Color.Red;

        // Sphere resolution
        private const int SphereSegments = 16;
        private const int SphereRings = 8;
        #endregion

        #region Properties
        /// <summary>
        /// Color for static bodies (default: Green).
        /// </summary>
        public Color StaticColor
        {
            get => _staticColor;
            set => _staticColor = value;
        }

        /// <summary>
        /// Color for kinematic bodies (default: Blue).
        /// </summary>
        public Color KinematicColor
        {
            get => _kinematicColor;
            set => _kinematicColor = value;
        }

        /// <summary>
        /// Color for dynamic bodies (default: Yellow).
        /// </summary>
        public Color DynamicColor
        {
            get => _dynamicColor;
            set => _dynamicColor = value;
        }

        /// <summary>
        /// Color for trigger colliders (default: Red).
        /// </summary>
        public Color TriggerColor
        {
            get => _triggerColor;
            set => _triggerColor = value;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a PhysicsDebugRenderer in PostRender lifecycle.
        /// </summary>
        public PhysicsDebugRenderer(int order = 100)
            : base(FrameLifecycle.PostRender, order)
        {
        }
        #endregion

        #region Lifecycle Methods
        protected override void OnAdded()
        {
            if (Scene == null)
                throw new InvalidOperationException("PhysicsDebugRenderer requires a Scene.");

            _scene = Scene;
            _device = _scene.Context.GraphicsDevice;

            // Create BasicEffect for wireframe rendering
            _effect = new BasicEffect(_device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };

            // Initialize primitive geometry
            InitializeBoxWireframe();
            InitializeSphereWireframe();
        }

        public override void Draw(float deltaTime)
        {
            if (!_enabled)
                return;

            // Get active camera
            var camera = _scene.ActiveCamera;
            if (camera == null)
                return;

            // Set up effect matrices
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            // Disable depth write but enable depth test for wireframe overlay
            var oldDepthStencilState = _device.DepthStencilState;
            _device.DepthStencilState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = false
            };

            // Render all rigidbodies in the scene
            foreach (var gameObject in _scene.GameObjects)
            {
                var rigidBody = gameObject.GetComponent<RigidBody>();
                if (rigidBody == null || !rigidBody.Enabled)
                    continue;

                var collider = gameObject.GetComponent<Collider>();
                if (collider == null)
                    continue;

                // Determine color based on body type and trigger status
                Color color = GetDebugColor(rigidBody, collider);

                // Render based on collider type
                if (collider is BoxCollider boxCollider)
                {
                    DrawBoxCollider(gameObject.Transform, boxCollider, color);
                }
                else if (collider is SphereCollider sphereCollider)
                {
                    DrawSphereCollider(gameObject.Transform, sphereCollider, color);
                }
                else if (collider is CapsuleCollider capsuleCollider)
                {
                    DrawCapsuleCollider(gameObject.Transform, capsuleCollider, color);
                }
            }

            // Restore depth state
            _device.DepthStencilState = oldDepthStencilState;
        }

        protected override void OnRemoved()
        {
            _effect?.Dispose();
        }
        #endregion

        #region Drawing Methods
        private void DrawBoxCollider(Transform transform, BoxCollider box, Color color)
        {
            // Extract position and rotation from transform (without scale)
            var position = transform.Position;
            var rotation = transform.Rotation;

            // Apply collider center offset in world space
            var rotatedCenter = Vector3.Transform(box.Center, rotation);
            var worldPosition = position + rotatedCenter;

            // Build world matrix: Scale by collider size, then rotate and translate
            // NOTE: box.Size is already in world units, so we don't multiply by transform scale
            Matrix world = Matrix.CreateScale(box.Size) *
                          Matrix.CreateFromQuaternion(rotation) *
                          Matrix.CreateTranslation(worldPosition);

            _effect.World = world;
            _effect.DiffuseColor = color.ToVector3();

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(
                    PrimitiveType.LineList,
                    _boxVertices,
                    0,
                    _boxVertices.Length,
                    _boxIndices,
                    0,
                    _boxIndices.Length / 2
                );
            }
        }



        private void DrawSphereCollider(Transform transform, SphereCollider sphere, Color color)
        {
            // Extract position and rotation from transform (without scale)
            var position = transform.Position;
            var rotation = transform.Rotation;

            // Apply collider center offset in world space
            var rotatedCenter = Vector3.Transform(sphere.Center, rotation);
            var worldPosition = position + rotatedCenter;

            // Build world matrix: Scale by sphere diameter, then rotate and translate
            // NOTE: sphere.Radius is already in world units, so we don't multiply by transform scale
            Matrix world = Matrix.CreateScale(sphere.Radius * 2f) *
                          Matrix.CreateFromQuaternion(rotation) *
                          Matrix.CreateTranslation(worldPosition);

            _effect.World = world;
            _effect.DiffuseColor = color.ToVector3();

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(
                    PrimitiveType.LineList,
                    _sphereVertices,
                    0,
                    _sphereVertices.Length,
                    _sphereIndices,
                    0,
                    _sphereIndices.Length / 2
                );
            }
        }


        private void DrawCapsuleCollider(Transform transform, CapsuleCollider capsule, Color color)
        {
            // For simplicity, draw capsule as a combination of cylinder + 2 spheres
            // More accurate would be to build actual capsule geometry

            // Extract position and rotation from transform (without scale)
            var position = transform.Position;
            var rotation = transform.Rotation;

            // Apply collider center offset in world space
            var rotatedCenter = Vector3.Transform(capsule.Center, rotation);
            var worldPosition = position + rotatedCenter;

            float radius = capsule.Radius;
            float height = capsule.Height;

            // Draw cylinder body (simplified as box for now)
            // NOTE: capsule dimensions are already in world units
            Matrix world = Matrix.CreateScale(radius * 2f, height, radius * 2f) *
                          Matrix.CreateFromQuaternion(rotation) *
                          Matrix.CreateTranslation(worldPosition);

            _effect.World = world;
            _effect.DiffuseColor = color.ToVector3();

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(
                    PrimitiveType.LineList,
                    _boxVertices,
                    0,
                    _boxVertices.Length,
                    _boxIndices,
                    0,
                    _boxIndices.Length / 2
                );
            }

            // TODO: Draw hemispherical caps at top and bottom
        }


        private Color GetDebugColor(RigidBody rigidBody, Collider collider)
        {
            // Triggers override everything
            if (collider.IsTrigger)
                return _triggerColor;

            // Body type colors
            switch (rigidBody.BodyType)
            {
                case BodyType.Static:
                    return _staticColor;
                case BodyType.Kinematic:
                    return _kinematicColor;
                case BodyType.Dynamic:
                    return _dynamicColor;
                default:
                    return Color.White;
            }
        }
        #endregion

        #region Initialization Methods
        private void InitializeBoxWireframe()
        {
            // Unit cube centered at origin (will be scaled by collider size)
            float half = 0.5f;

            _boxVertices = new VertexPositionColor[8]
            {
                new VertexPositionColor(new Vector3(-half, -half, -half), Color.White), // 0: LBB (left-bottom-back)
                new VertexPositionColor(new Vector3( half, -half, -half), Color.White), // 1: RBB
                new VertexPositionColor(new Vector3(-half,  half, -half), Color.White), // 2: LTB (left-top-back)
                new VertexPositionColor(new Vector3( half,  half, -half), Color.White), // 3: RTB
                new VertexPositionColor(new Vector3(-half, -half,  half), Color.White), // 4: LBF (left-bottom-front)
                new VertexPositionColor(new Vector3( half, -half,  half), Color.White), // 5: RBF
                new VertexPositionColor(new Vector3(-half,  half,  half), Color.White), // 6: LTF
                new VertexPositionColor(new Vector3( half,  half,  half), Color.White)  // 7: RTF
            };

            // 12 edges (24 indices as line list)
            _boxIndices = new short[]
            {
                // Back face
                0, 1,  1, 3,  3, 2,  2, 0,
                // Front face
                4, 5,  5, 7,  7, 6,  6, 4,
                // Connecting edges
                0, 4,  1, 5,  2, 6,  3, 7
            };
        }

        private void InitializeSphereWireframe()
        {
            var vertices = new List<VertexPositionColor>();
            var indices = new List<short>();

            // Generate sphere vertices (unit sphere, will be scaled)
            for (int ring = 0; ring <= SphereRings; ring++)
            {
                float phi = ring * MathHelper.Pi / SphereRings;
                float y = (float)Math.Cos(phi);
                float ringRadius = (float)Math.Sin(phi);

                for (int seg = 0; seg <= SphereSegments; seg++)
                {
                    float theta = seg * MathHelper.TwoPi / SphereSegments;
                    float x = ringRadius * (float)Math.Cos(theta);
                    float z = ringRadius * (float)Math.Sin(theta);

                    vertices.Add(new VertexPositionColor(
                        new Vector3(x, y, z) * 0.5f, // Scale to unit radius (will scale by diameter)
                        Color.White
                    ));
                }
            }

            // Generate line indices for latitude rings
            for (int ring = 0; ring < SphereRings; ring++)
            {
                for (int seg = 0; seg < SphereSegments; seg++)
                {
                    int current = ring * (SphereSegments + 1) + seg;
                    int next = current + 1;

                    // Horizontal line
                    indices.Add((short)current);
                    indices.Add((short)next);
                }
            }

            // Generate line indices for longitude lines
            for (int seg = 0; seg <= SphereSegments; seg++)
            {
                for (int ring = 0; ring < SphereRings; ring++)
                {
                    int current = ring * (SphereSegments + 1) + seg;
                    int below = (ring + 1) * (SphereSegments + 1) + seg;

                    // Vertical line
                    indices.Add((short)current);
                    indices.Add((short)below);
                }
            }

            _sphereVertices = vertices.ToArray();
            _sphereIndices = indices.ToArray();
        }
        #endregion

        #region Housekeeping Methods
        public override string ToString()
        {
            return $"PhysicsDebugRenderer(Enabled={_enabled})";
        }
        #endregion
    }


}