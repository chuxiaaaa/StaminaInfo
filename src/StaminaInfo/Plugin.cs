using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using HarmonyLib;

using Peak.Afflictions;

using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;

using static CharacterAfflictions;


namespace StaminaInfo;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    public static Dictionary<string, TextMeshProUGUI> barTexts;
    public static Dictionary<string, float> lastKnownData;
    public static Dictionary<string, float> lastKnownData2;
    public static GUIManager guiManager;
    public static ConfigEntry<float> configFontSize;
    public static ConfigEntry<float> configOutlineWidth;
    public static ConfigEntry<Boolean> configRoundStamina;
    public static ConfigEntry<Boolean> configRoundAffliction;

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
                    lastKnownData2 = new Dictionary<string, float>();
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
            if (Time.time - lastKnownData[affliction.name] > 0.5f && affliction.size > 0)
            {
                bool needUpdate = affliction.afflictionType != STATUSTYPE.Weight && affliction.afflictionType != STATUSTYPE.Injury && affliction.afflictionType != STATUSTYPE.Curse;
                if (lastKnownData[affliction.name] != affliction.size || needUpdate)
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
                }
                if (needUpdate)
                {
                    float timeRemaining = GetReductionTimeRemaining(affliction.afflictionType);
                    if (timeRemaining != 0)
                    {
                        barTexts[affliction.name].text += $"({FormatTime(timeRemaining)})";
                    }
                }
                lastKnownData2[affliction.name] = affliction.size;
                lastKnownData[affliction.name] = Time.time;
            }
        }
    }


    private static float GetReductionTimeRemaining(STATUSTYPE statusType)
    {
        var afflictions = Character.observedCharacter?.refs?.afflictions;
        if (afflictions == null) return 0f;
        if (statusType == STATUSTYPE.Thorns)
        {
            var time = 0f;
            //bool solo = Character.AllCharacters.Count == 1;
            int count = 0;
            foreach (var item in afflictions.physicalThorns)
            {
                if (!item.stuckIn)
                {
                    continue;
                }
                count++;
                time += Time.time - item.popOutTime;
            }
            if (count == 0)
            {
                return 0;
            }
            return Mathf.Abs(time) / count;
        }
        // 直接从afflictions获取当前状态值
        float currentStatus = afflictions.GetCurrentStatus(statusType);
        if (currentStatus <= 0f) return 0f;

        float reductionRate = GetReductionRate(afflictions, statusType);
        if (reductionRate <= 0f) return 0f;

        float cooldown = GetCooldown(afflictions, statusType);
        float lastAddedTime = afflictions.LastAddedStatus(statusType);
        float currentTime = Time.time;

        // 检查是否在冷却时间内
        if (cooldown > 0f && currentTime - lastAddedTime < cooldown)
        {
            float cooldownRemaining = cooldown - (currentTime - lastAddedTime);
            float reductionTime = currentStatus / reductionRate;
            return cooldownRemaining + reductionTime;
        }

        // 直接计算减少时间：当前状态值 ÷ 每秒减少速率
        return currentStatus / reductionRate;
    }

    // 获取减少速率 - 从afflictions对象中获取
    private static float GetReductionRate(CharacterAfflictions afflictions, STATUSTYPE statusType)
    {
        switch (statusType)
        {
            case STATUSTYPE.Poison:
                return afflictions.poisonReductionPerSecond;
            case STATUSTYPE.Hot:
                return afflictions.hotReductionPerSecond;
            case STATUSTYPE.Spores:
                return afflictions.sporesReductionPerSecond;
            case STATUSTYPE.Drowsy:
                return afflictions.drowsyReductionPerSecond;
            default:
                return 0f;
        }
    }

    // 获取冷却时间 - 从afflictions对象中获取
    private static float GetCooldown(CharacterAfflictions afflictions, STATUSTYPE statusType)
    {
        switch (statusType)
        {
            case STATUSTYPE.Poison:
                return afflictions.poisonReductionCooldown;
            case STATUSTYPE.Hot:
                return afflictions.hotReductionCooldown;
            case STATUSTYPE.Spores:
                return afflictions.sporesReductionCooldown;
            case STATUSTYPE.Drowsy:
                return afflictions.drowsyReductionCooldown;
      
            default:
                return 0f;
        }
    }

    // 格式化时间显示
    private static string FormatTime(float seconds)
    {
        if (seconds < 60f)
        {
            return Mathf.CeilToInt(seconds).ToString();
        }
        else if (seconds < 3600f)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int remainingSeconds = Mathf.CeilToInt(seconds % 60f);
            return $"{minutes}:{remainingSeconds:D2}";
        }
        else
        {
            int hours = Mathf.FloorToInt(seconds / 3600f);
            int minutes = Mathf.FloorToInt((seconds % 3600f) / 60f);
            return $"{hours}:{minutes:D2}";
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
                var harmony = new Harmony(Plugin.Id);
                var patchType = Type.GetType("StaminaInfo.FixPeakStatsPatches, com.github.chuxiaaaa.StaminaInfo");
                harmony.PatchAll(patchType);
            }
        }
    }

    public static void AddTextObject(GameObject gameObj, string barName)
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
        staminaInfoText.transform.localScale = new Vector3(1f, 1f, 1f);
        barTexts.Add(barName, staminaInfoText);
        lastKnownData.Add(barName, 0f);
        staminaInfoText.outlineWidth = configOutlineWidth.Value; // Buggy?
    }
}