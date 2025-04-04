namespace VideoCutMarker.Utilities
{
	/// <summary>
	/// 인코딩 관련 유틸리티 함수들을 제공하는 클래스
	/// </summary>
	public static class EncodingUtils
	{
		private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

		/// <summary>
		/// 10진수를 Base36 문자열로 변환
		/// </summary>
		/// <param name="decimalNumber">변환할 10진수 값</param>
		/// <returns>Base36으로 인코딩된 문자열</returns>
		public static string DecimalToBase36(int decimalNumber)
		{
			if (decimalNumber == 0) return "0";

			string result = "";
			while (decimalNumber > 0)
			{
				int remainder = decimalNumber % 36;
				result = Base36Chars[remainder] + result;
				decimalNumber /= 36;
			}

			return result;
		}

		/// <summary>
		/// 실수 값을 Base36 문자열로 변환 (소수점 이하는 버림)
		/// </summary>
		/// <param name="decimalNumber">변환할 실수 값</param>
		/// <returns>Base36으로 인코딩된 문자열</returns>
		public static string DecimalToBase36(double decimalNumber)
		{
			return DecimalToBase36((int)decimalNumber);
		}

		/// <summary>
		/// Base36 문자열을 10진수로 변환
		/// </summary>
		/// <param name="base36">변환할 Base36 문자열</param>
		/// <returns>10진수 값</returns>
		public static int Base36ToDecimal(string base36)
		{
			base36 = base36.ToUpper();

			int result = 0;
			for (int i = 0; i < base36.Length; i++)
			{
				char c = base36[i];
				int digit = Base36Chars.IndexOf(c);
				if (digit < 0)
					throw new ArgumentException("Invalid Base36 character: " + c);

				result = result * 36 + digit;
			}

			return result;
		}
	}
}