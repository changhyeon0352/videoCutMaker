using CommunityToolkit.Maui.Views;
using System.Diagnostics;
using VideoCutMarker.Models;
using VideoCutMarker.Services;

namespace VideoCutMarker
{
	public partial class MainPage : ContentPage
	{
		// 서비스 객체
		private VideoEditorService editorService;

		// UI 관련 필드만 유지
		private int videoWidth, videoHeight;
		private int displayWidth, displayHeight;
		private int startX, startY, endX, endY;
		private float scale, actualVideoWidth, actualVideoHeight, horizontalOffset, verticalOffset;
		private string currentFilePath;
		private bool isFixSize = false;
		private int fixVideoWidth, fixVideoHeight; 

		// UI 관련 저장 변수
		private Dictionary<Button, EventHandler> buttonClickHandlers = new Dictionary<Button, EventHandler>();
		private List<Button> groupButtons = new List<Button>();
		public Action RequestMoveToBackground;

		public MainPage()
		{
			InitializeComponent();

			// 서비스 객체 초기화
			editorService = new VideoEditorService();

			myPicker.SelectedIndex = 0;

			// 동영상 로드 이벤트 처리
			mediaElement.MediaOpened += OnMediaElementOpened;

			// 드래깅으로 테두리 이동
			AddDragEventHandlers();
		}

		private async void OnMediaElementOpened(object sender, EventArgs e)
		{
			await Task.Delay(1000);
			if ((int)mediaElement.MediaWidth < 1)
			{
				await Task.Delay(1000);
			}

			mediaElement.Play();

			// 비디오 크기와 스케일 계산
			videoWidth = (int)mediaElement.MediaWidth;
			videoHeight = (int)mediaElement.MediaHeight;
			displayWidth = (int)mediaElement.Width;
			displayHeight = (int)mediaElement.Height;
			scale = Math.Min((float)displayWidth / (float)videoWidth, (float)displayHeight / (float)videoHeight);
			actualVideoHeight = videoHeight * scale;
			actualVideoWidth = videoWidth * scale;
			horizontalOffset = (displayWidth - actualVideoWidth) / 2;
			verticalOffset = (displayHeight - actualVideoHeight) / 2;

			// 서비스에 비디오 크기 정보 전달
			editorService.SetVideoSize(videoWidth, videoHeight);
			editorService.SetFilePath(currentFilePath);

			// 메타데이터 로드
			if (!string.IsNullOrEmpty(currentFilePath))
			{
				if (!await LoadMetadataFromFile(currentFilePath))
				{
					InitializeDefaultCropSettings();
				}
			}

			// 그룹 버튼 초기화
			InitializeGroupButtons();
		}

		public void PauseVideo()
		{
			mediaElement.Pause();
		}

		#region 메타데이터 로드/저장

		private async Task<bool> LoadMetadataFromFile(string filePath)
		{
			try
			{
				// 메타데이터 로드 서비스 호출
				var metadata = await MetadataManager.LoadMetadataAsync(filePath);

				if (metadata != null)
				{
					// 메타데이터 적용
					editorService.LoadMetadata(metadata);

					// UI 업데이트
					ApplyMetadataToUI();
					return true;
				}

				// 레거시 파일명 파싱 시도
				metadata = await MetadataManager.ParseLegacyFileNameFormat(filePath, videoWidth, videoHeight);
				if (metadata != null)
				{
					editorService.LoadMetadata(metadata);
					ApplyMetadataToUI();
					await SaveMetadataAsync();
					return true;
				}

				return false;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"메타데이터 로드 오류: {ex.Message}");
				return false;
			}
		}

		private void ApplyMetadataToUI()
		{
			// 회전 설정 적용
			var metadata = editorService.GetCurrentMetadata();
			myPicker.SelectedIndex = (int)metadata.VideoRotation;

			// 세그먼트 정보 적용
			var segments = editorService.GetAllSegments();

			if (segments.Count > 0)
			{
				// 현재 활성 그룹의 첫 세그먼트로 크롭 영역 설정
				var activeGroupId = metadata.ActiveGroupId;
				var segment = editorService.GetActiveGroupFirstSegment();

				if (segment != null)
				{
					// 중심점에서 시작/끝 좌표 계산
					startX = segment.CenterX - segment.Width / 2;
					startY = segment.CenterY - segment.Height / 2;
					endX = segment.CenterX + segment.Width / 2;
					endY = segment.CenterY + segment.Height / 2;

					// 고정 크기 활성화
					if (activeGroupId > 0)
					{
						fixVideoWidth = segment.Width;
						fixVideoHeight = segment.Height;
						SetFixSize(true);
					}
				}
			}

			// UI 업데이트
			UpdateCropLines();
			UpdateSegment();
		}

