// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 直接导入的协作命名空间包括：SecsGem.Core、Secs4Net。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using Secs4Net;

namespace SecsGem.Runtime;

public delegate Task SecsHandle(SecsMessage message, string data);

public delegate void SecsLevelHandle(string data);
