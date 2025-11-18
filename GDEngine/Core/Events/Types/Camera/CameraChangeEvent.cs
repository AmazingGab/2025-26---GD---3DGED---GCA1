namespace GDEngine.Core.Events.Types.Camera
{
    public class CameraChangeEvent
    {
        private string targetCameraName;

        public CameraChangeEvent(string targetCameraName)
        {
            this.targetCameraName = targetCameraName;
        }

        public string TargetCameraName { get => targetCameraName; set => targetCameraName = value; }
    }
}
