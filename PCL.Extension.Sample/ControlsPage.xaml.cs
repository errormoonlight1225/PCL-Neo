using System.Windows;
using System.Windows.Controls;
using PCL.Extension.SDK;

namespace PCL.Extension.Sample;

public partial class ControlsPage : UserControl
{
    private readonly IExtensionContext _context;
    private int _clicks;

    public ControlsPage(IExtensionContext context)
    {
        _context = context;
        InitializeComponent();
        RefreshInputs();
    }

    private void BtnNormal_Click(object sender, RoutedEventArgs e)
    {
        _clicks++;
        LabClicks.Text = _context.Localization.Text("Sample.Button.Clicked", _clicks);
    }

    private void TextSample_TextChanged(object sender, TextChangedEventArgs e) => RefreshInputs();

    private void ComboChoice_SelectionChanged(object? sender, EventArgs e) => RefreshInputs();

    private void CheckOption_Change(object sender, RoutedEventArgs e) => RefreshInputs();

    private void RefreshInputs()
    {
        if (LabInputs is null) return;
        LabInputs.Text = $"text = \"{TextSample.Text}\", combo = {ComboChoice.SelectedItem?.Tag ?? "-"}, " +
                         $"check = {CheckOption.IsChecked}";
    }
}
