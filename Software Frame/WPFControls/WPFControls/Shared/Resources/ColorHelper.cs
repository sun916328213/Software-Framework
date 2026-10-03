// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：ColorHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、System、System.Collections.Generic、System.Windows、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using WPFControls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WPFControls.Controls.Resources
{
    /// <summary>
    /// 颜色帮助类，用于从Resources.xaml中获取颜色资源
    /// </summary>
    public class ColorHelper 
    {
        #region Fields
        
        /// <summary>
        /// 颜色缓存，用于提高性能
        /// </summary>
        private static readonly Dictionary<string, SolidColorBrush> _colorCache = new Dictionary<string, SolidColorBrush>();
        
        /// <summary>
        /// 资源字典
        /// </summary>
        private static ResourceDictionary _resourceDictionary;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// 获取资源字典
        /// </summary>
        private ResourceDictionary ResourceDictionary
        {
            get
            {
                if (_resourceDictionary == null)
                {
                    LoadResourceDictionary();
                }
                return _resourceDictionary;
            }
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// 加载资源字典
        /// </summary>
        private void LoadResourceDictionary()
        {
            try
            {
                _resourceDictionary = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/WPFControls;component/Shared/Resources/Resources.xaml", UriKind.RelativeOrAbsolute)
                };
            }
            catch
            {
                _resourceDictionary = null;
            }
        }
        
        /// <summary>
        /// 从资源中获取颜色
        /// </summary>
        /// <param name="key">颜色资源键</param>
        /// <returns>SolidColorBrush颜色</returns>
        private static SolidColorBrush GetColorFromResources(string key)
        {
            // 首先尝试从缓存中获取
            if (_colorCache.TryGetValue(key, out SolidColorBrush cachedColor))
            {
                return cachedColor;
            }
            
            try
            {
                SolidColorBrush color = null;
                
                // 首先尝试从应用程序资源中获取
                if (global::System.Windows.Application.Current?.Resources.Contains(key) == true)
                {
                    color = global::System.Windows.Application.Current.Resources[key] as SolidColorBrush;
                }
                // 如果应用程序资源中没有，则从资源字典中获取
                else if (_resourceDictionary?.Contains(key) == true)
                {
                    color = _resourceDictionary[key] as SolidColorBrush;
                }
                
                // 缓存颜色以便下次快速获取
                if (color != null)
                {
                    _colorCache[key] = color;
                    return color;
                }
            }
            catch
            {
                // 忽略异常
            }
            
            return null;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// 根据键获取颜色
        /// </summary>
        /// <param name="key">颜色资源键</param>
        /// <returns>SolidColorBrush颜色</returns>
        public SolidColorBrush GetColor(string key)
        {
            return GetColorFromResources(key);
        }
        
        /// <summary>
        /// 获取颜色，如果无法从资源中获取则返回指定的默认颜色
        /// </summary>
        /// <param name="key">颜色资源键</param>
        /// <param name="defaultColor">默认颜色</param>
        /// <returns>SolidColorBrush颜色</returns>
        public SolidColorBrush GetColorOrDefault(string key, SolidColorBrush defaultColor)
        {
            var color = GetColorFromResources(key);
            return color ?? defaultColor;
        }
        
        /// <summary>
        /// 清除颜色缓存
        /// </summary>
        public void ClearCache()
        {
            _colorCache.Clear();
        }
        
        /// <summary>
        /// 重新加载资源字典
        /// </summary>
        public void ReloadResources()
        {
            _resourceDictionary = null;
            ClearCache();
            LoadResourceDictionary();
        }
        
        #endregion
        
        #region Color Properties
        
        /// <summary>
        /// 白色
        /// </summary>
        public static SolidColorBrush Color_White => GetColorFromResources("Color_White");
        
        /// <summary>
        /// 黑色
        /// </summary>
        public static SolidColorBrush Color_Black => GetColorFromResources("Color_Black");
        
        /// <summary>
        /// 灰色
        /// </summary>
        public static SolidColorBrush Color_Gray => GetColorFromResources("Color_Gray");
        
        /// <summary>
        /// 蓝色
        /// </summary>
        public static SolidColorBrush Color_Blue => GetColorFromResources("Color_Blue");
        
        /// <summary>
        /// 红色
        /// </summary>
        public static SolidColorBrush Color_Red => GetColorFromResources("Color_Red");
        
        /// <summary>
        /// 绿色
        /// </summary>
        public static SolidColorBrush Color_Green => GetColorFromResources("Color_Teal");
        
        /// <summary>
        /// 橙色
        /// </summary>
        public static SolidColorBrush Color_Orange => GetColorFromResources("Color_Orange");
        
        #endregion
    }
}