		private void InitializeDefaultCropSettings()
		{
			// 초기 크롭 영역은 전체 비디오
			startX = 0;
			endX = videoWidth;
			startY = 0;
			endY = videoHeight;

			UpdateCropLines();

			// UI 초기화
			markingOverlay.Children.Clear();
			barOverlay.Children.Clear();

			// 서비스에 기본 설정 요청
			editorService.InitializeDefaultCropSettings(mediaElement.Duration.TotalSeconds);

			// 세그먼트 UI 업데이트
			UpdateSegment();
		}

		private async Task SaveMetadataAsync()
		{
			try
			{
				// 현재 메타데이터 저장
				var metaId = await MetadataManager.SaveMetadataAsync(currentFilePath, editorService.GetCurrentMetadata());

				if (!string.IsNullOrEmpty(metaId))
				{
					// 파일명 업데이트
					currentFilePath = await MetadataManager.UpdateVideoFileNameAsync(currentFilePath, metaId);
					editorService.SetModified(false);

					// UI 업데이트
					UpdateCropLines();
				}
			}
			catch (Exception ex)
			{
				await DisplayAlert("오류", $"메타데이터 저장 실패: {ex.Message}", "확인");
			}
		}

		#endregion

		#region UI 업데이트

		private void UpdateCropLines()
		{
			// 테두리 위치 갱신
			topLine.TranslationY = (videoHeight - endY) * scale + verticalOffset;
			topLineCover.TranslationY = topLine.TranslationY - 10;
			bottomLine.TranslationY = -startY * scale - verticalOffset;
			bottomLineCover.TranslationY = bottomLine.TranslationY + 10;
			leftLine.TranslationX = startX * scale + horizontalOffset;
			leftLineCover.TranslationX = leftLine.TranslationX - 10;
			rightLine.TranslationX = (endX - videoWidth) * scale - horizontalOffset;
			rightLineCover.TranslationX = rightLine.TranslationX + 10;

			// 중심점 계산
			int centerX = startX + (endX - startX) / 2;
			int centerY = startY + (endY - startY) / 2;
			int width = endX - startX;
			int height = endY - startY;

			// 파일 이름 업데이트
			string fileName = Path.GetFileName(currentFilePath);
			resolutionLabel.Text = $"중심:({centerX},{centerY}) 크기:{width}x{height} {fileName}";

			// 현재 활성 그룹의 크기 정보 업데이트
			editorService.UpdateActiveGroupSize(width, height);
		}

		private void UpdateSegment()
		{
			int markerButtonWidth = 10;
			int markerButtonHeight = 15;

			// 현재 오버레이 상태 저장
			int markCount = markingOverlay.Children.Count;
			int segmentCount = barOverlay.Children.Count;

			// 세그먼트 정보 가져오기
			var segments = editorService.GetAllSegments();
			var keys = new List<double>(segments.Keys);

			// 마커 위치 계산 및 오프셋 처리
			var markerPositions = CalculateMarkerPositions(keys);
			var markerOffsets = CalculateMarkerVerticalOffsets(markerPositions);

			// 각 세그먼트별 UI 업데이트
			for (int i = 0; i < segments.Count; i++)
			{
				double prevTime = i > 0 ? keys[i - 1] : 0;
				double time = keys[i];
				var segment = segments[keys[i]];

				// 그룹에 따른 색상 및 투명도 결정
				var (color, opacity) = GetSegmentVisualProperties(segment.GroupId);

				// 위치 계산
				double position = time / mediaElement.Duration.TotalSeconds;
				double prevPosition = prevTime / mediaElement.Duration.TotalSeconds;

				// 마커 UI 업데이트
				UpdateMarkerUI(i, markCount, prevTime, markerPositions[i], markerOffsets[i], color, opacity);

				// 세그먼트 UI 업데이트
				UpdateSegmentUI(i, segmentCount, prevPosition, position, color, opacity);
			}

			// 불필요한 UI 요소 제거
			RemoveExcessUIElements(segments.Count, markCount, segmentCount);
		}

