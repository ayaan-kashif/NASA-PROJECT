using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace TMinus.EditorTools
{
    /// <summary>
    /// Records the Game view to MP4 (1920x1080, 30 fps, Unity Recorder) for the demo video. Used by the play script
    /// (`call TMinus.EditorTools.DemoRecorder.Start <take>` / `Stop x`) and never run in a player build.
    /// Takes are written to Builds/DemoVideo/ (git-ignored: they are large).
    /// </summary>
    public static class DemoRecorder
    {
        public const string OutDir = "Builds/DemoVideo";
        static RecorderController _ctrl;
        static string _take;

        public static string Start(string take)
        {
            if (_ctrl != null) return "already recording " + _take;
            if (string.IsNullOrEmpty(take)) take = "take";
            Directory.CreateDirectory(OutDir);
            _take = take;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.FrameRate = 30;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.CapFrameRate = true;
            settings.ExitPlayMode = false;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = take;
            movie.Enabled = true;
            movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movie.VideoBitRateMode = VideoBitrateMode.High;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.OutputFile = OutDir + "/" + take;
            settings.AddRecorderSettings(movie);

            _ctrl = new RecorderController(settings);
            _ctrl.PrepareRecording();
            bool ok = _ctrl.StartRecording();
            return ok ? "recording " + take + " to " + OutDir : "could not start " + take;
        }

        public static string Stop(string _)
        {
            if (_ctrl == null) return "not recording";
            _ctrl.StopRecording();
            string t = _take;
            _ctrl = null; _take = null;
            return "stopped " + t;
        }
    }
}
