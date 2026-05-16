using Il2Cpp;
using MelonLoader;
using System;
using System.Reflection;
using UnityEngine;
using FishNetNetworkObject = Il2CppFishNet.Object.NetworkObject;

public static class FakeServerPlayer
{
    public const string ProductId = "00000000000000000000000000000000";
    public const string VoiceId = "";
    public const int ConnectionId = 32766;
    public const long PlatformUserId = 1234567898765432L;

    private static PlayerReference _fakePlayerReference;
    private static GameObject _fakePlayerObject;

    public static bool IsFakeServerReference(PlayerReference pr)
    {
        if (pr == null)
            return false;

        return pr.ConnectionID == ConnectionId
            || pr.PlatformUserId == PlatformUserId
            || pr.ProductUserId == ProductId;
    }

    public static bool IsFakeServerPlayerControl(PlayerControl pc)
    {
        if (pc == null || _fakePlayerObject == null)
            return false;

        return pc.gameObject == _fakePlayerObject;
    }

    public static PlayerReference AddFakeServerPlayerReference()
    {
        var manager = PlayerReferenceManager.Instance;

        if (manager == null || manager.sync_PlayerReferences == null)
        {
            Debug.LogError("[Chat] Cannot create fake server player: player references are not ready.");
            return null;
        }

        PlayerReference existing = FindExisting(manager);
        if (existing != null)
        {
            _fakePlayerReference = existing;
            return existing;
        }

        if (manager.sync_PlayerReferences.Count == 0)
        {
            Debug.LogError("[Chat] Cannot create fake server player: no source player reference exists.");
            return null;
        }

        var src = manager.sync_PlayerReferences[0];

        if (src == null || src.PlayerControl == null)
        {
            Debug.LogError("[Chat] Cannot create fake server player: source PlayerControl is null.");
            return null;
        }

        _fakePlayerReference = null;

        PlayerControl fakePc = CreateFakePlayerControl(src);

        manager.Server_AddPlayerReference(
            ProductId,
            PlatformUserId,
            ConnectionId,
            "Server",
            VoiceId,
            src.AuthPlatform,
            fakePc
        );

        var fake = FindExisting(manager);

        if (fake == null)
        {
            Debug.LogError("[Chat] Failed to create fake server PlayerReference.");
            return null;
        }

        _fakePlayerReference = fake;

        if (!manager._communicationPoliciesByPlatformUserId.ContainsKey(PlatformUserId))
            manager.WarmCommunicationPolicy(fake, true);

        Debug.Log("[Chat] Fake server PlayerReference created.");

        return fake;
    }


    private static PlayerControl CreateFakePlayerControl(PlayerReference src)
    {
        if (_fakePlayerObject != null)
        {
            var existingPc = _fakePlayerObject.GetComponent<PlayerControl>();
            if (existingPc != null)
                return existingPc;

            UnityEngine.Object.Destroy(_fakePlayerObject);
            _fakePlayerObject = null;
        }

        var fakeGo = new GameObject("Fake Server PlayerControl");
        _fakePlayerObject = fakeGo;

        fakeGo.transform.position = src.PlayerControl.transform.position + new Vector3(0f, -9999f, 0f);

        var networkObject = fakeGo.AddComponent<FishNetNetworkObject>();
        var fakePc = fakeGo.AddComponent<PlayerControl>();

        UnityEngine.Object.DontDestroyOnLoad(fakeGo);

        try
        {
            var manager = PlayerReferenceManager.Instance;

            // Use this if PlayerReferenceManager exposes Spawn.
            manager.Spawn(fakeGo);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Chat] Failed to spawn fake PlayerControl NetworkObject. It may not be network-valid. " + ex);
        }

        return fakePc;
    }

    private static PlayerReference FindExisting(PlayerReferenceManager manager)
    {
        if (manager == null || manager.sync_PlayerReferences == null)
            return null;

        for (int i = 0; i < manager.sync_PlayerReferences.Count; i++)
        {
            var pr = manager.sync_PlayerReferences[i];

            if (IsFakeServerReference(pr))
                return pr;
        }

        return null;
    }

    public static void RemoveFakeServerPlayerReference()
    {
        if (_fakePlayerObject != null)
        {
            UnityEngine.Object.Destroy(_fakePlayerObject);
            _fakePlayerObject = null;
        }

        _fakePlayerReference = null;
    }
}