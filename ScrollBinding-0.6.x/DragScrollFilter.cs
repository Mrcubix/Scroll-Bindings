using OpenTabletDriver;
using OpenTabletDriver.Desktop.Binding;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;

namespace ScrollBinding
{
    [PluginName("Drag Scroll")]
    public class DragScrollFilter : IPositionedPipelineElement<IDeviceReport>
    {
        #region Fields

        private DragScrollBinding _binding;
        private TabletReference _tablet;
        private bool _initialized;

        public event Action<IDeviceReport> Emit;

        #endregion

        #region Properties

        [Resolved]
        public IDriver Driver { get; set; }

        [TabletReference]
        public TabletReference Tablet
        {
            get => _tablet;
            set
            {
                _tablet = value;
                Initialize();
            }
        }

        public PipelinePosition Position => PipelinePosition.PreTransform;

        #endregion

        #region Methods

        public void Consume(IDeviceReport report)
        {
            if (_initialized == false)
                Initialize();

            if (report is IAbsolutePositionReport positionReport)
                _binding?.Scroll(positionReport);

            Emit?.Invoke(report);
        }

        private void Initialize()
        {
            if (Driver is Driver driver)
            {
                IOutputMode outputMode = driver.InputDevices.Where(dev => dev.OutputMode.Tablet == _tablet)
                                                            .Select(dev => dev.OutputMode)
                                                            .FirstOrDefault();

                if (outputMode != null && outputMode.Elements != null &&
                    outputMode.Elements.OfType<BindingHandler>().FirstOrDefault() is BindingHandler bindingHandler)
                {
                    FetchBindingFromHandler(bindingHandler);
                    _initialized = true;
                }
                else
                    Log.Write("Drag Scroll Binding", "No output mode found for the specified tablet.");
            }
        }

        private void FetchBindingFromHandler(BindingHandler bindingHandler)
        {
            // Find the first DragScrollBinding in the binding handler

            if ((_binding = bindingHandler.Tip?.Binding as DragScrollBinding) != null)
                return;

            if ((_binding = bindingHandler.Eraser?.Binding as DragScrollBinding) != null)
                return;

            foreach (var penButton in bindingHandler.PenButtons)
            {
                if (penButton.Value?.Binding is DragScrollBinding dragScrollBinding)
                {
                    _binding = dragScrollBinding;
                    return;
                }
            }

            foreach (var button in bindingHandler.MouseButtons)
            {
                if (button.Value?.Binding is DragScrollBinding dragScrollBinding)
                {
                    _binding = dragScrollBinding;
                    return;
                }
            }

            foreach (var button in bindingHandler.AuxButtons)
            {
                if (button.Value?.Binding is DragScrollBinding dragScrollBinding)
                {
                    _binding = dragScrollBinding;
                    return;
                }
            }
        }

        #endregion
    }
}