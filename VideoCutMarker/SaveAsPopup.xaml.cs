using System;
using Microsoft.Maui.Controls;

#if ANDROID
namespace VideoCutMarker
{
	public partial class SaveAsPopup : ContentPage
	{
		public string FileName { get; private set; }
		private TaskCompletionSource<string> _taskCompletionSource;

		public SaveAsPopup(string initialFileName)
		{

			InitializeComponent();
			FileNameEntry.Text = initialFileName;

_taskCompletionSource = new TaskCompletionSource<string>();

			// 백그라운드 탭 처리 방지
			this.BackgroundColor = new Color(0, 0, 0, 0.5f);
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();

			// 파일 이름 필드에 포커스 및 전체 텍스트 선택
			Device.BeginInvokeOnMainThread(async () => {
				await Task.Delay(100); // 약간의 지연으로 UI가 렌더링될 시간 부여
				FileNameEntry.Focus();
				FileNameEntry.CursorPosition = 0;
				FileNameEntry.SelectionLength = FileNameEntry.Text?.Length ?? 0;
			});
		}

		private void OnCancelClicked(object sender, EventArgs e)
		{
			_taskCompletionSource.SetResult(null);
			Navigation.PopModalAsync();
		}

		private void OnSaveClicked(object sender, EventArgs e)
		{
			_taskCompletionSource.SetResult(FileNameEntry.Text);
			Navigation.PopModalAsync();
		}

		public Task<string> GetFileNameAsync()
		{
			return _taskCompletionSource.Task;
		}
	}
}
#endif