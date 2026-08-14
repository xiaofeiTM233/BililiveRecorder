using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Serilog.Events;

#nullable enable
namespace BililiveRecorder.Desktop.Converters
{
    /// <summary>
    /// 枚举值与 RadioButton IsChecked 的双向转换（ConverterParameter 传目标枚举值）。
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public static readonly EnumToBooleanConverter Instance = new EnumToBooleanConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
                return false;
            return value.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is not null && parameter.GetType() == targetType)
                return parameter;
            return BindingOperations.DoNothing;
        }
    }

    /// <summary>
    /// 枚举值与目标值不相等时为 true（用于 IsVisible，如“未关闭分段”时显示数值输入）。
    /// </summary>
    public class EnumNotToBooleanConverter : IValueConverter
    {
        public static readonly EnumNotToBooleanConverter Instance = new EnumNotToBooleanConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
                return true;
            return !value.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindingOperations.DoNothing;
    }

    public class BoolToRecordingBrushConverter : IValueConverter
    {
        public static readonly BoolToRecordingBrushConverter Instance = new BoolToRecordingBrushConverter();

        private static readonly IBrush recordingBrush = new SolidColorBrush(Colors.Red);
        private static readonly IBrush monitoringBrush = new SolidColorBrush(Colors.DarkOrange);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? recordingBrush : monitoringBrush;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindingOperations.DoNothing;
    }

    public class LogEventLevelToBrushConverter : IValueConverter
    {
        public static readonly LogEventLevelToBrushConverter Instance = new LogEventLevelToBrushConverter();

        private static readonly IBrush debugBrush = new SolidColorBrush(Colors.Gray);
        private static readonly IBrush infoBrush = new SolidColorBrush(Colors.Green);
        private static readonly IBrush warningBrush = new SolidColorBrush(Colors.DarkOrange);
        private static readonly IBrush errorBrush = new SolidColorBrush(Colors.Red);
        private static readonly IBrush fatalBrush = new SolidColorBrush(Colors.DarkRed);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                LogEventLevel.Verbose or LogEventLevel.Debug => debugBrush,
                LogEventLevel.Information => infoBrush,
                LogEventLevel.Warning => warningBrush,
                LogEventLevel.Error => errorBrush,
                LogEventLevel.Fatal => fatalBrush,
                _ => debugBrush,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindingOperations.DoNothing;
    }
}
