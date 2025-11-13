using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using PeakStats.MonoBehaviours;

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StaminaInfo;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    private static Dictionary<string, TextMeshProUGUI> barTexts;
    private static Dictionary<string, float> lastKnownData;
    private static GUIManager guiManager;
    private static ConfigEntry<float> configFontSize;
    private static ConfigEntry<float> configOutlineWidth;
    private static ConfigEntry<Boolean> configRoundStamina;
    private static ConfigEntry<Boolean> configRoundAffliction;

    private void Awake()
    {
        Log = Logger;
        configFontSize = ((BaseUnityPlugin)this).Config.Bind<float>("StaminaInfo", "Font Size", 20f, "Customize the Font Size for stamina bar text.");
        configOutlineWidth = ((BaseUnityPlugin)this).Config.Bind<float>("StaminaInfo", "Outline Width", 0.08f, "Customize the Outline Width for stamina bar text.");
        configRoundStamina = ((BaseUnityPlugin)this).Config.Bind<Boolean>("StaminaInfo", "Round Stamina Bars", true, "If true, rounds to the nearest whole number for the stamina and extra stamina bars.");
        configRoundAffliction = ((BaseUnityPlugin)this).Config.Bind<Boolean>("StaminaInfo", "Round Affliction Bars", false, "If true, rounds to the nearest whole number for affliction bars.");
        Harmony.CreateAndPatchAll(typeof(StaminaInfoStaminaBarUpdatePatch));
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private static class StaminaInfoStaminaBarUpdatePatch
    {
        [HarmonyPatch(typeof(StaminaBar), "Update")]
        [HarmonyPostfix]
        private static void StaminaInfoStaminaBarUpdate(StaminaBar __instance)
        {
            try
            {
                if (guiManager == null)
                {
                    barTexts = new Dictionary<string, TextMeshProUGUI>();
                    lastKnownData = new Dictionary<string, float>();
                    InitStaminaInfo(__instance);
                }
                else
                {
                    //if (guiManager.character != null)
                    if (Character.observedCharacter != null)
                    {
                        UpdateBarTexts(__instance);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError(e.Message + e.StackTrace);
            }
        }
    }

    private static void UpdateBarTexts(StaminaBar staminaBar)
    {
        if (lastKnownData[staminaBar.staminaBar.name] != staminaBar.desiredStaminaSize)
        {
            if (staminaBar.desiredStaminaSize >= 30f)
            {
                if (configRoundStamina.Value) { barTexts[staminaBar.staminaBar.name].text = Mathf.Round(staminaBar.desiredStaminaSize / 6f).ToString(); }
                else { barTexts[staminaBar.staminaBar.name].text = (staminaBar.desiredStaminaSize / 6f).ToString("F1"); }
                barTexts[staminaBar.staminaBar.name].gameObject.SetActive(true);
            }
            else if (staminaBar.desiredStaminaSize >= 15f)
            {
                barTexts[staminaBar.staminaBar.name].text = Mathf.Round(staminaBar.desiredStaminaSize / 6f).ToString();
                barTexts[staminaBar.staminaBar.name].gameObject.SetActive(true);
            }
            else
            {
                barTexts[staminaBar.staminaBar.name].gameObject.SetActive(false);
            }
            lastKnownData[staminaBar.staminaBar.name] = staminaBar.desiredStaminaSize;
        }

        if (lastKnownData["ExtraStamina"] != staminaBar.desiredExtraStaminaSize)
        {
            if (staminaBar.desiredExtraStaminaSize >= 30f)
            {
                if (configRoundStamina.Value) { barTexts["ExtraStamina"].text = Mathf.Round(staminaBar.desiredExtraStaminaSize / 6f).ToString(); }
                else { barTexts["ExtraStamina"].text = (staminaBar.desiredExtraStaminaSize / 6f).ToString("F1"); }
                barTexts["ExtraStamina"].gameObject.SetActive(true);
            }
            else if (staminaBar.desiredExtraStaminaSize >= 15f)
            {
                barTexts["ExtraStamina"].text = Mathf.Round(staminaBar.desiredExtraStaminaSize / 6f).ToString();
                barTexts["ExtraStamina"].gameObject.SetActive(true);
            }
            else
            {
                barTexts["ExtraStamina"].gameObject.SetActive(false);
            }
            lastKnownData["ExtraStamina"] = staminaBar.desiredExtraStaminaSize;
        }

        foreach (BarAffliction affliction in staminaBar.afflictions)
        {
            if (lastKnownData[affliction.name] != affliction.size)
            {
                if (affliction.size >= 30f)
                {
                    if (configRoundAffliction.Value) { barTexts[affliction.name].text = Mathf.Round(affliction.size / 6f).ToString(); }
                    else { barTexts[affliction.name].text = (affliction.size / 6f).ToString("F1").Replace(".0", ""); }
                }
                else if (affliction.size >= 15f)
                {
                    barTexts[affliction.name].text = Mathf.Round(affliction.size / 6f).ToString();
                }
                lastKnownData[affliction.name] = affliction.size;
            }
        }
    }

    public static void UpdateCharacterBarTexts(CharacterStaminaBar characterStaminaBar)
    {
        if (characterStaminaBar.observedCharacter == null) return;

        var id = characterStaminaBar.GetInstanceID();
        float currentStaminaSize = Mathf.Max(0f, characterStaminaBar.observedCharacter.data.currentStamina * characterStaminaBar.fullBarRectTransform.sizeDelta.x);
        string staminaBarKey = id + "_Stamina";

        if (!Plugin.lastKnownData.ContainsKey(staminaBarKey) || Plugin.lastKnownData[staminaBarKey] != currentStaminaSize)
        {
            if (currentStaminaSize >= 30f)
            {
                if (Plugin.configRoundStamina.Value)
                {
                    Plugin.barTexts[staminaBarKey].text = Mathf.Round(currentStaminaSize / 6f).ToString();
                }
                else
                {
                    Plugin.barTexts[staminaBarKey].text = (currentStaminaSize / 6f).ToString("F1");
                }
                Plugin.barTexts[staminaBarKey].gameObject.SetActive(true);
            }
            else if (currentStaminaSize >= 15f)
            {
                Plugin.barTexts[staminaBarKey].text = Mathf.Round(currentStaminaSize / 6f).ToString();
                Plugin.barTexts[staminaBarKey].gameObject.SetActive(true);
            }
            else
            {
                Plugin.barTexts[staminaBarKey].gameObject.SetActive(false);
            }
            Plugin.lastKnownData[staminaBarKey] = currentStaminaSize;
        }

        foreach (CharacterBarAffliction affliction in characterStaminaBar.characterBarAfflictions)
        {
            affliction.FetchDesiredSize();
            string afflictionKey = id + "_" + affliction.name;
            if (!Plugin.lastKnownData.ContainsKey(afflictionKey) || Plugin.lastKnownData[afflictionKey] != affliction.size)
            {
                if (affliction.size >= 30f)
                {
                    if (Plugin.configRoundAffliction.Value)
                    {
                        Plugin.barTexts[afflictionKey].text = Mathf.Round(affliction.size / 6f).ToString();
                    }
                    else
                    {
                        Plugin.barTexts[afflictionKey].text = (affliction.size / 6f).ToString("F1").Replace(".0", "");
                    }
                }
                else if (affliction.size >= 15f)
                {
                    Plugin.barTexts[afflictionKey].text = Mathf.Round(affliction.size / 6f).ToString();
                }
                Plugin.lastKnownData[afflictionKey] = affliction.size;
            }
        }
    }

    public static void InitCharacterStaminaInfo(CharacterStaminaBar characterStaminaBar)
    {
        var id = characterStaminaBar.GetInstanceID();
        string staminaBarKey = id + "_Stamina";
        string maxStaminaBarKey = id + "_MaxStamina";
        var fontSize = Plugin.configFontSize.Value;
        Plugin.AddTextObject(characterStaminaBar.staminaBarRectTransform.gameObject, staminaBarKey);
        foreach (CharacterBarAffliction affliction in characterStaminaBar.characterBarAfflictions)
        {
            Plugin.AddTextObject(affliction.gameObject, id + "_" + affliction.gameObject.name);
        }

    }

    public static class FixPeakStatsPatches
    {
        [HarmonyPatch(typeof(PeakStats.MonoBehaviours.CharacterStaminaBar), "Update")]
        [HarmonyPostfix]
        public static void Update(CharacterStaminaBar __instance)
        {
            try
            {
                var id = __instance.GetInstanceID();
                if (!Plugin.barTexts.ContainsKey(id + "_Stamina"))
                {
                    Plugin.InitCharacterStaminaInfo(__instance);
                }
                if (Character.observedCharacter != null)
                {
                    Plugin.UpdateCharacterBarTexts(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e.Message + e.StackTrace);
            }
        }
    }

    public static bool peakstatus { get; set; }

    private static void InitStaminaInfo(StaminaBar staminaBar)
    {
        GameObject guiManagerGameObj = GameObject.Find("GAME/GUIManager");
        guiManager = guiManagerGameObj.GetComponent<GUIManager>();
        AddTextObject(staminaBar.staminaBar.gameObject, staminaBar.staminaBar.name);
        AddTextObject(staminaBar.extraBarStamina.gameObject, "ExtraStamina");
        foreach (BarAffliction affliction in staminaBar.afflictions)
        {
            AddTextObject(affliction.gameObject, affliction.gameObject.name);
        }
        if (!peakstatus)
        {
            peakstatus = true;
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("nickklmao.peakstats"))
            {
                Harmony.CreateAndPatchAll(typeof(FixPeakStatsPatches));
            }
        }
    }

    private static void AddTextObject(GameObject gameObj, string barName)
    {
        TMPro.TMP_FontAsset font = guiManager.heroDayText.font;
        GameObject staminaInfo = new GameObject("StaminaInfo");
        staminaInfo.transform.SetParent(gameObj.transform);

        TextMeshProUGUI staminaInfoText = staminaInfo.AddComponent<TextMeshProUGUI>();
        RectTransform staminaInfoRect = staminaInfo.GetComponent<RectTransform>();
        gameObj.SetActive(true); // Necessary to update .fontSharedMaterial?

        staminaInfoText.font = font;
        staminaInfoText.fontSize = configFontSize.Value;
        staminaInfoRect.offsetMin = new Vector2(0f, 0f);
        staminaInfoRect.offsetMax = new Vector2(0f, 0f);
        staminaInfoText.alignment = TextAlignmentOptions.Center;
        staminaInfoText.verticalAlignment = VerticalAlignmentOptions.Capline;
        staminaInfoText.textWrappingMode = TextWrappingModes.NoWrap;
        staminaInfoText.text = "";

        barTexts.Add(barName, staminaInfoText);
        lastKnownData.Add(barName, 0f);
        staminaInfoText.outlineWidth = configOutlineWidth.Value; // Buggy?
    }
}