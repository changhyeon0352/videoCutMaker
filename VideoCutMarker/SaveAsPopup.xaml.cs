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

			// ��׶��� �� ó�� ����
			this.BackgroundColor = new Color(0, 0, 0, 0.5f);
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();

			// ���� �̸� �ʵ忡 ��Ŀ�� �� ��ü �ؽ�Ʈ ����
			Device.BeginInvokeOnMainThread(async () => {
				await Task.Delay(100); // �ణ�� �������� UI�� �������� �ð� �ο�
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