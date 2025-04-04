using VideoCutMarker.Models;
using CommunityToolkit.Maui.Views;

namespace VideoCutMarker.Services
{
	/// <summary>
	/// 비디오 편집 기능을 제공하는 서비스 클래스
	/// </summary>
	public class VideoEditorService
	{
		// 비디오 정보
		private int videoWidth, videoHeight;
		private VideoEditMetadata currentMetadata;
		private SortedDictionary<double, CropSegmentInfo> markTimesDic = new SortedDictionary<double, CropSegmentInfo>();
		private string currentFilePath;
		private string currentMetaId;
		private bool isModified = false;
		private readonly List<Color> groupColors = new List<Color>
		{
			Colors.OrangeRed,   // 주황빨강
            Colors.LimeGreen,   // 라임그린
            Colors.RoyalBlue,   // 로얄블루
            Colors.HotPink,     // 핫핑크
            Colors.Orange,      // 주황
            Colors.Purple,      // 보라
            Colors.Turquoise,   // 터쿼이즈
            Colors.Gold,        // 금색
            Colors.DeepSkyBlue, // 하늘색
            Colors.Crimson      // 크림슨
        };

		/// <summary>
		/// 생성자
		/// </summary>
		public VideoEditorService()
		{
			// 메타데이터 초기화
			currentMetadata = new VideoEditMetadata();
			CreateDefaultGroup();
		}

		/// <summary>
		/// 비디오 크기 설정
		/// </summary>
		/// <param name="width">비디오 너비</param>
		/// <param name="height">비디오 높이</param>
		public void SetVideoSize(int width, int height)
		{
			videoWidth = width;
			videoHeight = height;

			// 기본 그룹 크기 업데이트
			if (currentMetadata.Groups.TryGetValue(1, out GroupInfo group))
			{
				group.Width = width;
				group.Height = height;
			}
		}

		/// <summary>
		/// 현재 파일 경로 설정
		/// </summary>
		/// <param name="filePath">비디오 파일 경로</param>
		public void SetFilePath(string filePath)
		{
			currentFilePath = filePath;
			currentMetadata.VideoFileName = Path.GetFileName(filePath);
		}

		/// <summary>
		/// 기본 그룹 생성
		/// </summary>
		public void CreateDefaultGroup()
		{
			// 그룹 1 생성 (기본 그룹)
			var defaultGroup = new GroupInfo("1", groupColors[0], videoWidth, videoHeight);
			currentMetadata.Groups[1] = defaultGroup;
			currentMetadata.ActiveGroupId = 1;
		}

		/// <summary>
		/// 새 그룹 생성
		/// </summary>
		/// <param name="width">그룹 너비</param>
		/// <param name="height">그룹 높이</param>
		/// <returns>생성된 그룹 ID</returns>
		public int CreateNewGroup(int width, int height)
		{
			// 새 그룹 ID 생성 (기존 최대 ID + 1)
			int newGroupId = currentMetadata.Groups.Count > 0 ? currentMetadata.Groups.Keys.Max() + 1 : 1;

			// 색상 선택 (순환)
			Color newColor = groupColors[newGroupId % groupColors.Count];

			// 새 그룹 생성
			var newGroup = new GroupInfo(newGroupId.ToString(), newColor, width, height);

			// 그룹 추가
			currentMetadata.Groups[newGroupId] = newGroup;

			// 변경 사항 표시
			isModified = true;

			return newGroupId;
		}

		/// <summary>
		/// 활성 그룹 설정
		/// </summary>
		/// <param name="groupId">활성화할 그룹 ID</param>
		public void SetActiveGroup(int groupId)
		{
			if (currentMetadata.Groups.ContainsKey(groupId))
			{
				currentMetadata.ActiveGroupId = groupId;
				isModified = true;
			}
		}

		/// <summary>
		/// 현재 활성 그룹 ID 가져오기
		/// </summary>
		/// <returns>활성 그룹 ID</returns>
		public int GetActiveGroupId()
		{
			return currentMetadata.ActiveGroupId;
		}

		/// <summary>
		/// 그룹 정보 가져오기
		/// </summary>
		/// <param name="groupId">그룹 ID</param>
		/// <returns>그룹 정보 객체</returns>
		public GroupInfo GetGroupInfo(int groupId)
		{
			if (currentMetadata.Groups.TryGetValue(groupId, out GroupInfo group))
			{
				return group;
			}
			return null;
		}

		/// <summary>
		/// 모든 그룹 정보 가져오기
		/// </summary>
		/// <returns>그룹 정보 사전</returns>
		public Dictionary<int, GroupInfo> GetAllGroups()
		{
			return currentMetadata.Groups;
		}

