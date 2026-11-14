using UnityEngine.UIElements;

namespace TMinus.Flight
{
    /// <summary>The time slider of the failure replay (audit F24): drag back to see the flight at any second, down to the moment things went wrong.</summary>
    public static class ReplayScrubber
    {
        public static void Build(VisualElement parent, FlightController controller, double flightTimeS)
        {
            if (controller == null || flightTimeS <= 0) return;
            var label = new Label("Replay: drag to any moment of the flight. The numbers, position and camera follow; the 3D model keeps the stages it already dropped.");
            label.AddToClassList("check-detail");
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            var time = new Label($"T+{flightTimeS:0} s");
            time.AddToClassList("check-title");
            var slider = new Slider("Time (s)", 0f, (float)flightTimeS) { value = (float)flightTimeS };
            slider.AddToClassList("kick-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                time.text = $"T+{e.newValue:0} s";
                controller.ScrubTo(e.newValue);
            });
            parent.Add(time);
            parent.Add(slider);
        }
    }
}
