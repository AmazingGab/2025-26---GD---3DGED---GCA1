using GDEngine.Core.Rendering;
using Microsoft.Xna.Framework;

namespace GDEngine.Core.Culling
{
    /// <summary>
    /// Write-once octree for static (immovable) renderers. Used for bake-time PVS and runtime coarse queries.
    /// </summary>
    public sealed class Octree
    {
        #region Static Fields
        #endregion

        #region Fields
        private Node _root;
        private int _maxDepth;
        private int _leafCapacity;
        #endregion

        #region Properties
        public BoundingBox Bounds => _root.Bounds;
        public int MaxDepth => _maxDepth;
        public int LeafCapacity => _leafCapacity;
        #endregion

        #region Constructors
        public Octree(BoundingBox sceneBounds, int maxDepth = 6, int leafCapacity = 16)
        {
            _root = new Node(sceneBounds);
            _maxDepth = Math.Max(1, maxDepth);
            _leafCapacity = Math.Max(1, leafCapacity);
        }
        #endregion

        #region Methods
        public void Insert(MeshRenderer meshRenderer)
        {
            if (meshRenderer == null)
                throw new ArgumentNullException(nameof(meshRenderer));

            if (meshRenderer.GameObject == null)
                return;

            if (!meshRenderer.GameObject.IsStatic)
                return;

            if (!meshRenderer.TryGetWorldBounds(out BoundingBox world))
                return;

            InsertRecursive(_root, meshRenderer, world, 0);
        }

        public void Query(BoundingFrustum frustum, List<MeshRenderer> destinationList)
        {
            if (destinationList == null)
                throw new ArgumentNullException(nameof(destinationList));

            destinationList.Clear();
            QueryRecursive(_root, frustum, destinationList);
        }

        public bool TryFindLeafKey(Vector3 position, out OctreeKey key)
        {
            return TryFindLeafKeyRecursive(_root, position, 0, out key);
        }
        #endregion

        #region Lifecycle Methods
        #endregion

        #region Housekeeping Methods
        private void InsertRecursive(Node node, MeshRenderer meshRenderer, BoundingBox world, int depth)
        {
            if (depth >= _maxDepth || node.IsLeaf && node.Items.Count < _leafCapacity)
            {
                node.Items.Add(new Item { Renderer = meshRenderer, WorldBounds = world });
                return;
            }

            if (node.IsLeaf)
                node.Subdivide();

            int childIndex = node.ChildIndexContaining(world);
            if (childIndex >= 0)
            {
                InsertRecursive(node.Children[childIndex], meshRenderer, world, depth + 1);
                return;
            }

            node.Items.Add(new Item { Renderer = meshRenderer, WorldBounds = world });
        }

        private void QueryRecursive(Node node, BoundingFrustum frustum, List<MeshRenderer> destinationList)
        {
            ContainmentType c = frustum.Contains(node.Bounds);
            if (c == ContainmentType.Disjoint)
                return;

            if (c == ContainmentType.Contains)
            {
                for (int i = 0; i < node.Items.Count; i++)
                    destinationList.Add(node.Items[i].Renderer);

                if (!node.IsLeaf)
                {
                    for (int i = 0; i < 8; i++)
                        QueryRecursive(node.Children[i], frustum, destinationList);
                }
                return;
            }

            for (int i = 0; i < node.Items.Count; i++)
            {
                if (frustum.Contains(node.Items[i].WorldBounds) != ContainmentType.Disjoint)
                    destinationList.Add(node.Items[i].Renderer);
            }

            if (!node.IsLeaf)
            {
                for (int i = 0; i < 8; i++)
                    QueryRecursive(node.Children[i], frustum, destinationList);
            }
        }

        private bool TryFindLeafKeyRecursive(Node node, Vector3 position, int depth, out OctreeKey key)
        {
            key = default;

            if (!node.Bounds.Contains(position).HasFlag(ContainmentType.Contains))
                return false;

            if (node.IsLeaf || depth >= _maxDepth)
            {
                key = new OctreeKey(node.Bounds, depth);
                return true;
            }

            for (int i = 0; i < 8; i++)
            {
                if (node.Children[i].Bounds.Contains(position) == ContainmentType.Contains)
                    return TryFindLeafKeyRecursive(node.Children[i], position, depth + 1, out key);
            }

            key = new OctreeKey(node.Bounds, depth);
            return true;
        }
        #endregion

        #region Nested types
        public readonly struct OctreeKey : IEquatable<OctreeKey>
        {
            public readonly BoundingBox Bounds;
            public readonly int Depth;

            public OctreeKey(BoundingBox bounds, int depth)
            {
                Bounds = bounds;
                Depth = depth;
            }

            public bool Equals(OctreeKey other)
            {
                return Depth == other.Depth && Bounds.Min == other.Bounds.Min && Bounds.Max == other.Bounds.Max;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Depth;
                    h = (h * 397) ^ Bounds.Min.GetHashCode();
                    h = (h * 397) ^ Bounds.Max.GetHashCode();
                    return h;
                }
            }

            public override string ToString()
            {
                return $"OctreeKey(Depth:{Depth}, Min:{Bounds.Min}, Max:{Bounds.Max})";
            }
        }

        private sealed class Node
        {
            public BoundingBox Bounds;
            public Node[] Children;
            public List<Item> Items;

            public bool IsLeaf => Children == null;

            public Node(BoundingBox bounds)
            {
                Bounds = bounds;
                Items = new List<Item>(8);
            }

            public void Subdivide()
            {
                if (Children != null)
                    return;

                Children = new Node[8];
                Vector3 center = (Bounds.Min + Bounds.Max) * 0.5f;

                int idx = 0;
                for (int z = 0; z < 2; z++)
                    for (int y = 0; y < 2; y++)
                        for (int x = 0; x < 2; x++)
                        {
                            Vector3 min = new Vector3(
                                x == 0 ? Bounds.Min.X : center.X,
                                y == 0 ? Bounds.Min.Y : center.Y,
                                z == 0 ? Bounds.Min.Z : center.Z
                            );
                            Vector3 max = new Vector3(
                                x == 0 ? center.X : Bounds.Max.X,
                                y == 0 ? center.Y : Bounds.Max.Y,
                                z == 0 ? center.Z : Bounds.Max.Z
                            );
                            Children[idx++] = new Node(new BoundingBox(min, max));
                        }
            }

            public int ChildIndexContaining(BoundingBox box)
            {
                if (Children == null)
                    return -1;

                for (int i = 0; i < 8; i++)
                {
                    if (Contains(Children[i].Bounds, box))
                        return i;
                }
                return -1;
            }

            private static bool Contains(BoundingBox a, BoundingBox b)
            {
                return a.Min.X <= b.Min.X && a.Min.Y <= b.Min.Y && a.Min.Z <= b.Min.Z &&
                       a.Max.X >= b.Max.X && a.Max.Y >= b.Max.Y && a.Max.Z >= b.Max.Z;
            }
        }

        private struct Item
        {
            public MeshRenderer Renderer;
            public BoundingBox WorldBounds;
        }
        #endregion
    }
}
