namespace HexMaster.ThePrey.Maui.App.Controls;

/// <summary>
/// The in-game HUD overlay. A self-contained <see cref="ContentView"/> the (separately-owned) gameplay
/// map page embeds at the bottom of the screen; the host sets its <c>BindingContext</c> to an
/// initialized <see cref="ViewModels.GameHudViewModel"/> and drives its activate/deactivate lifecycle.
/// </summary>
public partial class GameHudView : ContentView
{
    public GameHudView()
    {
        InitializeComponent();
    }
}
