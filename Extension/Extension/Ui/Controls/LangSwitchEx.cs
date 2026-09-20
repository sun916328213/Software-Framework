using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace Extension.UI.Controls
{
    public enum ELang
    { 
       ZH,
       EN,
    }

    public static class LangSwitchEx
    {
        // 语言选择：ZH为中文，EN为英文
        public static ELang Language { get; set; } = ELang.ZH;

        // 根据当前语言获取语言ID
        public static int LanguageID 
        { 
            get { return (int)Language; }
        }

        /// <summary>
        /// 语言切换事件
        /// </summary>
        public static event Action<ELang> LanguageChanged;

        /// <summary>
        /// 切换语言
        /// </summary>
        /// <param name="language">目标语言</param>
        public static void SwitchLanguage(ELang language)
        {
            if (Language != language)
            {
                Language = language;
                
                // 设置当前线程的区域性
                CultureInfo culture = new CultureInfo("zh");
                switch (language)
                {
                    case ELang.ZH: culture = new CultureInfo("zh"); break;
                    case ELang.EN: culture = new CultureInfo("en"); break;
                }
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                
                // 触发语言切换事件
                LanguageChanged?.Invoke(Language);
            }
        }

        /// <summary>
        /// 切换到中文
        /// </summary>
        public static void SwitchToChinese()
        {
            SwitchLanguage(ELang.ZH);
        }

        /// <summary>
        /// 切换到英文
        /// </summary>
        public static void SwitchToEnglish()
        {
            SwitchLanguage(ELang.EN);
        }

        /// <summary>
        /// 获取指定键的资源字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>本地化字符串</returns>
        public static string GetResource(string key)
        {
            // 在WPF中，通常通过资源字典来管理多语言资源
            // 这里可以实现从资源字典中获取字符串的逻辑
            try
            {
                if (Application.Current != null)
                {
                    object resource = Application.Current.TryFindResource(key);
                    if (resource != null)
                        return resource.ToString();
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException
                                          or System.Windows.ResourceReferenceKeyNotFoundException
                                          or ArgumentNullException)
            {
                // 找不到资源时返回 key 本身（"缺翻译"比"崩溃"好），这个降级保留。
                // 留痕便于批量发现漏翻译的 key，而不是等用户在界面上看到英文键名。
                Debug.WriteLine($"[LangSwitchEx] 查找资源 '{key}' 失败，回退显示键名：{ex.Message}");
            }

            return key; // 如果找不到资源，返回键本身
        }

        /// <summary>
        /// 根据报警字段和语言选择返回指定内容
        /// </summary>
        /// <param name="alarmField"></param>
        /// <returns></returns>
        public static string GetMessage(this string[] alarmField)
        {
 
            if (LanguageID >= 0 && LanguageID < alarmField.Length)
            {
                return alarmField[LanguageID];
            }
            return "未知报警";
        }
    }
}