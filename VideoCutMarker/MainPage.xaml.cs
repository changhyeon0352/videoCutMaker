
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using System.Text;
using Microsoft.Extensions.Primitives;
using System;
namespace VideoCutMarker
{
	public partial class MainPage : ContentPage
	{
		private SortedDictionary<double,(int,int, bool)> markTimesDic = new SortedDictionary<double, (int, int, bool)>();    // 마크된 시간 목록
		private readonly List<Color> segmentColors = new List<Color>
{
	Color.FromArgb("#FF5733"), // 주황
    Color.FromArgb("#33FF57"), // 녹색
    Color.FromArgb("#3357FF"), // 파랑
    Color.FromArgb("#FF33A8"), // 핑크
    Color.FromArgb("#FF8C33"), // 밝은 주황
    Color.FromArgb("#8D33FF"), // 보라
    Color.FromArgb("#33FFF5"), // 밝은 청록
    Color.FromArgb("#FFD633"), // 노랑
    Color.FromArgb("#33D4FF"), // 밝은 하늘색
    Color.FromArgb("#FF3333")  // 빨강
};
		private Random random = new Random();
		private int videoWidth, videoHeight;
		private int fixVideoWidth, fixVideoHeight;
		private int displayWidth, displayHeight;
		private int startX, startY, endX, endY;
		private float scale, actualVideoWidth,actualVideoHeight, horizontalOffset,verticalOffset;
		private string currentFilePath;
		public Action RequestMoveToBackground;
		private bool isFixSize = false;
		// 버튼과 이벤트 핸들러를 저장할 딕셔너리 추가 (클래스 멤버 변수로)
		private Dictionary<Button, EventHandler> buttonClickHandlers = new Dictionary<Button, EventHandler>();

		public MainPage()
		{

			InitializeComponent();
			// 동영상 크기 설정 및 crop 테두리 설정
			mediaElement.MediaOpened += async (s, e) =>
			{
				await Task.Delay(1000);
				if((int)mediaElement.MediaWidth < 1)
				{
					await Task.Delay(1000);
				}
				mediaElement.Play();
				videoWidth = (int)mediaElement.MediaWidth;
				videoHeight = (int)mediaElement.MediaHeight;
				displayWidth = (int)mediaElement.Width;
				displayHeight = (int)mediaElement.Height;
				scale = Math.Min((float)displayWidth / (float)videoWidth, (float)displayHeight / (float)videoHeight);
				actualVideoHeight = videoHeight * scale;
				actualVideoWidth = videoWidth * scale;
				horizontalOffset = (displayWidth - actualVideoWidth) / 2;
				verticalOffset = (displayHeight - actualVideoHeight) / 2;
				// 처음에 테두리를 기본적으로 중앙에 위치
				startX = 0;
				endX = videoWidth;
				startY = 0;
				endY = videoHeight;

				UpdateCropLines();
				markTimesDic.Clear();
				markingOverlay.Children.Clear();
				barOverlay.Children.Clear();
				double totalTime = mediaElement.Duration.TotalSeconds;
				markTimesDic.Add(totalTime,(0,0,true));
				// 마킹된 위치에 표시 추가
				UpdateSegment();
			};

			// 드래깅으로 테두리 이동
			AddDragEventHandlers();
		}
		public void PauseVideo()
		{
			mediaElement.Pause();
		}

		// 테두리 이동 시 업데이트
		private void UpdateCropLines()
		{
			// 테두리 위치 갱신
			topLine.TranslationY = (videoHeight - endY) * scale + verticalOffset;
			topLineCover.TranslationY = topLine.TranslationY -10;
			bottomLine.TranslationY = - startY * scale - verticalOffset;
			bottomLineCover.TranslationY = bottomLine.TranslationY + 10;
			leftLine.TranslationX = startX * scale + horizontalOffset;
			leftLineCover.TranslationX = leftLine.TranslationX - 10;
			rightLine.TranslationX = (endX - videoWidth) * scale - horizontalOffset;
			rightLineCover.TranslationX = rightLine.TranslationX + 10;

			// 파일 이름 업데이트
			string fileName = Path.GetFileName(currentFilePath);
			resolutionLabel.Text = $"W:{endX - startX}, H:{endY - startY} {fileName}";
		}
		private void OnMarkClicked(object sender, EventArgs e)
		{
			// 현재 재생 시간을 마킹
			double currentTime = mediaElement.Position.TotalSeconds;
			markTimesDic.Add(currentTime, (startX,startY, true));

			// 마킹된 위치에 표시 추가
			UpdateSegment();
		}

