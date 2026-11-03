using Vision.Core.Calibration;
using Vision.Core.Engine;
using Vision.Core.Operations;
using Vision.Core.Results;
using Vision.Tools.Execution;

namespace Vision.Tools.Common;

internal static partial class CommonVisionOperations
{
    private static BackendOutput CalibrateUniformPixelScale(PixelScaleCalibrationOperation operation)
    {
        var first = operation.First.Position;
        var second = operation.Second.Position;
        var pixels = double.Hypot(second.X - first.X, second.Y - first.Y);
        var scale = operation.KnownLengthMillimeters / pixels;
        return new(new UniformPixelScaleCalibration(Guid.NewGuid(), operation.First, operation.Second,
            pixels, operation.KnownLengthMillimeters, scale));
    }

    private static BackendOutput ConvertLengthToMillimeters(LengthToMillimetersOperation operation)
    {
        // 原像素测量保持不变；溢出或正数下溢由强类型结果边界拒绝，不改单位标签或伪造零值。
        var millimeters = operation.Source.Value * operation.Calibration.MillimetersPerPixel;
        return new(new MillimeterLengthOutput(operation.Source, operation.Calibration, millimeters));
    }

    private static void ValidateCalibrationOutput(VisionOperation operation, BackendOutput output)
    {
        if (output.NoMatch)
        {
            throw new InvalidOperationException("比例标定/毫米换算/公差不能以NoMatch伪装数值失败。");
        }

        if (operation is PixelScaleCalibrationOperation calibration)
        {
            var expectedPixels = double.Hypot(calibration.Second.Position.X - calibration.First.Position.X,
                calibration.Second.Position.Y - calibration.First.Position.Y);
            if (output.Value is not UniformPixelScaleCalibration result
                || !ReferenceEquals(result.First, calibration.First) || !ReferenceEquals(result.Second, calibration.Second)
                || result.ReferencePixelLength != expectedPixels || result.KnownLengthMillimeters != calibration.KnownLengthMillimeters)
            {
                throw new InvalidOperationException("比例标定结果与本次参考点或已知长度不符。");
            }
            return;
        }

        if (operation is LengthToMillimetersOperation conversion)
        {
            if (output.Value is not MillimeterLengthOutput result
                || !ReferenceEquals(result.SourcePixels, conversion.Source) || !ReferenceEquals(result.Calibration, conversion.Calibration)
                || result.ValueMillimeters != conversion.Source.Value * conversion.Calibration.MillimetersPerPixel)
            {
                throw new InvalidOperationException("毫米换算结果与本次像素测量或标定依据不符。");
            }
            return;
        }

        if (operation is MillimeterRangeOperation range)
        {
            if (output.Value is not MillimeterRangeOutput result || !ReferenceEquals(result.Measurement, range.Source)
                || result.MinimumMillimeters != range.MinimumMillimeters || result.MaximumMillimeters != range.MaximumMillimeters)
            {
                throw new InvalidOperationException("毫米公差结果与本次测量或上下限不符。");
            }
            return;
        }

        throw new InvalidOperationException("未约定的标定操作。");
    }
}
