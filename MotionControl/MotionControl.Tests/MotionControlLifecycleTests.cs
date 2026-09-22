using MotionControl.Definition;
using Xunit;

namespace MotionControl.Tests;

public sealed class MotionControlLifecycleTests
{
    [Fact]
    public async Task Disconnect_WaitsForInFlightCommandBeforeReleasingConnection()
    {
        var card = new BlockingMotionCard
        {
            Param = new MotionCardParam { Axises = [new AxisParam()] },
        };
        Assert.True(card.Connect());

        var move = Task.Run(() => card.MoveAbsResult(0, 1));
        Assert.True(card.MoveEntered.Wait(TimeSpan.FromSeconds(2)));

        var disconnect = Task.Run(card.DisconnectResult);
        Assert.False(card.DisconnectEntered.Wait(TimeSpan.FromMilliseconds(150)));

        card.ReleaseMove.Set();
        var moveResult = await move;
        var disconnectResult = await disconnect;

        Assert.True(moveResult.IsSuccess);
        Assert.True(disconnectResult.IsSuccess);
        Assert.Equal(["move-start", "move-end", "disconnect"], card.Events);
        Assert.False(card.IsConnected);
    }

    private sealed class BlockingMotionCard : MotionControlBase
    {
        public ManualResetEventSlim MoveEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseMove { get; } = new(false);
        public ManualResetEventSlim DisconnectEntered { get; } = new(false);
        public List<string> Events { get; } = new();

        protected override EMotionResult ConnectRefactor() => EMotionResult.Success;

        protected override EMotionResult DisConnectRefactor()
        {
            DisconnectEntered.Set();
            lock (Events)
                Events.Add("disconnect");
            return EMotionResult.Success;
        }

        protected override EMotionResult ServoOnRefactor(int axis) => EMotionResult.Success;
        protected override EMotionResult ServoOffRefactor(int axis) => EMotionResult.Success;
        protected override EMotionResult ClearAxisStatusRefactor(int axis) => EMotionResult.Success;
        protected override EMotionResult SetSpeedRefactor(int axis, long velPulse) => EMotionResult.Success;

        protected override EMotionResult MoveAbsRefactor(int axis, long pulse)
        {
            lock (Events)
                Events.Add("move-start");
            MoveEntered.Set();
            if (!ReleaseMove.Wait(TimeSpan.FromSeconds(3)))
                return EMotionResult.Timeout;
            lock (Events)
                Events.Add("move-end");
            return EMotionResult.Success;
        }

        protected override EMotionResult MoveRelRefactor(int axis, long pulse) => EMotionResult.Success;
        protected override EMotionResult JogStartRefactor(int axis, short direction) => EMotionResult.Success;
        protected override EMotionResult StopAxisRefactor(int axis, EStopMode stopMode) => EMotionResult.Success;
        protected override EMotionResult StopAllRefactor(EStopMode stopMode) => EMotionResult.Success;
        protected override EMotionResult HomeStartRefactor(int axis) => EMotionResult.Success;

        protected override EMotionResult GetCommandPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            return EMotionResult.Success;
        }

        protected override EMotionResult GetEncoderPositionRefactor(int axis, out long pulse)
        {
            pulse = 0;
            return EMotionResult.Success;
        }

        protected override EMotionResult GetAxisStatusRefactor(int axis, out long raw)
        {
            raw = (long)EAxisStatusFlag.InPosition;
            return EMotionResult.Success;
        }

        protected override EMotionResult GetInputRefactor(int index, out bool state)
        {
            state = false;
            return EMotionResult.Success;
        }

        protected override EMotionResult GetOutputRefactor(int index, out bool state)
        {
            state = false;
            return EMotionResult.Success;
        }

        protected override EMotionResult SetOutputRefactor(int index, bool state) => EMotionResult.Success;
    }
}
