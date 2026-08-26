using Device;
using MotionControl.Definition;
using MotionControl.Vendors.ADLINK;
using MotionControl.Vendors.Googol;
using MotionControl.Vendors.Inovance;
using MotionControl.Vendors.Leadshine;
using MotionControl.Vendors.Simulation;
using MotionControl.Vendors.ZMotion;

namespace MotionControl
{
    /// <summary>
    /// 运动控制卡管理类（注册制工厂）
    /// <para>内置仿真/凌华/雷赛品牌注册，开箱即用；</para>
    /// <para>第三方新增品牌：继承MotionControlBase实现各Refactor方法后调用Register即可，无需修改本项目源码</para>
    /// </summary>
    public class MotionManager : ManagerBase<IMotionCard, MotionCardParam>
    {
        private readonly Dictionary<EMotionCardType, Func<MotionCardParam, IMotionCard>> _factories = new();

        public MotionManager()
        {
            // 内置品牌默认注册（重复Register同类型会覆盖）
            Register(EMotionCardType.Simulation, p => new SimulationMotionCard());
            Register(EMotionCardType.ADLINK, p => new AdlinkHelper());
            Register(EMotionCardType.Leadshine, p => new LeadshineHelper());
            Register(EMotionCardType.Googol, p => new GoogolHelper());
            Register(EMotionCardType.ZMotion, p => new ZMotionHelper());
            Register(EMotionCardType.Inovance, p => new InovanceHelper());
        }

        /// <summary>
        /// 注册控制卡工厂（同一类型重复注册将覆盖旧实现）
        /// </summary>
        /// <param name="type">控制卡类型标识</param>
        /// <param name="factory">创建实例的工厂委托</param>
        public void Register(EMotionCardType type, Func<MotionCardParam, IMotionCard> factory)
        {
            _factories[type] = factory;
        }

        /// <summary>
        /// 指定类型是否已注册
        /// </summary>
        public bool IsRegistered(EMotionCardType type) => _factories.ContainsKey(type);

        /// <inheritdoc/>
        public override void Config(List<MotionCardParam> param)
        {
            Devices.Clear();
            foreach (var item in param)
                Create(item);
        }

        /// <inheritdoc/>
        public override IMotionCard Create(MotionCardParam param)
        {
            if (!_factories.TryGetValue(param.CardType, out var factory))
                throw new NotSupportedException($"{param.CardType} 未注册，请先调用{nameof(Register)}");
            var device = factory(param);
            device.Param = param;
            Devices.Add(device);
            return device;
        }

        /// <summary>
        /// 紧急停止所有卡的所有轴
        /// </summary>
        public void StopAll()
        {
            foreach (var device in Devices)
                device.StopAll(EStopMode.Emergency);
        }
    }
}
