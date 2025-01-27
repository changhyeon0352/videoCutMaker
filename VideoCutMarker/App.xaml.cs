namespace VideoCutMarker
{
	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();
			MainPage = new MainPage();
		}
		protected override Window CreateWindow(IActivationState? activationState)
		{
			var Window = new Window(MainPage);
			return Window;
		}

	}
}
