using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;

namespace StaminaInfo
{
    public static class FixPeakStatsPatches
    {
        // 缓存字段/方法/委托
        private static Type T_CharacterStaminaBar;
        private static FieldInfo F_observedCharacter;
        private static FieldInfo F_characterBarAfflictions;
        private static FieldInfo F_staminaBarRect;
        private static FieldInfo F_fullBarRect;
        private static MethodInfo M_GetInstanceID;

        private static FieldInfo F_afflictionSize;
        private static MethodInfo M_FetchDesiredSize;
        private static FieldInfo F_afflictionGameObject;

        private delegate void DFetchDesiredSize(object instance);
        private static DFetchDesiredSize FetchDesiredSizeDelegate;

        public static Dictionary<int, float> lastUpdateTime { get; set; } = new();

        // --------------------
        //  Harmony Patch 动态绑定
        // --------------------
        [HarmonyPatch]
        private static class PatchWrapper
        {
            static Type TargetType()
            {
                T_CharacterStaminaBar ??=
                    AccessTools.TypeByName("PeakStats.MonoBehaviours.CharacterStaminaBar");
                return T_CharacterStaminaBar;
            }

            static MethodBase TargetMethod()
            {
                var t = TargetType();
                if (t == null) return null;

                CacheFieldsAndMethods(t);
                return AccessTools.Method(t, "Update");
            }

            static void Postfix(object __instance)
            {
                FixPeakStatsPatches.Update(__instance);
            }
        }

        // --------------------
        //   一次性缓存所有反射对象
        // --------------------
        private static void CacheFieldsAndMethods(Type t)
        {
            if (F_observedCharacter != null) return; // 已缓存过

            F_observedCharacter = AccessTools.Field(t, "observedCharacter");
            F_characterBarAfflictions = AccessTools.Field(t, "characterBarAfflictions");
            F_staminaBarRect = AccessTools.Field(t, "staminaBarRectTransform");
            F_fullBarRect = AccessTools.Field(t, "fullBarRectTransform");
            M_GetInstanceID = AccessTools.Method(t, "GetInstanceID");

            // 取 affliction 的类型
            var afflictionType = AccessTools.TypeByName("PeakStats.MonoBehaviours.CharacterBarAffliction");

            F_afflictionSize = AccessTools.Field(afflictionType, "size");
            F_afflictionGameObject = AccessTools.Field(afflictionType, "gameObject");

            M_FetchDesiredSize = AccessTools.Method(afflictionType, "FetchDesiredSize");

            // 创建 Delegate（比 Invoke 快 100 倍）
            if (M_FetchDesiredSize != null)
            {
                FetchDesiredSizeDelegate = (DFetchDesiredSize)Delegate.CreateDelegate(
                    typeof(DFetchDesiredSize),
                    null,
                    M_FetchDesiredSize
                );
            }

            Plugin.Log.LogInfo("[StaminaInfo] PeakStats compatibility initialized with cached reflection.");
        }

        // --------------------
        //  每帧逻辑（零 Invoke）
        // --------------------
        public static void Update(object instance)
        {
            if (instance == null) return;

            try
            {
                int id = (int)M_GetInstanceID.Invoke(instance, null);

                if (!lastUpdateTime.ContainsKey(id))
                    lastUpdateTime[id] = -1;

                if (!Plugin.barTexts.ContainsKey(id + "_Stamina"))
                    InitCharacterStaminaInfo(instance, id);

                if (Character.observedCharacter != null &&
                    Time.time - lastUpdateTime[id] > 1f)
                {
                    UpdateCharacterBarTexts(instance, id);
                    lastUpdateTime[id] = Time.time;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e.ToString());
            }
        }

        // --------------------
        //  初始化 UI 文本
        // --------------------
        private static void InitCharacterStaminaInfo(object instance, int id)
        {
            var staminaRT = (RectTransform)F_staminaBarRect.GetValue(instance);
            Plugin.AddTextObject(staminaRT.gameObject, id + "_Stamina");

            var affs = (System.Collections.IEnumerable)F_characterBarAfflictions.GetValue(instance);

            foreach (var aff in affs)
            {
                var go = (GameObject)F_afflictionGameObject.GetValue(aff);
                Plugin.AddTextObject(go, id + "_" + go.name);
            }
        }

        // --------------------
        //  更新 UI 文本
        // --------------------
        private static void UpdateCharacterBarTexts(object instance, int id)
        {
            var observedChar = F_observedCharacter.GetValue(instance);
            if (observedChar == null) return;

            float currentStamina = GetCurrentStamina(instance, observedChar);
            string staminaKey = id + "_Stamina";

            if (!Plugin.lastKnownData.ContainsKey(staminaKey) ||
                Plugin.lastKnownData[staminaKey] != currentStamina)
            {
                UpdateStaminaText(staminaKey, currentStamina);
                Plugin.lastKnownData[staminaKey] = currentStamina;
            }

            var affs = (System.Collections.IEnumerable)F_characterBarAfflictions.GetValue(instance);

            foreach (var aff in affs)
                UpdateAffliction(aff, id);
        }

        // 获取体力条大小（每帧零反射）
        private static float GetCurrentStamina(object instance, object observedChar)
        {
            float current = (float)AccessTools.Field(observedChar.GetType(), "currentStamina").GetValue(observedChar);
            var fullBar = (RectTransform)F_fullBarRect.GetValue(instance);

            return Mathf.Max(0f, current * fullBar.sizeDelta.x);
        }

        // 主体力条更新
        private static void UpdateStaminaText(string key, float v)
        {
            if (v >= 30f)
            {
                Plugin.barTexts[key].text =
                    Plugin.configRoundStamina.Value ? Mathf.Round(v / 6f).ToString() :
                    (v / 6f).ToString("F1");

                Plugin.barTexts[key].gameObject.SetActive(true);
            }
            else if (v >= 15f)
            {
                Plugin.barTexts[key].text = Mathf.Round(v / 6f).ToString();
                Plugin.barTexts[key].gameObject.SetActive(true);
            }
            else
            {
                Plugin.barTexts[key].gameObject.SetActive(false);
            }
        }

        // 负面 Buff 更新
        private static void UpdateAffliction(object affliction, int id)
        {
            // 不使用 Invoke，使用 delegate（极速）
            FetchDesiredSizeDelegate(affliction);

            float size = (float)F_afflictionSize.GetValue(affliction);
            var go = (GameObject)F_afflictionGameObject.GetValue(affliction);
            string key = id + "_" + go.name;

            if (!Plugin.lastKnownData.ContainsKey(key) || Plugin.lastKnownData[key] != size)
            {
                var txt = Plugin.barTexts[key];
                txt.fontSize = Plugin.configFontSize.Value;

                if (size >= 30f)
                {
                    txt.text = Plugin.configRoundAffliction.Value ?
                        Mathf.Round(size / 6f).ToString() :
                        (size / 6f).ToString("F1").Replace(".0", "");
                }
                else if (size >= 15f)
                {
                    txt.text = Mathf.Round(size / 6f).ToString();
                }

                Plugin.lastKnownData[key] = size;
            }
        }
    }
}
