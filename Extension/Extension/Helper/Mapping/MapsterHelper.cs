using Extension.Struct;
using Mapster;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Helper.Mapping
{

    /// <summary>
    /// 统一的映射配置管理类，负责所有Struct的映射初始化
    /// </summary>
    public static class MappingConfig
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化所有Struct的映射配置
        /// </summary>
        public static void InitializeAllMappings()
        {
            if (_isInitialized) return;

            // 初始化SImageInfo相关映射
            ImageInfoMapping.Configure(TypeAdapterConfig.GlobalSettings);
            LogInfoMapping.Configure(TypeAdapterConfig.GlobalSettings);
 
            _isInitialized = true;
        }

        /// <summary>
        /// 添加映射配置（类似ImageInfoMapping.Configure的功能）
        /// </summary>
        /// <param name="configureAction">配置动作</param>
        public static void AddMapping(Action<TypeAdapterConfig> configureAction)
        {
            // 执行传入的配置动作，类似于ImageInfoMapping.Configure
            configureAction?.Invoke(TypeAdapterConfig.GlobalSettings);
        }
    }
}