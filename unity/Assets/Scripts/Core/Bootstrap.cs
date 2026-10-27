// C# / Assets/Scripts/Core/Bootstrap.cs / Unity 2022.3+ / spawns the game, whatever scene is open
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// The whole game lives in code, so it does not depend on a hand-wired scene: it
    /// boots itself before the first scene loads. Press Play on any scene and
    /// NIGHTFALL starts.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Launch()
        {
            if (Object.FindObjectOfType<GameApp>() != null) return;

            var go = new GameObject("NIGHTFALL");
            go.AddComponent<GameApp>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
