# Lobby Search Parameter Injection Notes

These notes capture what was found while investigating the decompiled and converted `UILobbyExplorer.GetListOfLobbies` search path.

## Source Quality

`GameSource/converted_cs` is LLM-converted C# from decompiled native output. It is useful for control flow and rough intent, but it can omit or mistranslate important details. For this search path, the converted C# missed how lobby attribute keys are assigned inside `AddSearchParam`.

When exact behavior matters, cross-check:

- `GameSource/converted_cs/_Scripts_UI_Pre_Game_UILobbyExplorer__GetListOfLobbies_498c8bd2.cs`
- `GameSource/converted_cs/_Scripts_UI_Pre_Game_UILobbyExplorer__AddSearchParam_ab063677.cs`
- `GameSource/converted_cs/_Scripts_Managers_LobbyManager__SearchByAttributes_dee787ce.cs`
- `GameSource/GameAssembly.dll.c`
- `GameSource/Extracted/Assembly-CSharp/_Scripts.UI.Pre_Game/UILobbyExplorer.cs`
- `GameSource/Extracted/Assembly-CSharp/_Scripts.Managers/LobbyManager.cs`

## Search Flow

`UILobbyExplorer.GetListOfLobbies` builds a `List<LobbySearchSetParameterOptions>` and passes it to `LobbyManager.SearchByAttributes`.

`LobbyManager.SearchByAttributes` creates an EOS lobby search and loops over each `LobbySearchSetParameterOptions`, calling `LobbySearch.SetParameter` for every item in the list. That makes the parameter list a clean injection point before EOS receives it.

Relevant extracted signatures:

```csharp
private void UILobbyExplorer.GetListOfLobbies()
private void UILobbyExplorer.AddSearchParam(
    List<LobbySearchSetParameterOptions> list,
    LobbyAttributeType type,
    object value,
    ComparisonOp op)

public void LobbyManager.SearchByAttributes(
    List<LobbySearchSetParameterOptions> searchParameters,
    EOSLobbyManager.OnLobbySearchCallback SearchCompleted)
```

In the generated Il2Cpp wrappers, the list type is:

```csharp
Il2CppSystem.Collections.Generic.List<Il2CppEpic.OnlineServices.Lobby.LobbySearchSetParameterOptions>
```

## EOS Search Lifecycle

The in-game search path ultimately goes through the Epic Online Services lobby search wrapper:

1. `LobbyManager.SearchByAttributes` receives an Il2Cpp `List<LobbySearchSetParameterOptions>`.
2. It gets `EOSManager.Instance` and then `GetEOSLobbyInterface()`.
3. It checks `EOSManager.LocalProductUserId` and calls `ProductUserId.IsValid()`.
4. It creates `CreateLobbySearchOptions` with `MaxResults = 20`.
5. It calls `LobbyInterface.CreateLobbySearch(options, out lobbySearch)`.
6. It loops the search parameter list and calls `lobbySearch.SetParameter(param)`.
7. It stores the search handle in `LobbyManager.CurrentSearch`.
8. It stores the completion delegate in `LobbyManager.LobbySearchCallback`.
9. It calls `lobbySearch.Find(findOptions, null, OnLobbySearchFindCallback)`.

The converted version simplifies some signatures, but reflection against the Il2Cpp assemblies shows the actual wrapper method:

```csharp
public void LobbyManager.SearchByAttributes(
    Il2CppSystem.Collections.Generic.List<LobbySearchSetParameterOptions> searchParameters,
    EOSLobbyManager.OnLobbySearchCallback SearchCompleted)
```

The relevant EOS wrapper types live under `Il2CppEpic.OnlineServices`:

```csharp
Il2CppEpic.OnlineServices.ComparisonOp
Il2CppEpic.OnlineServices.Utf8String
Il2CppEpic.OnlineServices.Lobby.AttributeData
Il2CppEpic.OnlineServices.Lobby.AttributeDataValue
Il2CppEpic.OnlineServices.Lobby.LobbyInterface
Il2CppEpic.OnlineServices.Lobby.LobbySearch
Il2CppEpic.OnlineServices.Lobby.LobbySearchSetParameterOptions
```

`LobbySearchSetParameterOptions` is an Il2Cpp reference wrapper with these useful public properties:

```csharp
Il2CppSystem.Nullable<AttributeData> Parameter { get; set; }
ComparisonOp ComparisonOp { get; set; }
```

`AttributeData` wraps the key and value:

```csharp
Utf8String Key { get; set; }
AttributeDataValue Value { get; set; }
```

`AttributeDataValue` stores one active typed value and exposes these useful properties:

```csharp
Il2CppSystem.Nullable<long> AsInt64 { get; set; }
Il2CppSystem.Nullable<double> AsDouble { get; set; }
Il2CppSystem.Nullable<bool> AsBool { get; set; }
Utf8String AsUtf8 { get; set; }
```

Important Il2Cpp wrapper details:

- `AttributeDataValue.AsInt64` expects `Il2CppSystem.Nullable<long>`, not a raw `long`.
- `AttributeDataValue.AsBool` expects `Il2CppSystem.Nullable<bool>`, not a raw `bool`.
- `LobbySearchSetParameterOptions.Parameter` expects `Il2CppSystem.Nullable<AttributeData>`, not a raw `AttributeData`.
- `string` can be converted to `Utf8String` with an explicit cast: `(Utf8String)"CROSSPLAY"`.
- `List<>` is ambiguous if both System and Il2Cpp namespaces are visible; use an alias for the Il2Cpp list.

Example aliases:

```csharp
using SearchParamList =
    Il2CppSystem.Collections.Generic.List<Il2CppEpic.OnlineServices.Lobby.LobbySearchSetParameterOptions>;
```

EOS exposes special lobby search keys on `LobbyInterface`:

```csharp
LobbyInterface.SEARCH_BUCKET_ID
LobbyInterface.SEARCH_MINCURRENTMEMBERS
LobbyInterface.SEARCH_MINSLOTSAVAILABLE
```

`SEARCH_MINCURRENTMEMBERS` is the likely server-side key for a minimum-current-players filter. `SEARCH_MINSLOTSAVAILABLE` is the corresponding search key for open slots. These are not the same thing as the game's custom `MAXPLAYERS` lobby attribute, which describes capacity.

## Existing Filters

The game already adds these search parameters in `UILobbyExplorer.GetListOfLobbies`:

- `LEVEL == DefaultSettings.DEFAULT_LEVEL`
- `PLATFORM == LobbyUtilities.PlatformString()`
- `PEACEFUL == <mode>` when a peaceful filter is selected
- `REQUIRE_PASSWORD == false` when locked lobbies are hidden
- `INVITE_ONLY == false`
- `MAXPLAYERS == sliderValue` when the max-player slider is above zero
- `MAXPLAYERS > 1` always
- `MODDED == showModdedLobbiesToggle.isOn`
- `PROXIMITY_VOICE_CHAT == text-only/proximity flag`
- `LANGUAGE == currentLanguageString` when language-only is selected
- `REGION contains <region search string>` for non-worldwide region searches
- `LOBBYCODE == normalizedToken` when searching by lobby code

The converted C# says the always-present max-player parameter uses op `2`; reflection against `Il2CppEpic.OnlineServices.ComparisonOp` confirmed:

```text
0 Equal
1 Notequal
2 Greaterthan
3 Greaterthanorequal
4 Lessthan
5 Lessthanorequal
6 Distance
7 Anyof
8 Notanyof
9 Oneof
10 Notoneof
11 Contains
12 Regexmatch
13 Size
```

## Attribute Keys

The converted `AddSearchParam` is incomplete. It shows only the value being assigned, but the native C shows the `LobbyAttributeType` enum value is converted with `System.Enum.ToString()` and used as the EOS `AttributeData.Key`.

That means search keys are string names, not raw numeric IDs.

Confirmed `LobbyAttributeType` names:

```text
0  REQUIRE_PASSWORD
1  PASSWORD
2  PEACEFUL
3  VERSION
4  LOBBYCODE
5  LEVEL
6  ANTICHEAT
7  NAME
8  PRODUCTUSERID
9  MAXPLAYERS
10 PROXIMITY_VOICE_CHAT
11 MODDED
12 REGION
13 PLATFORM
14 HEARTBEAT
15 SPEED
16 LANGUAGE
17 INVITE_ONLY
18 INVITE_KEY
19 CROSSPLAY
```

Lobby creation uses the same enum-to-string behavior in `LobbyManager.AddAttribute`, so injected search keys should match those names.

For injected custom lobby attributes, use the enum name as the EOS key:

```csharp
"MAXPLAYERS"
"CROSSPLAY"
"PEACEFUL"
"MODDED"
"REQUIRE_PASSWORD"
```

For EOS-provided lobby search fields, use the `LobbyInterface.SEARCH_*` constants instead of custom attribute names.

## Recommended Injection Point

Prefer a Harmony prefix on `LobbyManager.SearchByAttributes`.

Reasons:

- `SearchByAttributes` is public and receives the final mutable parameter list.
- It avoids direct patches on private `UILobbyExplorer` methods.
- It keeps UI preference restoration separate from search-query construction.

Avoid patching these unless there is no other option:

