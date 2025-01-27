using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoCutMarker
{
    class InputStreamWrapper : Stream
	{
		private readonly SharpCifs.Util.Sharpen.InputStream _inputStream;

		public InputStreamWrapper(SharpCifs.Util.Sharpen.InputStream inputStream)
		{
			_inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush() => throw new NotSupportedException();

		public override int Read(byte[] buffer, int offset, int count)
		{
			return _inputStream.Read(buffer, offset, count);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_inputStream?.Close();
			}

			base.Dispose(disposing);
		}
	}
}
