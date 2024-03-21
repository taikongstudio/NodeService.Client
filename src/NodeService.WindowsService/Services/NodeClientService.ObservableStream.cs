namespace NodeService.WindowsService.Services
{
    internal class ObservableStream : Stream
    {

        public Stream SourceStream { get; private set; }

        public ObservableStream(Stream stream)
        {
            SourceStream = stream;
        }

        public bool IsClosed { get; private set; }

        public override bool CanRead => SourceStream.CanRead;

        public override bool CanSeek => SourceStream.CanSeek;

        public override bool CanWrite => SourceStream.CanWrite;

        public override long Length => SourceStream.Length;

        public override long Position { get => SourceStream.Position; set => SetPosition(value); }

        private void SetPosition(long position)
        {
            SourceStream.Position = position;

        }

        public override void Flush()
        {
            SourceStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return SourceStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return SourceStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            SourceStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            SourceStream.Write(buffer, offset, count);
        }

        public override void Close()
        {
            IsClosed = true;
            SourceStream.Close();
            base.Close();
        }
    }
}
