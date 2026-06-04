using Microsoft.Extensions.DependencyInjection;
using ThePrey.Application.App.Services;

namespace ThePrey.Application.App;

public partial class App
{
	private readonly IGameEngineService _gameEngine;

	public App(IGameEngineService gameEngine)
	{
		InitializeComponent();
		_gameEngine = gameEngine;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// MAUI has no Application.Paused/Resumed events; Window.Stopped/Resumed are the
		// cross-platform equivalents (Android onStop, iOS DidEnterBackground). The game engine
		// suspends its loops while backgrounded and restarts them on foreground.
		window.Stopped += async (_, _) => await _gameEngine.SuspendAsync();
		window.Resumed += async (_, _) => await _gameEngine.ResumeAsync();

		return window;
	}
}