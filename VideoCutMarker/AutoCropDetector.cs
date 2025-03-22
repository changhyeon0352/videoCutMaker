using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Views;
using System.Diagnostics;
using System.Numerics;

namespace VideoCutMarker
{
	public class AutoCropDetector
	{
		private const int BrightnessThreshold = 10;
		private const int FrameSampleCount = 3;

		const int jumpStep = 30;
		const int fineStep = 5;

		public async Task<Rect> DetectCropAreaAsync(MediaElement mediaElement, string videoPath)
		{
			try
			{
				double duration = mediaElement.Duration.TotalSeconds;
				List<double> samplePoints = GetSamplePoints(duration, FrameSampleCount);
				List<(int Left, int Top, int Right, int Bottom)> detectedBorders = new List<(int, int, int, int)>();

#if ANDROID
                var frameCapturer = new Platforms.AndroidModule.VideoFrameCapture();
                
                foreach (var timePoint in samplePoints)
                {
                    long timeMs = (long)(timePoint * 1000);
					var border = await frameCapturer.DetectBordersAsync(videoPath, timeMs);
                    
					if (border.Left >= 0) // 유효한 경계 확인
					{
						detectedBorders.Add(border);
						Debug.WriteLine($"프레임 {timePoint}초: L={border.Left}, T={border.Top}, R={border.Right}, B={border.Bottom}");
					}
					
                }
#elif IOS
				// iOS 구현
				// TODO: iOS 플랫폼용 프레임 캡처 구현 추가
#elif WINDOWS
                // Windows 구현
                // TODO: Windows 플랫폼용 프레임 캡처 구현 추가
#else
                // 대체 구현 (미디어 플레이어 접근이 불가능한 경우)
                // 여기서는 간단히 전체 영역을 반환
                return new Rect(0, 0, (int)mediaElement.MediaWidth, (int)mediaElement.MediaHeight);
#endif

				// 모든 프레임에서 감지된 경계의 중앙값 계산
				return CalculateMedianBorders(detectedBorders, (int)mediaElement.MediaWidth, (int)mediaElement.MediaHeight);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"자동 크롭 감지 오류: {ex.Message}");
				return new Rect(0, 0, (int)mediaElement.MediaWidth, (int)mediaElement.MediaHeight);
			}
		}


		private List<double> GetSamplePoints(double duration, int count)
		{
			List<double> points = new List<double>();
			double interval = duration / (count + 1);

			for (int i = 1; i <= count; i++)
			{
				points.Add(interval * i);
			}

			return points;
		}


		private Rect CalculateMedianBorders(List<(int Left, int Bottom, int Right, int Top)> detectedBorders, int width, int height)
		{
			if (detectedBorders.Count == 0)
				return new Rect(0, 0, width, height);

			// 각 방향별 값 모으기
			List<int> lefts = new List<int>();
			List<int> tops = new List<int>();
			List<int> rights = new List<int>();
			List<int> bottoms = new List<int>();

			foreach (var border in detectedBorders)
			{
				lefts.Add(border.Left);
				tops.Add(border.Top);
				rights.Add(border.Right);
				bottoms.Add(border.Bottom);
			}

			// 중앙값 계산
			int medianLeft = GetMedian(lefts);
			int medianTop = GetMedian(tops);
			int medianRight = GetMedian(rights);
			int medianBottom = GetMedian(bottoms);

			// 결과가 유효한지 확인
			if (medianRight <= medianLeft || medianBottom >= medianTop)
			{
				Debug.WriteLine("잘못된 중앙값 경계, 첫 번째 경계 사용");
				var first = detectedBorders[0];
				return new Rect(first.Left, first.Bottom, first.Right - first.Left, first.Top - first.Bottom);
			}

			Debug.WriteLine($"최종 경계: L={medianLeft}, T={medianTop}, R={medianRight}, B={medianBottom}");

			return new Rect(
				medianLeft,
				medianTop,
				medianRight - medianLeft,
				medianBottom - medianTop
			);
		}

		private int GetMedian(List<int> values)
		{
			if (values.Count == 0)
				return 0;

			values.Sort();

			int middle = values.Count / 2;
			if (values.Count % 2 == 0)
				return (values[middle - 1] + values[middle]) / 2;
			else
				return values[middle];
		}
	}
}