		private List<double> CalculateMarkerPositions(List<double> timeKeys)
		{
			List<double> positions = new List<double>();

			for (int i = 0; i < timeKeys.Count; i++)
			{
				double prevTime = i > 0 ? timeKeys[i - 1] : 0;
				double position = prevTime / mediaElement.Duration.TotalSeconds;
				positions.Add(progressBar.Width * position);
			}

			return positions;
		}

		private Dictionary<int, int> CalculateMarkerVerticalOffsets(List<double> positions)
		{
			Dictionary<int, int> offsets = new Dictionary<int, int>();
			Dictionary<int, (double start, double end)> bounds = new Dictionary<int, (double, double)>();
			int markerWidth = 10;

			for (int i = 0; i < positions.Count; i++)
			{
				offsets[i] = 0;
				double xPos = positions[i];

				// 각 레벨별 위치 확인
				for (int level = 0; level < positions.Count; level++)
				{
					bool levelIsFree = true;

					foreach (var entry in bounds)
					{
						if (offsets[entry.Key] == level && Math.Abs(xPos - entry.Value.start) < markerWidth * 1.5)
						{
							levelIsFree = false;
							break;
						}
					}

					if (levelIsFree)
					{
						offsets[i] = level;
						break;
					}
				}

				bounds[i] = (xPos - markerWidth / 2, xPos + markerWidth / 2);
			}

			return offsets;
		}

		private (Color, double) GetSegmentVisualProperties(int groupId)
		{
			int activeGroupId = editorService.GetActiveGroupId();

			if (groupId == 0) // 미선택 구간
			{
				return (Colors.Gray, 0.4);
			}
			else if (groupId == activeGroupId) // 활성 그룹
			{
				return (editorService.GetGroupInfo(groupId).Color, 1.0);
			}
			else // 비활성 그룹
			{
				return (editorService.GetGroupInfo(groupId).Color, 0.6);
			}
		}

		private void UpdateMarkerUI(int index, int existingCount, double prevTime, double xPosition,
								   int verticalOffset, Color color, double opacity)
		{
			int markerWidth = 10;
			int markerHeight = 15;

			Grid markerGrid;

			if (index >= existingCount)
			{
				// 새 마커 생성
				var boxView = new BoxView
				{
					WidthRequest = 2,
					Color = color,
					HeightRequest = markerHeight,
					VerticalOptions = LayoutOptions.Center,
					HorizontalOptions = LayoutOptions.Center,
					Opacity = opacity
				};

				markerGrid = new Grid
				{
					WidthRequest = markerWidth,
					HeightRequest = markerHeight,
					BackgroundColor = Colors.Transparent,
					HorizontalOptions = LayoutOptions.Start,
					VerticalOptions = LayoutOptions.End,
					ZIndex = 1000,
					InputTransparent = false
				};

				markerGrid.Children.Add(boxView);
				markingOverlay.Children.Add(markerGrid);
			}
			else
			{
				// 기존 마커 업데이트
				markerGrid = (Grid)markingOverlay.Children[index];
				var boxView = markerGrid.Children[0] as BoxView;
				if (boxView != null)
				{
					boxView.Color = color;
					boxView.Opacity = opacity;
				}
			}

			// 마커 위치 설정
			markerGrid.Margin = new Thickness(
				xPosition - markerWidth / 2,
				0,
				0,
				markerHeight * verticalOffset
			);

			// 마커 제스처 설정
			SetupMarkerGestures(markerGrid, prevTime);
		}

