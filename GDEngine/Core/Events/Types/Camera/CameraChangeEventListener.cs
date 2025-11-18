using GDEngine.Core.Components;
using GDEngine.Core.Entities;
using GDEngine.Core.Events;
using GDEngine.Core.Events.Types.Camera;

namespace GDEngine.Core
{
    public class CameraChangeEventListener : Component
    {
        private Scene? _scene;
        private EventBus? _events;
        private Camera? _camera;

        protected override void Start()
        {
            if (GameObject == null) throw new NullReferenceException(nameof(GameObject));

            _scene = GameObject.Scene;

            if (_scene == null) throw new NullReferenceException(nameof(_scene));

            _events = _scene.Context.Events;

            _events.On<CameraChangeEvent>()
                .Do(HandleCameraChange);
        }

        private void HandleCameraChange(CameraChangeEvent @event)
        {
            string targetName = @event.TargetCameraName;
            var go = _scene.Find(go => go.Name.Equals(targetName));
            _camera = go.GetComponent<Camera>();
            if(_camera != null)
                _scene.SetActiveCamera(_camera);
        }
    }
}
