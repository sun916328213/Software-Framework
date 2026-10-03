// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：MappingConfig。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Imaging.Models、Mapster、System。导入不代表所有类型都会在运行时加载。
using Extension.Imaging.Models;
using Mapster;
using System;

namespace Extension.Integration.Mapster
{

    /// <summary>
    /// 统一的映射配置管理类，负责所有Struct的映射初始化
    /// </summary>
    public static class MappingConfig
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化 Extension 内 Struct 的映射配置
        /// </summary>
        /// <remarks>
        /// 2026-09-16：日志相关的 <c>LogInfoMapping</c> 已随 LogInfo 迁入 DataManager
        /// （日志属 DataManager 的领域概念，通用工具库不应定义它）。
        /// 日志映射改由 <c>DataManager.Log.Definition.LogMappingBootstrap.Initialize()</c> 注册，
        /// 应用启动时需要同时调用两者。
        /// </remarks>
        public static void InitializeAllMappings()
        {
            if (_isInitialized) return;

            // 初始化SImageInfo相关映射
            ImageInfoMapping.Configure(TypeAdapterConfig.GlobalSettings);

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