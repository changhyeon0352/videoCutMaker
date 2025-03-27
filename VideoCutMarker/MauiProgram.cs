using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace VideoCutMarker
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkitMediaElement()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

#if DEBUG
			builder.Logging.AddDebug();
#endif
			builder.ConfigureMauiHandlers(cf => {
#if ANDROID
				cf.AddHandler(typeof(Picker), typeof(Platforms.AndroidModule.PickerHandlerFixAndroidFocus));
#endif
			});

			return builder.Build();
		}


	}
}
