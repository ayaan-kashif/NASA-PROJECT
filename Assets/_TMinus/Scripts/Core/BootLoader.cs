using UnityEngine;

namespace TMinus.Core
{
    /// <summary>Sits in the Boot scene. GameState is already up (it self-creates); go to the menu.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        void Start()
        {
            SceneFlow.Load(SceneNames.MainMenu);
        }
    }
}
