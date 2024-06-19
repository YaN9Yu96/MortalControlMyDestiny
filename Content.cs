using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HarmonyLib;
using Mortal.Story;
using Mortal.Core;
using Fungus;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ControlMyDestiny
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Content : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);

        private void Awake()
        {
            LoadConfig();
            harmony.PatchAll();
            Console.WriteLine("开始注入");
        }

        private void OnDestroy()
        {
            Console.WriteLine("取消注入");
            harmony.UnpatchSelf();
        }

        private ConfigEntry<bool> _enableOptionSelection;
        private ConfigEntry<bool> _displayCondition;
        private ConfigEntry<bool> _displayEventName;
        private ConfigEntry<bool> _displayFlagChanged;
        private ConfigEntry<string> _successColorConfig;
        private ConfigEntry<string> _failureColorConfig;
        private ConfigEntry<string> _flagColorConfig;

        public static bool EnableOptionSelection;
        public static bool DisplayCondition;
        public static bool DisplayEventName;
        public static bool DisplayFlagChanged;
        public static string SuccessColor;
        public static string FailureColor;
        public static string FlagColor;
        private void LoadConfig()
        {
            _enableOptionSelection = Config.Bind("General", "EnableOptionSelection", true, "是否开启天命选择功能 true-开启 false-关闭");
            _displayCondition = Config.Bind("General", "DisplayCondition", true, "是否显示分支条件 true-开启 false-关闭");
            _displayEventName = Config.Bind("General", "DisplayEventName", true, "是否显示事件名称 true-显示名称 false-显示ID（防剧透模式）");
            _successColorConfig = Config.Bind("General", "SuccessColor", "#78cc82", "判定成功时的颜色");
            _failureColorConfig = Config.Bind("General", "FailureColor", "#ff5050", "判定失败时的颜色");
            _displayFlagChanged = Config.Bind("General", "DisplayFlagChanged", true, "当检查点更新时，是否开启显示 true-开启 false-关闭");
            _flagColorConfig = Config.Bind("General", "FlagColor", "#50c8ff", "提醒检查点更新时的颜色");

            SuccessColor = _successColorConfig.Value;
            FailureColor = _failureColorConfig.Value;
            FlagColor = _flagColorConfig.Value;
            DisplayEventName = _displayEventName.Value;
            EnableOptionSelection = _enableOptionSelection.Value;
            DisplayCondition = _displayCondition.Value;
            DisplayFlagChanged = _displayFlagChanged.Value;
        }
    }


    [HarmonyPatch]
    internal class ConditionPatches
    {

        //// 抓取本地化key
        //[HarmonyPatch(typeof(LeanLocalizationResolver), "GetString")]
        //[HarmonyPostfix]
        //public static void test(ref string __result, string key)
        //{
        //    Debug.Log($"{key} : {__result}");
        //}

        [HarmonyPatch(typeof(SwitchResultData), "Execute")]
        [HarmonyPostfix]
        public static void SwitchResultData_Execute_Postfix(SwitchResultData __instance)
        {
            try
            {
                var items = (List<ConditionResultItem>)__instance.GetPrivateVariable("_items");
                var countItems = (List<ValueCompareItem>)__instance.GetPrivateVariable("_countItems");
                for (int i = 0; i < items.Count; i++)
                {
                    int index = (items.Count > 1) ? i : -1;
                    MortalExtension.DisplayConditionResultItem(items[i], index);
                }
                foreach (var countItem in countItems)
                {
                    YExtension.DebugInfo("判断次数", countItem.ToString());
                    //MortalExtension.DisplayMessage("判断次数");
                    //MortalExtension.DisplayMessage(countItem.ToString());
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [HarmonyPatch(typeof(MissionManagerData), "AddFlag")]
        [HarmonyPostfix]
        public static void MissionManagerData_AddFlag_Postfix(MissionManagerData __instance, string name, int value)
        {
            if (!Content.DisplayFlagChanged)
                return;
            try
            {
                var flagData = __instance.GetFlag(name);
                MortalExtension.DisplayFlag(flagData);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [HarmonyPatch(typeof(MissionManagerData), "SetFlag")]
        [HarmonyPostfix]
        public static void MissionManagerData_SetFlag_Postfix(MissionManagerData __instance, string name, int value)
        {
            if (!Content.DisplayFlagChanged)
                return;
            try
            {
                var flagData = __instance.GetFlag(name);
                MortalExtension.DisplayFlag(flagData);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        #region 随机种子
        private static void PrintFreeRandomState(UnityRandomState randomState)
        {
            Console.WriteLine($"当前随机种子 - s0: {randomState.s0} s1: {randomState.s1} s2: {randomState.s2} s3:{randomState.s3}");
        }

        [HarmonyPatch(typeof(PlayerStatManagerData), "ResetFreeRandomState")]
        [HarmonyPostfix]
        public static void ResetFreeRandomState_Postfix(PlayerStatManagerData __instance)
        {
            Console.WriteLine("随机种子已被重置");
            PrintFreeRandomState(__instance.FreeRandomState);
        }

        [HarmonyPatch(typeof(PlayerStatManagerData), "Load")]
        [HarmonyPostfix]
        public static void Load_Postfix(PlayerStatManagerData __instance, GameSave gameSave)
        {
            Console.WriteLine("加载随机种子");
            PrintFreeRandomState(__instance.FreeRandomState);
        }
        #endregion


    }


    [HarmonyPatch]
    internal class DicePatches
    {
        #region 掷骰选项可点击

        // 掷骰结果返回时
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        [HarmonyPostfix]
        public static void CheckPointManager_Dice_Postfix(ref DiceCheckResult __result, string name, ref int random)
        {
            if (_clickSelectionIndex >= 0)
            {
                __result.Result = _clickSelectionIndex;
                ClearRegistDiceOption();
            }
        }

        // 懒得找逻辑了，全部有可能重置界面的方法都执行一遍清理
        // 点击掷骰时

        [HarmonyPatch(typeof(DiceMenuDialog), "StartButtonClick")]
        [HarmonyPostfix]
        public static void DiceMenuDialog_StartButtonClick_Postfix()
        {
            ClearRegistDiceOption();
        }

        // 初始化时
        [HarmonyPatch(typeof(DiceMenuDialog), "Setup")]
        [HarmonyPostfix]
        public static void DiceMenuDialog_Setup_Postfix()
        {
            ClearRegistDiceOption();
        }

        // 面板清空时
        [HarmonyPatch(typeof(MenuDialog), "Clear")]
        [HarmonyPostfix]
        public static void MenuDialog_Clear_Postfix()
        {
            ClearRegistDiceOption();
        }

        // 面板隐藏时
        [HarmonyPatch(typeof(MenuDialog), "SetActive")]
        [HarmonyPostfix]
        public static void MenuDialog_SetActive_Postfix()
        {
            ClearRegistDiceOption();
        }


        private static Button[] _diceOptionButtons = new Button[0];
        private static List<Button> _registDiceOptionButtonList = new List<Button>();
        private static int _clickSelectionIndex = -1;
        private static DiceMenuDialog? _diceMenuDialog = null;
        // 有新增选项时
        [HarmonyPatch(typeof(CustomMenuDialog), "UpdateOptions")]
        [HarmonyPostfix]
        public static void CustomMenuDialog_UpdateOption_Postfix(CustomMenuDialog __instance)
        {
            if (!Content.EnableOptionSelection)
                return;

            _diceOptionButtons = new Button[0];
            _diceMenuDialog = null;

            try
            {
                var buttons = (Button[])__instance.GetPrivateVariable("cachedButtons");
                if (buttons == null)
                    return;

                if (!(__instance is DiceMenuDialog))
                    return;

                // 再次确保是掷骰选项
                foreach (var button in buttons)
                {
                    if (!button.gameObject.activeSelf)
                        continue;
                    if (button.GetComponent<DiceOption>())
                        break;
                    else
                        return;
                }

                _diceOptionButtons = buttons;
                _diceMenuDialog = (DiceMenuDialog)__instance;

                // 遍历按钮，添加事件
                for (int i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    if (!button.gameObject.activeSelf)
                        continue;
                    if (_registDiceOptionButtonList.Contains(button))
                        continue;
                    RegisterButtonEvents(button, i);
                }
            }
            catch (Exception e)
            {
                ClearRegistDiceOption();
                Console.WriteLine(e);
            }
        }

        private static void RegisterButtonEvents(Button button, int index)
        {
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            // mouseover
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entry.callback.AddListener((eventData) => { OnMouseEnterDiceOptionButton(button); });
            trigger.triggers.Add(entry);

            // click
            entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            entry.callback.AddListener((eventData) => { OnClickDiceOptionButton(button); });
            trigger.triggers.Add(entry);

            Console.WriteLine($"已为第{index + 1}/{_diceOptionButtons.Length + 1}个按钮注册事件");
            _registDiceOptionButtonList.Add(button);
        }

        private static void OnMouseEnterDiceOptionButton(Button selection)
        {
            try
            {
                for (int i = 0; i < _diceOptionButtons.Length; i++)
                {
                    var button = _diceOptionButtons[i];
                    if (button == null)
                        continue;

                    DiceOption diceOption = button.GetComponent<DiceOption>();
                    if (diceOption == null)
                        continue;

                    if (selection == button)
                    {
                        Console.WriteLine($"选中第{i + 1}个选项");
                        YExtension.InvokePrivateMethod(diceOption, "SetActive", new object[] { true });
                    }
                    else
                    {
                        var option = button.GetComponent<DiceOption>();
                        YExtension.InvokePrivateMethod(diceOption, "SetActive", new object[] { false });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void OnClickDiceOptionButton(Button selection)
        {
            for (int i = 0; i < _diceOptionButtons.Length; i++)
            {
                var button = _diceOptionButtons[i];
                if (button && selection == button)
                {
                    _clickSelectionIndex = i + 1;
                    if (_diceMenuDialog != null)
                        _diceMenuDialog.PressStartButton();
                }
            }
        }

        private static void ClearRegistDiceOption()
        {
            _diceOptionButtons = new Button[0];
            _clickSelectionIndex = -1;
            _diceMenuDialog = null;
            if (_registDiceOptionButtonList.Count == 0)
                return;

            try
            {
                foreach (Button button in _registDiceOptionButtonList)
                {
                    if (button == null)
                        continue;

                    EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null)
                        continue;

                    trigger.triggers.Clear();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            _registDiceOptionButtonList.Clear();
            Console.WriteLine("注册的掷骰事件已清空");
        }

        #endregion
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "ControlMyDestiny";
        public const string PLUGIN_NAME = "知天命";
        public const string PLUGIN_VERSION = "1.0.2";
    }

    internal static class MortalExtension
    {


        public static void DisplayConditionResultItem(ConditionResultItem conditionResultItem, int index = -1)
        {
            try
            {
                string conditionTitle = index < 0 ? "[分支]" : $"[分支{index + 1}]";
                string str = "";

                var logicOp = conditionResultItem.GetPrivateVariable("_logicOp");
                var items = (List<StatCompareItem>)conditionResultItem.GetPrivateVariable("_items");

                // 逻辑运算类型
                string logicStr = ParsedLogicOp(logicOp);

                for (int i = 0; i < items.Count; i++)
                {
                    string itemStr = ParsedStatCompareItem(items[i]);

                    if (YExtension.GetParsedText(str + itemStr).Length > 30)
                    {
                        DisplayMessage(str);
                        str = (i > 0 ? logicStr : conditionTitle) + itemStr;
                    }
                    else
                    {
                        str += (i > 0 ? logicStr : conditionTitle) + itemStr;
                    }
                }
                DisplayMessage(str);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static string ParsedStatCompareItem(StatCompareItem item)
        {
            try
            {
                var value1 = (StatGroupVariable)item.GetPrivateVariable("_value1");
                var value2 = (StatGroupVariable)item.GetPrivateVariable("_value2");
                var compareOp = item.GetPrivateVariable("_compareOp");

                string valueStr1 = ParsedStatGroupVariable(value1);
                string valueStr2 = ParsedStatGroupVariable(value2);
                string compareStr = ParsedLogicOp(compareOp);

                var valueLower1 = valueStr1.ToLower();
                // 解锁设施
                if (valueLower1.Contains("facility"))
                {
                    valueStr1 = $"{Localization.GetLocalizedStr("设施状态")}[{Localization.GetFacilityName(valueStr1)}]";
                    if (compareStr == "＝" && valueStr2 == "0")
                    {
                        compareStr = "：";
                        valueStr2 = $"{Localization.GetLocalizedStr("未解锁")}";
                    }
                    else if ((compareStr == "＝" || compareStr == "≥") && valueStr2 == "1")
                    {
                        compareStr = "：";
                        valueStr2 = $"{Localization.GetLocalizedStr("解锁")}";
                    }
                }
                // 心上人
                else if (valueLower1.Contains("心上人"))
                {
                    int index;
                    if (int.TryParse(valueStr2, out index))
                        valueStr2 = Localization.GetLover(index);
                }
                // 事件
                else if (YExtension.IsEnglishAlphanumericOrSymbols(valueLower1))
                {
                    var devNote = GetStatGroupVariableDevNote(value1);
                    if (devNote != "" && Content.DisplayEventName)
                        valueStr1 = devNote;
                    else if (valueLower1.Contains("talkoption"))
                        valueStr1 = $"{Localization.GetLocalizedStr("对话选项")}[{valueStr1}]";
                    else
                        valueStr1 = $"{Localization.GetLocalizedStr("经历事件")}[{valueStr1}]{Localization.GetLocalizedStr("次数")}";
                }
                // 剩下的应该都是属性类判断，不作处理

                bool result = item.Check();
                string color = result ? $"<color={Content.SuccessColor}>" : $"<color={Content.FailureColor}>";
                string itemStr = $"{color}{valueStr1}{(compareStr)}{valueStr2}</color>";

                return itemStr;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private static string GetStatGroupVariableDevNote(StatGroupVariable statGroupVariable)
        {
            try
            {
                var value = (List<StatValueReference>)statGroupVariable.GetPrivateVariable("_value");
                if (value.Count == 0)
                    return "";

                var flag = (FlagData)value[0].GetPrivateVariable("_flag");
                var devNote = (string)flag.GetPrivateVariable("_devNote");
                return devNote.Trim().Replace("\n", "").Replace(" ", "");
            }
            catch (Exception e)
            {
                return "";
            }
        }

        public static string ParsedStatGroupVariable(StatGroupVariable statGroupVariable)
        {
            if (statGroupVariable == null)
                return Localization.GetLocalizedStr("未定义");

            var str = statGroupVariable.name;
            str = str.Trim();
            str = str.Replace(" ", "");
            str = str.Replace("常數_", "");
            str = str.Replace("數值_", "");
            str = str.Replace("時間_", "");
            str = str.Replace("旗標_", "");
            return str;
        }

        public static string ParsedLogicOp(object? logic)
        {
            if (logic == null)
                return "?";
            var logicString = logic.ToString().ToLower();
            if (logicString == "and")
                return "且";
            if (logicString == "or")
                return "或";
            if (logicString == "equal")
                return "＝";
            if (logicString == "notequal")
                return "≠";
            if (logicString == "greaterequal")
                return "≥";
            if (logicString == "lessequal")
                return "≤";
            if (logicString == "greater")
                return "＞";
            if (logicString == "less")
                return "＜";

            return logicString;
        }

        public static void DisplayFlag(FlagData flagData)
        {
            try
            {
                var name = flagData.DevNote;
                if (name == "") name = flagData.name;

                MortalExtension.DisplayMessage($"[{Localization.GetLocalizedStr("检查点更新")}]" +
                    $"<color={Content.FlagColor}>{name}={flagData.State}</color>");
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static void DisplayMessage(string str)
        {
            if (!Content.DisplayCondition)
                return;

            try
            {
                StoryMainUI.Instance.DisplayMessageText($"<size=23%>{str}</size>");

                // 尝试写入回想，但是没生效
                //var entry = new NarrativeLogEntry() { name = PluginInfo.PLUGIN_NAME, text = str };
                //NarrativeLog.DoNarrativeAdded(entry);
            }
            catch (Exception e)
            {
                Console.WriteLine($"【没有找到面板】{str}");
            }
        }
    }

    internal static class Localization
    {
        private static Dictionary<string, string> _localizationDic = new Dictionary<string, string>
        {
            { "经历事件","經歷事件"},
            { "次数","次數"},
            { "设施状态","設施狀態"},
            { "解锁","解鎖"},
            { "未解锁","未解鎖"},
            { "对话选项","對話選項"},
            { "未定义","未定義"},
            { "检查点更新","檢查點更新"},
            // 人名
            { "小师妹","小師妹"},
            { "上官萤","上官螢"},
            { "夏侯兰","夏侯蘭"},
            { "龙湘","龍湘"},
            { "瑞杏","瑞杏" },
            { "叶云裳","葉雲裳" },
            { "虞小梅","虞小梅" },
            { "郁竹","郁竹" },
            { "魏菊","魏菊" },
            { "自己","自己" },
        };

        public static string GetLocalizedStr(string key)
        {
            var lang = LocalizationManager.Instance.LocaleResolver.GetCurrentLang();

            if (lang == "ChineseTraditional" && _localizationDic.ContainsKey(key))
                return _localizationDic[key];
            else
                return key;
        }

        public static string GetFacilityName(string id)
        {
            if (!id.StartsWith("Facility_"))
                return id;

            id = id.Replace("_", "/Name/");
            return LocalizationManager.Instance.LocaleResolver.GetString(id);
        }

        private static Dictionary<int, string> _loverDic = new Dictionary<int, string>
        {
            { 0,"小师妹" },
            { 1,"瑞杏" },
            { 2,"叶云裳" },
            { 3,"虞小梅" },
            { 4,"上官萤"},
            { 5,"夏侯兰"},
            { 6,"郁竹" },
            { 7,"魏菊" },
            { 8,"龙湘"},
            { 99,"自己" },
        };

        public static string GetLover(int index)
        {
            if (_loverDic.ContainsKey(index))
                return _loverDic[index];
            else
                return index.ToString();
        }
    }

    internal static class YExtension
    {
        public static string GetParsedText(string input)
        {
            var pattern = @"<[^>]*>";
            var result = Regex.Replace(input, pattern, "");

            return result;
        }

        public static bool IsEnglishAlphanumericOrSymbols(string input)
        {
            string pattern = @"^[a-zA-Z0-9!@#\$%\^\&*\)\(+=._-]+$";
            return Regex.IsMatch(input, pattern);
        }

        public static object InvokePrivateMethod(this object instance, string methodName, object[] parameters)
        {
            var type = instance.GetType();
            var methodInfo = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (methodInfo != null)
                return methodInfo.Invoke(instance, parameters);
            else
                throw new ArgumentException($"没有在 {type.FullName} 中找到私有方法 {methodName}");
        }

        public static object GetPrivateVariable(this object instance, string variableName)
        {
            var type = instance.GetType();
            var fieldInfo = type.GetField(variableName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
                return fieldInfo.GetValue(instance);
            else
                throw new ArgumentException($"私有变量 {variableName} 不存在或不可访问。");
        }

        public static void DebugInfo(string title, string content)
        {
            Console.WriteLine($"=========={title} 开始==========");
            Console.WriteLine(content);
            Console.WriteLine($"=========={title} 结束==========");
        }

        /* 需要溯源代码时，复制这个过去

            StackTrace stackTrace = new StackTrace();
            StackFrame[] stackFrames = stackTrace.GetFrames();

            if (stackFrames != null && stackFrames.Length > 1)
            {
                for (int i = 1; i < stackFrames.Length; i++)
                {
                    StackFrame callerFrame = stackFrames[i];
                    MethodBase callerMethod = callerFrame.GetMethod();

                    string callerClassName = callerMethod.ReflectedType.FullName;
                    string callerMethodName = callerMethod.Name;

                    Console.WriteLine($"{callerClassName}.{callerMethodName}");
                }
            }
        */
    }
}