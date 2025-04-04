using System.Text.Json;
using VideoCutMarker.Models;
using VideoCutMarker.Utilities;

namespace VideoCutMarker.Services
{
	/// <summary>
	/// 메타데이터 파일 관리를 위한 서비스 클래스
	/// </summary>
	public class MetadataManager
	{
		// 메타데이터 저장
		public static async Task<string> SaveMetadataAsync(string videoFilePath, VideoEditMetadata metadata)
		{
			try
			{
				// 메타 ID 생성 또는 재사용
				string metaId = Guid.NewGuid().ToString("N").Substring(0, 8);
				string metaFilePath = VideoEditMetadata.GenerateMetadataFileName(videoFilePath, metaId);

				// JSON으로 직렬화
				var options = new JsonSerializerOptions { WriteIndented = true };
				string jsonData = JsonSerializer.Serialize(metadata, options);

				// 메타데이터 파일 저장
				await File.WriteAllTextAsync(metaFilePath, jsonData);

				return metaId;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"메타데이터 저장 오류: {ex.Message}");
				throw;
			}
		}

		// 메타데이터 로드
		public static async Task<VideoEditMetadata> LoadMetadataAsync(string metaFilePath)
		{
			try
			{
				if (!File.Exists(metaFilePath))
					return null;

				string jsonData = await File.ReadAllTextAsync(metaFilePath);
				return JsonSerializer.Deserialize<VideoEditMetadata>(jsonData);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"메타데이터 로드 오류: {ex.Message}");
				return null;
			}
		}

		// 비디오 파일명 업데이트 (메타 ID 포함)
		public static async Task<string> UpdateVideoFileNameAsync(string originalPath, string metaId)
		{
			string directory = Path.GetDirectoryName(originalPath);
			string extension = Path.GetExtension(originalPath);
			string fileName = Path.GetFileNameWithoutExtension(originalPath);

			// 기존 메타 ID 제거
			if (fileName.StartsWith("[m") && fileName.Contains("]"))
			{
				int endIndex = fileName.IndexOf("]") + 1;
				fileName = fileName.Substring(endIndex);
			}

			// 새 파일명 생성
			string newFileName = $"[m{metaId}]{fileName}{extension}";
			string newFilePath = Path.Combine(directory, newFileName);

			try
			{
				// 파일 이름 변경
				File.Move(originalPath, newFilePath);
				return newFilePath;
			}
			catch (Exception ex)
			{
				// Android에서 SAF 사용 필요 시
#if ANDROID
                var mainActivity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as MainActivity;
                if (mainActivity != null)
                {
                    bool success = await mainActivity.RenameFileUsingSaf(originalPath, newFileName);
                    if (success)
                        return newFilePath;
                }
#endif
				throw new Exception($"파일 이름 변경 실패: {ex.Message}");
			}
		}

		// 메타데이터 ID로 메타데이터 파일 찾기
		public static string FindMetadataFile(string videoFilePath, string metaId)
		{
			string directory = Path.GetDirectoryName(videoFilePath);
			string fileName = Path.GetFileNameWithoutExtension(videoFilePath);

			// 메타 ID가 파일명에 포함된 경우 추출
			if (fileName.StartsWith("[m") && fileName.Contains("]"))
			{
				int endIndex = fileName.IndexOf("]");
				metaId = fileName.Substring(2, endIndex - 2);
				fileName = fileName.Substring(endIndex + 1);
			}

			string metaFileName = $"{fileName}_meta_{metaId}.vcm";
			string metaFilePath = Path.Combine(directory, metaFileName);

			return File.Exists(metaFilePath) ? metaFilePath : null;
		}

		// 기존 파일명 형식에서 메타데이터 파싱 (하위 호환성 유지)
		public static async Task<VideoEditMetadata> ParseLegacyFileNameFormat(string filePath, int videoWidth, int videoHeight)
		{
			string fileName = Path.GetFileName(filePath);

			// 기본 형식 검사: [로 시작하고 ]를 포함해야 함
			if (!fileName.StartsWith("[") || !fileName.Contains("]"))
				return null;

			try
			{
				VideoEditMetadata metadata = new VideoEditMetadata
				{
					VideoFileName = Path.GetFileName(filePath)
				};

				// 마커 정보 부분 추출
				int endBracketIndex = fileName.IndexOf(']');
				if (endBracketIndex <= 1) // 최소한 []안에 내용이 있어야 함
					return null;

				string markerInfo = fileName.Substring(1, endBracketIndex - 1);

				// 회전 처리
				if (markerInfo.Contains("(CW"))
				{
					var rotationStartIdx = markerInfo.IndexOf("(");
					var RotationEndIdx = markerInfo.IndexOf(")");
					string rotateStr = markerInfo.Substring(rotationStartIdx + 1, RotationEndIdx - rotationStartIdx - 1);

					metadata.VideoRotation = (Rotation)Enum.Parse(typeof(Rotation), rotateStr, true); // true: 대소문자 무시
					markerInfo = markerInfo.Replace($"({rotateStr})", "");
				}

				// 해상도 정보 형식 검사: (w_h) 패턴 포함 여부
				if (!markerInfo.Contains("(") || !markerInfo.Contains(")"))
					return null;

				int resolutionStartIndex = markerInfo.IndexOf('(');
				int resolutionEndIndex = markerInfo.IndexOf(')');

				if (resolutionStartIndex >= resolutionEndIndex || resolutionEndIndex - resolutionStartIndex <= 1)
					return null;

				string resolutionPart = markerInfo.Substring(resolutionStartIndex + 1, resolutionEndIndex - resolutionStartIndex - 1);
				if (!resolutionPart.Contains("_"))
					return null;

				string[] dimensions = resolutionPart.Split('_');
				int fixVideoWidth = 0, fixVideoHeight = 0;

				if (dimensions.Length == 2)
				{
					// 화면 해상도 및 크롭 설정 처리
					if (int.TryParse(dimensions[0], out fixVideoWidth) &&
						int.TryParse(dimensions[1], out fixVideoHeight))
					{
						// 기본 그룹 생성
						var defaultGroup = new GroupInfo("1", Colors.Orange, fixVideoWidth, fixVideoHeight);
						metadata.Groups[1] = defaultGroup;
						metadata.ActiveGroupId = 1;
					}
				}

				// 구간 정보 추출
				string segments = markerInfo.Substring(resolutionEndIndex + 3); // '_' 다음부터
				if (!segments.Contains("x") || !segments.Contains("y") || !segments.Contains("t"))
					return null;

				string[] segmentArray = segments.Split('_');

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

							int startX = EncodingUtils.Base36ToDecimal(xStr);
							int startY = EncodingUtils.Base36ToDecimal(yStr);
							double startTime = EncodingUtils.Base36ToDecimal(startTimeStr) / 10.0;
							double endTime = EncodingUtils.Base36ToDecimal(endTimeStr) / 10.0; // 10으로 나누어 초 단위로 변환

							// 새 세그먼트 생성 (중심점 기반)
							var cropSegment = new CropSegmentInfo
							{
								CenterX = startX + fixVideoWidth / 2,
								CenterY = startY + fixVideoHeight / 2,
								Width = fixVideoWidth,
								Height = fixVideoHeight,
								GroupId = 1, // 기본 그룹
								StartTime = startTime,
								EndTime = endTime
							};

							// 메타데이터에 추가
							metadata.Segments.Add(cropSegment);

							lastTime = endTime;
							hasValidSegment = true;
						}
					}
				}

				if (!hasValidSegment)
					return null;

				return metadata;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"파일 이름 파싱 오류: {ex.Message}");
				return null;
			}
		}
	}
}