		/// <summary>
		/// 구간 정보 추가
		/// </summary>
		/// <param name="centerX">중심점 X 좌표</param>
		/// <param name="centerY">중심점 Y 좌표</param>
		/// <param name="width">너비</param>
		/// <param name="height">높이</param>
		/// <param name="endTime">종료 시간 (초)</param>
		/// <param name="groupId">그룹 ID (0: 미선택)</param>
		public void AddSegment(int centerX, int centerY, int width, int height, double endTime, int groupId)
		{
			// 시작 시간 계산 (이전 구간의 끝 시간)
			double startTime = 0;
			if (markTimesDic.Count > 0)
			{
				var lastKey = markTimesDic.Keys.Max();
				if (lastKey < endTime)
				{
					startTime = lastKey;
				}
			}

			// 새 세그먼트 생성
			var segment = new CropSegmentInfo
			{
				CenterX = centerX,
				CenterY = centerY,
				Width = width,
				Height = height,
				StartTime = startTime,
				EndTime = endTime,
				GroupId = groupId
			};

			// 이미 같은 시간에 마크가 있으면 업데이트, 없으면 추가
			if (markTimesDic.ContainsKey(endTime))
			{
				markTimesDic[endTime] = segment;

				// 메타데이터에서 해당 구간 찾아 업데이트
				var existingSegment = currentMetadata.Segments.FirstOrDefault(s => Math.Abs(s.EndTime - endTime) < 0.01);
				if (existingSegment != null)
				{
					existingSegment.CenterX = centerX;
					existingSegment.CenterY = centerY;
					existingSegment.Width = width;
					existingSegment.Height = height;
					existingSegment.GroupId = groupId;
				}
			}
			else
			{
				markTimesDic.Add(endTime, segment);
				currentMetadata.Segments.Add(segment);
			}

			// 시간 순서대로 세그먼트 정렬 및 시작/종료 시간 업데이트
			UpdateSegmentTimes();

			isModified = true;
		}