		private void SetupMarkerGestures(Grid markerGrid, double time)
		{
			markerGrid.GestureRecognizers.Clear();

			// 탭 제스처 - 해당 시간으로 이동
			var tapGesture = new TapGestureRecognizer
			{
				Command = new Command(() =>
				{
					mediaElement.SeekTo(TimeSpan.FromSeconds(time));
					mediaElement.Play();
					UpdateCropLinesForMarker(time);
				})
			};

			// 드래그 제스처 - 마커 삭제
			var moveGesture = new PanGestureRecognizer();
			moveGesture.PanUpdated += (sender, e) =>
			{
				if (e.StatusType == GestureStatus.Completed)
				{
					editorService.RemoveSegment(time);
					UpdateSegment();
				}
			};

			markerGrid.GestureRecognizers.Add(tapGesture);
			markerGrid.GestureRecognizers.Add(moveGesture);
		}

		private void UpdateSegmentUI(int index, int existingCount, double startPos, double endPos,
									Color color, double opacity)
		{
			Button segmentButton;

			if (index >= existingCount)
			{
				// 새 세그먼트 버튼 생성
				segmentButton = new Button
				{
					HeightRequest = 15,
					VerticalOptions = LayoutOptions.Center,
					HorizontalOptions = LayoutOptions.Start,
					Padding = new Thickness(0),
					BorderWidth = 0,
					CornerRadius = 0,
					Text = (index + 1).ToString(),
					FontSize = 10,
					FontAttributes = FontAttributes.Bold,
					TextColor = Colors.White,
					Opacity = opacity
				};

				barOverlay.Children.Add(segmentButton);
			}
			else
			{
				// 기존 세그먼트 버튼 업데이트
				segmentButton = (Button)barOverlay.Children[index];
				segmentButton.Opacity = opacity;
			}

			// 세그먼트 버튼 속성 설정
			segmentButton.BackgroundColor = color;
			segmentButton.WidthRequest = progressBar.Width * (endPos - startPos);
			segmentButton.Margin = new Thickness(progressBar.Width * startPos, 0, 0, 0);

			// 이벤트 핸들러 설정
			SetupSegmentClickHandler(segmentButton, index);
		}

		private void SetupSegmentClickHandler(Button button, int index)
		{
			// 이전 핸들러 제거
			if (buttonClickHandlers.ContainsKey(button))
			{
				button.Clicked -= buttonClickHandlers[button];
				buttonClickHandlers.Remove(button);
			}

			// 새 핸들러 추가
			EventHandler clickHandler = (s, e) => OnSegmentButtonClicked((Button)s, index);
			button.Clicked += clickHandler;
			buttonClickHandlers[button] = clickHandler;
		}

		private void RemoveExcessUIElements(int requiredCount, int markerCount, int segmentCount)
		{
			// 불필요한 마커 제거
			while (markerCount > requiredCount)
			{
				markingOverlay.Children.RemoveAt(requiredCount);
				markerCount--;
			}

			// 불필요한 세그먼트 버튼 제거
			while (segmentCount > requiredCount)
			{
				barOverlay.Children.RemoveAt(requiredCount);
				segmentCount--;
			}
		}

		private void UpdateCropLinesForMarker(double markerTime)
		{
			var segment = editorService.GetSegmentAfterTime(markerTime);

			if (segment != null)
			{
				// 중심점에서 시작/끝 좌표 계산
				startX = segment.CenterX - segment.Width / 2;
				startY = segment.CenterY - segment.Height / 2;
				endX = segment.CenterX + segment.Width / 2;
				endY = segment.CenterY + segment.Height / 2;

				// 그룹 활성화
				if (segment.GroupId > 0 && segment.GroupId != editorService.GetActiveGroupId())
				{
					SetActiveGroup(segment.GroupId);
				}

				// UI 업데이트
				UpdateCropLines();
			}
		}

		#endregion

		#region 그룹 관리

		private void InitializeGroupButtons()
		{
			// 기존 버튼 제거
			groupButtonsContainer.Children.Clear();
			groupButtons.Clear();

			// 그룹 버튼 생성
			foreach (var group in editorService.GetAllGroups().OrderBy(g => g.Key))
			{
				AddGroupButton(group.Key, group.Value);
			}

			// 그룹 추가 버튼
			Button addButton = new Button
			{
				Text = "+",
				WidthRequest = 40,
				HeightRequest = 40,
				Margin = new Thickness(5, 0),
				BackgroundColor = Colors.Gray,
				TextColor = Colors.White,
				FontSize = 18,
				CornerRadius = 20
			};

			addButton.Clicked += OnAddGroupButtonClicked;
			groupButtonsContainer.Children.Add(addButton);

			// 활성 그룹 표시 업데이트
			UpdateActiveGroupButton();
		}

