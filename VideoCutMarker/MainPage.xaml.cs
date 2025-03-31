
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
		private Rotation rotation;
		// 버튼과 이벤트 핸들러를 저장할 딕셔너리 추가 (클래스 멤버 변수로)
		private Dictionary<Button, EventHandler> buttonClickHandlers = new Dictionary<Button, EventHandler>();

		public MainPage()
		{

			InitializeComponent();
			myPicker.SelectedIndex = 0;
			rotation = VideoCutMarker.Rotation.None;
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
				
				// 파일 이름 파싱 추가
				if (string.IsNullOrEmpty(currentFilePath) == false)
				{
					if (!ParseFileNameAndUpdateMarkers(currentFilePath))
					{
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
						markTimesDic.Add(totalTime, (0, 0, true));
						// 마킹된 위치에 표시 추가
						UpdateSegment();
					}
				}
				
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
			markTimesDic.TryAdd(currentTime, (startX,startY, true));

			// 마킹된 위치에 표시 추가
			UpdateSegment();
		}

		private void UpdateSegment()
		{
			int markerButtonWidth = 10;
			int markerButtonHeight = 15;
			// 현재 마크된 시간과 색상 목록을 사용하여 마크 표시를 업데이트합니다.
			int markCount = markingOverlay.Children.Count; // 기존 마크 표시 수
			int segmentCount = barOverlay.Children.Count; // 기존 세그먼트 수
			var keys = new List<double>(markTimesDic.Keys);
			var values = new List<(int, int, bool)>(markTimesDic.Values);

			// 마커 위치 사전 계산 (X축 위치)
			List<double> markerPositions = new List<double>();
			for (int i = 0; i < markTimesDic.Count; i++)
			{
				double preTime = 0;
				if (i != 0)
				{
					preTime = keys[i - 1];
				}
				double position = preTime / mediaElement.Duration.TotalSeconds;
				markerPositions.Add(progressBar.Width * position);
			}

			// 마커 고도(층) 계산 - 2D 공간 고려
			Dictionary<int, int> markerVerticalOffsets = new Dictionary<int, int>();
			Dictionary<int, (double xStart, double xEnd)> markerBounds = new Dictionary<int, (double, double)>();

			for (int i = 0; i < markerPositions.Count; i++)
			{
				markerVerticalOffsets[i] = 0; // 시작은 바닥 레벨(0)
				double xPos = markerPositions[i];

				// 각 레벨별로 마커 배치 가능 여부 확인
				bool foundLevel = false;
				for (int level = 0; level <= markTimesDic.Count; level++) // 최대 마커 수 만큼 층이 필요할 수 있음
				{
					bool levelIsFree = true;

					// 이 레벨에 있는 다른 마커들과 확인
					foreach (var entry in markerBounds)
					{
						int otherIndex = entry.Key;
						var bounds = entry.Value;

						// 같은 레벨에 있는 마커인지 확인
						if (markerVerticalOffsets[otherIndex] == level)
						{
							// X축에서 충돌하는지 확인
							if (Math.Abs(xPos - bounds.xStart) < markerButtonWidth * 1.5)
							{
								levelIsFree = false;
								break;
							}
						}
					}

					if (levelIsFree)
					{
						markerVerticalOffsets[i] = level;
						foundLevel = true;
						break;
					}
				}

				// 마커의 경계 저장 (클릭 영역 고려)
				markerBounds[i] = (xPos - markerButtonWidth/2, xPos + markerButtonWidth/2); // 30 픽셀 너비의 클릭 영역
			}

			// 마커 및 세그먼트 UI 업데이트
			for (int i = 0; i < markTimesDic.Count; i++)
			{
				int index = i;
				double preTime = 0;
				if (i != 0)
				{
					preTime = keys[i - 1];
				}
				double time = keys[i];

				Color color = values[index].Item3 ? segmentColors[i % segmentColors.Count] : Colors.Grey; // 각 마크에 해당하는 색상 가져오기

				// 마킹 위치 비율을 계산하여 표시 위치 조정
				double position = time / mediaElement.Duration.TotalSeconds;
				double prePosition = preTime / mediaElement.Duration.TotalSeconds;
				Grid markerGrid = null;
				if (i >= markCount)
				{
					var boxView = new BoxView
					{
						WidthRequest = 2,
						Color = color,
						HeightRequest = markerButtonHeight,
						VerticalOptions = LayoutOptions.Center,
						HorizontalOptions = LayoutOptions.Center
					};
					markerGrid = new Grid
					{
						WidthRequest = markerButtonWidth, // 클릭 영역을 더 넓게 설정
						HeightRequest = markerButtonHeight,
						BackgroundColor = Colors.Transparent, // 투명한 색상
						HorizontalOptions = LayoutOptions.Start,
						VerticalOptions = LayoutOptions.End,
						ZIndex = 1000, // 높은 ZIndex 설정으로 다른 요소보다 우선시되도록 함
						InputTransparent = false // 입력 이벤트를 투명하게 처리하지 않음
						
					};
					markerGrid.Children.Add(boxView);
					markingOverlay.Children.Add(markerGrid);
				}
				else
				{
					markerGrid = (Grid)markingOverlay.Children[i];
				}

				// 수직 위치 오프셋 적용 (최대 높이 제한)
				int verticalOffset = markerVerticalOffsets[i];
				//좌,상,우,하
				markerGrid.Margin = new Thickness(
					progressBar.Width * prePosition - markerButtonWidth/2, // X 위치
					0,
					0,
					markerButtonHeight * verticalOffset // Y 위치 (위로 올림)
				);

				//마커 제스쳐 재세팅
				markerGrid.GestureRecognizers.Clear();
				var tapGesture = new TapGestureRecognizer
				{
					Command = new Command(() =>
					{
						// 짧은 터치 처리
						mediaElement.SeekTo(TimeSpan.FromSeconds(preTime));
						mediaElement.Play();
						// 이 마커 이후 구간의 크롭 정보 찾기
						UpdateCropLinesForMarker(preTime);
					})
				};
				markerGrid.GestureRecognizers.Add(tapGesture);
				var moveGesture = new PanGestureRecognizer();
				moveGesture.PanUpdated += (sender, e) =>
				{
					switch (e.StatusType)
					{
						case GestureStatus.Completed:
							markTimesDic.Remove(preTime);
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
		// 마커 시간에 기반하여 크롭 라인 업데이트하는 새 메서드
		private void UpdateCropLinesForMarker(double markerTime)
		{
			var sortedMarks = markTimesDic.OrderBy(x => x.Key).ToList();

			// 현재 클릭한 마커가 몇 번째 마커인지 찾기
			int currentMarkerIndex = -2;
			if (markerTime < 0.01f)
			{
				currentMarkerIndex = -1; // 0초
			}
			else
			{
				for (int i = 0; i < sortedMarks.Count; i++)
				{
					if (Math.Abs(sortedMarks[i].Key - markerTime) < 0.01) // 부동소수점 비교를 위한 오차 허용
					{
						currentMarkerIndex = i;
						break;
					}
				}
			}
			if (currentMarkerIndex >= -1)
			{
				// 이 마커 이후 구간의 크롭 정보를 가져와야 함
				// 마지막 마커라면 다음 구간이 없으므로 현재 마커의 크롭 정보 사용
				// 아니라면 다음 마커(구간의 끝)의 크롭 정보 사용
				int cropInfoIndex = (currentMarkerIndex == sortedMarks.Count - 1) ?
									currentMarkerIndex :
									currentMarkerIndex + 1;

				// 해당 마커의 크롭 정보 적용
				startX = sortedMarks[cropInfoIndex].Value.Item1;
				startY = sortedMarks[cropInfoIndex].Value.Item2;

				// 필요한 경우 endX, endY도 업데이트 (고정 크기 모드인 경우)
				if (isFixSize)
				{
					endX = startX + fixVideoWidth;
					endY = startY + fixVideoHeight;
				}

				// 크롭 라인 업데이트
				UpdateCropLines();
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
			sb.Append("[");
			if (rotation != VideoCutMarker.Rotation.None)
			{
				sb.Append($"({rotation.ToString()})");
			}

			sb.Append($"({(int)(endX - startX)}_{(int)(endY - startY)})_");
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
			var onlyName = fileName.Replace(".mp4","");
			var popup = new SaveAsPopup(onlyName);
			await Navigation.PushModalAsync(popup);

			// 사용자 입력 대기
			result = await popup.GetFileNameAsync();
			
#endif
			if (!string.IsNullOrEmpty(result))
			{
				result += ".mp4";
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

		// 편집된 파일에서 마커 정보를 추출하는 함수, 성공 여부를 반환
		private bool ParseFileNameAndUpdateMarkers(string filePath)
		{
			string fileName = Path.GetFileName(filePath);

			// 기본 형식 검사: [로 시작하고 ]를 포함해야 함
			if (!fileName.StartsWith("[") || !fileName.Contains("]"))
				return false;

			try
			{
				// 마커 정보 부분 추출
				int endBracketIndex = fileName.IndexOf(']');
				if (endBracketIndex <= 1) // 최소한 []안에 내용이 있어야 함
					return false;

				string markerInfo = fileName.Substring(1, endBracketIndex - 1);

				// 회전 처리
				if (markerInfo.Contains("(CW"))
				{
					var rotationStartIdx = markerInfo.IndexOf("(");
					var RotationEndIdx = markerInfo.IndexOf(")");
					string rotateStr = markerInfo.Substring(rotationStartIdx + 1, RotationEndIdx -1);

					rotation = (Rotation)Enum.Parse(typeof(Rotation), rotateStr, true); // true: 대소문자 무시;
					myPicker.SelectedIndex = (int)rotation;
					markerInfo = markerInfo.Replace($"({rotateStr})", "");
				}
					
				// 해상도 정보 형식 검사: (w_h) 패턴 포함 여부
				if (!markerInfo.Contains("(") || !markerInfo.Contains(")"))
					return false;

				int resolutionStartIndex = markerInfo.IndexOf('(');
				int resolutionEndIndex = markerInfo.IndexOf(')');

				if (resolutionStartIndex >= resolutionEndIndex || resolutionEndIndex - resolutionStartIndex <= 1)
					return false;

				string resolutionPart = markerInfo.Substring(resolutionStartIndex + 1, resolutionEndIndex - resolutionStartIndex - 1);
				if (!resolutionPart.Contains("_"))
					return false;

				string[] dimensions = resolutionPart.Split('_');
				if (dimensions.Length == 2)
				{
					// 화면 해상도 및 크롭 설정 처리 (필요한 경우)
					if (int.TryParse(dimensions[0], out int width) &&
						int.TryParse(dimensions[1], out int height))
					{
						fixVideoWidth = width;
						fixVideoHeight = height;
						isFixSize = true;
					}
				}

				// 구간 정보 추출
				string segments = markerInfo.Substring(resolutionEndIndex + 3); // '_' 다음부터
				if (!segments.Contains("x") || !segments.Contains("y") || !segments.Contains("t"))
					return false;

				string[] segmentArray = segments.Split('_');

				// markTimesDic 초기화
				markTimesDic.Clear();

				// 각 세그먼트 정보 파싱
				double lastTime = 0;
				bool hasValidSegment = false;

				foreach (string segment in segmentArray)
				{
					if (segment.StartsWith("x") &&
						segment.Contains("y") &&
						segment.Contains("t") &&
						segment.Contains("-"))
					{
						int yPos = segment.IndexOf('y');
						int tPos = segment.IndexOf('t');
						int dashPos = segment.IndexOf('-');

						if (yPos > 1 && tPos > yPos && dashPos > tPos)
						{
							string xStr = segment.Substring(1, yPos - 1);
							string yStr = segment.Substring(yPos + 1, tPos - yPos - 1);
							string startTimeStr = segment.Substring(tPos + 1, dashPos - tPos - 1);
							string endTimeStr = segment.Substring(dashPos + 1);

							int startX = Base36ToDecimal(xStr);
							int startY = Base36ToDecimal(yStr);
							double startTime = Base36ToDecimal(startTimeStr) / 10.0;
							double endTime = Base36ToDecimal(endTimeStr) / 10.0; // 10으로 나누어 초 단위로 변환

							// markTimesDic에 추가 (끝 시간을 키로, 시작 위치를 값으로)
							if (startTime > 0.1)
								markTimesDic.TryAdd(startTime, (startX, 0, false));
							markTimesDic.Add(endTime, (startX, startY, true));
							lastTime = endTime;
							hasValidSegment = true;
						}
					}
				}

				if (!hasValidSegment)
					return false;

				// 영상 끝까지의 구간도 추가
				markTimesDic.TryAdd(mediaElement.Duration.TotalSeconds, (0, 0, true));

				// 마커와 세그먼트 업데이트
				UpdateSegment();
				

				// 첫 번째 구간의 크롭 설정으로 초기화
				if (markTimesDic.Count > 0)
				{
					var firstSegment = markTimesDic.OrderBy(x => x.Key).FirstOrDefault();
					startX = firstSegment.Value.Item1;
					startY = firstSegment.Value.Item2;
					endX = startX + fixVideoWidth;
					endY = startY + fixVideoHeight;
					UpdateCropLines();
				}

				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"파일 이름 파싱 오류: {ex.Message}");
				return false;
			}
		}

		// Base36 문자열을 10진수로 변환하는 메서드
		private int Base36ToDecimal(string base36)
		{
			const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			base36 = base36.ToUpper();

			int result = 0;
			for (int i = 0; i < base36.Length; i++)
			{
				char c = base36[i];
				int digit = chars.IndexOf(c);
				if (digit < 0)
					throw new ArgumentException("Invalid Base36 character: " + c);

				result = result * 36 + digit;
			}

			return result;
		}

		// MainPage 클래스에 아래 메서드 추가
		private async void AutoCrop(object sender, EventArgs e)
		{
			try
			{
				// 로딩 표

				// 현재 재생 위치 저장
				var currentPosition = mediaElement.Position;

				// 미디어 일시 정지
				mediaElement.Pause();

				// 자동 크롭 감지 실행
				var detector = new AutoCropDetector();
				var cropRect = await detector.DetectCropAreaAsync(mediaElement,currentFilePath);

				// 감지된 영역으로 크롭 테두리 설정
				startX = (int)cropRect.X;
				startY = (int)cropRect.Y;
				endX = startX + (int)cropRect.Width;
				endY = startY + (int)cropRect.Height;

				// 테두리 업데이트
				UpdateCropLines();

				// 고정 크기 모드 활성화
				SetFixSize(true);

				// 원래 위치로 돌아가기
				mediaElement.SeekTo(currentPosition);
				mediaElement.Play();

				// 결과 알림
				await DisplayAlert("자동 크롭", $"검은색 영역이 감지되었습니다.\n크롭 영역: top{videoHeight-endY} bottom {startY} left {startX}  right {videoWidth - endX}", "확인");
			}
			catch (Exception ex)
			{
				await DisplayAlert("오류", $"자동 크롭 감지 중 오류가 발생했습니다: {ex.Message}", "확인");
			}
			finally
			{
				// 버튼 상태 복원
				btnAutoCrop.IsEnabled = true;
			}
		}
		
		// spin 피커(드롭다운)처리
		private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
		{
			var picker = (Picker)sender;
			int selectedIndex = picker.SelectedIndex;

			if (selectedIndex != -1)
			{
				rotation = (Rotation)picker.SelectedIndex;
			}
			else
			{
				rotation = VideoCutMarker.Rotation.None;
			}
		}
	}
	public enum Rotation
	{
		None = 0,    // 0° 회전
		CW90 = 1,    // 90° 시계 방향
		CW180 = 2,   // 180° 회전
		CW270 = 3    // 270° 시계 방향 (또는 90° 반시계 방향)
	}
}