		/// <summary>
		/// 구간 그룹 변경
		/// </summary>
		/// <param name="endTime">종료 시간으로 구간 식별</param>
		/// <param name="newGroupId">새 그룹 ID</param>
		/// <returns>성공 여부</returns>
		public bool ChangeSegmentGroup(double endTime, int newGroupId)
		{
			if (markTimesDic.TryGetValue(endTime, out CropSegmentInfo segment))
			{
				segment.GroupId = newGroupId;

				// 메타데이터에서도 업데이트
				var metaSegment = currentMetadata.Segments.FirstOrDefault(s => Math.Abs(s.EndTime - endTime) < 0.01);
				if (metaSegment != null)
				{
					metaSegment.GroupId = newGroupId;
				}

				isModified = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// 구간 제거
		/// </summary>
		/// <param name="endTime">종료 시간으로 구간 식별</param>
		/// <returns>성공 여부</returns>
		public bool RemoveSegment(double endTime)
		{
			if (markTimesDic.ContainsKey(endTime))
			{
				// 메타데이터에서도 제거
				var segment = currentMetadata.Segments.FirstOrDefault(s => Math.Abs(s.EndTime - endTime) < 0.01);
				if (segment != null)
				{
					currentMetadata.Segments.Remove(segment);
				}

				markTimesDic.Remove(endTime);

				// 시간 업데이트
				UpdateSegmentTimes();

				isModified = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// 세그먼트 시간 업데이트
		/// </summary>
		private void UpdateSegmentTimes()
		{
			// 모든 세그먼트를 시간순으로 정렬
			var sortedSegments = currentMetadata.Segments.OrderBy(s => s.EndTime).ToList();

			// 세그먼트의 시작/종료 시간 업데이트
			for (int i = 0; i < sortedSegments.Count; i++)
			{
				double startTime = i > 0 ? sortedSegments[i - 1].EndTime : 0;
				sortedSegments[i].StartTime = startTime;
			}
		}

		/// <summary>
		/// 모든 구간 정보 가져오기
		/// </summary>
		/// <returns>시간별 구간 정보 사전</returns>
		public SortedDictionary<double, CropSegmentInfo> GetAllSegments()
		{
			return markTimesDic;
		}

		/// <summary>
		/// 메타데이터 로드
		/// </summary>
		/// <param name="metadata">로드할 메타데이터</param>
		public void LoadMetadata(VideoEditMetadata metadata)
		{
			currentMetadata = metadata;

			// markTimesDic 업데이트
			markTimesDic.Clear();
			foreach (var segment in metadata.Segments)
			{
				markTimesDic.Add(segment.EndTime, segment);
			}

			isModified = false;
		}

		/// <summary>
		/// 현재 메타데이터 가져오기
		/// </summary>
		/// <returns>현재 메타데이터 객체</returns>
		public VideoEditMetadata GetCurrentMetadata()
		{
			return currentMetadata;
		}

		/// <summary>
		/// 기본 크롭 설정 초기화
		/// </summary>
		/// <param name="totalDuration">비디오 총 재생 시간 (초)</param>
		public void InitializeDefaultCropSettings(double totalDuration)
		{
			// 새 메타데이터 초기화
			currentMetadata = new VideoEditMetadata
			{
				VideoFileName = Path.GetFileName(currentFilePath),
				VideoRotation = Rotation.None
			};

			// 기본 그룹 생성
			CreateDefaultGroup();

			// 전체 구간을 하나의 세그먼트로 추가
			var segment = new CropSegmentInfo
			{
				CenterX = videoWidth / 2,
				CenterY = videoHeight / 2,
				Width = videoWidth,
				Height = videoHeight,
				GroupId = 1, // 기본 그룹
				StartTime = 0,
				EndTime = totalDuration
			};

			currentMetadata.Segments.Add(segment);
			markTimesDic.Add(totalDuration, segment);

			isModified = true;
		}

		/// <summary>
		/// 크롭 영역이 경계를 벗어나지 않도록 조정
		/// </summary>
		/// <param name="centerX">중심점 X 좌표 (참조)</param>
		/// <param name="centerY">중심점 Y 좌표 (참조)</param>
		/// <param name="width">너비</param>
		/// <param name="height">높이</param>
		public void AdjustCropBoundaries(ref int centerX, ref int centerY, int width, int height)
		{
			int startX = centerX - width / 2;
			int startY = centerY - height / 2;
			int endX = centerX + width / 2;
			int endY = centerY + height / 2;

			// 화면 밖으로 나가지 않도록 조정
			if (startX < 0)
			{
				centerX -= startX; // 중심점을 오른쪽으로 이동
			}

			if (startY < 0)
			{
				centerY -= startY; // 중심점을 아래쪽으로 이동
			}

			if (endX > videoWidth)
			{
				centerX -= (endX - videoWidth); // 중심점을 왼쪽으로 이동
			}

			if (endY > videoHeight)
			{
				centerY -= (endY - videoHeight); // 중심점을 위쪽으로 이동
			}
		}

		/// <summary>
		/// 회전 설정
		/// </summary>
		/// <param name="rotation">회전 값</param>
		public void SetRotation(Rotation rotation)
		{
			currentMetadata.VideoRotation = rotation;
			isModified = true;
		}

		/// <summary>
		/// 현재 회전 설정 가져오기
		/// </summary>
		/// <returns>회전 값</returns>
		public Rotation GetRotation()
		{
			return currentMetadata.VideoRotation;
		}

		/// <summary>
		/// 변경 상태 가져오기
		/// </summary>
		/// <returns>변경되었는지 여부</returns>
		public bool IsModified()
		{
			return isModified;
		}

		/// <summary>
		/// 변경 상태 설정
		/// </summary>
		/// <param name="modified">변경 상태</param>
		public void SetModified(bool modified)
		{
			isModified = modified;
		}

		/// <summary>
		/// 메타데이터 ID 설정
		/// </summary>
		/// <param name="metaId">메타데이터 ID</param>
		public void SetMetaId(string metaId)
		{
			currentMetaId = metaId;
		}

		/// <summary>
		/// 메타데이터 ID 가져오기
		/// </summary>
		/// <returns>메타데이터 ID</returns>
		public string GetMetaId()
		{
			return currentMetaId;
		}

		/// <summary>
		/// 활성 그룹의 크기 업데이트
		/// </summary>
		/// <param name="width">너비</param>
		/// <param name="height">높이</param>
		public void UpdateActiveGroupSize(int width, int height)
		{
			int activeGroupId = currentMetadata.ActiveGroupId;
			if (currentMetadata.Groups.TryGetValue(activeGroupId, out GroupInfo group))
			{
				group.Width = width;
				group.Height = height;
				isModified = true;
			}
		}

		/// <summary>
		/// 활성 그룹의 첫 번째 세그먼트 가져오기
		/// </summary>
		/// <returns>세그먼트 정보</returns>
		public CropSegmentInfo GetActiveGroupFirstSegment()
		{
			int activeGroupId = currentMetadata.ActiveGroupId;
			return currentMetadata.Segments
				.Where(s => s.GroupId == activeGroupId)
				.OrderBy(s => s.StartTime)
				.FirstOrDefault();
		}

		/// <summary>
		/// 특정 시간 이후의 세그먼트 가져오기
		/// </summary>
		/// <param name="time">기준 시간</param>
		/// <returns>세그먼트 정보</returns>
		public CropSegmentInfo GetSegmentAfterTime(double time)
		{
			var sortedSegments = markTimesDic.OrderBy(x => x.Key).ToList();

			// 현재 클릭한 마커가 몇 번째 마커인지 찾기
			int currentMarkerIndex = -1;
			if (time < 0.01f)
			{
				currentMarkerIndex = -1; // 0초 지점
			}
			else
			{
				for (int i = 0; i < sortedSegments.Count; i++)
				{
					if (Math.Abs(sortedSegments[i].Key - time) < 0.01) // 부동소수점 비교용 오차 허용
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
				int segmentIndex = (currentMarkerIndex == sortedSegments.Count - 1) ?
								   currentMarkerIndex :
								   currentMarkerIndex + 1;

				if (segmentIndex >= 0 && segmentIndex < sortedSegments.Count)
				{
					return sortedSegments[segmentIndex].Value;
				}
			}

			return null;
		}
	}
}