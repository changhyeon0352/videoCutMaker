using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Provider;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Java.Nio.FileNio;
using Microsoft.Maui.Controls.PlatformConfiguration;
using System.Diagnostics;

namespace VideoCutMarker
{
	[Activity(
		Label = "VideoCutMarker",
		Icon = "@mipmap/appicon",
		Theme = "@style/MainTheme",
		MainLauncher = true,
		LaunchMode = LaunchMode.SingleTask,
		Exported = true, // Android 12 이상에서는 반드시 명시적으로 설정
		ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
	)]
	[IntentFilter(
		new[] { Android.Content.Intent.ActionView, Android.Content.Intent.ActionEdit },
		Categories = new[] { Android.Content.Intent.CategoryDefault },
		DataMimeType = "video/*"
	)]
	public class MainActivity : MauiAppCompatActivity
	{
		private const int REQUEST_EXTERNAL_STORAGE = 1;
		private static readonly string[] PERMISSIONS_STORAGE =
		{
			Manifest.Permission.ReadExternalStorage,
			Manifest.Permission.WriteExternalStorage
		};
		protected override void OnCreate(Bundle? savedInstanceState)
		{
			if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
			{
				// 권한 요청
				ActivityCompat.RequestPermissions(this, new String[] { Manifest.Permission.WriteExternalStorage }, 1);
			}
			base.OnCreate(savedInstanceState);
			HandleIntent(Intent);
			var actionBar = SupportActionBar;
			if (actionBar != null)
			{
				actionBar.Hide();
			}
			var mainPage = (MainPage)App.Current.MainPage;
			if (mainPage != null)
			{
				mainPage.RequestMoveToBackground = MoveToBackground;
			}
			VerifyStoragePermissions();
		}
		protected override void OnNewIntent(Intent? intent)
		{
			base.OnNewIntent(intent);

			// 앱이 다시 포그라운드로 올 때 인텐트 처리
			HandleIntent(intent);
			VerifyStoragePermissions();
		}
		
		private void HandleIntent(Intent intent)
		{
			// 인텐트를 통해 비디오 파일 가져오기 처리
			if (Android.Content.Intent.ActionView.Equals(intent.Action) && intent.Data != null)
			{
				var videoUri = intent.Data;

				// videoUri를 사용하여 MediaElement에 소스 설정
				var mainPage = (MainPage)App.Current.MainPage;
				if (mainPage != null)
				{
					var fullPath = GetRealPathFromURI(videoUri);
					mainPage?.SetRealPath(fullPath);
					mainPage?.SetVideoSource(videoUri.ToString());
				}
			}
		}
		public void VerifyStoragePermissions()
		{
			// 이미 권한이 있는지 확인
			var permission1 = ActivityCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage);

			if (permission1 != (int)Permission.Granted)
			{
				// 권한 요청
				ActivityCompat.RequestPermissions(this, PERMISSIONS_STORAGE, REQUEST_EXTERNAL_STORAGE);
			}
		}

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
		{
			base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

			if (requestCode == REQUEST_EXTERNAL_STORAGE)
			{
				if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
				{
					// 권한이 허용된 경우
					Console.WriteLine("외부 저장소에 대한 권한이 허용되었습니다.");
				}
				else
				{
					// 권한이 거부된 경우
					Console.WriteLine("외부 저장소에 대한 권한이 거부되었습니다.");
				}
			}
		}

		public override void OnBackPressed()
		{
			//base.OnBackPressed();
			var mainPage = (MainPage)App.Current.MainPage;
			if (mainPage != null)
			{
				mainPage.PauseVideo();
			}
			MoveToBackground();
		}

		public string GetRealPathFromURI(Android.Net.Uri contentUri)
		{
			string[] proj = {MediaStore.Video.Media.InterfaceConsts.Data, MediaStore.Video.Media.InterfaceConsts.Id, MediaStore.Video.Media.InterfaceConsts.DisplayName, MediaStore.Video.Media.InterfaceConsts.Size };
			using (var cursor = ContentResolver.Query(contentUri, proj, null, null, null))
			{
				// Cursor가 null이 아니고, 첫 번째 행으로 이동할 수 있는지 확인
				if (cursor != null && cursor.MoveToFirst())
				{
					int column_index = cursor.GetColumnIndexOrThrow(MediaStore.Video.Media.InterfaceConsts.Data); // "_data"의 인덱스 찾기
					int column_index1 = cursor.GetColumnIndexOrThrow(MediaStore.Video.Media.InterfaceConsts.Id); // "_data"의 인덱스 찾기
					int column_index2 = cursor.GetColumnIndexOrThrow(MediaStore.Video.Media.InterfaceConsts.DisplayName); // "_data"의 인덱스 찾기
					string filePath = cursor.GetString(column_index); // 파일 경로 가져오기
					string fileId = cursor.GetString(column_index1); // 파일 경로 가져오기
					string fileName = cursor.GetString(column_index2); // 파일 경로 가져오기

					if (!string.IsNullOrEmpty(filePath)) // 경로가 null이나 비어있지 않은지 확인
					{
						return filePath; // 유효한 파일 경로 반환
					}
					else
					{
						return "/storage/emulated/0/VideoCutMarker/" + fileName;
					}
				}
				else
				{
					Console.WriteLine("Cursor is empty or null.");
				}
			}
			return null;
		}
		public void MoveToBackground()
		{
			MoveTaskToBack(true);
		}


	}

}