		private void UpdateSegment()
		{
			// 현재 마크된 시간과 색상 목록을 사용하여 마크 표시를 업데이트합니다.
			int markCount = markingOverlay.Children.Count; // 기존 마크 표시 수
			int segmentCount = barOverlay.Children.Count; // 기존 세그먼트 수
			var keys = new List<double>(markTimesDic.Keys);
			var values = new List<(int,int,bool)>(markTimesDic.Values);
			

			for (int i = 0; i < markTimesDic.Count; i++)
			{
				int index = i;
				double preTime = 0;
				if(i != 0)
				{
					preTime = keys[i -1];
				}
				double time = keys[i];

				Color color = values[index].Item3 ? segmentColors[i % segmentColors.Count] : Colors.Grey; // 각 마크에 해당하는 색상 가져오기

				// 마킹 위치 비율을 계산하여 표시 위치 조정
				double position = time / mediaElement.Duration.TotalSeconds;
				double prePosition = preTime / mediaElement.Duration.TotalSeconds;
				Grid markerGrid = null;
				if ( i >= markCount)
				{
					var boxView = new BoxView
					{
						WidthRequest = 2,
						Color = Colors.Red,
						HeightRequest = 15,
						VerticalOptions = LayoutOptions.Center,
						HorizontalOptions = LayoutOptions.Center
					};
					markerGrid = new Grid
					{
						WidthRequest = 30, // 클릭 영역을 더 넓게 설정
						HeightRequest = 30,
						BackgroundColor = Colors.Transparent, // 투명한 색상
						VerticalOptions = LayoutOptions.Center,
						HorizontalOptions = LayoutOptions.Start
					};
					markerGrid.Children.Add(boxView);
					markingOverlay.Children.Add(markerGrid);
				}
				else
				{
					markerGrid = (Grid)markingOverlay.Children[i];
				}
				markerGrid.Margin = new Thickness(progressBar.Width * position - 15, 0, 0, 0);// 마커 중앙에 맞춰 

				//제스쳐 재세팅
				markerGrid.GestureRecognizers.Clear();
				var tapGesture = new TapGestureRecognizer
				{
					Command = new Command(() =>
					{
						// 짧은 터치 처리
						mediaElement.SeekTo(TimeSpan.FromSeconds(time));
						mediaElement.Play();
					})
				};
				markerGrid.GestureRecognizers.Add(tapGesture);
				var moveGesture = new PanGestureRecognizer();
				moveGesture.PanUpdated += (sender, e) =>
				{
					switch (e.StatusType)
					{
						case GestureStatus.Completed:
							markTimesDic.Remove(time);
							UpdateSegment();
							break;
					}
				};
				markerGrid.GestureRecognizers.Add(moveGesture);

				// BoxView 대신 Button으로 세그먼트 생성
				Button segmentButton = null;
				if (i >= segmentCount)
				{
					segmentButton = new Button
					{
						HeightRequest = 15,
						VerticalOptions = LayoutOptions.Center,
						HorizontalOptions = LayoutOptions.Start,
						Padding = new Thickness(0),
						BorderWidth = 0,
						CornerRadius = 0, // 직사각형 버튼
						Text = (i + 1).ToString(),
						FontSize = 10,
						FontAttributes = FontAttributes.Bold,
						TextColor = Colors.White // 텍스트 색상
					};
					barOverlay.Children.Add(segmentButton);
				}
				else
				{
					segmentButton = (Button)barOverlay.Children[i];
				}

				segmentButton.BackgroundColor = color;
				segmentButton.WidthRequest = progressBar.Width * (position - prePosition);
				segmentButton.Margin = new Thickness(progressBar.Width * prePosition, 0, 0, 0); // 시작 위치 지정

				// 버튼 클릭 이벤트 추가
				int capturedIndex = index; // 클로저를 위한 인덱스 캡처
				// UpdateSegment() 메서드 내의 이벤트 핸들러 부분 수정
				// 기존 이벤트 핸들러가 있으면 제거
				if (buttonClickHandlers.ContainsKey(segmentButton))
				{
					segmentButton.Clicked -= buttonClickHandlers[segmentButton];
					buttonClickHandlers.Remove(segmentButton);
				}

				// 새 이벤트 핸들러 생성
				EventHandler clickHandler = (s, e) =>
				{
					ToggleSegmentButton((Button)s, capturedIndex);
				};

				// 새 이벤트 핸들러 등록
				segmentButton.Clicked += clickHandler;
				buttonClickHandlers[segmentButton] = clickHandler;
			}
			// 불필요한 마커와 세그먼트 버튼 제거
			for (int i = markTimesDic.Count; i < markCount; i++)
			{
				markingOverlay.Children.RemoveAt(markTimesDic.Count);
			}

			for (int i = markTimesDic.Count; i < segmentCount; i++)
			{
				barOverlay.Children.RemoveAt(markTimesDic.Count);
			}
		}
		private void ToggleSegmentButton(Button button, int index)
		{
			List<double> keys = new List<double>(markTimesDic.Keys);
			double markTime = keys[index];
			markTimesDic[markTime] = (markTimesDic[markTime].Item1, markTimesDic[markTime].Item2, !markTimesDic[markTime].Item3);
			// 버튼이 회색이라면 흰색으로 전환
			// 버튼 색상 업데이트
			if (markTimesDic[markTime].Item3)
			{
				// 활성화 - 원래 색상으로 변경
				button.BackgroundColor = segmentColors[index % segmentColors.Count];
			}
			else
			{
				// 비활성화 - 회색으로 변경
				button.BackgroundColor = Colors.Grey;
			}
		}

		

