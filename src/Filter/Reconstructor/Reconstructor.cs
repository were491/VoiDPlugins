using System;
using System.Numerics;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OpenTabletDriver.Plugin.Timing;

namespace VoiDPlugins.Filter
{
    [PluginName("Reconstructor")]
    public class Reconstructor : IPositionedPipelineElement<IDeviceReport>
    {
        private Vector2? lastAvg;
        private float weight;

        private TimeSpan delay;
        private readonly HPETDeltaStopwatch stopwatch = new HPETDeltaStopwatch();

        [Property("EMA Weight"), DefaultPropertyValue(0.5f), ToolTip
        (
            "Default: 0.5\n\n" +
            "Defines the weight of the latest sample against previous ones [Range: 0.0 - 1.0]\n" +
            "  Lower == More hardware smoothing removed\n" +
            "  1 == No effect"
        )]
        public float EMAWeight
        {
            set => weight = Math.Clamp(value, 0, 1);
            get => weight;
        }

        [Property("Reset Delay"), Unit("ms"), DefaultPropertyValue(50.0f), ToolTip
        (
            "Default: 50ms\n\n" +
            "Defines the time in which no samples are received before EMA resets.\n" +
            "Usually not required if tablet reports out-of-range."
        )]
        public double ResetDelay
        {
            set => delay = TimeSpan.FromMilliseconds(value);
            get => delay.TotalMilliseconds;
        }

        public event Action<IDeviceReport>? Emit;

        public PipelinePosition Position => PipelinePosition.PreTransform;

        public void Consume(IDeviceReport value)
        {
            if (value is OutOfRangeReport) {
                lastAvg = null;
            }
            else if (value is ITabletReport report)
            {
                var truePoint = (lastAvg.HasValue && stopwatch.Restart() <= delay) ?
                                ReverseEMAFunc(report.Position, lastAvg.Value, (float)EMAWeight) : report.Position;
                lastAvg = report.Position;
                report.Position = truePoint;
                value = report;
            }

            Emit?.Invoke(value);
        }

        private static Vector2 ReverseEMAFunc(Vector2 currentEMA, Vector2 lastEMA, float weight)
        {
            return ((currentEMA - lastEMA) / weight) + lastEMA;
        }
    }
}