		private void AddGroupButton(int groupId, GroupInfo groupInfo)
		{
			Button groupButton = new Button
			{
				Text = groupInfo.Name,
				WidthRequest = 40,
				HeightRequest = 40,
				Margin = new Thickness(5, 0),
				BackgroundColor = groupInfo.Color,
				TextColor = Colors.White,
				FontSize = 16,
				CornerRadius = 20
			};

			groupButton.Clicked += (sender, e) => SetActiveGroup(groupId);
			groupButtonsContainer.Children.Add(groupButton);
			groupButtons.Add(groupButton);
		}

		private void OnAddGroupButtonClicked(object sender, EventArgs e)
		{
			// 새 그룹 생성
			int width = endX - startX;
			int height = endY - startY;
			int newGroupId = editorService.CreateNewGroup(width, height);

			// 버튼 추가 및 활성화
			var groupInfo = editorService.GetGroupInfo(newGroupId);
			if (groupInfo != null)
			{
				AddGroupButton(newGroupId, groupInfo);
				SetActiveGroup(newGroupId);
			}
		}

		private void SetActiveGroup(int groupId)
		{
			int currentActiveGroupId = editorService.GetActiveGroupId();

			// 이미 활성화된 그룹이면 무시
			if (currentActiveGroupId == groupId)
				return;

			// 서비스에 활성 그룹 변경 요청
			editorService.SetActiveGroup(groupId);

			// 그룹의 크기로 크롭 영역 업데이트
			UpdateCropSizeFromGroup(groupId);

			// UI 업데이트
			UpdateActiveGroupButton();
			UpdateSegment();
		}

		private void UpdateActiveGroupButton()
		{
			int activeGroupId = editorService.GetActiveGroupId();
			var groups = editorService.GetAllGroups();

			for (int i = 0; i < groupButtons.Count; i++)
			{
				Button button = groupButtons[i];
				int buttonGroupId = groups.Keys.ElementAt(i);

				// 버튼 스타일 업데이트
				if (buttonGroupId == activeGroupId)
				{
					button.BorderColor = Colors.White;
					button.BorderWidth = 3;
					button.Opacity = 1.0;
				}
				else
				{
					button.BorderWidth = 0;
					button.Opacity = 0.7;
				}
			}
		}

		private void UpdateCropSizeFromGroup(int groupId)
		{
			var groupInfo = editorService.GetGroupInfo(groupId);
			if (groupInfo != null)
			{
				// 중심점은 유지하고 크기만 변경
				int centerX = startX + (endX - startX) / 2;
				int centerY = startY + (endY - startY) / 2;

				startX = centerX - groupInfo.Width / 2;
				startY = centerY - groupInfo.Height / 2;
				endX = centerX + groupInfo.Width / 2;
				endY = centerY + groupInfo.Height / 2;

				// 경계 확인 및 조정
				AdjustCropBoundaries();

				// UI 업데이트
				UpdateCropLines();
			}
		}

		private void AdjustCropBoundaries()
		{
			// 화면 밖으로 나가지 않도록 조정
			if (startX < 0)
			{
				endX -= startX;
				startX = 0;
			}

			if (startY < 0)
			{
				endY -= startY;
				startY = 0;
			}

			if (endX > videoWidth)
			{
				startX -= (endX - videoWidth);
				endX = videoWidth;
			}

			if (endY > videoHeight)
			{
				startY -= (endY - videoHeight);
				endY = videoHeight;
			}
		}

		#endregion

		#region 이벤트 핸들러