		public void Cut3(object sender, EventArgs e)
		{
			startX = (int)(videoWidth / 3f);
			endX = (int)(videoWidth / 3f * 2);
			startY = 0;
			endY = videoHeight;
			SetFixSize(true);
			UpdateCropLines();
		}
		public void Cut2(object sender, EventArgs e)
		{
			startX = (int)(videoWidth * 0.25f);
			endX = (int)(videoWidth * 0.75f);
			startY = 0;
			endY = videoHeight;
			SetFixSize(true);
			UpdateCropLines();
		}

		public void CutBg(object sender, EventArgs e)
		{
			
		}
		public void CutBestSize(object sender, EventArgs e)
		{
			float displayRatio = (float)displayHeight/(float)displayWidth;
			float videoRatio = (float)videoHeight / (float)videoWidth;
			if(videoRatio <= displayRatio) // 가로 긴 영상
			{
				var newWidth = (float)videoHeight / (float)displayRatio;
				var offset = (videoWidth - newWidth) * 0.5f;
				startX = (int)offset;
				endX = (int)(videoWidth - offset);
				startY = 0;
				endY = videoHeight;
			}
			else
			{
				var newHeight = videoWidth * displayRatio;
				var offset = (videoHeight - newHeight) * 0.5f;
				startY = (int)offset;
				endY = (int)(videoHeight - offset);
				startX = 0;
				endX = videoWidth;
			}
			UpdateCropLines();
			SetFixSize(false);
		}
		// 드래그 이벤트 핸들러 추가
		private void AddDragEventHandlers()
		{
			// 각 테두리에 드래그 이벤트 핸들러를 추가합니다.
			var panGestureRecognizer = new PanGestureRecognizer();
			panGestureRecognizer.PanUpdated += (sender, e) =>
			{
				switch (e.StatusType)
				{
					case GestureStatus.Running:
						// 각 선의 위치를 이동
						if (sender == topLineCover)
						{
							if(isFixSize)
							{
								if(e.TotalY > 0) // 내려감
								{
									startY -= (int)e.TotalY;
									startY = startY < 0 ? 0 : startY;
									endY = startY + fixVideoHeight;
								}
								else
								{
									endY -= (int)e.TotalY;
									endY = endY > videoHeight ? videoHeight : endY;
									startY = endY - fixVideoHeight;
								}
							}
							else
							{
								endY -= (int)e.TotalY;
								endY = endY > videoHeight ? videoHeight : endY;
							}
						}
						else if (sender == bottomLineCover)
						{
							if (isFixSize)
							{
								if (e.TotalY < 0) // 내려감
								{
									endY -= (int)e.TotalY;
									endY = endY > videoHeight ? videoHeight : endY;
									startY = endY - fixVideoHeight;
								}
								else
								{
									startY -= (int)e.TotalY;
									startY = startY < 0 ? 0 : startY;
									endY = startY + fixVideoHeight;
								}
							}
							else
							{
								startY -= (int)e.TotalY;
								startY = startY < 0 ? 0 : startY;
							}
						}
						else if (sender == leftLineCover)
						{
							if (isFixSize)
							{
								if(e.TotalX > 0)
								{
									endX += (int)e.TotalX;
									endX = endX > videoWidth ? videoWidth : endX;
									startX = endX - fixVideoWidth;
								}
								else
								{
									startX += (int)e.TotalX;
									startX = startX < 0 ? 0 : startX;
									endX = startX + fixVideoWidth;
								}
							}
							else
							{
								startX += (int)e.TotalX;
								startX = startX < 0 ? 0 : startX;
							}
						}
						else if (sender == rightLineCover)
						{
							if (isFixSize)
							{
								if (e.TotalX < 0)
								{
									startX += (int)e.TotalX;
									startX = startX < 0 ? 0 : startX;
									endX = startX + fixVideoWidth;
								}
								else
								{
									endX += (int)e.TotalX;
									endX = endX > videoWidth ? videoWidth : endX;
									startX = endX - fixVideoWidth;
								}
							}
							else
							{
								endX += (int)e.TotalX;
								endX = endX > videoWidth ? videoWidth : endX;
							}
						}

						UpdateCropLines();
						break;
				}
			};

			topLineCover.GestureRecognizers.Add(panGestureRecognizer);
			bottomLineCover.GestureRecognizers.Add(panGestureRecognizer);
			leftLineCover.GestureRecognizers.Add(panGestureRecognizer);
			rightLineCover.GestureRecognizers.Add(panGestureRecognizer);
		}
		private async void UpdateFileName(string fileName)
		{
			// 기존 파일 경로에서 디렉토리 가져오기
			string directory = Path.GetDirectoryName(currentFilePath); // 비디오 파일의 실제 경로를 사용할 수 있어야 함
			

			var markTimesList = markTimesDic.OrderBy(x => x.Key).ToList();
			StringBuilder sb = new();
			sb.Append($"[({(int)(endX - startX)}_{(int)(endY - startY)})_");
			
			for (int i = 0; i < markTimesList.Count; i++)
			{
				if (!markTimesList[i].Value.Item3)
				{
					continue;
				}
				if(i == 0 && markTimesList.Count == 1)
				{
					sb.Append($"_x{DecimalToBase36(startX)}y{DecimalToBase36(startY)}t0-{DecimalToBase36(markTimesList[i].Key * 10)}");
					break;
				}
				if (i == 0)
					sb.Append($"x{DecimalToBase36(markTimesList[i].Value.Item1)}y{DecimalToBase36(markTimesList[i].Value.Item2)}t0-{DecimalToBase36(markTimesList[i].Key * 10)}");
				else if (i == markTimesList.Count -1)
					sb.Append($"_x{DecimalToBase36(startX)}y{DecimalToBase36(startY)}t{DecimalToBase36(markTimesList[i - 1].Key * 10)}-{DecimalToBase36(markTimesList[i].Key * 10)}");
				else
					sb.Append($"_x{DecimalToBase36(markTimesList[i].Value.Item1)}y{DecimalToBase36(markTimesList[i].Value.Item2)}t{DecimalToBase36(markTimesList[i-1].Key * 10)}-{DecimalToBase36(markTimesList[i].Key * 10)}");

			}
			sb.Append("]");
			sb.Append(fileName);
			string newFileName = sb.ToString();// 새 파일 이름 생성

			// 새 파일 경로 생성
			string newFilePath = Path.Combine(directory, newFileName);

			// 파일 이름 변경 (기존 파일 이름을 새 파일 이름으로 변경)
			try {
				bool success = false;
				try
				{
					File.Move(currentFilePath, newFilePath);
					success = true;
				}
				catch 
				{
#if ANDROID
					// MainActivity의 SAF 메서드 호출
					var mainActivity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as MainActivity;
					if (mainActivity != null)
					{
						success = await mainActivity.RenameFileUsingSaf(currentFilePath, newFileName);
					}
#endif
				}

				if (success)
				{
					// 경로 업데이트
					currentFilePath = newFilePath;
					UpdateCropLines();
				}
				else
				{
					// 실패 시 클립보드에 복사
					await Clipboard.Default.SetTextAsync(newFileName);
					await DisplayAlert("알림", "파일 이름을 변경할 수 없습니다. 이름이 클립보드에 복사되었습니다.", "확인");
				}
			}
			catch(Exception error)
			{
				Debug.WriteLine(error);
				Clipboard.Default.SetTextAsync(newFileName);
			}

			// 미디어 소스 업데이트 (새 경로로)
			mediaElement.Stop();
			RequestMoveToBackground?.Invoke();
		}