- `UILobbyExplorer.GetListOfLobbies`
- `UILobbyExplorer.AddSearchParam`
- `UILobbyExplorer.OnSearchFilterChange`
- per-frame `UILobbyExplorer.Update`

## Compile-Checked Injection Shape

This shape was compile-checked against the Il2Cpp assemblies in the installed game:

```csharp
using HarmonyLib;
using Il2CppEpic.OnlineServices;
using Il2CppEpic.OnlineServices.Lobby;
using Il2Cpp_Scripts.Managers;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;

using SearchParamList =
    Il2CppSystem.Collections.Generic.List<Il2CppEpic.OnlineServices.Lobby.LobbySearchSetParameterOptions>;

[HarmonyPatch(typeof(LobbyManager), "SearchByAttributes")]
[HarmonyPrefix]
private static void LobbyManager_SearchByAttributes_Prefix(
    SearchParamList searchParameters,
    EOSLobbyManager.OnLobbySearchCallback SearchCompleted)
{
    if (searchParameters == null)
        return;

    if (!MultiplayerToolsCore.SearchCrossplay)
        searchParameters.Add(BuildInt64("CROSSPLAY", 0, ComparisonOp.Equal));

    if (MultiplayerToolsCore.SearchMinPlayers > 1)
    {
        searchParameters.Add(BuildInt64(
            LobbyInterface.SEARCH_MINCURRENTMEMBERS,
            MultiplayerToolsCore.SearchMinPlayers,
            ComparisonOp.Greaterthanorequal));
    }
}

private static LobbySearchSetParameterOptions BuildInt64(Utf8String key, long value, ComparisonOp comparisonOp)
{
    var attributeValue = new AttributeDataValue();
    attributeValue.AsInt64 = new Il2CppSystem.Nullable<long>(value);

    var attribute = new AttributeData();
    attribute.Key = key;
    attribute.Value = attributeValue;

    var option = new LobbySearchSetParameterOptions();
    option.Parameter = new Il2CppSystem.Nullable<AttributeData>(attribute);
    option.ComparisonOp = comparisonOp;
    return option;
}

private static LobbySearchSetParameterOptions BuildInt64(string key, long value, ComparisonOp comparisonOp)
{
    return BuildInt64((Utf8String)key, value, comparisonOp);
}

private static LobbySearchSetParameterOptions BuildBool(Utf8String key, bool value, ComparisonOp comparisonOp)
{
    var attributeValue = new AttributeDataValue();
    attributeValue.AsBool = new Il2CppSystem.Nullable<bool>(value);

    var attribute = new AttributeData();
    attribute.Key = key;
    attribute.Value = attributeValue;

    var option = new LobbySearchSetParameterOptions();
    option.Parameter = new Il2CppSystem.Nullable<AttributeData>(attribute);
    option.ComparisonOp = comparisonOp;
    return option;
}

private static LobbySearchSetParameterOptions BuildUtf8(Utf8String key, string value, ComparisonOp comparisonOp)
{
    var attributeValue = new AttributeDataValue();
    attributeValue.AsUtf8 = value ?? string.Empty;

    var attribute = new AttributeData();
    attribute.Key = key;
    attribute.Value = attributeValue;

    var option = new LobbySearchSetParameterOptions();
    option.Parameter = new Il2CppSystem.Nullable<AttributeData>(attribute);
    option.ComparisonOp = comparisonOp;
    return option;
}
```

Important wrapper details:

- `AttributeDataValue.AsInt64` expects `Il2CppSystem.Nullable<long>`, not a raw `long`.
- `AttributeDataValue.AsBool` expects `Il2CppSystem.Nullable<bool>`, not a raw `bool`.
- `AttributeDataValue.AsUtf8` accepts `Utf8String`; assigning a C# string works through the wrapper conversion.
- `LobbySearchSetParameterOptions.Parameter` expects `Il2CppSystem.Nullable<AttributeData>`, not a raw `AttributeData`.
- `string` keys need conversion to `Utf8String`; explicit `(Utf8String)key` compiles.
- The Harmony prefix parameter must use the Il2Cpp list alias, otherwise `List<>` is ambiguous between `System.Collections.Generic` and `Il2CppSystem.Collections.Generic`.

## Open Questions

- Whether `CROSSPLAY == false` is the desired behavior when crossplay is disabled, or whether crossplay should be omitted entirely and filtered client-side.
- Whether the game's current max-player filter semantics are correct. The existing game code uses `MAXPLAYERS == sliderValue` for the selected slider value and also always adds `MAXPLAYERS > 1`.
- Whether a minimum player filter should use `LobbyInterface.SEARCH_MINCURRENTMEMBERS >= value` or a client-side post-filter after search results are copied. The EOS key is the most likely server-side option.