		// 드래그 이벤트 핸들러 추가
		private void AddDragEventHandlers()
		{
			var panGestureRecognizer = new PanGestureRecognizer();
			panGestureRecognizer.PanUpdated += (sender, e) =>
			{
				if (e.StatusType == GestureStatus.Running)
				{
					// 현재 중심점 및 크기 계산
					int centerX = startX + (endX - startX) / 2;
					int centerY = startY + (endY - startY) / 2;
					int width = endX - startX;
					int height = endY - startY;

					// 각 선에 따라 처리
					if (sender == topLineCover)
						height -= (int)e.TotalY;
					else if (sender == bottomLineCover)
						height += (int)e.TotalY;
					else if (sender == leftLineCover)
						width -= (int)e.TotalX;
					else if (sender == rightLineCover)
						width += (int)e.TotalX;

					// 최소 크기 보장
					width = Math.Max(width, 50);
					height = Math.Max(height, 50);

					// 새 크롭 위치 계산
					startX = centerX - width / 2;
					startY = centerY - height / 2;
					endX = centerX + width / 2;
					endY = centerY + height / 2;

					// 경계 조정 및 UI 업데이트
					AdjustCropBoundaries();
					UpdateCropLines();
				}
			};

			// 각 테두리에 제스처 추가
			topLineCover.GestureRecognizers.Add(panGestureRecognizer);
			bottomLineCover.GestureRecognizers.Add(panGestureRecognizer);
			leftLineCover.GestureRecognizers.Add(panGestureRecognizer);
			rightLineCover.GestureRecognizers.Add(panGestureRecognizer);
		}

		// 마크 버튼 클릭
		private void OnMarkClicked(object sender, EventArgs e)
		{
			// 현재 재생 시간 가져오기
			double currentTime = mediaElement.Position.TotalSeconds;

			// 중심점 계산
			int centerX = startX + (endX - startX) / 2;
			int centerY = startY + (endY - startY) / 2;
			int width = endX - startX;
			int height = endY - startY;

			// 서비스에 구간 추가 요청
			editorService.AddSegment(centerX, centerY, width, height, currentTime, editorService.GetActiveGroupId());

			// UI 업데이트
			UpdateSegment();
		}

		// 세그먼트 버튼 클릭
		private void OnSegmentButtonClicked(Button button, int index)
		{
			var segments = editorService.GetAllSegments();
			var time = segments.Keys.ElementAt(index);
			var segment = segments[time];

			// 그룹 선택 메뉴 표시
			ShowGroupSelectionMenu(segment, time);
		}

		// 그룹 선택 메뉴
		private async void ShowGroupSelectionMenu(CropSegmentInfo segment, double time)
		{
			// 옵션 목록 생성
			var options = new List<string> { "미선택 (그룹 없음)" };

			foreach (var group in editorService.GetAllGroups().OrderBy(g => g.Key))
			{
				options.Add($"그룹 {group.Value.Name}");
			}

			// 선택 다이얼로그 표시
			string result = await DisplayActionSheet("구간 그룹 선택", "취소", null, options.ToArray());

			if (!string.IsNullOrEmpty(result) && result != "취소")
			{
				int newGroupId = 0;

				if (result != "미선택 (그룹 없음)")
				{
					// 그룹 ID 추출
					string groupName = result.Replace("그룹 ", "");
					var group = editorService.GetAllGroups().FirstOrDefault(g => g.Value.Name == groupName);
					newGroupId = group.Key;
				}

				// 세그먼트 그룹 변경
				if (editorService.ChangeSegmentGroup(time, newGroupId))
				{
					// 새 그룹으로 활성화
					if (newGroupId > 0)
					{
						SetActiveGroup(newGroupId);
					}
					else
					{
						// UI만 업데이트
						UpdateSegment();
					}
				}
			}
		}

		// 저장 버튼 클릭
		private async void Save(object sender, EventArgs e)
		{
			await SaveMetadataAsync();
			RequestMoveToBackground?.Invoke();
		}