		private async void Save(object sender, EventArgs e)
		{
			string fileName = Path.GetFileName(currentFilePath);
			UpdateFileName(fileName);
		}

		private async void SaveAs(object sender, EventArgs e)
		{
			string fileName = Path.GetFileName(currentFilePath);
			string result="";
#if IOS

			// 입력 필드를 포함한 다이얼로그 생성
			 result = await DisplayPromptAsync(
				"파일 이름 변경",
				"새 파일 이름을 입력하세요:",
				accept: "저장",
				cancel: "취소",
				initialValue: fileName,
				maxLength: 100);
			
			// 사용자가 확인을 누르면
			if (!string.IsNullOrEmpty(result))
			{
				// 새 이름으로 UpdateFileName 메서드 호출
				UpdateFileName(result);
			}
#elif ANDROID

			// 커스텀 팝업 생성 및 표시
			var popup = new SaveAsPopup(fileName);
			await Navigation.PushModalAsync(popup);

			// 사용자 입력 대기
			result = await popup.GetFileNameAsync();
			
#endif
			result += ".mp4";
			if (!string.IsNullOrEmpty(result))
			{
				// 기존 UpdateFileName 메서드에 맞게 호출 방식 조정
				// 직접 파일명을 사용하는 오버로드가 있다면:
				UpdateFileName(result);
				// 또는 기존 메서드를 유지한다면:
				// UpdateFileName(sender, e); // 내부에서 result 값을 사용하도록 수정 필요
			}
		}


