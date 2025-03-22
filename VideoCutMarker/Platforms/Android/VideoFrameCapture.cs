using Android.Graphics;
using Android.Media;
using Android.Util;
using System;
using System.Threading.Tasks;

namespace VideoCutMarker.Platforms.AndroidModule
{
	/// <summary>
	/// 빠른 비디오 프레임 캡처 및 경계 감지 클래스
	/// </summary>
	public class VideoFrameCapture
	{
		private const string TAG = "VideoFrameCapture";
		private const int BRIGHTNESS_THRESHOLD = 10;

		/// <summary>
		/// 검은색 테두리를 제외한 비디오 콘텐츠의 경계를 감지합니다.
		/// </summary>
		/// <param name="videoPath">비디오 파일 경로</param>
		/// <param name="timeInMs">프레임을 캡처할 시간(밀리초)</param>
		/// <returns>감지된 경계값 (left, top, right, bottom)</returns>
		public async Task<(int Left, int Bottom, int Right, int Top)> DetectBordersAsync(string videoPath, long timeInMs)
		{
			try
			{
				// 백그라운드 스레드에서 실행
				return await Task.Run(() => {
					MediaMetadataRetriever retriever = null;
					Bitmap bitmap = null;

					try
					{
						retriever = new MediaMetadataRetriever();
						retriever.SetDataSource(videoPath);

						// 프레임 캡처
						bitmap = retriever.GetFrameAtTime(timeInMs * 1000, Android.Media.Option.ClosestSync);

						if (bitmap == null)
						{
							Log.Error(TAG, "프레임 캡처 실패: 비트맵이 null");
							return (0, 0, 0, 0);
						}

						// 대형 비디오는 다운샘플링 (선택적)
						//if (bitmap.Width > 1280 || bitmap.Height > 720)
						//{
						//	float scale = Math.Min(640f / bitmap.Width, 360f / bitmap.Height);
						//	int newWidth = (int)(bitmap.Width * scale);
						//	int newHeight = (int)(bitmap.Height * scale);

						//	Bitmap scaledBitmap = Bitmap.CreateScaledBitmap(bitmap, newWidth, newHeight, false);
						//	bitmap.Recycle();
						//	bitmap = scaledBitmap;

						//	Log.Debug(TAG, $"비트맵 다운샘플링: {newWidth}x{newHeight}");
						//}

						// 샘플링 포인트 (1/4, 1/2, 3/4 지점)
						int[] samplePointsX = { bitmap.Width * 3 / 8, bitmap.Width * 4 / 8, bitmap.Width * 5 / 8, bitmap.Width * 6 / 8};
						int[] samplePointsY = { bitmap.Height * 3 / 8, bitmap.Height * 4 / 8, bitmap.Height * 5 / 8, bitmap.Height * 6 / 8};

						// 각 방향에서 경계 감지
						int left = FindBorder(bitmap, false, true, samplePointsY);
						int top = FindBorder(bitmap, true, true, samplePointsX);
						int right = bitmap.Width - FindBorder(bitmap, false, false, samplePointsY);
						int bottom = bitmap.Height - FindBorder(bitmap, true, false, samplePointsX);

						// 경계 유효성 검증
						if (left > bitmap.Width / 2 || top > bitmap.Height / 2 ||
							right > bitmap.Width / 2 || bottom > bitmap.Height / 2)
						{
							Log.Warn(TAG, "비정상적인 경계 감지됨, 기본값 사용");
							return (0, 0, bitmap.Width, bitmap.Height);
						}

						Log.Debug(TAG, $"경계 감지: L={left}, T={top}, R={right}, B={bottom}");

						// 다운샘플링 했다면 원본 크기로 결과 변환
						//if (bitmap.Width != retriever.ExtractMetadata(MetadataKey.VideoWidth).ToInt())
						//{
						//	float scaleBack = (float)retriever.ExtractMetadata(MetadataKey.VideoWidth).ToInt() / bitmap.Width;
						//	left = (int)(left * scaleBack);
						//	top = (int)(top * scaleBack);
						//	right = (int)(right * scaleBack);
						//	bottom = (int)(bottom * scaleBack);
						//}

						return (left, bottom, bitmap.Width-right, bitmap.Height - top);
					}
					finally
					{
						// 리소스 정리
						bitmap?.Recycle();
						retriever?.Release();

						// Dispose 패턴 준수
						bitmap?.Dispose();
						retriever?.Dispose();
					}
				});
			}
			catch (Exception ex)
			{
				Log.Error(TAG, $"경계 감지 예외: {ex.Message}");
				return (0, 0, 0, 0);
			}
		}

		/// <summary>
		/// 지정된 방향에서 콘텐츠 경계를 찾습니다.
		/// </summary>
		private int FindBorder(Bitmap bitmap, bool isVertical, bool fromStart, int[] samplePoints)
		{
			// 초기값 및 제한 설정
			int pos = fromStart ? 0 : (isVertical ? bitmap.Height - 1 : bitmap.Width - 1);
			int limit = isVertical ? bitmap.Height / 2 : bitmap.Width / 2;
			int direction = fromStart ? 1 : -1;

			// 큰 스텝으로 검색
			int jumpStep = 30;
			while ((fromStart && pos < limit) || (!fromStart && pos > limit))
			{
				if (HasContent(bitmap, pos, samplePoints, isVertical))
					break;

				pos += jumpStep * direction;
			}

			// 이전 위치로 돌아가서 정밀 검색
			pos -= jumpStep * direction;
			pos = Math.Max(0, Math.Min(pos, isVertical ? bitmap.Height - 1 : bitmap.Width - 1));

			// 작은 스텝으로 검색
			int fineStep = 5;
			while ((fromStart && pos < limit) || (!fromStart && pos > limit))
			{
				if (HasContent(bitmap, pos, samplePoints, isVertical))
					break;

				pos += fineStep * direction;
			}

			// 1픽셀 단위로 최종 검색
			pos -= fineStep * direction;
			pos = Math.Max(0, Math.Min(pos, isVertical ? bitmap.Height - 1 : bitmap.Width - 1));

			while ((fromStart && pos < limit) || (!fromStart && pos > limit))
			{
				if (HasContent(bitmap, pos, samplePoints, isVertical))
					return pos;

				pos += direction;
			}

			// 경계를 찾지 못한 경우
			return fromStart ? 0 : (isVertical ? bitmap.Height - 1 : bitmap.Width - 1);
		}

		/// <summary>
		/// 지정된 행 또는 열에 콘텐츠가 있는지 확인합니다.
		/// </summary>
		private bool HasContent(Bitmap bitmap, int pos, int[] samplePoints, bool isVertical)
		{
			foreach (int point in samplePoints)
			{
				// 범위 체크
				if (point < 0 ||
				   (isVertical && point >= bitmap.Width) ||
				   (!isVertical && point >= bitmap.Height))
					continue;

				// 색상 가져오기
				int pixel = isVertical ? bitmap.GetPixel(point, pos) : bitmap.GetPixel(pos, point);

				// 밝기 계산 (간단한 RGB 평균)
				int r = (pixel >> 16) & 0xff;
				int g = (pixel >> 8) & 0xff;
				int b = pixel & 0xff;
				int brightness = r + g + b;

				// 임계값보다 밝으면 콘텐츠로 판단
				if (brightness > BRIGHTNESS_THRESHOLD)
					return true;
			}

			return false;
		}
	}

	// 확장 메서드
	public static class StringExtensions
	{
		public static int ToInt(this string value, int defaultValue = 0)
		{
			return int.TryParse(value, out int result) ? result : defaultValue;
		}
	}
}