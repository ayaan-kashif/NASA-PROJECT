namespace TMinus.Core
{
    /// <summary>
    /// "Show me why" (audit F24): the debrief names the part that lost the mission, and this carries the player to it. The debrief sets
    /// the request; the room it names takes it when it loads, frames the joint or port and shows a one-line explanation.
    /// </summary>
    public static class FocusRequest
    {
        static string _scene, _target, _note;

        public static void Set(string scene, string target, string note)
        {
            _scene = scene; _target = target; _note = note;
        }

        /// <summary>Takes the request if it is for this room. A request is used once.</summary>
        public static bool TryTake(string scene, out string target, out string note)
        {
            target = null; note = null;
            if (_scene == null || _scene != scene) return false;
            target = _target; note = _note;
            _scene = _target = _note = null;
            return true;
        }
    }
}
