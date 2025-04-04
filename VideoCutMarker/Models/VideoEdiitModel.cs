using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoCutMarker.Models
{
	// 크롭 구간 정보를 담는 클래스
	public class CropSegmentInfo
	{
		public int CenterX { get; set; }
		public int CenterY { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int GroupId { get; set; } = 0; // 0: 미선택, 1~n: 그룹 ID
		public double StartTime { get; set; }
		public double EndTime { get; set; }
	}

	// 그룹 정보 클래스
	public class GroupInfo
	{
		public string Name { get; set; }
		public Color Color { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }

		public GroupInfo(string name, Color color, int width, int height)
		{
			Name = name;
			Color = color;
			Width = width;
			Height = height;
		}

		// JSON 직렬화를 위한 매개변수 없는 생성자
		public GroupInfo() { }
	}

	// 비디오 편집 메타데이터 클래스
	public class VideoEditMetadata
	{
		public string VideoFileName { get; set; }
		public string MetadataVersion { get; set; } = "1.0";
		[JsonConverter(typeof(JsonStringEnumConverter))]
		public Rotation VideoRotation { get; set; } = Rotation.None;
		public List<CropSegmentInfo> Segments { get; set; } = new List<CropSegmentInfo>();
		public Dictionary<int, GroupInfo> Groups { get; set; } = new Dictionary<int, GroupInfo>();
		public int ActiveGroupId { get; set; } = 1; // 현재 활성화된 그룹 ID
		public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();

		// 메타데이터 파일명 생성
		public static string GenerateMetadataFileName(string videoPath, string metaId = null)
		{
			string directory = Path.GetDirectoryName(videoPath);
			string fileName = Path.GetFileNameWithoutExtension(videoPath);

			// 메타ID가 없으면 랜덤 생성
			if (string.IsNullOrEmpty(metaId))
			{
				metaId = Guid.NewGuid().ToString("N").Substring(0, 8);
			}

			return Path.Combine(directory, $"{fileName}_meta_{metaId}.vcm");
		}

		// 메타 ID를 파일명에서 추출
		public static string ExtractMetaIdFromFileName(string fileName)
		{
			// [m123]filename.mp4 형식에서 m123 추출
			if (fileName.StartsWith("[m") && fileName.Contains("]"))
			{
				int endIndex = fileName.IndexOf("]");
				return fileName.Substring(2, endIndex - 2);
			}
			return null;
		}
	}

	// 회전 열거형
	public enum Rotation
	{
		None = 0,    // 0° 회전
		CW90 = 1,    // 90° 시계 방향
		CW180 = 2,   // 180° 회전
		CW270 = 3    // 270° 시계 방향 (또는 90° 반시계 방향)
	}
}