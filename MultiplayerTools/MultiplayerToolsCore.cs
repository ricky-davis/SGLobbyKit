using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppFishNet.Object;
using Il2CppFishNet.Object.Synchronizing;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cpp_Scripts.Managers;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;
using UnityEngine.Rendering;
using Input = UnityEngine.Input;
using static UnityEngine.Application;
using Object = UnityEngine.Object;
using static Il2Cpp.Hat;
using System.Reflection;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using System.Runtime.InteropServices;
using MelonLoader.NativeUtils;
using Il2CppInterop.Common;
using Il2CppMono.Security.X509;
using Il2Cpp_Scripts.Systems.Chat;


[assembly: MelonInfo(typeof(MultiplayerTools.MultiplayerToolsCore), "MultiplayerTools", "0.1.0", "Spyci")]

namespace MultiplayerTools
{
	public class MultiplayerToolsCore : MelonMod
    {
        private delegate void OnCreateLobbyCompletedDelegate(
        IntPtr __this,
        IntPtr createLobbyCallbackInfo,
        IntPtr methodInfo);

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
            // Prevent recursive calls to avoid stack overflow
            referencesLoaded = false;
            // Start loading references for the new scene
            MelonCoroutines.Start(LoadReferences());
            // Add any additional scene initialization logic here
        }


        private IEnumerator LoadReferences()
		{
			while ((Object)(object)playerReferenceManager == (Object)null)
			{
				playerReferenceManager = PlayerReferenceManager.Instance;
				yield return null;
            }
            
            (Object.FindObjectsByType<StatueSetup>((FindObjectsSortMode)0)).ToList();
            referencesLoaded = true;
            Patches.MultiplayerTools.Instance.SetReferences(playerReferenceManager);
            Debug.Log("Loaded references.");
		}

		public override void OnUpdate()
		{
			if (localPlayer == null || localPlayer.PlayerControl == null)
			{
				return;
            }
		}

		public PlayerReference GetLocalPlayer()
		{
			if (localPlayer == null)
			{
				return null;
			}
			return localPlayer;
		}
		public PlayerReference[] GetPlayers()
		{
			return (PlayerReference[])playerReferenceManager.GetPlayerReferences().Collection.ToArray();
		}
	}
}