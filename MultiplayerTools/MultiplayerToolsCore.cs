using System;
using System.Collections;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using MelonLoader.NativeUtils;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(MultiplayerTools.MultiplayerToolsCore), "MultiplayerTools", "0.1.0", "Spyci")]

namespace MultiplayerTools
{
    public class MultiplayerToolsCore : MelonMod
    {
        private delegate void OnCreateLobbyCompletedDelegate(IntPtr __this, IntPtr createLobbyCallbackInfo, IntPtr methodInfo);

        private static OnCreateLobbyCompletedDelegate _detourDelegate;
        private static NativeHook<OnCreateLobbyCompletedDelegate> _hook;

        public interface IArbitrary
        {
            string Format();
        }

        public static MultiplayerToolsCore Instance;

        private PlayerReferenceManager playerReferenceManager;
        private PlayerReference localPlayer;
        private PlayerReference[] players = (PlayerReference[])(object)new PlayerReference[0];
        private GameObject[] espObjects = Array.Empty<GameObject>();

        public bool referencesLoaded = false;

        public void SavePrefs()
        {
            MelonPreferences.Save();
        }

        public override unsafe void OnInitializeMelon()
        {
            Instance = this;
            HarmonyInstance.PatchAll();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            referencesLoaded = false;
            MelonCoroutines.Start(LoadReferences());
        }

        private IEnumerator LoadReferences()
        {
            while ((Object)(object)playerReferenceManager == (Object)null)
            {
                playerReferenceManager = PlayerReferenceManager.Instance;
                yield return null;
            }

            Object.FindObjectsByType<StatueSetup>((FindObjectsSortMode)0).ToList();
            referencesLoaded = true;
            Patches.LobbyPatchFeatures.SetReferences(playerReferenceManager);
            Debug.Log("Loaded references.");
        }

        public override void OnUpdate()
        {
            if (localPlayer == null || localPlayer.PlayerControl == null)
                return;
        }

        public PlayerReference GetLocalPlayer()
        {
            return localPlayer;
        }

        public PlayerReference[] GetPlayers()
        {
            return (PlayerReference[])playerReferenceManager.GetPlayerReferences().Collection.ToArray();
        }
    }
}
