using GDEngine.Core.Input.Devices;
using Microsoft.Xna.Framework.Input;

namespace GDEngine.Core.Extensions
{
    /// <summary>
    /// Extension to GDMouseInput to support UI button and slider interactions.
    /// Automatically feeds MouseState updates to IInputReceiver components.
    /// </summary>
    /// <remarks>
    /// Add this as a partial class extension to your existing GDMouseInput,
    /// or incorporate the UpdateUIComponents method into your existing mouse input device.
    /// </remarks>
    public static class GDMouseInputUIExtensions
    {
        /// <summary>
        /// Feed current mouse state to UI components that need it.
        /// Call this from your GDMouseInput.Update() method after updating _current.
        /// </summary>
        public static void FeedMouseStateToUI(this GDMouseInput mouseInput, MouseState currentState, IEnumerable<IInputReceiver> receivers)
        {
            if (receivers == null)
                return;

            // Update each receiver that needs mouse state
            foreach (var receiver in receivers)
            {
                // Use reflection to update mouse state on UI components
                // This allows buttons and sliders to track mouse movement and clicks
                UpdateMouseStateOnReceiver(receiver, currentState);
            }
        }

        private static void UpdateMouseStateOnReceiver(IInputReceiver receiver, MouseState currentState)
        {
            var receiverType = receiver.GetType();

            // Try to find _currentMouseState field
            var mouseStateField = receiverType.GetField("_currentMouseState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (mouseStateField != null)
            {
                mouseStateField.SetValue(receiver, currentState);
            }
        }
    }

    /// <summary>
    /// Alternative: Modified GDMouseInput implementation with built-in UI support.
    /// Use this as a template to update your existing GDMouseInput class.
    /// </summary>
    /// <remarks>
    /// This is a reference implementation showing where to add the UI update call.
    /// Integrate this into your existing GDMouseInput.cs file.
    /// </remarks>
    public class GDMouseInput_UISupported_Reference
    {
        /*
        // Add to your existing GDMouseInput class:

        private MouseState _current;
        private MouseState _previous;
        private List<IInputReceiver> _receivers = new List<IInputReceiver>(); // Track receivers

        public void Update(float deltaTime)
        {
            _previous = _current;
            _current = Mouse.GetState();

            // ... existing button detection logic ...

            // ADD THIS: Feed mouse state to UI components
            FeedMouseStateToUIComponents();
        }

        private void FeedMouseStateToUIComponents()
        {
            foreach (var receiver in _receivers)
            {
                // Update mouse state on UI components
                var receiverType = receiver.GetType();
                var mouseStateField = receiverType.GetField("_currentMouseState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (mouseStateField != null)
                {
                    mouseStateField.SetValue(receiver, _current);
                }
            }
        }

        public void Feed(IInputReceiver receiver)
        {
            // Track receiver for later mouse state updates
            if (!_receivers.Contains(receiver))
                _receivers.Add(receiver);

            // ... existing Feed implementation ...
        }

        public void ResetTransient()
        {
            // Clean up receiver tracking if needed
            // ... existing ResetTransient implementation ...
        }
        */
    }
}

// ============================================================================
// INTEGRATION INSTRUCTIONS
// ============================================================================
// To integrate mouse support into your existing GDMouseInput class:
//
// 1. Open your GDMouseInput.cs file
//
// 2. Add a field to track receivers:
//    private readonly List<IInputReceiver> _receivers = new List<IInputReceiver>();
//
// 3. In your Feed() method, add:
//    if (!_receivers.Contains(receiver))
//        _receivers.Add(receiver);
//
// 4. In your Update() method, AFTER updating _current, add:
//    FeedMouseStateToUIComponents();
//
// 5. Add this helper method:
//    private void FeedMouseStateToUIComponents()
//    {
//        foreach (var receiver in _receivers)
//        {
//            var receiverType = receiver.GetType();
//            var field = receiverType.GetField("_currentMouseState",
//                System.Reflection.BindingFlags.NonPublic | 
//                System.Reflection.BindingFlags.Instance);
//            field?.SetValue(receiver, _current);
//        }
//    }
//
// 6. (Optional) In ResetTransient(), add:
//    _receivers.Clear();
//
// This allows UIButton and UISlider to receive mouse state updates
// automatically through the input system.
// ============================================================================