		// 다른 이름으로 저장 버튼 클릭
		private async void SaveAs(object sender, EventArgs e)
		{
			string fileName = Path.GetFileName(currentFilePath);
			string result = "";

#if IOS
            // iOS에서는 내장 다이얼로그 사용
            result = await DisplayPromptAsync(
                "파일 이름 변경",
                "새 파일 이름을 입력하세요:",
                accept: "저장",
                cancel: "취소",
                initialValue: fileName,
                maxLength: 100);
#elif ANDROID
            // Android에서는 커스텀 팝업 사용
            var onlyName = fileName.Replace(".mp4", "");
            var popup = new SaveAsPopup(onlyName);
            await Navigation.PushModalAsync(popup);
            result = await popup.GetFileNameAsync();
#endif

			if (!string.IsNullOrEmpty(result))
			{
				// 확장자 추가
				if (!result.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
					result += ".mp4";

				// 파일명 설정 후 저장
				editorService.GetCurrentMetadata().VideoFileName = result;
				await SaveMetadataAsync();
				RequestMoveToBackground?.Invoke();
			}
		}

		// 회전 선택 변경
		private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
		{
			var picker = (Picker)sender;
			int selectedIndex = picker.SelectedIndex;

			if (selectedIndex != -1)
			{
				// 회전 설정 업데이트
				editorService.SetRotation((Rotation)selectedIndex);
			}
		}

		// 고정 크기 설정
		public void ToggleFixSize(object sender, EventArgs e)
		{
			SetFixSize(!isFixSize);
		}

		private void SetFixSize(bool isFix)
		{
			isFixSize = isFix;

			if (isFixSize)
			{
				// 고정 크기 활성화
				fixVideoWidth = endX - startX;
				fixVideoHeight = endY - startY;
				btnFixSize.TextColor = Colors.Red;

				// 현재 활성 그룹의 크기 저장
				editorService.UpdateActiveGroupSize(fixVideoWidth, fixVideoHeight);
			}
			else
			{
				btnFixSize.TextColor = Colors.White;
			}
		}

		// 3등분 자르기
		public void Cut3(object sender, EventArgs e)
		{
			startX = (int)(videoWidth / 3f);
			endX = (int)(videoWidth / 3f * 2);
			startY = 0;
			endY = videoHeight;
			SetFixSize(true);
			UpdateCropLines();
		}

		// 2등분 자르기
		public void Cut2(object sender, EventArgs e)
		{
			startX = (int)(videoWidth * 0.25f);
			endX = (int)(videoWidth * 0.75f);
			startY = 0;
			endY = videoHeight;
			SetFixSize(true);
			UpdateCropLines();
		}

		// 비율 맞추기
		public void CutBestSize(object sender, EventArgs e)
		{
			float displayRatio = (float)displayHeight / (float)displayWidth;
			float videoRatio = (float)videoHeight / (float)videoWidth;

			if (videoRatio <= displayRatio) // 가로 긴 영상
			{
				var newWidth = (float)videoHeight / (float)displayRatio;
				var offset = (videoWidth - newWidth) * 0.5f;
				startX = (int)offset;
				endX = (int)(videoWidth - offset);
				startY = 0;
				endY = videoHeight;
			}
			else // 세로 긴 영상
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

		// 자동 크롭 감지
		private async void AutoCrop(object sender, EventArgs e)
		{
			try
			{
				// 버튼 비활성화
				btnAutoCrop.IsEnabled = false;

				// 현재 위치 저장
				var currentPosition = mediaElement.Position;
				mediaElement.Pause();

				// 자동 크롭 감지 실행
				var detector = new AutoCropDetector();
				var cropRect = await detector.DetectCropAreaAsync(mediaElement, currentFilePath);

				// 감지된 영역으로 설정
				startX = (int)cropRect.X;
				startY = (int)cropRect.Y;
				endX = startX + (int)cropRect.Width;
				endY = startY + (int)cropRect.Height;

				// UI 업데이트
				UpdateCropLines();
				SetFixSize(true);

				// 원래 위치로 돌아가기
				mediaElement.SeekTo(currentPosition);
				mediaElement.Play();

				// 결과 알림
				await DisplayAlert("자동 크롭", $"검은색 영역이 감지되었습니다.\n크롭 영역: top{videoHeight - endY} bottom {startY} left {startX} right {videoWidth - endX}", "확인");
			}
			catch (Exception ex)
			{
				await DisplayAlert("오류", $"자동 크롭 감지 중 오류가 발생했습니다: {ex.Message}", "확인");
			}
			finally
			{
				btnAutoCrop.IsEnabled = true;
			}
		}

		#endregion

		#region 공용 메서드

		// 비디오 소스 설정
		public void SetVideoSource(string videoPath)
		{
			if (Application.Current.MainPage is MainPage mainPage)
			{
				mainPage.mediaElement.Source = videoPath;
			}
		}

		// 실제 경로 설정
		public void SetRealPath(string realPath)
		{
			currentFilePath = realPath;
		}

		#endregion
	}
}