		private static string DecimalToBase36(int decimalNumberDouble)
		{
			int decimalNumber = (int)(decimalNumberDouble);
			const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			if (decimalNumber == 0) return "0";

			string result = "";
			while (decimalNumber > 0)
			{
				int remainder = decimalNumber % 36;
				result = chars[remainder] + result;
				decimalNumber /= 36;
			}

			return result;
		}
		private static string DecimalToBase36(double decimalNumberDouble)
		{
			return DecimalToBase36((int)decimalNumberDouble);
		}
		public void SetVideoSource(string videoPath)
		{
			// MediaElement의 Source를 설정
			if (Application.Current.MainPage is MainPage mainPage)
			{
				mainPage.mediaElement.Source = videoPath;
				
			}
		}
		public void SetRealPath(string realPath)
		{
			currentFilePath = realPath;
		}
		public void ToggleFixSize(object sender, EventArgs e)
		{
			SetFixSize(!isFixSize);
		}
		private void SetFixSize(bool isFix)
		{
			isFixSize = isFix;
			if (isFixSize)
			{
				
				fixVideoWidth = endX - startX;
				fixVideoHeight = endY - startY;
				btnFixSize.TextColor = Colors.Red;
			}
			else
			{
				btnFixSize.TextColor = Colors.White;
			}
		}
	}

}
